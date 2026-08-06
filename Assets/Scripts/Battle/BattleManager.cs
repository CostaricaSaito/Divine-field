using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// バトル全体の管理を担当するクラス
/// 
/// 【役割】
/// - バトルの開始・終了制御
/// - ゲーム状態の管理
/// - ターン進行の制御
/// - プレイヤー入力の処理
/// - 各システム間の連携
/// 
/// 【責任範囲】
/// - バトルフローの全体制御
/// - 状態遷移の管理
/// - プレイヤー・敵のステータス管理
/// - 手札の管理
/// - カード選択の処理
/// 
/// 【他のクラスとの関係】
/// - BattleUIManager: UI表示の制御
/// - BattleProcessor: 戦闘処理の実行
/// - CardDealer: カード配布の管理
/// - HandRefillService: 手札補充の管理
/// - CardSequenceManager: カード演出シーケンスの管理
/// - CardStatsDisplay: TotalATKDEF表示の管理
/// - EnemyAI: 敵の行動決定
/// - BuyFeature: 経済アクション（買う）の処理
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager I;

    /// <summary>手札・敵手札リストの論理上限（スロットが多い場合でもこの枚数まで）。</summary>
    public const int MaxHandCards = 18;

    // グレーアウト制御フラグ
    private bool shouldGrayOutCards = false;

    private readonly SummonTurnCounterState _summonTurnCounters = new();

    /// <summary>
    /// 相手側 MagicPool のスナップショット（プレイヤー側UI・ロジック用。<see cref="RefreshEnemyMagicPoolSnapshot"/> で更新）。
    /// </summary>
    private readonly List<MagicCardEntry> _enemyMagicPoolSnapshot = new();
    /// <summary>召喚ライフサイクル用：各側が自分のターンを終えた回数（UI表示などに使用可）。</summary>
    public SummonTurnCounterState SummonTurnCounters => _summonTurnCounters;

    [Header("バトルUI")]
    public BattleStatusUI statusUI;
    public CutInController cutInController;

    [Header("カードUI")]
    public Transform handPanel;
    public GameObject cardUIPrefab;
    public Sprite cardBackSprite;

    [Header("システム")]
    public CardDealer cardDealer;
    public BattleProcessor battleProcessor;

    [Header("状態異常（ScriptableObject）")]
    [SerializeField] private StatusProgressionConfig statusProgressionConfig;
    [SerializeField] private DiseaseTurnEndSettings diseaseTurnEndSettings;
    [SerializeField, FormerlySerializedAs("shivaDirectAttackSealSettings")]
    private ShivaDirectAttackFreezeSettings shivaDirectAttackFreezeSettings;

    [Header("UI/演出")]
    public SummonSkillButton summonSkillButton;
    [Tooltip("PvP：相手側召喚アイコン用。未設定なら片側のみ。")]
    [SerializeField] private SummonSkillButton enemySummonSkillButton;
    public CardPurchaseAnimation cardPurchaseAnimation;
    [SerializeField] private CardSellAnimation cardSellAnimation;

    [Header("リザルト")]
    [Tooltip("未設定時は Resources.Load(\"Prefab/GameResult\") を試す")]
    [SerializeField] private GameObject gameResultPrefab;
    
    [SerializeField] private HandRefillService handRefill;
    public HandRefillService HandRefill => handRefill;
    [SerializeField] private CardStatsDisplay cardStatsDisplay;
    [SerializeField] private CardSequenceManager cardSequenceManager;
    /// <summary>介入・テスト用に外部からシーケンスを参照する。</summary>
    public CardSequenceManager Sequences => cardSequenceManager;
    [SerializeField] private MagicPoolManager magicPoolManager;
    private EnemyAI enemyAI = new EnemyAI();

    /// <summary>反射連鎖で敵の防御選択に使用する。</summary>
    public EnemyAI GetEnemyAI() => enemyAI;

    /// <summary>オンライン対戦中か（機能制限・入力送信の判定用）。</summary>
    public bool IsOnlineMatch => OnlineMatchContext.IsOnline;

    /// <summary>オンライン：相手の複数枚攻撃（受信値）。戦闘解決時に enemy 側の攻撃リストとして使う。</summary>
    private List<CardData> _onlineEnemyAttackCombo;

    /// <summary>CPU／演出用：相手ターン開始時の攻撃コンボ（複数枚時の合算属性判定用）。</summary>
    private List<CardData> _enemyAttackComboForCombat;

    public void SetEnemyAttackComboForCombat(List<CardData> cards)
        => _enemyAttackComboForCombat = cards != null && cards.Count > 0
            ? new List<CardData>(cards) : null;

    public void ClearEnemyAttackComboForCombat() => _enemyAttackComboForCombat = null;

    public void SetOnlineEnemyAttackCombo(List<CardData> cards)
        => _onlineEnemyAttackCombo = cards != null ? new List<CardData>(cards) : null;

    /// <summary>プレイヤー攻撃確定後の複数枚コンボ（UI 選択クリア後も戦闘・演出で参照）。</summary>
    private List<CardData> _playerAttackComboForCombat;

    public void SetPlayerAttackComboForCombat(List<CardData> cards)
        => _playerAttackComboForCombat = cards != null && cards.Count > 0
            ? new List<CardData>(cards) : null;

    public void ClearPlayerAttackComboForCombat() => _playerAttackComboForCombat = null;

    /// <summary>
    /// 相手側 MagicPool のコピー（カード参照＋残り回数）。プレイヤー側から「相手がチャージしている魔法」を参照する用途。
    /// <see cref="MagicPoolManager"/> 更新時に同期される。
    /// </summary>
    public IReadOnlyList<MagicCardEntry> GetEnemyMagicPoolSnapshot() => _enemyMagicPoolSnapshot;

    private void RefreshEnemyMagicPoolSnapshot()
    {
        _enemyMagicPoolSnapshot.Clear();
        if (MagicPoolManager.I == null) return;
        foreach (var e in MagicPoolManager.I.GetPoolEntries(PlayerType.Enemy))
        {
            if (e?.cardData == null) continue;
            _enemyMagicPoolSnapshot.Add(new MagicCardEntry(e.cardData, e.remainingUses));
        }
    }
    private BuyFeature buyFeature = new BuyFeature();
    private SellFeature sellFeature = new SellFeature();
    [SerializeField] private ExchangeFeature exchangeFeature;

    // バトルデータ
    private PlayerStatus playerStatus, enemyStatus;
    public List<CardData> playerHand = new();
    public List<CardData> cpuHand = new();
    

    public GameState CurrentState { get; private set; } = GameState.OpeningPhase;
    public PlayerType CurrentTurnOwner { get; private set; } = PlayerType.Player;

    /// <summary>Opening first-turn owner (for summon turn-end ordering).</summary>
    public PlayerType OpeningTurnOwner { get; private set; } = PlayerType.Player;

    private CardData currentAttackCard;

    /// <summary>Resources の SummonSkillPopup が開いている間、手札・経済・重複表示を防ぐ。</summary>
    public bool IsSummonSkillPopupOpen => _summonSkillPopupRoot != null;

    private GameObject _summonSkillPopupRoot;
    private bool _manifestationFlowRunning;

    /// <summary>攻撃フェーズでプレイヤーが「自分自身」を攻撃対象にするモード（TotalATK/DEF タップで切替）。</summary>
    private bool _playerSelfAttackTargetMode;

    /// <summary>自分自身への攻撃を確定するモードか（CPUは使用しない）。</summary>
    public bool IsPlayerSelfAttackTargetMode => _playerSelfAttackTargetMode;

    public void SetPlayerSelfAttackTargetMode(bool value)
    {
        if (_playerSelfAttackTargetMode == value) return;
        _playerSelfAttackTargetMode = value;
        UpdateTotalATKDEFDisplay();
    }

    public void TogglePlayerSelfAttackTargetMode()
    {
        if (CurrentState != GameState.AttackPhase || CurrentTurnOwner != PlayerType.Player)
            return;
        SetPlayerSelfAttackTargetMode(!_playerSelfAttackTargetMode);
    }

    public void ClearPlayerSelfAttackTargetMode() => SetPlayerSelfAttackTargetMode(false);

    /// <summary>
    /// 攻撃選択が変わったとき、効果対象トグルを既定へ戻す（TOTAL 赤オフ）。
    /// 数値 ATK が出ない選択では TOTAL で対象切替するため、選び直しのたびにリセットする。
    /// 回復系では既定＝自分へ効く（赤＝相手へ回復）。
    /// </summary>
    public void ResetPlayerEffectTargetToDefaultForCurrentAttackSelection()
    {
        if (CurrentState != GameState.AttackPhase || CurrentTurnOwner != PlayerType.Player) return;
        if (IsReflectionChainDefensePending()) return;

        var cards = BattleUIManager.I?.GetSelectedAttackCards();
        if (cards == null || cards.Count == 0)
        {
            ClearPlayerSelfAttackTargetMode();
            return;
        }

        if (cardStatsDisplay != null && cardStatsDisplay.IsPlayerAttackSelectionNumericAtkZero(cards))
            ClearPlayerSelfAttackTargetMode();
    }

    /// <summary>混乱時、攻撃のランダム対象が確定したあとの表示用（未確定時は相手命中想定で表示）。</summary>
    private bool _confusionAttackTargetResolved;
    private bool _confusionAttackTargetsSelf;

    public void SetConfusionAttackTargetResolvedForDisplay(bool targetsSelf)
    {
        _confusionAttackTargetResolved = true;
        _confusionAttackTargetsSelf = targetsSelf;
    }

    public void ClearConfusionAttackTargetResolvedForDisplay()
    {
        _confusionAttackTargetResolved = false;
    }

    public bool TryGetConfusionAttackTargetResolved(out bool targetsSelf)
    {
        if (_confusionAttackTargetResolved)
        {
            targetsSelf = _confusionAttackTargetsSelf;
            return true;
        }

        targetsSelf = false;
        return false;
    }

    /// <summary>マジカルエクスプロージョン：演出で MP を 0 にしたあとの攻撃力計算用（他カードの魔法 MP 消費後の残り）。</summary>
    private bool _magicalExplosionMpSnapActive;
    private int _magicalExplosionMpPoolAfterOtherCosts;

    public void SetMagicalExplosionComboMpPoolSnapshot(int mpRemainingBeforeMeDrain)
    {
        _magicalExplosionMpSnapActive = true;
        _magicalExplosionMpPoolAfterOtherCosts = Mathf.Max(0, mpRemainingBeforeMeDrain);
    }

    public bool TryGetMagicalExplosionComboMpPoolSnapshot(out int mp)
    {
        if (_magicalExplosionMpSnapActive)
        {
            mp = _magicalExplosionMpPoolAfterOtherCosts;
            return true;
        }

        mp = 0;
        return false;
    }

    public void ClearMagicalExplosionComboMpPoolSnapshot()
    {
        _magicalExplosionMpSnapActive = false;
    }

    /// <summary>100万ドルバズーカ：演出で GP を 0 にしたあとの攻撃力計算用（GP 全消費前の残量）。</summary>
    private bool _millionDollarBazookaGpSnapActive;
    private int _millionDollarBazookaGpPoolBeforeDrain;

    public void SetMillionDollarBazookaComboGpPoolSnapshot(int gpRemainingBeforeDrain)
    {
        _millionDollarBazookaGpSnapActive = true;
        _millionDollarBazookaGpPoolBeforeDrain = Mathf.Max(0, gpRemainingBeforeDrain);
    }

    public bool TryGetMillionDollarBazookaComboGpPoolSnapshot(out int gp)
    {
        if (_millionDollarBazookaGpSnapActive)
        {
            gp = _millionDollarBazookaGpPoolBeforeDrain;
            return true;
        }

        gp = 0;
        return false;
    }

    public void ClearMillionDollarBazookaComboGpPoolSnapshot()
    {
        _millionDollarBazookaGpSnapActive = false;
    }

    /// <summary>気狂いハンマー：演出で決定したランダム攻撃力（ダメージ計算・表示用）。</summary>
    private bool _hammadnessRollSnapActive;
    private int _hammadnessRolledAttackPower;

    public void SetHammadnessRollSnapshot(int rolledAttackPower)
    {
        _hammadnessRollSnapActive = true;
        _hammadnessRolledAttackPower = Mathf.Clamp(
            rolledAttackPower,
            HammadnessRules.MinRollInclusive,
            HammadnessRules.MaxRollInclusive);
    }

    public bool TryGetHammadnessRollSnapshot(out int rolledAttackPower)
    {
        if (_hammadnessRollSnapActive)
        {
            rolledAttackPower = _hammadnessRolledAttackPower;
            return true;
        }

        rolledAttackPower = 0;
        return false;
    }

    public void ClearHammadnessRollSnapshot()
    {
        _hammadnessRollSnapActive = false;
        _hammadnessRolledAttackPower = 0;
    }

    /// <summary>マジカルソード：MP 支払いで上乗せする攻撃力（プレイヤー今回分）。0 のとき上乗せなし。</summary>
    private int _magicalSwordAttackPowerBonus;

    /// <summary>マジカルエクスプロージョン演出前：マジカルソードの ATK/ TOTAL ランプを出し済み（Resolve 内で重複防止）。</summary>
    private bool _magicalSwordPlayerPreMeRampVisualDone;

    public int MagicalSwordAttackPowerBonus => _magicalSwordAttackPowerBonus;

    public void SetMagicalSwordAttackPowerBonus(int value) => _magicalSwordAttackPowerBonus = Mathf.Max(0, value);

    public void ClearMagicalSwordAttackPowerBonus() => _magicalSwordAttackPowerBonus = 0;

    public bool MagicalSwordPlayerPreMeRampVisualDone => _magicalSwordPlayerPreMeRampVisualDone;

    public void SetMagicalSwordPlayerPreMeRampVisualDone(bool value) => _magicalSwordPlayerPreMeRampVisualDone = value;

    public void ClearMagicalSwordPlayerAttackState()
    {
        _magicalSwordAttackPowerBonus = 0;
        _magicalSwordPlayerPreMeRampVisualDone = false;
    }

    /// <summary>オンライン：相手（enemy 側）がマジカルソードで支払った上乗せ攻撃力（RemotePlayerAgent が受信して設定）。</summary>
    private int _magicalSwordEnemyAttackPowerBonus;

    public int MagicalSwordEnemyAttackPowerBonus => _magicalSwordEnemyAttackPowerBonus;

    public void SetMagicalSwordEnemyAttackPowerBonus(int value) => _magicalSwordEnemyAttackPowerBonus = Mathf.Max(0, value);

    public void ClearMagicalSwordEnemyAttackPowerBonus() => _magicalSwordEnemyAttackPowerBonus = 0;

    /// <summary>マジカルエクスプロージョン演出前：敵マジカルソード ATK ランプ済み（Resolve 内重複防止）。</summary>
    private bool _magicalSwordEnemyPreMeRampVisualDone;

    public bool MagicalSwordEnemyPreMeRampVisualDone => _magicalSwordEnemyPreMeRampVisualDone;

    public void SetMagicalSwordEnemyPreMeRampVisualDone(bool value) => _magicalSwordEnemyPreMeRampVisualDone = value;

    public void ClearMagicalSwordEnemyAttackState()
    {
        _magicalSwordEnemyAttackPowerBonus = 0;
        _magicalSwordEnemyPreMeRampVisualDone = false;
    }

    /// <summary>Tribute Blood: HP paid in popup (player turn snapshot).</summary>
    private bool _tributeBloodHpPaidSnapActive;
    private int _tributeBloodPlayerHpPaid;
    private int _tributeBloodEnemyHpPaid;

    public void SetTributeBloodPlayerHpPaidSnapshot(int hpPaid)
    {
        _tributeBloodHpPaidSnapActive = true;
        _tributeBloodPlayerHpPaid = Mathf.Max(0, hpPaid);
    }

    public void SetTributeBloodEnemyHpPaidSnapshot(int hpPaid)
    {
        _tributeBloodHpPaidSnapActive = true;
        _tributeBloodEnemyHpPaid = Mathf.Max(0, hpPaid);
    }

    public bool TryGetTributeBloodHpPaidSnapshot(PlayerStatus attacker, out int hpPaid)
    {
        hpPaid = 0;
        if (!_tributeBloodHpPaidSnapActive || attacker == null)
            return false;

        if (ReferenceEquals(attacker, playerStatus))
        {
            hpPaid = _tributeBloodPlayerHpPaid;
            return true;
        }

        if (ReferenceEquals(attacker, enemyStatus))
        {
            hpPaid = _tributeBloodEnemyHpPaid;
            return true;
        }

        return false;
    }

    public void ClearTributeBloodHpPaidSnapshot()
    {
        _tributeBloodHpPaidSnapActive = false;
        _tributeBloodPlayerHpPaid = 0;
        _tributeBloodEnemyHpPaid = 0;
    }

    /// <summary>敵の双剣デュアリズム：プレイヤー防御1回目解決直後=0、2回目の防御入力待ち中=1。</summary>
    private int _playerDefenseVsEnemyDualBladeStreakIndex;

    /// <summary>敵攻撃＋双剣：2回目の「許す／使用」入力待ち中（<see cref="GameState.CombatResolvePhase"/> かつ専用インデックス）。</summary>
    public bool IsPlayerDualBladeSecondDefenseWaitActive() => _playerDefenseVsEnemyDualBladeStreakIndex == 1;

    /// <summary>
    /// 敵攻撃＋双剣デュアリズム：1回目の解決の共有後処理の前に呼ぶ。2回目の防御選択へ回すとき true（後処理をスキップ）。
    /// </summary>
    public async Task<bool> TryPreparePlayerDualBladeSecondDefenseIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (Attacker != PlayerType.Enemy || Defender != PlayerType.Player) return false;
        if (currentAttackCard == null) return false;
        if (!DualBladeDualismRules.ContainsDualBladeDualism(
                new List<CardData> { currentAttackCard }))
            return false;
        if (playerStatus == null || enemyStatus == null) return false;
        if (playerStatus.IsDead() || enemyStatus.IsDead())
        {
            _playerDefenseVsEnemyDualBladeStreakIndex = 0;
            return false;
        }

        if (_playerDefenseVsEnemyDualBladeStreakIndex == 1)
        {
            _playerDefenseVsEnemyDualBladeStreakIndex = 0;
            return false;
        }

        _playerDefenseVsEnemyDualBladeStreakIndex = 1;
        await BeginPlayerDualBladeSecondDefenseEntryAsync(cancellationToken);
        return true;
    }

    private async Task BeginPlayerDualBladeSecondDefenseEntryAsync(CancellationToken cancellationToken = default)
    {
        // 1本目の「使用」で isProcessingUseButton だけが真のまま残る（本メソッドが走る前に
        // SetGameState せず戻るための TryPrepare 早期 return）。2回目の「許す／使用」を受け付けられるよう必ず解除する。
        try
        {
            if (cancellationToken.IsCancellationRequested) return;

            BattleUIManager.I?.HideAllCardDetails();
            cardStatsDisplay?.ClearSequenceCards();
            cardStatsDisplay?.UpdateDisplay();

            await Task.Delay(300, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;

            if (currentAttackCard != null)
            {
                var atkList = GetAttackCardsForCombat();
                if (atkList == null || atkList.Count == 0)
                    atkList = new List<CardData> { currentAttackCard };

                BattleUIManager.I?.ClearAllCardDisplaysAndSelectionImmediate();
                BattleUIManager.I?.ShowCardSheetsVisualOnlyBatch(atkList, Side.Enemy);
                cardStatsDisplay?.SetSequenceCards(atkList, "攻撃", Side.Enemy);
                cardStatsDisplay?.UpdateDisplay();
                SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            }

            await Task.Delay(500, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;

            SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す13.mp3");
            Debug.Log("[BattleManager] 双剣デュアリズム: 2回目の防御選択");
            BattleUIManager.I?.SyncRestraintHeavyOverlay();

            selectedDefenseCard = null;
            ResetPlayerDefenseUseButtonLocks();
            BattleUIManager.I?.SetHandClickable(true);
            RefreshPlayerDefensePhaseInteractivity();
            BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
            TryAutoPassPlayerDefenseIfChantingArchMagic();
        }
        finally
        {
            isProcessingUseButton = false;
        }
    }

    /// <summary>
    /// 現在の攻撃カードを設定（BuyFeature、CardSequenceManagerから使用）
    /// </summary>
    public void SetCurrentAttackCard(CardData card)
    {
        currentAttackCard = card;
        if (card == null)
        {
            ClearPlayerAttackComboForCombat();
            ClearEnemyAttackComboForCombat();
        }
    }

    /// <summary>
    /// 選択中の防御カードを設定（CardSequenceManagerから使用）
    /// </summary>
    public void SetSelectedDefenseCard(CardData card)
    {
        selectedDefenseCard = card;
    }

    /// <summary>
    /// 選択中のカードを設定（CardSequenceManagerから使用）
    /// </summary>
    public void SetSelectedCard(CardData card)
    {
        selectedCard = card;
    }

    /// <summary>
    /// MagicPanel プール済みカードを選択する（MagicCardSlot.OnClick から呼ぶ）
    /// 手札のカード選択と同じ UI フローを実行し、CardSelectionManager にも追加する
    /// </summary>
    public void SelectMagicPoolCard(CardData card)
    {
        if (card == null) return;

        bool fromAttack = CurrentState == GameState.AttackPhase && Attacker == PlayerType.Player;
        bool fromDefense = IsPlayerDefenseInputActive()
            && CardRules.IsUsableInDefensePhase(card);

        if (!fromAttack && !fromDefense)
        {
            Debug.Log($"[BattleManager] MagicPanel カード選択不可: 現在のState={CurrentState}");
            return;
        }

        if (fromDefense)
        {
            var incoming = GetIncomingAttackSnapshotForDefenseUi();
            if (BlockingRules.IsPhysicalBlockingCard(card)
                && (incoming == null || !BlockingRules.CanUsePhysicalBlockingAgainstAttack(card, incoming)))
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel(
                    "無属性の物理攻撃にのみ使えます", new Color(0.85f, 0.25f, 0.2f));
                return;
            }
            if (card.cardType == CardType.Magic && playerStatus != null
                && !BlockingRules.CanAffordMagicDefenseMp(card, playerStatus))
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("MPが足りない", new Color(0.95f, 0.22f, 0.2f));
                return;
            }
        }

        // ShowCardDetail 内で AddCardSelection / CancelCardSelection が処理される
        selectedCard = card;
        BattleUIManager.I?.ShowCardDetail(card, Side.Player);
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        UpdateTotalATKDEFDisplay();
        Debug.Log($"[BattleManager] MagicPanel カード選択: {card.cardName} (残り{MagicPoolManager.I?.GetRemainingUses(card)}回)");
    }
    private CardData selectedCard;
    private CardData selectedDefenseCard;
    private TaskCompletionSource<List<CardData>> _reflectionChainDefenseTcs;
    private List<CardData> _reflectionChainAttackSnapshot;
    private TaskCompletionSource<List<CardData>> _parryRerunDefenseTcs;

    /// <summary>反射スライド後：TOTAL ATK を攻撃側から反射側へ移した表示用（<see cref="SetReflectionAttackTotalDisplayAfterSlide"/>）。</summary>
    private bool _reflectionAtkTotalActive;
    /// <summary>true のときプレイヤー側パネルに ATK、false のとき敵側パネルに ATK。</summary>
    private bool _reflectionAtkTotalOnPlayerSide;
    /// <summary>反射 TOTAL の加護表示用。カードの元攻撃者（イフリート等）。</summary>
    private PlayerStatus _reflectionAtkBlessAttacker;
    /// <summary>反射 TOTAL の加護表示用。跳ね返されたダメージの受け手側視点の抑制（リヴァイアサン等）。</summary>
    private PlayerStatus _reflectionAtkBlessDefender;
    private readonly List<CardData> _reflectionAtkCardsForTotalDisplay = new();
    /// <summary>宝玉反撃のように <see cref="_reflectionAtkCardsForTotalDisplay"/> のカード合計が 0 だが、実ダメ B 等から算出した数値を TOTAL に出す場合。</summary>
    private int? _reflectionAtkDisplayStrengthOverride;
    /// <summary>
    /// 宝玉反撃中：敵が元攻撃者のまま <see cref="GetCurrentAttackCard"/> も生きるため、敵パネル TOTAL の古いATK行を出さない。
    /// 戦闘行の <see cref="currentAttackCard"/> クリア時に戻す。
    /// </summary>
    private bool _suppressEnemyStaleAttackerInTotalByOrb;
    private TaskCompletionSource<bool> _interventionDefenseSubmitTcs;
    private List<CardData> _interventionAttackForDefenseUi;

    private TaskCompletionSource<bool> _postDeathDefenseSubmitTcs;
    private List<CardData> _postDeathAttackForDefenseUi;
    private bool _postDeathPlayerIsDefender;
    public bool IsPostDeathSequenceActive { get; private set; }
    public bool IsPostDeathPlayerDefender => _postDeathPlayerIsDefender;
    private bool isProcessingUseButton;
    public bool IsUseButtonLocked => isProcessingUseButton;

    /// <summary>CardSequence 例外で中断したとき、UseButton / 手札の入力ロックを戻す。</summary>
    public void ReleaseCardSequenceInputLocks()
    {
        isProcessingUseButton = false;
        BattleUIManager.I?.SetHandClickable(true);
        BattleUIManager.I?.RefreshUseButton();
    }

    /// <summary>DefensePhase で「許す／使用」確定後、戦闘解決完了まで true。</summary>
    private bool _playerDefenseCombatResolving;
    public bool IsPlayerDefenseCombatResolving => _playerDefenseCombatResolving;

    /// <summary>
    /// 選択中のカードを取得（CardStatsDisplayから使用）
    /// </summary>
    public CardData GetSelectedCard() => selectedCard;

    /// <summary>
    /// 選択中の防御カードを取得（CardStatsDisplayから使用）
    /// </summary>
    public CardData GetSelectedDefenseCard() => selectedDefenseCard;

    /// <summary>
    /// プレイヤー攻撃が命中したあと：敵の防御を選びカードを表示（状態遷移はしない）。
    /// </summary>
    /// <param name="playerAttackCards">戦闘解決に使う攻撃カード一覧（合算属性の算出に使用）</param>
    public async Task PickAndDisplayEnemyDefenseAfterPlayerHitAsync(List<CardData> playerAttackCards)
    {
        ElementType attackElement = ElementHelper.GetCombinedElement(playerAttackCards);
        selectedDefenseCard = await enemyAI.ExecuteDefenseSelectAsync(cpuHand, attackElement, playerAttackCards);
        cardStatsDisplay?.UpdateDisplay();

        var defenseCards = GetEnemyDefenseCardsForCombat();
        if (defenseCards == null || defenseCards.Count == 0)
            return;

        await BattleUIManager.I?.ShowEnemyDefenseCardsPresentationSequenceAsync(defenseCards);
    }

    private CancellationTokenSource _phaseCts;

    [SerializeField] private float cutInDelay = 0.5f;

    private PlayerType Attacker => CurrentTurnOwner;
    private PlayerType Defender => (CurrentTurnOwner == PlayerType.Player) ? PlayerType.Enemy : PlayerType.Player;

    /// <summary>
    /// 攻撃者を取得（CardStatsDisplayから使用）
    /// </summary>
    public PlayerType AttackerPublic => Attacker;

    /// <summary>
    /// 防御者を取得（CardStatsDisplayから使用）
    /// </summary>
    public PlayerType DefenderPublic => Defender;   

    /// <summary>
    /// 現在の攻撃カードを取得（CardStatsDisplayから使用）
    /// </summary>
    public CardData GetCurrentAttackCard() => currentAttackCard;

    public PlayerStatus GetPlayerStatus() => playerStatus;
    public PlayerStatus GetEnemyStatus() => enemyStatus;

    /// <summary>CombatResolvePhase 中、敵介入のプレイヤー防御入力待ちか。</summary>
    public bool IsInterventionDefenseWaitActive()
    {
        return _interventionDefenseSubmitTcs != null && !_interventionDefenseSubmitTcs.Task.IsCompleted;
    }

    public void BeginInterventionPlayerDefensePhase(List<CardData> attackCardsForElement)
    {
        _interventionAttackForDefenseUi = attackCardsForElement;
        _interventionDefenseSubmitTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        ResetPlayerDefenseUseButtonLocks();

        BattleUIManager.I?.ClearAllSelections();
        BattleUIManager.I?.HidePlayerCardDetails();

        RefreshPlayerDefensePhaseInteractivity();
        BattleUIManager.I?.SetHandClickable(true);
        TryAutoPassPlayerDefenseIfChantingArchMagic();
    }

    public async Task WaitForInterventionPlayerDefenseSubmitAsync(CancellationToken ct)
    {
        if (_interventionDefenseSubmitTcs == null) return;
        var tcs = _interventionDefenseSubmitTcs;
        using (ct.Register(() => tcs.TrySetCanceled()))
            await tcs.Task;
    }

    public void ClearInterventionDefenseWait()
    {
        _interventionAttackForDefenseUi = null;
        if (_interventionDefenseSubmitTcs != null && !_interventionDefenseSubmitTcs.Task.IsCompleted)
            _interventionDefenseSubmitTcs.TrySetCanceled();
        _interventionDefenseSubmitTcs = null;
    }

    /// <summary>PostDeath キュー中、生存プレイヤーの防御入力待ちか。</summary>
    public bool IsPostDeathDefenseWaitActive()
    {
        return _postDeathDefenseSubmitTcs != null && !_postDeathDefenseSubmitTcs.Task.IsCompleted;
    }

    /// <summary>
    /// 自プレイヤーが防御カードを選ぶ入力待ちか（通常防御・介入・PostDeath・反射連鎖・打ち払い再防御・双剣2回目）。
    /// </summary>
    public bool IsPlayerDefenseInputActive()
    {
        if (IsReflectionChainDefensePending() || IsParryRerunDefensePending())
            return true;
        if (IsPostDeathDefenseWaitActive() && IsPostDeathPlayerDefender)
            return true;
        if (CurrentState == GameState.CombatResolvePhase && IsInterventionDefenseWaitActive()
            && Defender == PlayerType.Player)
            return true;
        if (CurrentState == GameState.CombatResolvePhase && IsPlayerDualBladeSecondDefenseWaitActive()
            && Defender == PlayerType.Player)
            return true;
        return CurrentState == GameState.DefensePhase && Defender == PlayerType.Player;
    }

    /// <summary>防御入力開始時：UseButton ロック解除と相手側 yurusu 装飾の非表示。</summary>
    public void ResetPlayerDefenseUseButtonLocks()
    {
        isProcessingUseButton = false;
        _playerDefenseCombatResolving = false;
        BattleUIManager.I?.HideYurusuButton();
    }

    public void BeginPostDeathPlayerDefenseWait(List<CardData> attackCardsForElement)
    {
        _postDeathAttackForDefenseUi = attackCardsForElement;
        _postDeathPlayerIsDefender = true;
        _postDeathDefenseSubmitTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        ResetPlayerDefenseUseButtonLocks();

        BattleUIManager.I?.ClearAllSelections();
        BattleUIManager.I?.HidePlayerCardDetails();

        RefreshPlayerDefensePhaseInteractivity();
        BattleUIManager.I?.SetHandClickable(true);
        TryAutoPassPlayerDefenseIfChantingArchMagic();
        UpdateTotalATKDEFDisplay();
    }

    public async Task WaitForPostDeathPlayerDefenseSubmitAsync(CancellationToken ct)
    {
        if (_postDeathDefenseSubmitTcs == null) return;
        var tcs = _postDeathDefenseSubmitTcs;
        using (ct.Register(() => tcs.TrySetCanceled()))
            await tcs.Task;
    }

    public void ClearPostDeathDefenseWait()
    {
        _postDeathAttackForDefenseUi = null;
        _postDeathPlayerIsDefender = false;
        if (_postDeathDefenseSubmitTcs != null && !_postDeathDefenseSubmitTcs.Task.IsCompleted)
            _postDeathDefenseSubmitTcs.TrySetCanceled();
        _postDeathDefenseSubmitTcs = null;
    }

    private void TrySubmitPostDeathPlayerDefense()
    {
        var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
        if (selectedDefenseCards == null)
            selectedDefenseCards = new List<CardData>();

        if (playerStatus != null && playerStatus.HasRestraintEffect() && selectedDefenseCards.Count > 1)
        {
            BattleUIManager.I?.ShowInfoPopupOnCardPanel("体が重い", new Color(0.22f, 0.24f, 0.38f));
            isProcessingUseButton = false;
            BattleUIManager.I?.SetHandClickable(true);
            BattleUIManager.I?.RefreshUseButton();
            return;
        }

        if (IsOnlineMatch && selectedDefenseCards.Count > 1)
        {
            BattleUIManager.I?.ShowInfoPopupOnCardPanel(
                "オンライン対戦ではカードは1枚ずつ使用できます", new Color(0.95f, 0.25f, 0.2f));
            isProcessingUseButton = false;
            BattleUIManager.I?.SetHandClickable(true);
            BattleUIManager.I?.RefreshUseButton();
            return;
        }

        if (IsOnlineMatch)
            NetworkBattleBridge.SendDefenseSelection(selectedDefenseCards);

        _postDeathDefenseSubmitTcs?.TrySetResult(true);
        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.SetUseButtonInteractable(false);
        isProcessingUseButton = false;
    }

    private void TrySubmitInterventionPlayerDefense()
    {
        var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
        if (selectedDefenseCards == null)
            selectedDefenseCards = new List<CardData>();

        if (playerStatus != null && playerStatus.HasRestraintEffect() && selectedDefenseCards.Count > 1)
        {
            BattleUIManager.I?.ShowInfoPopupOnCardPanel("体が重い", new Color(0.22f, 0.24f, 0.38f));
            isProcessingUseButton = false;
            BattleUIManager.I?.SetHandClickable(true);
            BattleUIManager.I?.RefreshUseButton();
            return;
        }

        if (IsOnlineMatch && selectedDefenseCards.Count > 1)
        {
            BattleUIManager.I?.ShowInfoPopupOnCardPanel(
                "オンライン対戦ではカードは1枚ずつ使用できます", new Color(0.95f, 0.25f, 0.2f));
            isProcessingUseButton = false;
            BattleUIManager.I?.SetHandClickable(true);
            BattleUIManager.I?.RefreshUseButton();
            return;
        }

        // オンライン：介入防御の確定選択（空＝許す）を相手へ送信
        if (IsOnlineMatch)
            NetworkBattleBridge.SendDefenseSelection(selectedDefenseCards);

        _interventionDefenseSubmitTcs?.TrySetResult(true);
        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.SetUseButtonInteractable(false);
        isProcessingUseButton = false;
    }

    private bool IsPlayerChantingArchMagicWhileDefending()
    {
        return playerStatus != null && playerStatus.IsCastingArchMagic && IsPlayerDefenseInputActive();
    }

    private void ApplyArchMagicChantingDefenseBlockUi()
    {
        if (BattleUIManager.I == null) return;
        BattleUIManager.I.RefreshDefenseInteractivity(playerHand, new List<CardData>());
        BattleUIManager.I.RefreshUseButton();
        RefreshPlayerHandStatusTextForDefenseSnapshot();
    }

    /// <summary>詠唱中プレイヤーは防御不可。「許す」相当で即進行する。</summary>
    private bool TryAutoPassPlayerDefenseIfChantingArchMagic()
    {
        if (!IsPlayerChantingArchMagicWhileDefending())
            return false;

        ApplyArchMagicChantingDefenseBlockUi();
        BattleUIManager.I?.ClearAllSelections();
        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.SetUseButtonInteractable(false);
        isProcessingUseButton = false;

        if (IsInterventionDefenseWaitActive())
        {
            if (IsOnlineMatch)
                NetworkBattleBridge.SendDefenseSelection(null);
            _interventionDefenseSubmitTcs?.TrySetResult(true);
            return true;
        }

        if (IsPostDeathDefenseWaitActive())
        {
            if (IsOnlineMatch)
                NetworkBattleBridge.SendDefenseSelection(null);
            _postDeathDefenseSubmitTcs?.TrySetResult(true);
            return true;
        }

        if (_reflectionChainDefenseTcs != null && !_reflectionChainDefenseTcs.Task.IsCompleted)
        {
            if (IsOnlineMatch)
                NetworkBattleBridge.SendDefenseSelection(null);
            _reflectionChainDefenseTcs.TrySetResult(new List<CardData>());
            UpdateTotalATKDEFDisplay();
            return true;
        }

        if (_parryRerunDefenseTcs != null && !_parryRerunDefenseTcs.Task.IsCompleted)
        {
            if (IsOnlineMatch)
                NetworkBattleBridge.SendDefenseSelection(null);
            _parryRerunDefenseTcs.TrySetResult(new List<CardData>());
            UpdateTotalATKDEFDisplay();
            return true;
        }

        if (IsOnlineMatch)
            NetworkBattleBridge.SendDefenseSelection(null);
        HandleNoDefenseCard();
        return true;
    }

    /// <summary>
    /// 防御フェーズ等でプレイヤーが防御側のとき、手札グレーアウト（拘束時は選択済み1枚のみ）と「体が重い」オーバーレイを更新。
    /// </summary>
    public void RefreshPlayerDefensePhaseInteractivity()
    {
        if (BattleUIManager.I == null) return;
        if (!IsPlayerDefenseInputActive()) return;

        if (IsPlayerChantingArchMagicWhileDefending())
        {
            ApplyArchMagicChantingDefenseBlockUi();
            return;
        }

        List<CardData> attackSource = GetIncomingAttackSnapshotForDefenseUi();
        if (attackSource == null || attackSource.Count == 0)
        {
            BattleUIManager.I?.RefreshUseButton();
            return;
        }

        if (CardRules.IncomingRequiresFullOnlyReactiveDefense(attackSource))
        {
            var defenseChoicesRestricted = CardRules.GetFullOnlyReactiveDefenseChoices(playerHand, attackSource);
            var selectedDefenseR = BattleUIManager.I.GetSelectedDefenseCards();
            defenseChoicesRestricted = CardRules.ApplyRestraintDefenseFilter(
                defenseChoicesRestricted,
                selectedDefenseR,
                playerStatus != null && playerStatus.HasRestraintEffect());
            BattleUIManager.I.RefreshDefenseInteractivity(playerHand, defenseChoicesRestricted);
            BattleUIManager.I.RefreshUseButton();
            RefreshPlayerHandStatusTextForDefenseSnapshot();
            return;
        }

        ElementType attackElement = ElementHelper.GetCombinedElement(attackSource);
        var defenseChoices = CardRules.GetDefenseChoicesAgainstAttack(playerHand, attackElement, attackSource);
        if (ReflectionRules.CanReflectPhysical(attackSource))
        {
            foreach (var c in playerHand)
            {
                if (c != null && ReflectionRules.CanUsePhysicalReflectionAgainstAttack(c, attackSource) && !defenseChoices.Contains(c))
                    defenseChoices.Add(c);
            }
        }
        else
        {
            defenseChoices.RemoveAll(c => c != null && ReflectionRules.IsPhysicalReflectionCard(c));
        }

        if (ReflectionRules.CanReflectMagic(attackSource))
        {
            foreach (var c in playerHand)
            {
                if (c != null && ReflectionRules.CanUseMagicReflectionAgainstAttack(c, attackSource) && !defenseChoices.Contains(c))
                    defenseChoices.Add(c);
            }
        }
        else
        {
            defenseChoices.RemoveAll(c => c != null && ReflectionRules.IsMagicReflectionCard(c));
        }

        if (BlockingRules.CanBlockPhysical(attackSource))
        {
            foreach (var c in playerHand)
            {
                if (c != null && BlockingRules.IsPhysicalBlockingCard(c) && !defenseChoices.Contains(c))
                    defenseChoices.Add(c);
            }
        }
        else
        {
            defenseChoices.RemoveAll(c => c != null && BlockingRules.IsPhysicalBlockingCard(c));
        }

        defenseChoices.RemoveAll(c => c != null && ParryRules.IsParryCard(c) && !ParryRules.CanParryIncoming(c, attackSource));
        foreach (var c in playerHand)
        {
            if (c != null && ParryRules.CanParryIncoming(c, attackSource) && !defenseChoices.Contains(c))
                defenseChoices.Add(c);
        }

        var selectedDefense = BattleUIManager.I.GetSelectedDefenseCards();
        defenseChoices = CardRules.ApplyRestraintDefenseFilter(
            defenseChoices,
            selectedDefense,
            playerStatus != null && playerStatus.HasRestraintEffect());

        BattleUIManager.I.RefreshDefenseInteractivity(playerHand, defenseChoices);
        BattleUIManager.I.RefreshUseButton();
        RefreshPlayerHandStatusTextForDefenseSnapshot();
    }

    /// <summary>全プレイヤー手札の Card Status Text を再適用。手札操作可否は <see cref="BattleUIManager.IsHandInputBlocked"/> を参照（REFLECT 等の切替）。</summary>
    public void RefreshPlayerHandStatusTextForDefenseSnapshot()
    {
        if (playerHand == null) return;
        foreach (var c in playerHand)
        {
            if (c?.cardUI == null) continue;
            c.cardUI.RefreshHandStatusText();
        }
    }

    /// <summary>連鎖反射でプレイヤーが再防御を選ぶまで待つ（許す＝空リスト）。</summary>
    /// <summary>連鎖反射の防御入力待ち中か（UI「弾き返す」判定用）。</summary>
    public bool IsReflectionChainDefensePending()
    {
        return _reflectionChainDefenseTcs != null && !_reflectionChainDefenseTcs.Task.IsCompleted;
    }

    /// <summary>打ち払い後、攻撃が自分側に戻ったあとの再防御入力待ちか。</summary>
    public bool IsParryRerunDefensePending()
    {
        return _parryRerunDefenseTcs != null && !_parryRerunDefenseTcs.Task.IsCompleted;
    }

    /// <summary>打ち払い「こちらに飛んできた！」後、再び防御を選ぶまで待つ（許す＝空リスト）。</summary>
    public async Task<List<CardData>> WaitForParryRerunDefenseSubmitAsync(CancellationToken cancellationToken)
    {
        _parryRerunDefenseTcs = new TaskCompletionSource<List<CardData>>(TaskCreationOptions.RunContinuationsAsynchronously);
        ResetPlayerDefenseUseButtonLocks();
        selectedDefenseCard = null;
        BattleUIManager.I?.ClearAllSelections();
        ClearSelectedCards();
        BattleUIManager.I?.SetHandClickable(true);
        RefreshPlayerDefensePhaseInteractivity();
        BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す13.mp3");
        TryAutoPassPlayerDefenseIfChantingArchMagic();

        try
        {
            using (cancellationToken.Register(() =>
                   {
                       if (_parryRerunDefenseTcs != null && !_parryRerunDefenseTcs.Task.IsCompleted)
                           _parryRerunDefenseTcs.TrySetCanceled();
                   }))
            {
                return await _parryRerunDefenseTcs.Task;
            }
        }
        finally
        {
            _parryRerunDefenseTcs = null;
            BattleUIManager.I?.SetHandClickable(false);
        }
    }

    /// <summary>連鎖反射時の攻撃カードスナップショット（可否判定用）。</summary>
    public List<CardData> GetReflectionChainAttackSnapshot()
    {
        return _reflectionChainAttackSnapshot;
    }

    /// <summary>介入の防御入力待ち時の攻撃カード（UI 用）。</summary>
    public List<CardData> GetInterventionDefenseAttackSnapshot()
    {
        return _interventionAttackForDefenseUi;
    }

    /// <summary>防御 UI から現在の攻撃カード一覧を参照（物理反射ボタン表示など）。</summary>
    public List<CardData> GetAttackCardsForCombatPublic()
    {
        return GetAttackCardsForCombat();
    }

    /// <summary>デバッグ UI 用：Layer1 Turn（手番の短い表記）。</summary>
    public string GetBattleTurnDebugLabel()
    {
        return CurrentTurnOwner == PlayerType.Player ? "プレイヤー" : "敵";
    }

    /// <summary>Layer3 Step（反射連鎖・介入待ちは <see cref="GameState"/> より優先して解決）。</summary>
    public BattleStep CurrentBattleStep => ResolveCurrentBattleStep();

    private BattleStep ResolveCurrentBattleStep()
    {
        if (IsReflectionChainDefensePending())
            return BattleStep.ReflectionChainDefenseSelect;
        if (CurrentState == GameState.CombatResolvePhase && IsInterventionDefenseWaitActive())
            return BattleStep.InterventionDefenseSelect;

        switch (CurrentState)
        {
            case GameState.OpeningPhase:
                return BattleStep.OpeningSequence;
            case GameState.StandByPhase:
                return BattleStep.StandBy;
            case GameState.AttackPhase:
                return BattleStep.MainActionSelect;
            case GameState.DefensePhase:
                return BattleStep.DefenseSelect;
            case GameState.DefenseConfirmPhase:
                return BattleStep.CombatSequenceResolve;
            case GameState.CombatResolvePhase:
                return BattleStep.CombatResolveProcessing;
            case GameState.EndPhase:
                return BattleStep.EndPhaseProcessing;
            case GameState.BattleEndPhase:
                return BattleStep.BattleResult;
            default:
                return BattleStep.Unknown;
        }
    }

    public async Task<List<CardData>> WaitForReflectionChainDefenseAsync(
        List<CardData> attackSnapshot,
        CancellationToken cancellationToken)
    {
        _reflectionChainAttackSnapshot = attackSnapshot != null
            ? new List<CardData>(attackSnapshot)
            : new List<CardData>();
        _reflectionChainDefenseTcs = new TaskCompletionSource<List<CardData>>(TaskCreationOptions.RunContinuationsAsynchronously);
        ResetPlayerDefenseUseButtonLocks();
        BattleUIManager.I?.SetHandClickable(true);
        BattleUIManager.I?.ClearAllSelections();
        ClearSelectedCards();
        ClearStatsDisplaySequenceCards();
        RefreshReflectionChainInteractivity(attackSnapshot);
        RefreshPlayerDefensePhaseInteractivity();
        BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
        TryAutoPassPlayerDefenseIfChantingArchMagic();

        try
        {
            using (cancellationToken.Register(() =>
                   {
                       if (_reflectionChainDefenseTcs != null && !_reflectionChainDefenseTcs.Task.IsCompleted)
                           _reflectionChainDefenseTcs.TrySetCanceled();
                   }))
            {
                return await _reflectionChainDefenseTcs.Task;
            }
        }
        finally
        {
            _reflectionChainAttackSnapshot = null;
            _reflectionChainDefenseTcs = null;
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.HideYurusuButton();
        }
    }

    /// <summary>選択変更後に連鎖反射の手札グレーアウトを更新（<see cref="BattleUIManager"/> から呼ぶ）。</summary>
    public void RefreshReflectionChainInteractivityIfPending()
    {
        if (_reflectionChainAttackSnapshot != null)
            RefreshReflectionChainInteractivity(_reflectionChainAttackSnapshot);
    }

    private void RefreshReflectionChainInteractivity(List<CardData> attackSnapshot)
    {
        if (BattleUIManager.I == null || attackSnapshot == null) return;

        if (IsPlayerChantingArchMagicWhileDefending())
        {
            ApplyArchMagicChantingDefenseBlockUi();
            return;
        }

        if (CardRules.IncomingRequiresFullOnlyReactiveDefense(attackSnapshot))
        {
            var defenseChoicesR = CardRules.GetFullOnlyReactiveDefenseChoices(playerHand, attackSnapshot);
            var selectedDefenseR = BattleUIManager.I.GetSelectedDefenseCards();
            defenseChoicesR = CardRules.ApplyRestraintDefenseFilter(
                defenseChoicesR,
                selectedDefenseR,
                playerStatus != null && playerStatus.HasRestraintEffect());
            BattleUIManager.I.RefreshDefenseInteractivity(playerHand, defenseChoicesR);
            BattleUIManager.I.RefreshUseButton();
            RefreshPlayerHandStatusTextForDefenseSnapshot();
            return;
        }

        ElementType attackElement = ElementHelper.GetCombinedElement(attackSnapshot);
        var defenseChoices = CardRules.GetDefenseChoicesAgainstAttack(playerHand, attackElement, attackSnapshot);
        if (ReflectionRules.CanReflectPhysical(attackSnapshot))
        {
            foreach (var c in playerHand)
            {
                if (c != null && ReflectionRules.CanUsePhysicalReflectionAgainstAttack(c, attackSnapshot) && !defenseChoices.Contains(c))
                    defenseChoices.Add(c);
            }
        }
        else
        {
            defenseChoices.RemoveAll(c => c != null && ReflectionRules.IsPhysicalReflectionCard(c));
        }

        if (ReflectionRules.CanReflectMagic(attackSnapshot))
        {
            foreach (var c in playerHand)
            {
                if (c != null && ReflectionRules.CanUseMagicReflectionAgainstAttack(c, attackSnapshot) && !defenseChoices.Contains(c))
                    defenseChoices.Add(c);
            }
        }
        else
        {
            defenseChoices.RemoveAll(c => c != null && ReflectionRules.IsMagicReflectionCard(c));
        }

        if (BlockingRules.CanBlockPhysical(attackSnapshot))
        {
            foreach (var c in playerHand)
            {
                if (c != null && BlockingRules.IsPhysicalBlockingCard(c) && !defenseChoices.Contains(c))
                    defenseChoices.Add(c);
            }
        }
        else
        {
            defenseChoices.RemoveAll(c => c != null && BlockingRules.IsPhysicalBlockingCard(c));
        }

        defenseChoices.RemoveAll(c => c != null && ParryRules.IsParryCard(c) && !ParryRules.CanParryIncoming(c, attackSnapshot));
        foreach (var c in playerHand)
        {
            if (c != null && ParryRules.CanParryIncoming(c, attackSnapshot) && !defenseChoices.Contains(c))
                defenseChoices.Add(c);
        }

        var selectedDefense = BattleUIManager.I.GetSelectedDefenseCards();
        defenseChoices = CardRules.ApplyRestraintDefenseFilter(
            defenseChoices,
            selectedDefense,
            playerStatus != null && playerStatus.HasRestraintEffect());

        BattleUIManager.I.RefreshDefenseInteractivity(playerHand, defenseChoices);
        BattleUIManager.I.RefreshUseButton();
        RefreshPlayerHandStatusTextForDefenseSnapshot();
    }

    /// <summary>
    /// 防御 UI・併用不可判定用の現在の攻撃スナップショット（反射連鎖／介入／通常防御）。</summary>
    public List<CardData> GetIncomingAttackSnapshotForDefenseUi()
    {
        if (IsReflectionChainDefensePending())
        {
            if (_reflectionChainAttackSnapshot == null || _reflectionChainAttackSnapshot.Count == 0)
                return null;
            return new List<CardData>(_reflectionChainAttackSnapshot);
        }

        if (CurrentState == GameState.CombatResolvePhase && IsInterventionDefenseWaitActive())
        {
            if (_interventionAttackForDefenseUi == null || _interventionAttackForDefenseUi.Count == 0)
                return null;
            return new List<CardData>(_interventionAttackForDefenseUi);
        }

        if (IsPostDeathDefenseWaitActive() && _postDeathAttackForDefenseUi != null)
        {
            if (_postDeathAttackForDefenseUi.Count == 0) return null;
            return new List<CardData>(_postDeathAttackForDefenseUi);
        }

        if (CurrentState == GameState.CombatResolvePhase && IsPlayerDualBladeSecondDefenseWaitActive()
            && DefenderPublic == PlayerType.Player)
            return GetAttackCardsForCombat();

        if (IsParryRerunDefensePending())
            return GetAttackCardsForCombat();

        if ((CurrentState == GameState.DefensePhase || CurrentState == GameState.DefenseConfirmPhase)
            && DefenderPublic == PlayerType.Player)
            return GetAttackCardsForCombat();

        return null;
    }

    private void Awake()
    {
        I = this;
    }

    private static void EnsureBattleBgmController()
    {
        if (UnityEngine.Object.FindObjectOfType<BattleBgmController>() != null) return;
        GameObject bgmGo = GameObject.Find("BGM");
        if (bgmGo != null && bgmGo.GetComponent<BattleBgmController>() == null)
            bgmGo.AddComponent<BattleBgmController>();
    }

    void Start()
    {
        // オンライン対戦：決定的乱数を初期化し、敵入力をリモートエージェントへ差し替える
        if (OnlineMatchContext.IsOnline)
        {
            BattleRandom.InitOnline(OnlineMatchContext.RandomSeed, OnlineMatchContext.IsHost);
            enemyAI = new RemotePlayerAgent();
            Debug.Log($"[BattleManager] Online mode (host={OnlineMatchContext.IsHost}, opponent={OnlineMatchContext.RemotePlayerName})");
        }

        // ステータス初期化
        playerStatus = new PlayerStatus();
        enemyStatus = new PlayerStatus();
        playerStatus.InitializeAsPlayer();
        enemyStatus.InitializeAsEnemy();

        // 召喚データ（プレイヤー：選択済み、敵：オンラインは相手の選択・CPUはランダム）
        if (SummonSelectionManager.I != null)
        {
            playerStatus.summonData = SummonSelectionManager.I.GetSelectedSummonData();
            enemyStatus.summonData = OnlineMatchContext.IsOnline
                ? enemyAI.SelectRandomEnemySummon()
                : GetRandomEnemySummon();
        }
        else
        {
            // 召喚選択シーンを経由しない実行時の既定（デバッグはガルーダ）
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var fallback = Resources.Load<SummonData>("Summons/Garuda");
#else
            SummonData fallback = null;
#endif
            if (fallback == null)
                fallback = Resources.Load<SummonData>("Summons/Ifrit");
            playerStatus.summonData = fallback;
            enemyStatus.summonData = fallback;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!OnlineMatchContext.IsOnline)
        {
            var battleDebug = FindObjectOfType<BattleDebugTools>();
            battleDebug?.ApplyInitialSummonOverrides(playerStatus, enemyStatus);
        }
#endif

        summonSkillButton?.Configure(playerStatus, enemyStatus);
        enemySummonSkillButton?.Configure(enemyStatus, playerStatus);

        // システム初期化
        cardDealer.Initialize(playerStatus, enemyStatus, handPanel, cardUIPrefab, cardBackSprite);
        battleProcessor.Initialize(playerStatus, enemyStatus, statusUI, cardDealer);
        DiseaseTurnEndProcessor.BindSettings(diseaseTurnEndSettings);
        ShivaDirectAttackFreezeFlow.BindSettings(shivaDirectAttackFreezeSettings);
        battleProcessor.ConfigureStatusEffects(statusProgressionConfig);

        if (handRefill != null)
            handRefill.Initialize(handPanel, cardUIPrefab, cardBackSprite, cardDealer);

        // CardSequenceManagerの初期化
        if (cardSequenceManager != null)
        {
            cardSequenceManager.Initialize(this, battleProcessor, handRefill, cardStatsDisplay);
        }

        // BuyFeatureの初期化
        buyFeature.Initialize(this, playerStatus, enemyStatus, playerHand, cpuHand, cardDealer, cardPurchaseAnimation);

        // SellFeatureの初期化
        GameObject sellPopupPrefab = null;
        Canvas popupCanvas = null;
        if (BattleUIManager.I != null)
        {
            sellPopupPrefab = BattleUIManager.I.GetSellConfirmPopupPrefab();
            popupCanvas = BattleUIManager.I.GetPopupCanvas();
            Debug.Log($"[BattleManager] sellPopupPrefab取得: {(sellPopupPrefab != null ? sellPopupPrefab.name : "null")}");
            Debug.Log($"[BattleManager] popupCanvas取得: {(popupCanvas != null ? popupCanvas.name : "null")}");
        }
        else
        {
            Debug.LogWarning("[BattleManager] BattleUIManager.Iがnullです");
        }
        sellFeature.Initialize(this, playerStatus, enemyStatus, playerHand, cpuHand, cardDealer, sellPopupPrefab, popupCanvas, cardSellAnimation, handRefill);

        // ExchangeFeatureの初期化
        if (exchangeFeature != null)
        {
            GameObject exchangePopupPrefab = BattleUIManager.I?.GetExchangePopupPrefab();
            exchangeFeature.Initialize(this, playerStatus, exchangePopupPrefab, popupCanvas);
            Debug.Log($"[BattleManager] exchangeFeature初期化完了");
        }
        else
        {
            Debug.LogWarning("[BattleManager] ExchangeFeatureがアタッチされていません");
        }

        // MagicPoolManager の初期化
        if (magicPoolManager != null)
        {
            magicPoolManager.RegisterOnPoolChanged(() =>
            {
                BattleUIManager.I?.UpdateMagicPanel();
                BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
            });
            magicPoolManager.RegisterOnEnemyPoolChanged(() =>
            {
                RefreshEnemyMagicPoolSnapshot();
                BattleUIManager.I?.OnEnemyMagicPoolChanged();
            });
            RefreshEnemyMagicPoolSnapshot();
            BattleUIManager.I?.OnEnemyMagicPoolChanged();
            Debug.Log("[BattleManager] MagicPoolManager初期化完了");
        }

        if (cardStatsDisplay != null)
        {
            cardStatsDisplay?.UpdateDisplay();
        }

        EnsureBattleBgmController();

        _ = BattleStartSequenceAsync();
    }

    /// <summary>
    /// 配布・カットイン等の開幕 <see cref="BattleStartSequenceAsync"/> が完了し、<see cref="GameState.StandByPhase"/> 直前の時点で true になる。
    /// 手札リロード UI はこれが true かつ攻撃プレイヤーの <see cref="GameState.AttackPhase"/> などで有効化する。
    /// </summary>
    public bool IsBattleOpeningSequenceComplete { get; private set; }

    //================ 状態遷移 ================
    public void SetGameState(GameState newState)
    {
        if (CurrentState == newState)
        {
            isProcessingUseButton = false;
            Debug.Log($"[State] noop {newState}");
            return;
        }

        // ゲーム終了済みの場合、BattleEndPhase 以外への遷移は無視する（後続処理による逆戻りを防ぐ）
        if (_gameEndTriggered && newState != GameState.BattleEndPhase)
        {
            Debug.Log($"[State] ゲーム終了中のため {newState} への遷移を無視");
            return;
        }

        _phaseCts?.Cancel(); _phaseCts?.Dispose();
        _phaseCts = new CancellationTokenSource();

        Debug.Log($"[State]{CurrentState} → {newState}(Turn: {CurrentTurnOwner})");
        CurrentState = newState;
        isProcessingUseButton = false;
        HandleStateChange();
        // 攻撃フェーズ外では入口を消す。EnterAttackPhase 内でも呼ぶが、他フェーズへ移った直後の表示残りを防ぐ。
        HandReloadController.I?.RefreshReloadEntryButton();
    }

    private void HandleStateChange()
    {
        switch (CurrentState)
        {
            case GameState.OpeningPhase:
                break;

            case GameState.StandByPhase:
                OnStandByPhaseEntered();
                break;

            case GameState.AttackPhase:
                EnterAttackPhase();
                break;

            case GameState.DefensePhase:
                _ = RunDefenseSelectAsync();
                break;

            case GameState.DefenseConfirmPhase:
                _ = RunDefenseConfirmAsync();
                break;

            case GameState.CombatResolvePhase:
                _ = RunCombatResolvePhaseAsync();
                break;

            case GameState.EndPhase:
                // TurnEnd 中は TOTALATKDEF を出さない。シーケンス／反射オーバーレイの残りで空パネルだけ残るのを防ぐ。
                cardStatsDisplay?.ClearSequenceCardsAndAttackDisplayLocks();
                ClearReflectionAttackTotalDisplay();
                _ = RunEndPhaseAsync();
                break;

            case GameState.BattleEndPhase:
                break;
        }
    }

    private System.Collections.IEnumerator OpeningDealBridge(int openingPlayer, int openingCpu, System.Action onComplete)
    {
        yield return StartCoroutine(cardDealer.DealOpeningHands(playerHand, cpuHand, openingPlayer, openingCpu));
        onComplete?.Invoke();
    }

    private System.Threading.Tasks.Task RunOpeningDealAsync(int openingPlayer, int openingCpu)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        StartCoroutine(OpeningDealBridge(openingPlayer, openingCpu, () => tcs.SetResult(true)));
        return tcs.Task;
    }

    //================ バトル開始（Layer1: Turn 前の開幕のみ） ================
    private async System.Threading.Tasks.Task BattleStartSequenceAsync()
    {
        IsBattleOpeningSequenceComplete = false;

        // リザルト画面のカウント起点に使う RP スナップショットを取得
        GameProfile.I?.CaptureBattleStartRP();

        SummonGarudaLifecycle.GetOpeningHandTargets(playerStatus, enemyStatus, out int openingPlayer, out int openingCpu);
        await RunOpeningDealAsync(openingPlayer, openingCpu);

        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);

        await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(cutInDelay));

        if (cutInController != null)
        {
            var cutInTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            cutInController.OnCutInComplete = () => cutInTcs.TrySetResult(true);
            cutInController.PlayCutIn();
            await cutInTcs.Task;
        }

        BattleUIManager.I?.SetIntroModeUI(playerHand);

        DetermineOpeningFirstTurn();

        IsBattleOpeningSequenceComplete = true;
        SetGameState(GameState.StandByPhase);
    }

    /// <summary>
    /// 開幕の先攻・後攻。既定は 50/50。Editor / Development では <see cref="BattleDebugTools"/> で固定可。
    /// </summary>
    private void DetermineOpeningFirstTurn()
    {
        // オンライン対戦：ハンドシェイクでホストが決定済み（自分視点に変換済み）
        if (OnlineMatchContext.IsOnline)
        {
            CurrentTurnOwner = OnlineMatchContext.LocalPlayerGoesFirst ? PlayerType.Player : PlayerType.Enemy;
            OpeningTurnOwner = CurrentTurnOwner;
            Debug.Log($"[BattleManager] 先攻(オンライン): {CurrentTurnOwner}");
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var dbg = FindObjectOfType<BattleDebugTools>();
        if (dbg != null)
            CurrentTurnOwner = dbg.ResolveOpeningTurnOwner();
        else
            CurrentTurnOwner = RollRandomOpeningTurnOwner();
#else
        CurrentTurnOwner = RollRandomOpeningTurnOwner();
#endif
        OpeningTurnOwner = CurrentTurnOwner;
        Debug.Log($"[BattleManager] 先攻: {CurrentTurnOwner}");
    }

    private static PlayerType RollRandomOpeningTurnOwner()
    {
        return UnityEngine.Random.Range(0, 2) == 0 ? PlayerType.Player : PlayerType.Enemy;
    }

    private void OnStandByPhaseEntered()
    {
        BattleUIManager.I?.HideYurusuButton();
        BattleUIManager.I?.RefreshTurnCountDisplay(_summonTurnCounters, CurrentTurnOwner);

        if (CurrentTurnOwner == PlayerType.Player)
        {
            
                SoundEffectPlayer.I.Play("Assets/SE/決定ボタンを押す13.mp3");

        }

        if (CurrentTurnOwner == PlayerType.Player) playerStatus.OnTurnStart();
        else enemyStatus.OnTurnStart();

        // 経済アクションのクールダウンを更新（プレイヤーのターン開始時のみ）
        if (CurrentTurnOwner == PlayerType.Player)
        {
            EconomicAction.I?.OnTurnStart();
            // クールダウン更新後にUIを更新
            BattleUIManager.I?.UpdateEconomicActionButtons();
        }

        BattleUIManager.I?.HideAllCardDetails();
        currentAttackCard = null;
        _onlineEnemyAttackCombo = null;
        ClearMagicalSwordEnemyAttackState();
        SetSuppressEnemyStaleAttackerInTotalByOrb(false);
        cardStatsDisplay?.UpdateDisplay();

        // StandBy 時点ではグレーアウトしない
        BattleUIManager.I?.SetIntroModeUI(playerHand);
        
        // グレーアウト制御フラグを設定（AttackPhase でグレーアウトを有効にする）
        shouldGrayOutCards = true;

        // 大魔法詠唱中：自分の攻撃フェーズでは EnterAttackPhase 側で詠唱演出に差し替えるため、
        // 敵ターン時は RunEnemyTurnAsync をスキップして AttackPhase だけ入れる。
        bool ownerIsCasting = CurrentTurnOwner == PlayerType.Player
            ? playerStatus != null && playerStatus.IsCastingArchMagic
            : enemyStatus != null && enemyStatus.IsCastingArchMagic;

        if (CurrentTurnOwner == PlayerType.Player)
        {
            SetGameState(GameState.AttackPhase);
        }
        else
        {
            SetGameState(GameState.AttackPhase);
            if (!ownerIsCasting)
                _ = RunEnemyTurnAsync();
        }
    }

    private void EnterAttackPhase()
    {
        ClearConfusionAttackTargetResolvedForDisplay();
        cardStatsDisplay?.ClearAllAttackSequenceDisplayLocks();
        BattleUIManager.I?.SetHandClickable(true);

        // 大魔法詠唱中は通常の攻撃フェーズを差し替え、詠唱演出 → TurnEnd（または発動）を走らせる。
        var castOwner = Attacker == PlayerType.Player ? playerStatus : enemyStatus;
        if (castOwner != null && castOwner.IsCastingArchMagic && cardSequenceManager != null
            && !cardSequenceManager.IsArchMagicCastIntroInProgress
            && !cardSequenceManager.IsArchMagicCountdownInProgress)
        {
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.SetIntroModeUI(playerHand);
            BattleUIManager.I?.RefreshUseButton();
            Side ownerSide = Attacker == PlayerType.Player ? Side.Player : Side.Enemy;
            _ = cardSequenceManager.RunArchMagicCastingTurnAsync(castOwner, ownerSide, _phaseCts.Token);
            return;
        }

        PlayerStatus attackPhaseOwner = CurrentTurnOwner == PlayerType.Player ? playerStatus : enemyStatus;
        if (FreezeAttackSelectFlow.IsTurnOwnerFrozen(attackPhaseOwner))
        {
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.SetUseButtonInteractable(false);
            BattleUIManager.I?.DisableEconomicActionButtonsTemporarily();
            if (CurrentTurnOwner == PlayerType.Player)
            {
                BattleUIManager.I?.SetHandGrayedOut(playerHand, grayedOut: true);
                _ = RunFrozenAttackSelectSkipAsync(attackPhaseOwner);
            }
            return;
        }

        if (Attacker == PlayerType.Player)
        {
            ClearPlayerSelfAttackTargetMode();
            // ターンプレイヤー（攻撃側）の処理
            var attackables = CardRules.GetAttackChoices(playerHand);
            if (attackables.Count == 0)
            {
                BattleUIManager.I?.SetPrayModeUI(playerHand);
            }
            else
            {
                if (shouldGrayOutCards)
                {
                    BattleUIManager.I?.RefreshAttackInteractivity(playerHand, CardRules.GetAttackChoices(playerHand));
                }
                else
                {
                    BattleUIManager.I?.SetIntroModeUI(playerHand);
                }

                BattleUIManager.I?.UpdateEconomicActionButtons();
            }

            BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
            BattleUIManager.I?.RefreshUseButton();

            RefreshSummonSkillButtonInteractables();
            HandReloadController.I?.RefreshReloadEntryButton();
        }
        else
        {
            // 敵が攻撃側の AttackPhase：相手の選択待ち。手札・魔法パネルはすべてグレーアウト
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.SetUseButtonInteractable(false);
            BattleUIManager.I?.SetHandGrayedOut(playerHand, grayedOut: true);
            BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
        }

        RefreshSummonSkillButtonInteractables();
    }

    /// <summary>手札リロードのポップアップ表示中、またはリロード演出シーケンス中。経済・魔法パネル等のブロックに使用。</summary>
    public bool IsHandReloadPopupOpen => HandReloadController.I != null && HandReloadController.I.IsHandReloadUiBlocking;

    /// <summary>手札リロードのキャンセル／リロード確定演出の完了後、攻撃フェーズの手札・ボタンを再構築する。</summary>
    public void RefreshUIFromHandReloadClose()
    {
        if (CurrentState != GameState.AttackPhase || CurrentTurnOwner != PlayerType.Player) return;
        ClearPlayerSelfAttackTargetMode();
        var attackables = CardRules.GetAttackChoices(playerHand);
        if (attackables.Count == 0)
        {
            BattleUIManager.I?.SetPrayModeUI(playerHand);
        }
        else
        {
            if (shouldGrayOutCards)
                BattleUIManager.I?.RefreshAttackInteractivity(playerHand, CardRules.GetAttackChoices(playerHand));
            else
                BattleUIManager.I?.SetIntroModeUI(playerHand);
        }
        BattleUIManager.I?.UpdateEconomicActionButtons();
        BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
        BattleUIManager.I?.RefreshUseButton();
        RefreshSummonSkillButtonInteractables();
        HandReloadController.I?.RefreshReloadEntryButton();
    }

    private async Task RunDefenseSelectAsync()
    {
        // 売却確定後はダミー攻撃カードが載っている。DefenseConfirm に到達しなかった場合も SellFeature のフラグを戻す。
        bool sellFlowFromPendingConfirm = currentAttackCard != null
            && currentAttackCard.cardName == "経済アクション（売却）";
        bool reachedDefenseConfirm = false;

        try
        {
            // 攻撃カード確定後のインターバルと効果音
            await Task.Delay(1000);
            SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す13.mp3");
            Debug.Log("[BattleManager] 攻撃カード確定、防御カード選択開始");

            BattleUIManager.I?.SyncRestraintHeavyOverlay();

            if (Defender == PlayerType.Enemy)
            {
                // EnemyAIで防御選択を実行（攻撃属性はプレイヤー攻撃カードから合算）
                ElementType attackElement = ElementHelper.GetCombinedElement(GetAttackCardsForCombat());
                selectedDefenseCard = await enemyAI.ExecuteDefenseSelectAsync(cpuHand, attackElement, GetAttackCardsForCombat());

                cardStatsDisplay?.UpdateDisplay();

                SetGameState(GameState.DefenseConfirmPhase);
                reachedDefenseConfirm = true;
            }
            else
            {
                BattleUIManager.I?.HidePlayerCardDetails();
                BattleUIManager.I?.SetHandClickable(true);

                RefreshPlayerDefensePhaseInteractivity();

                BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
                TryAutoPassPlayerDefenseIfChantingArchMagic();
            }
        }
        finally
        {
            if (sellFlowFromPendingConfirm && !reachedDefenseConfirm && sellFeature != null)
                sellFeature.ForceEndSellProcessingState();
        }
    }

    private async Task RunDefenseConfirmAsync()
    {
        bool sellFlow = currentAttackCard != null && currentAttackCard.cardName == "経済アクション（売却）";

        try
        {
            if (currentAttackCard == null)
            {
                Debug.LogWarning("攻撃カードが設定されていません");
                SetGameState(GameState.AttackPhase);
                return;
            }

            // 経済アクションの場合は特別処理（DefenseConfirm からのみ呼ばれる想定。CurrentState 条件は外し、取りこぼしを防ぐ）
            if (currentAttackCard.cardName == "経済アクション")
            {
                Debug.Log("[BattleManager] 経済アクション（購入）の防御フェーズ処理");
                await buyFeature.ProcessEconomicActionAsync();
                currentAttackCard = null;
                selectedDefenseCard = null;
                UpdateTotalATKDEFDisplay();
                SetGameState(GameState.CombatResolvePhase);
                return;
            }

            if (currentAttackCard.cardName == "経済アクション（売却）")
            {
                Debug.Log("[BattleManager] 経済アクション（売却）の防御フェーズ処理");
                await sellFeature.ProcessEconomicActionAsync();
                currentAttackCard = null;
                selectedDefenseCard = null;
                UpdateTotalATKDEFDisplay();
                SetGameState(GameState.CombatResolvePhase);
                return;
            }

            // プレイヤーの防御カード選択はCardSequenceManagerで処理済み（HandleDefenseUse経由）
            if (Defender == PlayerType.Player)
            {
                return;
            }

            // 敵の防御カードの処理（複数枚対応）
            var defenseCardsToDisplay = GetEnemyDefenseCardsForCombat();
            if (defenseCardsToDisplay != null && defenseCardsToDisplay.Count > 0)
            {
                await BattleUIManager.I?.ShowEnemyDefenseCardsPresentationSequenceAsync(defenseCardsToDisplay);
            }

            var atk = (Attacker == PlayerType.Player) ? playerStatus : enemyStatus;
            var def = (Defender == PlayerType.Player) ? playerStatus : enemyStatus;
            var defHand = (Defender == PlayerType.Player) ? playerHand : cpuHand;

            List<CardData> attackCards = GetAttackCardsForCombat();
            var defenseCardsForCombat = defenseCardsToDisplay != null && defenseCardsToDisplay.Count > 0
                ? defenseCardsToDisplay
                : new List<CardData>();
            CardData enemyDefenseCard = defenseCardsForCombat.Count > 0 ? defenseCardsForCombat[0] : null;

            bool enemyPhysicalReflect = enemyDefenseCard != null
                && ReflectionRules.CanUsePhysicalReflectionAgainstAttack(enemyDefenseCard, attackCards);
            bool enemyMagicReflect = enemyDefenseCard != null
                && ReflectionRules.CanUseMagicReflectionAgainstAttack(enemyDefenseCard, attackCards);
            bool enemyPhysicalBlock = enemyDefenseCard != null
                && BlockingRules.CanUsePhysicalBlockingAgainstAttack(enemyDefenseCard, attackCards);
            bool enemyParry = enemyDefenseCard != null
                && ParryRules.RequiresParryExclusiveLock(enemyDefenseCard, attackCards);

            bool showYurusuDuringCombat =
                Defender == PlayerType.Enemy && defenseCardsForCombat.Count == 0 && BattleUIManager.I != null;
            using (YurusuDisplayScope.ShowIf(showYurusuDuringCombat))
            {
                if (attackCards != null && attackCards.Count == 1 && attackCards[0] != null
                    && CardRules.IncomingRequiresFullOnlyReactiveDefense(attackCards))
                {
                    await Task.Delay(DamagePopup.PreImmediateEffectDelayMs, _phaseCts.Token);
                    await battleProcessor.ResolveImmediateEffectAsync(attackCards[0], atk, def);
                }
                else if (enemyPhysicalReflect || enemyMagicReflect)
                {
                    await PhysicalReflectionFlow.RunEnemyDefenderReflectsPlayerAttackAsync(
                        this,
                        battleProcessor,
                        handRefill,
                        enemyAI,
                        attackCards,
                        enemyDefenseCard,
                        _phaseCts.Token);
                }
                else if (enemyParry)
                {
                    await ParryFlow.RunEnemyDefenderParriesPlayerAttackAsync(
                        this,
                        battleProcessor,
                        handRefill,
                        enemyAI,
                        attackCards,
                        enemyDefenseCard,
                        _phaseCts.Token);
                }
                else if (enemyPhysicalBlock)
                {
                    if (enemyDefenseCard.cardType == CardType.Magic && cardSequenceManager != null)
                        await cardSequenceManager.ApplyEnemyMagicDefenseFromHandOrPoolAsync(enemyDefenseCard);
                    await BlockingNullifyFlow.RunEnemyDefenderNullifiesAsync(
                        this,
                        battleProcessor,
                        handRefill,
                        attackCards,
                        enemyDefenseCard,
                        _phaseCts.Token,
                        defenseCardAlreadyConsumed: enemyDefenseCard.cardType == CardType.Magic);
                }
                else if (defenseCardsForCombat.Count > 1)
                {
                    await battleProcessor.ResolveCombatAsync(attackCards, defenseCardsForCombat, atk, def, defHand);
                }
                else
                {
                    CardData singleDef = defenseCardsForCombat.Count == 1 ? defenseCardsForCombat[0] : null;
                    await battleProcessor.ResolveCombatAsync(attackCards, singleDef, atk, def, defHand);
                }
            }

            if (_phaseCts.Token.IsCancellationRequested) return;

            ClearMagicalExplosionComboMpPoolSnapshot();
            ClearMillionDollarBazookaComboGpPoolSnapshot();
            ClearTributeBloodHpPaidSnapshot();
            ClearHammadnessRollSnapshot();
            BattleUIManager.I?.HideAllCardDetails();

            bool skipPostCombatEnemyDefenseUse = enemyPhysicalReflect || enemyMagicReflect || enemyParry
                || enemyPhysicalBlock
                || (attackCards != null && attackCards.Count == 1 && attackCards[0] != null
                    && CardRules.IncomingRequiresFullOnlyReactiveDefense(attackCards));
            if (defenseCardsForCombat.Count > 0 && !skipPostCombatEnemyDefenseUse)
            {
                foreach (var defenseCardToUse in defenseCardsForCombat)
                {
                    if (defenseCardToUse == null) continue;
                    if (IsOnlineMatch && defenseCardToUse.cardType == CardType.Magic)
                        continue;
                    handRefill?.RecordEnemyUse(defenseCardToUse);
                    battleProcessor.UseCard(defenseCardToUse, defHand);
                }
            }

            cardStatsDisplay?.ClearSequenceCardsAndAttackDisplayLocks();
            currentAttackCard = null;
            SetSuppressEnemyStaleAttackerInTotalByOrb(false);
            cardStatsDisplay?.UpdateDisplay();
            SetGameState(GameState.CombatResolvePhase);
        }
        finally
        {
            if (sellFlow && sellFeature != null)
                sellFeature.ForceEndSellProcessingState();
        }
    }

    /// <summary>Layer2 CombatResolve：介入による再戦闘など。完了後に <see cref="EndPhase"/> へ。</summary>
    private async Task RunCombatResolvePhaseAsync()
    {
        CancellationToken phaseToken = _phaseCts != null ? _phaseCts.Token : default;

        try
        {
            if (CurrentState != GameState.CombatResolvePhase) return;

            // オンライン：戦闘解決直後のダメージ結果（HP/MP/GP）をホスト権威で同期する
            ClearMagicalSwordEnemyAttackState();
            await RunOnlineResolveStateSyncAsync(phaseToken);

            if (CurrentState != GameState.CombatResolvePhase) return;

            // 戦闘ダメージで大魔法詠唱がキャンセルされた場合の演出を先に消化する。
            await ProcessArchMagicCancelIfPendingAsync(phaseToken);

            if (CurrentState != GameState.CombatResolvePhase) return;

            try
            {
                await InterventionTurnEndProcessor.ProcessIfNeededAsync(this, phaseToken);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BattleManager] InterventionTurnEnd: キャンセル");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (CurrentState != GameState.CombatResolvePhase) return;

            // 介入による追撃ダメージでキャンセルが発生した場合も消化する。
            await ProcessArchMagicCancelIfPendingAsync(phaseToken);

            if (CurrentState != GameState.CombatResolvePhase) return;

            SetGameState(GameState.EndPhase);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            TryRecoverLateTurnPhaseToStandByPhase();
        }
    }

    /// <summary>Layer2 End：病・補充・表向き・ターン交代。</summary>
    private async Task RunEndPhaseAsync()
    {
        CancellationToken phaseToken = _phaseCts != null ? _phaseCts.Token : default;

        try
        {
            if (CurrentState != GameState.EndPhase) return;

            // 攻撃フェーズ終了直後：攻撃側の病系処理（補充・ドローより先）
            PlayerStatus attackerStatus = CurrentTurnOwner == PlayerType.Player ? playerStatus : enemyStatus;
            try
            {
                await DiseaseTurnEndProcessor.ProcessForAttackerAsync(attackerStatus, phaseToken);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BattleManager] DiseaseTurnEndProcessor: キャンセル（EndPhase 続行を試みます）");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            PlayerStatus turnOwnerStatus = CurrentTurnOwner == PlayerType.Player ? playerStatus : enemyStatus;
            try
            {
                await FreezeTurnEndProcessor.ProcessTurnOwnerDecayAsync(turnOwnerStatus, phaseToken);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BattleManager] FreezeTurnEndProcessor: cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            // 病ダメージで詠唱がキャンセルされた場合の演出
            await ProcessArchMagicCancelIfPendingAsync(phaseToken);

            if (CurrentState != GameState.EndPhase) return;

            // Summon turn-end passives (Garuda draw / Indra hand destroy) before Refill
            try
            {
                await SummonTurnEndLifecycle.ProcessTurnEndAsync(this, _summonTurnCounters, phaseToken);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BattleManager] SummonTurnEndLifecycle: cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            // 顕現スキルダメージで詠唱がキャンセルされた場合の演出
            await ProcessArchMagicCancelIfPendingAsync(phaseToken);

            if (CurrentState != GameState.EndPhase) return;

            if (handRefill != null)
            {
                try
                {
                    await handRefill.RefillAtTurnEndAsync(playerHand, cpuHand, phaseToken);
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("[BattleManager] RefillAtTurnEnd: キャンセル");
                }
            }

            if (CurrentState != GameState.EndPhase) return;

            await ProcessEconomicActionDrawAsync();

            if (CurrentState != GameState.EndPhase) return;

            await RevealFaceDownCardsAsync();

            if (CurrentState != GameState.EndPhase) return;

            BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);

            BattleUIManager.I?.SetIntroModeUI(playerHand);

            await Task.Delay(500);

            if (CurrentState != GameState.EndPhase) return;

            shouldGrayOutCards = true;

            // オンライン：両者の演出完了を待ち合わせ、ホスト権威で
            // HP/MP/GP・手札全リスト・次ターン所有者・ターンカウンタを同期してから次ターンへ
            bool turnOwnerAppliedBySync = false;
            if (IsOnlineMatch && !_gameEndTriggered)
                turnOwnerAppliedBySync = await RunOnlineTurnBoundarySyncAsync(phaseToken);

            if (CurrentState != GameState.EndPhase) return;

            if (!turnOwnerAppliedBySync)
                ToggleTurnOwner();
            SetGameState(GameState.StandByPhase);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            TryRecoverLateTurnPhaseToStandByPhase();
        }
    }

    /// <summary>
    /// 大魔法詠唱のキャンセル（被ダメージ起因）待ちが立っていればキャンセル演出を消化する。
    /// </summary>
    private async Task ProcessArchMagicCancelIfPendingAsync(CancellationToken phaseToken)
    {
        if (cardSequenceManager == null) return;

        try
        {
            if (playerStatus != null && playerStatus.archMagicCancelPending)
                await cardSequenceManager.RunArchMagicCastCancelAsync(playerStatus, phaseToken);

            if (enemyStatus != null && enemyStatus.archMagicCancelPending)
                await cardSequenceManager.RunArchMagicCastCancelAsync(enemyStatus, phaseToken);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[BattleManager] ArchMagicCastCancel: キャンセル");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    /// <summary>
    /// CombatResolve / End の処理が例外や中断で StandBy に進めなかったときの保険。
    /// </summary>
    private void TryRecoverLateTurnPhaseToStandByPhase()
    {
        if (CurrentState != GameState.EndPhase && CurrentState != GameState.CombatResolvePhase) return;

        Debug.LogWarning("[BattleManager] CombatResolve/End から復帰できなかったため StandByPhase に移行します");
        shouldGrayOutCards = true;
        ToggleTurnOwner();
        SetGameState(GameState.StandByPhase);
    }

    private async Task RunFrozenAttackSelectSkipAsync(PlayerStatus frozenOwner)
    {
        CancellationToken token = _phaseCts != null ? _phaseCts.Token : default;
        try
        {
            await FreezeAttackSelectFlow.RunSkipFrozenTurnAsync(frozenOwner, token);
            if (CurrentState != GameState.AttackPhase) return;

            if (IsOnlineMatch && ReferenceEquals(frozenOwner, playerStatus))
                NetworkBattleBridge.SendAttackSelection(null);

            SetGameState(GameState.CombatResolvePhase);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[BattleManager] Frozen attack select skip: cancelled");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private async Task RunEnemyTurnAsync()
    {
        if (FreezeAttackSelectFlow.IsTurnOwnerFrozen(enemyStatus))
        {
            var frozenToken = _phaseCts != null ? _phaseCts.Token : default;
            try
            {
                await FreezeAttackSelectFlow.RunSkipFrozenTurnAsync(enemyStatus, frozenToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (CurrentState == GameState.AttackPhase)
                SetGameState(GameState.CombatResolvePhase);
            return;
        }

        // EnemyAIで攻撃ターンを実行（enemyStatusを渡してMP消費・魔法判定を行う）
        var attack = await enemyAI.ExecuteAttackTurnAsync(cpuHand, battleProcessor, handRefill, enemyStatus);
        
        if (attack == null)
        {
            SetGameState(GameState.CombatResolvePhase);
            return;
        }

        currentAttackCard = attack;
        _playerDefenseVsEnemyDualBladeStreakIndex = 0;

        var token = _phaseCts != null ? _phaseCts.Token : default;
        var atkList = GetEnemyAttackCardsForTurn(attack);

        if (atkList.Count == 1 && ArchMagicRules.IsArchMagicCard(attack))
        {
            Debug.Log($"[BattleManager] Enemy ArchMagic cast start: {attack.cardName}");
            if (cardSequenceManager != null)
            {
                try
                {
                    await cardSequenceManager.StartArchMagicCastIntroAsync(attack, Side.Enemy, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                return;
            }
        }

        if (CardRules.IsImmediateAction(attack) && atkList.Count == 1)
        {
            Debug.Log($"[BattleManager] 相手の即時カード: {attack.cardName}");
            try
            {
                await PlayAttackConfirmPresentationAsync(attack, Side.Enemy, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
            cardStatsDisplay?.UpdateDisplay();

            PlayerStatus immediateTarget = ResolveCpuImmediateEffectTarget(attack);
            if (immediateTarget == enemyStatus)
            {
                await Task.Delay(DamagePopup.PreImmediateEffectDelayMs, token);
                await battleProcessor.ResolveImmediateEffectAsync(attack, enemyStatus, enemyStatus);
                if (cardSequenceManager != null)
                    await cardSequenceManager.RunAfterCombatSharedCleanupAsync(token);
                else
                {
                    BattleUIManager.I?.HideAllCardDetails();
                    cardStatsDisplay?.ClearSequenceCards();
                    currentAttackCard = null;
                    ClearMagicalExplosionComboMpPoolSnapshot();
                    ClearTributeBloodHpPaidSnapshot();
                    ClearHammadnessRollSnapshot();
                    BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
                    cardStatsDisplay?.UpdateDisplay();
                    SetGameState(GameState.CombatResolvePhase);
                }
                return;
            }

            SetGameState(GameState.DefensePhase);
            return;
        }
        else if (ShouldUseEnemyAttackPresentationSequence(atkList, attack))
        {
            Debug.Log($"[BattleManager] 敵攻撃演出: {atkList.Count}枚");
            if (cardStatsDisplay != null)
            {
                PlayerAttackTotalDisplayFlow.ResetAttackSequenceDisplayLocks(cardStatsDisplay);
                cardStatsDisplay.BeginAttackSequenceReveal(Side.Enemy);
                cardStatsDisplay.SetSequenceCards(new List<CardData>(), "攻撃", Side.Enemy);
                cardStatsDisplay.UpdateDisplay();
            }
            await cardSequenceManager.PresentOnlineEnemyAttackSequenceAsync(atkList, token);
            BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
            cardStatsDisplay?.UpdateDisplay();
        }
        else
        {
            // 敵の攻撃カードを表示
            BattleUIManager.I?.ShowCardDetail(attack, Side.Enemy);
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            Debug.Log($"[BattleManager] 相手のカード決定: {attack.cardName}");
            BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
            cardStatsDisplay.UpdateDisplay();
            await Task.Delay(1000);
        }

        bool confusedEnemy = enemyStatus != null && enemyStatus.HasConfusionEffect();
        bool confusionTargetSelf = confusedEnemy && BattleRandom.Range(0, 2) == 0;
        if (confusedEnemy)
            SetConfusionAttackTargetResolvedForDisplay(confusionTargetSelf);

        if (confusionTargetSelf)
        {
            cardStatsDisplay?.UpdateDisplay();
            await Task.Delay(500);
            if (cardSequenceManager != null)
            {
                bool finished = await cardSequenceManager.ResolvePlayerAttackCombatAsync(atkList, enemyStatus, enemyStatus, cpuHand, token);
                BattleUIManager.I?.HideAllCardDetails();
                currentAttackCard = null;
                cardStatsDisplay?.UpdateDisplay();
                if (!finished)
                    return;
                SetGameState(GameState.CombatResolvePhase);
                return;
            }

            Debug.LogError("[BattleManager] CardSequenceManager が未設定のため、混乱時の自分攻撃を解決できません");
            SetGameState(GameState.CombatResolvePhase);
            return;
        }

        if (confusedEnemy)
            cardStatsDisplay?.UpdateDisplay();

        // オンライン：相手が TOTAL 切替で「自分自身」を攻撃対象にした場合、
        // こちらの防御フェーズはなく、相手自身に対して解決する（相手側の selfAttack 分岐のミラー）
        if (!confusedEnemy && IsOnlineMatch
            && enemyAI is RemotePlayerAgent remoteSelfAgent && remoteSelfAgent.LastAttackTargetSelf)
        {
            Debug.Log("[BattleManager] オンライン: 相手の自己対象攻撃を相手自身に解決（防御フェーズなし）");
            cardStatsDisplay?.UpdateDisplay();
            await Task.Delay(500);
            if (cardSequenceManager != null)
            {
                bool selfAttackFinished = await cardSequenceManager.ResolvePlayerAttackCombatAsync(
                    atkList, enemyStatus, enemyStatus, cpuHand, token);
                BattleUIManager.I?.HideAllCardDetails();
                currentAttackCard = null;
                cardStatsDisplay?.UpdateDisplay();
                if (!selfAttackFinished)
                    return;
                SetGameState(GameState.CombatResolvePhase);
                return;
            }

            Debug.LogError("[BattleManager] CardSequenceManager が未設定のため、相手の自己対象攻撃を解決できません");
            SetGameState(GameState.CombatResolvePhase);
            return;
        }

        var primary = HitRateRules.GetPrimaryForHitRate(atkList);
        int finalPct = HitRateRules.ComputeFinalHitPercent(primary, enemyStatus, playerStatus);
        bool rolledHit = HitRateRules.RollHit(finalPct);
        if (!rolledHit)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ニュッ1.mp3");
            BattleUIManager.I?.ShowMissPopup(playerStatus);
            await DamagePopup.WaitAfterPopupLifetimeAsync(DamagePopup.DefaultFadeDurationIfUnknown);
            BattleUIManager.I?.HideAllCardDetails();
            cardStatsDisplay?.ClearSequenceCardsAndAttackDisplayLocks();
            ClearMagicalExplosionComboMpPoolSnapshot();
            ClearMillionDollarBazookaComboGpPoolSnapshot();
            ClearTributeBloodHpPaidSnapshot();
            ClearHammadnessRollSnapshot();
            ClearMagicalSwordEnemyAttackState();
            currentAttackCard = null;
            SetGameState(GameState.CombatResolvePhase);
            return;
        }

        if (finalPct < 100)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/小パンチ.mp3");
            float popupSec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowCombatHitConfirmedPopup(playerStatus)
                : DamagePopup.DefaultFadeDurationIfUnknown;
            await DamagePopup.WaitAfterPopupLifetimeAsync(popupSec);
        }

        SetGameState(GameState.DefensePhase);
    }

    private List<CardData> GetEnemyAttackCardsForTurn(CardData primary)
    {
        if (_onlineEnemyAttackCombo != null && _onlineEnemyAttackCombo.Count > 0)
            return new List<CardData>(_onlineEnemyAttackCombo);
        if (enemyAI?.LastAttackSelection != null && enemyAI.LastAttackSelection.Count > 0)
            return new List<CardData>(enemyAI.LastAttackSelection);
        return primary != null ? new List<CardData> { primary } : new List<CardData>();
    }

    private bool ShouldUseEnemyAttackPresentationSequence(List<CardData> atkList, CardData primary)
    {
        if (cardSequenceManager == null || atkList == null || atkList.Count == 0) return false;
        if (RemotePlayerAgent.ShouldDeferRemoteAttackBookkeeping(atkList)) return true;
        if (atkList.Count > 1) return true;
        if (MagicalExplosionRules.ContainsMagicalExplosion(atkList)) return true;
        if (MillionDollarBazookaRules.ContainsMillionDollarBazooka(atkList)) return true;
        if (TributeBloodRules.ContainsTributeBlood(atkList)) return true;
        if (HammadnessRules.ContainsHammadness(atkList)) return true;
        return false;
    }

    public List<CardData> GetEnemyDefenseCardsForCombat()
    {
        if (enemyAI is RemotePlayerAgent remote
            && remote.LastDefenseSelection != null && remote.LastDefenseSelection.Count > 0)
            return new List<CardData>(remote.LastDefenseSelection);
        if (selectedDefenseCard != null)
            return new List<CardData> { selectedDefenseCard };
        return new List<CardData>();
    }

    public void SetSelectedCard(CardUI ui)
    {
        if (ui == null) return;
        var card = ui.GetCardData();
        if (card == null) return;

        if (HandReloadController.I != null && HandReloadController.I.IsReloadPopupContentOpen)
        {
            HandReloadController.I.OnHandCardClickedForReload(card);
            return;
        }

        // 連鎖反射 / PostDeath / 介入 / 双剣2回目など：GameState が攻撃系のままでも防御カード選択として扱う
        if (IsPlayerDefenseInputActive())
        {
            if (!CardRules.IsUsableInDefensePhase(card))
            {
                Debug.LogWarning($"このカードは防御フェーズでは使えません: {card.cardName} ({card.cardType})");
                return;
            }
            selectedDefenseCard = card;
            BattleUIManager.I?.ShowCardDetail(card, Side.Player);
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            UpdateTotalATKDEFDisplay();
            return;
        }

        if (CurrentState == GameState.AttackPhase && Attacker == PlayerType.Player)
        {
            // 売却モードが有効な場合は、売却処理に委譲
            if (sellFeature != null && sellFeature.IsSellModeActive())
            {
                sellFeature.OnCardSelected(card);
                return;
            }

            if (!CardRules.IsUsableInAttackPhase(card))
            {
                Debug.LogWarning($"このカードは攻撃フェーズでは使えません: {card.cardName} ({card.cardType})");
                return;
            }
            selectedCard = card;
            BattleUIManager.I?.ShowCardDetail(card, Side.Player);
            // カード選択音を再生
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            UpdateTotalATKDEFDisplay();
            return;
        }

        if (CurrentState != GameState.AttackPhase && CurrentState != GameState.DefensePhase)
        {
            Debug.Log($"カード選択は現在できません - State: {CurrentState}, Attacker: {Attacker}, Defender: {Defender}, Card: {card?.cardName}");
        }
    }

    public void OnUseButtonPressed()
    {
        if (isProcessingUseButton || _playerDefenseCombatResolving) return;

        if (IsPlayerDefenseInputActive())
        {
            isProcessingUseButton = true;
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.RefreshUseButton();

            if (IsPostDeathDefenseWaitActive())
                TrySubmitPostDeathPlayerDefense();
            else if (IsInterventionDefenseWaitActive())
                TrySubmitInterventionPlayerDefense();
            else
                HandleDefenseUse();
            return;
        }

        isProcessingUseButton = true;
        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.RefreshUseButton();

        if (CurrentState == GameState.AttackPhase && Attacker == PlayerType.Player)
        {
            if (playerStatus != null && playerStatus.HasFreezeEffect())
            {
                isProcessingUseButton = false;
                return;
            }
            HandleAttackUse();
        }
        else
        {
            isProcessingUseButton = false;
            BattleUIManager.I?.RefreshUseButton();
        }
    }

    /// <summary>
    /// 即時効果の対象（TOTAL のタップで切替）。攻撃・状態異常は赤＝自分側が効く相手。
    /// 回復・回復魔法は既定が自分、赤＝相手へ回復（直感に合わせて通常と逆）。
    /// </summary>
    private PlayerStatus ComputeImmediateEffectTargetForPlayerAttack(CardData card)
    {
        if (playerStatus != null && playerStatus.HasConfusionEffect())
            return BattleRandom.Range(0, 2) == 0 ? playerStatus : enemyStatus;
        bool recover = card != null && CardRules.IsRecoveryCard(card);
        if (recover)
            return _playerSelfAttackTargetMode ? enemyStatus : playerStatus;
        return _playerSelfAttackTargetMode ? playerStatus : enemyStatus;
    }

    /// <summary>敵ターンの即時効果の対象（回復＝自分、それ以外＝プレイヤー。混乱時はランダム）。
    /// オンラインでは相手が送ってきた対象トグル（自分へ攻撃／相手へ回復）を反映する。</summary>
    private PlayerStatus ResolveCpuImmediateEffectTarget(CardData attack)
    {
        if (attack == null) return playerStatus;
        if (enemyStatus != null && enemyStatus.HasConfusionEffect())
            return BattleRandom.Range(0, 2) == 0 ? enemyStatus : playerStatus;
        bool remoteTargetToggled = IsOnlineMatch
            && enemyAI is RemotePlayerAgent remoteAgent && remoteAgent.LastAttackTargetSelf;
        if (CardRules.IsRecoveryCard(attack))
            return remoteTargetToggled ? playerStatus : enemyStatus;
        return remoteTargetToggled ? enemyStatus : playerStatus;
    }

    private async Task ResolveImmediateEffectAsync(CardData card, int slotIndex, PlayerStatus presetEffectTarget = null)
    {
        // カード表示後、回復ポップアップより前に短い間（カード詳細の読み取り用）
        await Task.Delay(DamagePopup.PreImmediateEffectDelayMs);
        Debug.Log("[BattleManager] 回復カード表示後、即時効果前インターバル完了");

        // RecordPlayerUseSlotは既にHandleAttackUseで呼ばれている（UseCardの前）
        // ここでは呼ばない（二重呼び出しを防ぐ）

        PlayerStatus effectTarget = presetEffectTarget;
        if (effectTarget == null)
        {
            if (playerStatus != null && playerStatus.HasConfusionEffect())
            {
                ClearPlayerSelfAttackTargetMode();
                effectTarget = BattleRandom.Range(0, 2) == 0 ? playerStatus : enemyStatus;
            }
            else
            {
                effectTarget = ComputeImmediateEffectTargetForPlayerAttack(card);
            }
        }
        else if (playerStatus != null && playerStatus.HasConfusionEffect())
        {
            ClearPlayerSelfAttackTargetMode();
        }

        await battleProcessor.ResolveImmediateEffectAsync(card, playerStatus, effectTarget);

        ClearPlayerSelfAttackTargetMode();
        selectedCard = null;
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        UpdateTotalATKDEFDisplay();

        if (cardSequenceManager != null)
            await cardSequenceManager.RunAfterCombatSharedCleanupAsync(_phaseCts != null ? _phaseCts.Token : default);
        else
        {
            BattleUIManager.I?.HideAllCardDetails();
            cardStatsDisplay?.ClearSequenceCards();
            SetCurrentAttackCard(null);
            cardStatsDisplay?.UpdateDisplay();
            SetGameState(GameState.CombatResolvePhase);
        }
    }

    /// <summary>
    /// 攻撃確定後のカード掲示（<see cref="CardSequenceManager.StartCardSequenceAsync"/> の①クリア～②1枚表示と同じテンポ）。
    /// </summary>
    private async Task PlayAttackConfirmPresentationAsync(CardData card, Side side, CancellationToken ct)
    {
        if (card == null) return;

        cardStatsDisplay?.SetSequenceCards(new List<CardData>(), "攻撃", side);
        BattleUIManager.I?.ClearAllSelections();
        BattleUIManager.I?.HideAllCardDetails();

        await Task.Delay(300, ct);

        BattleUIManager.I?.ShowCardDetail(card, side);
        SetStatsDisplaySequenceCards(new List<CardData> { card }, "攻撃", side);
        cardStatsDisplay?.UpdateDisplay();
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");

        await Task.Delay(500, ct);
    }

    /// <summary>
    /// 攻撃フェーズで単体の即時カードを処理する。魔法のときは MP 消費・手札→MagicPanel 演出・MagicPool 登録を
    /// <see cref="CardSequenceManager"/> 経由と揃える。
    /// </summary>
    private async Task RunImmediateAttackSingleCardAsync(CardData card, int slotIndex)
    {
        // ShowCardDetail が「既選択のトグル解除」でターゲットモードをリセットする前に効果対象を固定する
        PlayerStatus immediateEffectTarget = ComputeImmediateEffectTargetForPlayerAttack(card);

        bool isMagic = card != null && card.cardType == CardType.Magic;
        bool useMagicPanel = isMagic && MagicPoolManager.I != null;
        bool fromMagicPanel =
            useMagicPanel && BattleUIManager.I != null && BattleUIManager.I.IsPlayerMagicCardUiOnMagicPanel(card);

        var tok = _phaseCts != null ? _phaseCts.Token : default;

        try
        {
            await PlayAttackConfirmPresentationAsync(card, Side.Player, tok);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (useMagicPanel)
        {
            if (!fromMagicPanel && MagicPoolManager.I != null
                && !MagicPoolManager.I.CanAddToPool(card, PlayerType.Player))
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel(
                    "マジックパネルに空きがありません", new Color(0.95f, 0.25f, 0.2f));
                isProcessingUseButton = false;
                BattleUIManager.I?.SetHandClickable(true);
                BattleUIManager.I?.RefreshUseButton();
                return;
            }

            if (playerStatus != null && card.mpCost > 0)
            {
                int pay = playerStatus.GetEffectiveMagicMpCost(card.mpCost);
                playerStatus.UseMP(pay);
                BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
            }

            if (fromMagicPanel)
            {
                // MagicPanel からの発動：手札→パネル演出・TryUseMagicCard（同種加算）は行わず ConsumeUse のみ
                MagicPoolManager.I.ConsumeUse(card);
                var drawn = await DrawOneCardAsync(trailingDelayMs: 0, playSoundOnDraw: false);
                if (drawn != null && handRefill != null)
                    await handRefill.RevealDrawnCardAfterCombatAsync(drawn, tok);
            }
            else
            {
                RectTransform handRt = null;
                if (card.cardUI != null)
                {
                    handRt = card.cardUI.cardImage != null
                        ? card.cardUI.cardImage.rectTransform
                        : card.cardUI.transform as RectTransform;
                }
                if (handRt != null && BattleUIManager.I != null && card.cardImage != null)
                {
                    int slot = MagicPoolManager.I.GetPredictedPlayerSlotIndex(card);
                    await BattleUIManager.I.PlayMagicFlyHandToPanelAsync(card, handRt, slot);
                }

                battleProcessor.UseCard(card, playerHand);

                System.Action drawCb = () => DrawOneCard();
                MagicPoolManager.I.TryUseMagicCard(card, playerHand, GetHandMaxCount(), drawCb);
            }
        }
        else
        {
            battleProcessor.UseCard(card, playerHand);
        }

        currentAttackCard = card;
        selectedCard = null;
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        UpdateTotalATKDEFDisplay();

        ClearPlayerSelfAttackTargetMode();

        if (immediateEffectTarget == playerStatus)
        {
            await ResolveImmediateEffectAsync(card, slotIndex, playerStatus);
            return;
        }

        SetGameState(GameState.DefensePhase);
    }

    private void HandleAttackUse()
    {
        var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
        if (selectedAttackCards == null || selectedAttackCards.Count == 0)
        {
            // オンライン：攻撃可能カードなし＝「祈り」でターンをパスし、相手に空の攻撃を通知する
            if (IsOnlineMatch && CardRules.GetAttackChoices(playerHand).Count == 0)
            {
                Debug.Log("[BattleManager] オンライン: 攻撃パス（祈り）");
                NetworkBattleBridge.SendAttackSelection(null);
                SetGameState(GameState.CombatResolvePhase);
                return;
            }

            Debug.LogWarning("攻撃カードが選択されていません");
            isProcessingUseButton = false;
            BattleUIManager.I?.SetHandClickable(true);
            BattleUIManager.I?.RefreshUseButton();
            return;
        }

        foreach (var c in selectedAttackCards)
        {
            if (c != null && c.cardType == CardType.Magic && playerStatus.IsMagicUseForbidden())
            {
                isProcessingUseButton = false;
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("魔法が使用できません", new Color(0.95f, 0.25f, 0.2f));
                BattleUIManager.I?.SetHandClickable(true);
                UpdateTotalATKDEFDisplay();
                return;
            }
        }

        if (!AttackComboSelectionRules.IsValidAttackSelection(selectedAttackCards))
        {
            isProcessingUseButton = false;
            BattleUIManager.I?.ShowInfoPopupOnCardPanel("先に攻撃カードを選んでください", new Color(0.85f, 0.35f, 0.15f));
            BattleUIManager.I?.SetHandClickable(true);
            UpdateTotalATKDEFDisplay();
            return;
        }

        int totalMagicMp = playerStatus.GetTotalEffectiveMagicMpForCards(selectedAttackCards);
        if (totalMagicMp > playerStatus.currentMP)
        {
            isProcessingUseButton = false;
            BattleUIManager.I?.ShowInfoPopupOnCardPanel("MPが足りません", new Color(0.95f, 0.25f, 0.2f));
            BattleUIManager.I?.SetHandClickable(true);
            UpdateTotalATKDEFDisplay();
            return;
        }

        // オンライン：検証を通過した確定選択を相手へ送信（相手側では RemotePlayerAgent が受信して同じ処理を実行）
        // 対象トグル（自分へ攻撃／相手へ回復）も一緒に送り、相手側で同じ対象解決を行う
        if (IsOnlineMatch)
            NetworkBattleBridge.SendAttackSelection(selectedAttackCards, _playerSelfAttackTargetMode);

        // 即時効果（回復・OnCardEffectResolve の状態異常など）の場合は通常処理
        // ※魔法カードはここでも MagicPool へ登録する（従来は即時分岐のみだと CardSequenceManager を経由せずプールに載らなかった）
        if (selectedAttackCards.Count == 1 && CardRules.IsImmediateAction(selectedAttackCards[0]))
        {
            var card = selectedAttackCards[0];
            int slotIndex = (card.cardUI != null) ? card.cardUI.transform.GetSiblingIndex() : -1;

            bool magicFromMagicPanel = card.cardType == CardType.Magic
                && BattleUIManager.I != null
                && BattleUIManager.I.IsPlayerMagicCardUiOnMagicPanel(card);
            if (slotIndex >= 0 && !magicFromMagicPanel)
                handRefill?.RecordPlayerUseSlot(slotIndex);

            _ = RunImmediateAttackSingleCardAsync(card, slotIndex);
            return;
        }

        // 攻撃カードの演出フローをCardSequenceManagerに委譲
        if (cardSequenceManager != null)
        {
            if (cardStatsDisplay != null && !ArchMagicRules.ContainsArchMagic(selectedAttackCards))
            {
                PlayerAttackTotalDisplayFlow.ResetAttackSequenceDisplayLocks(cardStatsDisplay);
                cardStatsDisplay.BeginAttackSequenceReveal(Side.Player);
                cardStatsDisplay.SetSequenceCards(new List<CardData>(), "攻撃", Side.Player);
                cardStatsDisplay.UpdateDisplay();
            }
            _ = RunPlayerAttackCardSequenceSafelyAsync(selectedAttackCards, _phaseCts.Token);
        }
        else
        {
            Debug.LogError("[BattleManager] CardSequenceManagerが設定されていません");
        }
    }

    private async Task RunPlayerAttackCardSequenceSafelyAsync(List<CardData> selectedAttackCards, CancellationToken cancellationToken)
    {
        if (cardSequenceManager == null) return;

        try
        {
            await cardSequenceManager.StartCardSequenceAsync(selectedAttackCards, "攻撃", Side.Player, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[BattleManager] Player attack card sequence cancelled");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ReleaseCardSequenceInputLocks();
        }
    }

    private void HandleDefenseUse()
    {
        if (_playerDefenseCombatResolving)
            return;

        if (IsPlayerChantingArchMagicWhileDefending())
        {
            TryAutoPassPlayerDefenseIfChantingArchMagic();
            return;
        }

        var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
        if (selectedDefenseCards == null || selectedDefenseCards.Count == 0)
        {
            // オンライン：空＝「許す」を相手へ通知
            if (IsOnlineMatch)
                NetworkBattleBridge.SendDefenseSelection(null);

            if (_reflectionChainDefenseTcs != null && !_reflectionChainDefenseTcs.Task.IsCompleted)
            {
                _reflectionChainDefenseTcs.TrySetResult(new List<CardData>());
                BattleUIManager.I?.ClearAllSelections();
                UpdateTotalATKDEFDisplay();
                return;
            }

            if (_parryRerunDefenseTcs != null && !_parryRerunDefenseTcs.Task.IsCompleted)
            {
                _parryRerunDefenseTcs.TrySetResult(new List<CardData>());
                BattleUIManager.I?.ClearAllSelections();
                UpdateTotalATKDEFDisplay();
                return;
            }

            // 防御カードを1枚も使わない場合（「許す」）
            Debug.Log("[BattleManager] 防御カードを使用せずにダメージを受ける（許す）");
            HandleNoDefenseCard();
            return;
        }

        if (_reflectionChainDefenseTcs != null && !_reflectionChainDefenseTcs.Task.IsCompleted)
        {
            if (playerStatus != null
                && playerStatus.HasRestraintEffect()
                && selectedDefenseCards.Count > 1)
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("体が重い", new Color(0.22f, 0.24f, 0.38f));
                return;
            }

            if (IsOnlineMatch)
                NetworkBattleBridge.SendDefenseSelection(selectedDefenseCards);

            _reflectionChainDefenseTcs.TrySetResult(new List<CardData>(selectedDefenseCards));
            BattleUIManager.I?.ClearAllSelections();
            UpdateTotalATKDEFDisplay();
            return;
        }

        if (_parryRerunDefenseTcs != null && !_parryRerunDefenseTcs.Task.IsCompleted)
        {
            if (playerStatus != null
                && playerStatus.HasRestraintEffect()
                && selectedDefenseCards.Count > 1)
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("体が重い", new Color(0.22f, 0.24f, 0.38f));
                return;
            }

            if (IsOnlineMatch)
                NetworkBattleBridge.SendDefenseSelection(selectedDefenseCards);

            _parryRerunDefenseTcs.TrySetResult(new List<CardData>(selectedDefenseCards));
            BattleUIManager.I?.ClearAllSelections();
            UpdateTotalATKDEFDisplay();
            return;
        }

        if (Defender == PlayerType.Player
            && playerStatus != null
            && playerStatus.HasRestraintEffect()
            && selectedDefenseCards.Count > 1)
        {
            BattleUIManager.I?.ShowInfoPopupOnCardPanel("体が重い", new Color(0.22f, 0.24f, 0.38f));
            return;
        }

        // オンライン：確定した防御選択を相手へ送信
        if (IsOnlineMatch)
            NetworkBattleBridge.SendDefenseSelection(selectedDefenseCards);

        if (Defender == PlayerType.Player && selectedDefenseCards.Count > 0)
        {
            var incoming = GetIncomingAttackSnapshotForDefenseUi();
            foreach (var defCard in selectedDefenseCards)
            {
                if (defCard == null) continue;
                if (defCard.cardType == CardType.Magic && playerStatus != null
                    && !BlockingRules.CanAffordMagicDefenseMp(defCard, playerStatus))
                {
                    BattleUIManager.I?.ShowInfoPopupOnCardPanel("MPが足りない", new Color(0.95f, 0.22f, 0.2f));
                    return;
                }
                if (BlockingRules.IsPhysicalBlockingCard(defCard)
                    && (incoming == null
                        || !BlockingRules.CanUsePhysicalBlockingAgainstAttack(defCard, incoming)))
                {
                    BattleUIManager.I?.ShowInfoPopupOnCardPanel(
                        "無属性の物理攻撃にのみ使えます", new Color(0.85f, 0.25f, 0.2f));
                    return;
                }
            }
        }

        // 防御カードの演出フローをCardSequenceManagerに委譲
        if (cardSequenceManager != null)
        {
            _playerDefenseCombatResolving = true;
            _ = RunPlayerDefenseCardSequenceAsync(selectedDefenseCards);
        }
        else
        {
            Debug.LogError("[BattleManager] CardSequenceManagerが設定されていません");
        }
    }

    private async Task RunPlayerDefenseCardSequenceAsync(List<CardData> selectedDefenseCards)
    {
        try
        {
            if (cardSequenceManager != null)
                await cardSequenceManager.StartCardSequenceAsync(
                    selectedDefenseCards, "防御", Side.Player, _phaseCts.Token);
        }
        finally
        {
            _playerDefenseCombatResolving = false;
        }
    }

    /// <summary>
    /// 防御カードを1枚も使わない場合の処理（「許す」）
    /// </summary>
    private async void HandleNoDefenseCard()
    {
        if (_playerDefenseCombatResolving)
            return;

        _playerDefenseCombatResolving = true;
        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.SetUseButtonInteractable(false);

        // キャンセルトークンを先にキャプチャ（await 後に _phaseCts が Dispose される可能性があるため）
        var token = _phaseCts.Token;

        try
        {
        // 選択状態をクリア
        BattleUIManager.I?.ClearAllSelections();
        UpdateTotalATKDEFDisplay();

        // 戦闘解決処理（防御カードなし）
        var atk = (Attacker == PlayerType.Player) ? playerStatus : enemyStatus;
        var def = (Defender == PlayerType.Player) ? playerStatus : enemyStatus;
        var defHand = (Defender == PlayerType.Player) ? playerHand : cpuHand;

        List<CardData> attackCards = GetAttackCardsForCombat();

        if (attackCards != null && attackCards.Count == 1 && attackCards[0] != null
            && CardRules.IncomingRequiresFullOnlyReactiveDefense(attackCards))
        {
            await battleProcessor.ResolveImmediateEffectAsync(attackCards[0], atk, def);
            if (token.IsCancellationRequested) return;
            ClearMagicalExplosionComboMpPoolSnapshot();
            ClearMillionDollarBazookaComboGpPoolSnapshot();
            ClearTributeBloodHpPaidSnapshot();
            ClearHammadnessRollSnapshot();
            BattleUIManager.I?.HideAllCardDetails();
            cardStatsDisplay?.ClearSequenceCardsAndAttackDisplayLocks();
            currentAttackCard = null;
            cardStatsDisplay?.UpdateDisplay();
            SetGameState(GameState.CombatResolvePhase);
            return;
        }

        // 防御カードなしで戦闘解決（敵の攻撃は RunEnemyTurnAsync で命中済み）
        bool skipHit = Attacker == PlayerType.Enemy;
        await battleProcessor.ResolveCombatAsync(attackCards, (CardData)null, atk, def, defHand, skipHit);

        if (token.IsCancellationRequested) return;

        if (await TryPreparePlayerDualBladeSecondDefenseIfNeededAsync(token))
            return;

        ClearMagicalExplosionComboMpPoolSnapshot();
        ClearMillionDollarBazookaComboGpPoolSnapshot();
        ClearTributeBloodHpPaidSnapshot();
        ClearHammadnessRollSnapshot();
        // ダメージ処理完了後、全カード表示をクリア
        BattleUIManager.I?.HideAllCardDetails();
        cardStatsDisplay?.ClearSequenceCardsAndAttackDisplayLocks();
        currentAttackCard = null;
        cardStatsDisplay?.UpdateDisplay();

        // 防御カード確定後の処理
        SetGameState(GameState.CombatResolvePhase);
        }
        finally
        {
            _playerDefenseCombatResolving = false;
            isProcessingUseButton = false;
        }
    }


    /// <summary>
    /// 戦闘用攻撃カードを取得（RunDefenseConfirmAsync、HandleNoDefenseCardから使用）
    /// </summary>
    private List<CardData> GetAttackCardsForCombat()
    {
        if (Attacker == PlayerType.Player)
        {
            var uiAttackCards = BattleUIManager.I?.GetSelectedAttackCards() ?? new List<CardData>();
            if (uiAttackCards.Count > 0)
                return uiAttackCards;
            if (_playerAttackComboForCombat != null && _playerAttackComboForCombat.Count > 0)
                return new List<CardData>(_playerAttackComboForCombat);
            if (currentAttackCard != null)
                return new List<CardData> { currentAttackCard };
            return uiAttackCards;
        }
        else
        {
            if (_enemyAttackComboForCombat != null && _enemyAttackComboForCombat.Count > 0)
                return new List<CardData>(_enemyAttackComboForCombat);
            if (_onlineEnemyAttackCombo != null && currentAttackCard != null
                && _onlineEnemyAttackCombo.Contains(currentAttackCard))
                return new List<CardData>(_onlineEnemyAttackCombo);
            if (IsOnlineMatch && enemyAI is RemotePlayerAgent remote
                && remote.LastAttackSelection != null && remote.LastAttackSelection.Count > 0
                && currentAttackCard != null && remote.LastAttackSelection.Contains(currentAttackCard))
                return new List<CardData>(remote.LastAttackSelection);
            return new List<CardData> { currentAttackCard };
        }
    }

    public void RefreshSummonSkillButtonInteractables()
    {
        summonSkillButton?.RefreshInteractable();
        enemySummonSkillButton?.RefreshInteractable();
    }

    public bool TryOpenSummonSkillPopup(PlayerStatus summoner, PlayerStatus opponent)
    {
        // オンライン対戦（PoC）：顕現スキルは未対応
        if (IsOnlineMatch) return false;
        if (_summonSkillPopupRoot != null || summoner == null || opponent == null) return false;
        if (summoner.hasUsedManifestationSkill) return false;
        if (summoner.HasFreezeEffect()) return false;
        if (CurrentState != GameState.AttackPhase) return false;

        bool summonerIsPlayer = ReferenceEquals(summoner, playerStatus);
        if (CurrentTurnOwner != (summonerIsPlayer ? PlayerType.Player : PlayerType.Enemy))
            return false;

        if (summoner.summonData == null || summoner.summonData.manifestationCard == null) return false;
        if (IsEconomicActionInProgress()) return false;
        if (CardSelectionManager.I != null && CardSelectionManager.I.SelectedCardCount > 0) return false;

        var prefab = Resources.Load<GameObject>("Prefab/SummonSkillPopup");
        if (prefab == null)
        {
            Debug.LogError("[BattleManager] Resources/Prefab/SummonSkillPopup が見つかりません");
            return false;
        }

        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetPopupCanvas() : null;
        if (canvas == null) return false;

        _summonSkillPopupRoot = Instantiate(prefab, canvas.transform, false);
        var rt = _summonSkillPopupRoot.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        BindSummonSkillPopupUi(_summonSkillPopupRoot, summoner, opponent);

        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.SetUseButtonInteractable(false);
        BattleUIManager.I?.DisableEconomicActionButtonsTemporarily();

        RefreshSummonSkillButtonInteractables();
        return true;
    }

    /// <summary>
    /// SummonSkillPopup プレハブは「外枠 → 内側パネル」の2段になっていることがあり、
    /// Instantiate のルート直下にはボタンが無い。その場合は子をパネルとして扱う。
    /// </summary>
    private static Transform ResolveSummonSkillPopupPanel(Transform root)
    {
        if (root == null) return null;
        if (root.Find("SummonButton") != null) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child != null && child.Find("SummonButton") != null)
                return child;
        }
        return root;
    }

    private void BindSummonSkillPopupUi(GameObject root, PlayerStatus summoner, PlayerStatus opponent)
    {
        var summon = summoner.summonData;
        if (summon == null || root == null) return;

        var panel = ResolveSummonSkillPopupPanel(root.transform);
        if (panel == null) return;

        var nameT = panel.Find("SummonSkillName")?.GetComponent<TMPro.TMP_Text>();
        var descT = panel.Find("SummonSkillDesc")?.GetComponent<TMPro.TMP_Text>();
        var manifestBtn = panel.Find("SummonButton")?.GetComponent<Button>();
        var cancelBtn = panel.Find("CancelButton")?.GetComponent<Button>();

        if (nameT != null)
        {
            nameT.text = summon.specialSkillName;
            summon.ApplyStyleTo(nameT, summon.textStyle);
        }
        if (descT != null)
        {
            descT.text = summon.specialSkillDescription;
            summon.ApplyStyleTo(descT, summon.popupSkillDescStyle);
        }

        if (manifestBtn != null)
        {
            manifestBtn.onClick.RemoveAllListeners();
            manifestBtn.onClick.AddListener(() => OnSummonSkillManifestClicked(summoner, opponent));
        }
        if (cancelBtn != null)
        {
            cancelBtn.onClick.RemoveAllListeners();
            cancelBtn.onClick.AddListener(OnSummonSkillPopupCancelClicked);
        }

        if (manifestBtn == null || cancelBtn == null)
            Debug.LogWarning("[BattleManager] SummonSkillPopup: SummonButton/CancelButton が見つかりません。プレハブ階層を確認してください。");
    }

    private void OnSummonSkillPopupCancelClicked()
    {
        if (_summonSkillPopupRoot != null)
        {
            Destroy(_summonSkillPopupRoot);
            _summonSkillPopupRoot = null;
        }
        RefreshSummonSkillButtonInteractables();
        if (CurrentState == GameState.AttackPhase && CurrentTurnOwner == PlayerType.Player)
            EnterAttackPhase();
        else if (CurrentState == GameState.AttackPhase && CurrentTurnOwner == PlayerType.Enemy)
        {
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.SetIntroModeUI(playerHand);
        }
    }

    private void OnSummonSkillManifestClicked(PlayerStatus summoner, PlayerStatus opponent)
    {
        if (_manifestationFlowRunning) return;
        if (_summonSkillPopupRoot != null)
        {
            Destroy(_summonSkillPopupRoot);
            _summonSkillPopupRoot = null;
        }
        RefreshSummonSkillButtonInteractables();
        _manifestationFlowRunning = true;
        summoner.MarkManifestationSkillUsed();
        statusUI?.UpdateStatus(playerStatus, enemyStatus);
        _ = RunSummonManifestationFlowAsync(summoner, opponent);
    }

    private async Task RunSummonManifestationFlowAsync(PlayerStatus summoner, PlayerStatus opponent)
    {
        try
        {
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.SetUseButtonInteractable(false);

            if (cardSequenceManager != null)
                await cardSequenceManager.RunManifestationSkillSequenceAsync(summoner, opponent);
        }
        finally
        {
            _manifestationFlowRunning = false;
            RefreshSummonSkillButtonInteractables();
            if (CurrentState == GameState.AttackPhase && CurrentTurnOwner == PlayerType.Player)
                EnterAttackPhase();
            else if (CurrentState == GameState.DefensePhase && Defender == PlayerType.Player)
            {
                BattleUIManager.I?.SetHandClickable(true);
                RefreshPlayerDefensePhaseInteractivity();
                BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
            }
        }
    }

    /// <summary>敵側からの顕現：命中判定の後、プレイヤー防御フェーズへ。</summary>
    public async Task PresentEnemyManifestationAttackToPlayerDefenseAsync(
        List<CardData> atkList,
        CancellationToken cancellationToken)
    {
        if (atkList == null || atkList.Count == 0 || enemyStatus == null || playerStatus == null) return;

        if (enemyStatus.HasConfusionEffect())
        {
            bool confusionTargetSelf = BattleRandom.Range(0, 2) == 0;
            SetConfusionAttackTargetResolvedForDisplay(confusionTargetSelf);
            if (confusionTargetSelf)
            {
                cardStatsDisplay?.UpdateDisplay();
                await Task.Delay(500, cancellationToken);
                if (cardSequenceManager != null)
                {
                    bool finished = await cardSequenceManager.ResolvePlayerAttackCombatAsync(
                        atkList, enemyStatus, enemyStatus, cpuHand, cancellationToken);
                    BattleUIManager.I?.HideAllCardDetails();
                    currentAttackCard = null;
                    cardStatsDisplay?.UpdateDisplay();
                    if (!finished) return;
                    await cardSequenceManager.RunAfterCombatSharedCleanupAsync(cancellationToken);
                }
                return;
            }
        }

        var primary = HitRateRules.GetPrimaryForHitRate(atkList);
        int finalPct = HitRateRules.ComputeFinalHitPercent(primary, enemyStatus, playerStatus);
        bool rolledHit = HitRateRules.RollHit(finalPct);
        if (!rolledHit)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ニュッ1.mp3");
            BattleUIManager.I?.ShowMissPopup(playerStatus);
            await DamagePopup.WaitAfterPopupLifetimeAsync(DamagePopup.DefaultFadeDurationIfUnknown, cancellationToken);
            BattleUIManager.I?.HideAllCardDetails();
            currentAttackCard = null;
            cardStatsDisplay?.ClearSequenceCards();
            cardStatsDisplay?.UpdateDisplay();
            SetGameState(GameState.CombatResolvePhase);
            return;
        }

        if (finalPct < 100)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/小パンチ.mp3");
            float popupSec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowCombatHitConfirmedPopup(playerStatus)
                : DamagePopup.DefaultFadeDurationIfUnknown;
            await DamagePopup.WaitAfterPopupLifetimeAsync(popupSec, cancellationToken);
        }

        SetGameState(GameState.DefensePhase);
    }

    public void ToggleTurnOwner()
    {
        CurrentTurnOwner = (CurrentTurnOwner == PlayerType.Player) ? PlayerType.Enemy : PlayerType.Player;
        Debug.Log($"[Turn] 手番変更: {CurrentTurnOwner}");
    }

    // ================ オンライン：ホスト権威の状態同期 ================

    /// <summary>戦闘解決後の HP/MP/GP 同期をクライアントが待つ最大時間。</summary>
    private const int OnlineResolveSyncTimeoutMs = 20000;
    /// <summary>ターン境界の待ち合わせ（演出完了の相互確認）の最大時間。</summary>
    private const int OnlineTurnSyncTimeoutMs = 45000;

    /// <summary>両クライアントで一致するターン識別タグ（古い同期メッセージの読み捨て用）。</summary>
    private int OnlineTurnTag => _summonTurnCounters.PlayerOwnTurnsEnded + _summonTurnCounters.EnemyOwnTurnsEnded;

    private static NetworkBattleBridge.SideStatus CaptureSideStatus(PlayerStatus s) => new NetworkBattleBridge.SideStatus
    {
        Hp = s != null ? s.currentHP : 0,
        Mp = s != null ? s.currentMP : 0,
        Gp = s != null ? s.currentGP : 0,
    };

    /// <summary>ホストの権威値でローカルの HP/MP/GP を上書きする（差分があれば警告ログ）。</summary>
    private static void ApplyAuthoritativeSideStatus(PlayerStatus target, NetworkBattleBridge.SideStatus a, string label)
    {
        if (target == null) return;
        if (target.currentHP == a.Hp && target.currentMP == a.Mp && target.currentGP == a.Gp) return;

        Debug.LogWarning(
            $"[OnlineSync] {label}のステータスをホスト値へ補正: " +
            $"HP {target.currentHP}→{a.Hp}, MP {target.currentMP}→{a.Mp}, GP {target.currentGP}→{a.Gp}");
        target.currentHP = Mathf.Clamp(a.Hp, 0, target.maxHP);
        target.currentMP = Mathf.Clamp(a.Mp, 0, target.maxMP);
        target.currentGP = Mathf.Clamp(a.Gp, 0, target.maxGP);
    }

    /// <summary>
    /// 戦闘解決フェーズ入口：ダメージ結果（HP/MP/GP）のホスト権威同期。
    /// ホストは自分の解決結果を送信し、クライアントは受信して自分の値を上書きする。
    /// </summary>
    private async Task RunOnlineResolveStateSyncAsync(CancellationToken ct)
    {
        if (!IsOnlineMatch || _gameEndTriggered) return;

        try
        {
            if (OnlineMatchContext.IsHost)
            {
                NetworkBattleBridge.SendResolveState(new NetworkBattleBridge.ResolveStateSync
                {
                    TurnTag = OnlineTurnTag,
                    Host = CaptureSideStatus(playerStatus),
                    Client = CaptureSideStatus(enemyStatus),
                });
                return;
            }

            // クライアント：ホストの解決結果を待つ（古いタグは読み捨て）
            NetworkBattleBridge.ResolveStateSync sync;
            for (int attempt = 0; ; attempt++)
            {
                var waitTask = NetworkBattleBridge.WaitForResolveStateAsync(ct);
                var finished = await Task.WhenAny(waitTask, Task.Delay(OnlineResolveSyncTimeoutMs, ct));
                if (finished != waitTask || ct.IsCancellationRequested)
                {
                    Debug.LogWarning("[OnlineSync] ResolveState 待ちがタイムアウト。ローカル値のまま続行します（ターン境界で補正）");
                    return;
                }
                sync = await waitTask;
                if (sync.TurnTag >= OnlineTurnTag || attempt >= 3) break;
                Debug.Log($"[OnlineSync] 古い ResolveState (tag={sync.TurnTag}) を読み捨て");
            }

            // クライアント視点：Client=自分、Host=相手
            ApplyAuthoritativeSideStatus(playerStatus, sync.Client, "自分");
            ApplyAuthoritativeSideStatus(enemyStatus, sync.Host, "相手");
            BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);

            // 権威補正で HP0 になった場合の終了処理
            await TryHandleDeathIfAnyAsync(ct);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[OnlineSync] ResolveState 同期がキャンセルされました");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    /// <summary>
    /// ターン境界（EndPhase 末尾）：両者の演出完了を待ち合わせ、ホスト権威で
    /// HP/MP/GP・手札全リスト・次ターン所有者・ターンカウンタ・召喚獣を同期する。
    /// </summary>
    /// <returns>true = ターン所有者を同期側で適用済み（呼び出し側の ToggleTurnOwner は不要）。</returns>
    private async Task<bool> RunOnlineTurnBoundarySyncAsync(CancellationToken ct)
    {
        if (!IsOnlineMatch || _gameEndTriggered) return false;

        try
        {
            if (OnlineMatchContext.IsHost)
            {
                // クライアントの演出完了（TurnReady）を待つ
                var readyTask = NetworkBattleBridge.WaitForTurnReadyAsync(ct);
                var finished = await Task.WhenAny(readyTask, Task.Delay(OnlineTurnSyncTimeoutMs, ct));
                if (finished != readyTask)
                    Debug.LogWarning("[OnlineSync] TurnReady 待ちがタイムアウト。同期を送信して続行します");

                var nextOwner = CurrentTurnOwner == PlayerType.Player ? PlayerType.Enemy : PlayerType.Player;
                NetworkBattleBridge.SendTurnSync(new NetworkBattleBridge.TurnBoundarySync
                {
                    TurnTag = OnlineTurnTag,
                    HostOwnsNextTurn = nextOwner == PlayerType.Player,
                    Host = CaptureSideStatus(playerStatus),
                    Client = CaptureSideStatus(enemyStatus),
                    HostSummonIndex = FindSummonIndex(playerStatus != null ? playerStatus.summonData : null),
                    ClientSummonIndex = FindSummonIndex(enemyStatus != null ? enemyStatus.summonData : null),
                    HostOwnTurnsEnded = _summonTurnCounters.PlayerOwnTurnsEnded,
                    ClientOwnTurnsEnded = _summonTurnCounters.EnemyOwnTurnsEnded,
                    HostHand = CollectCardNames(playerHand),
                    ClientHand = CollectCardNames(cpuHand),
                    HostArchMagic = CaptureArchMagicSideSync(playerStatus),
                    ClientArchMagic = CaptureArchMagicSideSync(enemyStatus),
                });

                CurrentTurnOwner = nextOwner;
                Debug.Log($"[Turn] 手番変更(ホスト権威): {CurrentTurnOwner}");
                return true;
            }
            else
            {
                // クライアント：自分の演出完了を通知し、ホストの権威状態を待つ
                NetworkBattleBridge.SendTurnReady(OnlineTurnTag);

                NetworkBattleBridge.TurnBoundarySync sync;
                for (int attempt = 0; ; attempt++)
                {
                    var syncTask = NetworkBattleBridge.WaitForTurnSyncAsync(ct);
                    var finished = await Task.WhenAny(syncTask, Task.Delay(OnlineTurnSyncTimeoutMs, ct));
                    if (finished != syncTask || ct.IsCancellationRequested)
                    {
                        Debug.LogWarning("[OnlineSync] TurnSync 待ちがタイムアウト。ローカル値のまま続行します");
                        return false;
                    }

                    sync = await syncTask;
                    if (sync.TurnTag >= OnlineTurnTag || attempt >= 3) break;
                    Debug.Log($"[OnlineSync] 古い TurnSync (tag={sync.TurnTag}) を読み捨て");
                }

                ApplyAuthoritativeTurnBoundary(sync);

                // 権威補正で HP0 になった場合の終了処理
                await TryHandleDeathIfAnyAsync(ct);
                return true;
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[OnlineSync] ターン境界同期がキャンセルされました");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return false;
        }
    }

    /// <summary>クライアント：ホストのターン境界同期を自分視点（Client=自分 / Host=相手）で適用する。</summary>
    private void ApplyAuthoritativeTurnBoundary(NetworkBattleBridge.TurnBoundarySync sync)
    {
        ApplyAuthoritativeSideStatus(playerStatus, sync.Client, "自分");
        ApplyAuthoritativeSideStatus(enemyStatus, sync.Host, "相手");

        _summonTurnCounters.PlayerOwnTurnsEnded = sync.ClientOwnTurnsEnded;
        _summonTurnCounters.EnemyOwnTurnsEnded = sync.HostOwnTurnsEnded;

        VerifyOrFixSummon(playerStatus, sync.ClientSummonIndex, "自分");
        VerifyOrFixSummon(enemyStatus, sync.HostSummonIndex, "相手");

        ReconcileHandToAuthoritative(playerHand, sync.ClientHand, withUi: true, label: "自分手札");
        ReconcileHandToAuthoritative(cpuHand, sync.HostHand, withUi: false, label: "相手手札");

        ApplyAuthoritativeArchMagicSide(playerStatus, sync.ClientArchMagic);
        ApplyAuthoritativeArchMagicSide(enemyStatus, sync.HostArchMagic);

        CurrentTurnOwner = sync.HostOwnsNextTurn ? PlayerType.Enemy : PlayerType.Player;
        Debug.Log($"[Turn] 手番変更(ホスト権威): {CurrentTurnOwner}");

        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        BattleUIManager.I?.RefreshTurnCountDisplay(_summonTurnCounters, CurrentTurnOwner);
    }

    /// <summary>SummonSelectionManager の一覧内インデックス（ハンドシェイクと同じ基準）。</summary>
    private static int FindSummonIndex(SummonData data)
    {
        var list = SummonSelectionManager.I != null ? SummonSelectionManager.I.GetAllSummonData() : null;
        if (list == null || data == null) return -1;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] == data) return i;
            if (list[i] != null && list[i].name == data.name) return i;
        }
        return -1;
    }

    private void VerifyOrFixSummon(PlayerStatus status, int authoritativeIndex, string label)
    {
        if (status == null || authoritativeIndex < 0) return;
        int local = FindSummonIndex(status.summonData);
        if (local == authoritativeIndex) return;

        var list = SummonSelectionManager.I != null ? SummonSelectionManager.I.GetAllSummonData() : null;
        if (list == null || authoritativeIndex >= list.Length || list[authoritativeIndex] == null) return;

        Debug.LogWarning($"[OnlineSync] {label}の召喚獣がホストと不一致（local={local}, host={authoritativeIndex}）。ホスト値を採用します");
        status.SetSummonData(list[authoritativeIndex]);
    }

    private static NetworkBattleBridge.ArchMagicSideSync CaptureArchMagicSideSync(PlayerStatus status)
    {
        if (status == null || !status.IsCastingArchMagic)
            return default;

        var card = status.archMagicCastingCard;
        return new NetworkBattleBridge.ArchMagicSideSync
        {
            RemainingTurns = status.archMagicRemainingTurns,
            BarrierRemaining = status.archMagicBarrierRemaining,
            CardName = card != null ? (string.IsNullOrEmpty(card.cardName) ? card.name : card.cardName) : "",
            TargetSelf = status.archMagicEffectTarget == status,
        };
    }

    private void ApplyAuthoritativeArchMagicSide(PlayerStatus status, NetworkBattleBridge.ArchMagicSideSync sync)
    {
        if (status == null) return;

        if (sync.RemainingTurns <= 0 || string.IsNullOrEmpty(sync.CardName))
        {
            if (status.IsCastingArchMagic && !status.archMagicCancelPending)
                status.ClearArchMagicCastingState();
            RefreshArchMagicBarrierUi(status);
            return;
        }

        var template = ArchMagicRules.FindTemplateByDisplayOrAssetName(sync.CardName);
        if (template == null)
        {
            Debug.LogWarning($"[OnlineSync] ArchMagic template not found: {sync.CardName}");
            return;
        }

        PlayerStatus effectTarget = sync.TargetSelf
            ? status
            : (ReferenceEquals(status, playerStatus) ? enemyStatus : playerStatus);
        status.ApplyAuthoritativeArchMagicCasting(template, sync.RemainingTurns, effectTarget, sync.BarrierRemaining);
        RefreshArchMagicBarrierUi(status);
    }

    private void RefreshArchMagicBarrierUi(PlayerStatus status)
    {
        if (status == null) return;
        Side side = ReferenceEquals(status, playerStatus) ? Side.Player : Side.Enemy;
        if (status.IsCastingArchMagic)
            BattleUIManager.I?.ShowArchMagicBarrier(side, status.archMagicBarrierRemaining);
        else
            BattleUIManager.I?.HideArchMagicBarrier(side);
    }

    private static List<string> CollectCardNames(List<CardData> hand)
    {
        var names = new List<string>(hand != null ? hand.Count : 0);
        if (hand == null) return names;
        foreach (var c in hand)
        {
            if (c != null)
                names.Add(c.cardName ?? "");
        }
        return names;
    }

    /// <summary>
    /// クライアント：手札のカードリストをホストの権威リストへ合わせる。
    /// 余分なカードは除去（UI ごと破棄）、不足分はテンプレートから生成して追加する。
    /// </summary>
    private void ReconcileHandToAuthoritative(List<CardData> hand, List<string> authoritative, bool withUi, string label)
    {
        if (hand == null || authoritative == null) return;

        var need = new Dictionary<string, int>();
        foreach (var n in authoritative)
        {
            if (string.IsNullOrEmpty(n)) continue;
            need.TryGetValue(n, out int c);
            need[n] = c + 1;
        }

        bool changed = false;

        // 余分なカードを除去（後ろから走査）
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            var card = hand[i];
            string nm = card != null ? card.cardName : null;
            if (!string.IsNullOrEmpty(nm) && need.TryGetValue(nm, out int remain) && remain > 0)
            {
                need[nm] = remain - 1;
                continue;
            }

            Debug.LogWarning($"[OnlineSync] {label}: ホストに無いカード '{nm}' を除去します");
            if (withUi && card != null && card.cardUI != null)
                Destroy(card.cardUI.gameObject);
            hand.RemoveAt(i);
            changed = true;
        }

        // 不足分をテンプレートから補充
        foreach (var kv in need)
        {
            for (int k = 0; k < kv.Value; k++)
            {
                var template = cardDealer != null ? cardDealer.FindTemplateByName(kv.Key) : null;
                if (template == null)
                {
                    Debug.LogError($"[OnlineSync] {label}: カード '{kv.Key}' のテンプレートが見つかりません");
                    continue;
                }

                var instance = cardDealer.InstantiateCardFromTemplate(template);
                if (instance == null) continue;
                hand.Add(instance);
                if (withUi)
                {
                    var ui = cardDealer.CreateCardUIForHand(instance);
                    ui?.Reveal();
                }
                changed = true;
                Debug.LogWarning($"[OnlineSync] {label}: 不足カード '{kv.Key}' をホスト値に合わせて追加しました");
            }
        }

        if (changed && withUi)
            BattleUIManager.I?.SetIntroModeUI(playerHand);
    }

    public void ClearSelectedCards()
    {
        selectedCard = null;
        selectedDefenseCard = null;
        UpdateTotalATKDEFDisplay();
    }

    public void UpdateTotalATKDEFDisplay()
    {
        cardStatsDisplay?.UpdateDisplay();
        BattleUIManager.I?.RefreshUseButton();
    }

    private List<CardData> _postDeathChainAttackDisplay;
    private Side _postDeathChainAttackDisplaySide = Side.Player;

    public bool IsPostDeathChainAttackDisplayActive =>
        _postDeathChainAttackDisplay != null && _postDeathChainAttackDisplay.Count > 0;

    public IReadOnlyList<CardData> GetPostDeathChainAttackDisplayCards() => _postDeathChainAttackDisplay;

    public Side GetPostDeathChainAttackDisplaySide() => _postDeathChainAttackDisplaySide;

    public void SetPostDeathChainAttackDisplay(IReadOnlyList<CardData> cards, Side deadSide)
    {
        _postDeathChainAttackDisplay = cards != null && cards.Count > 0
            ? new List<CardData>(cards)
            : null;
        _postDeathChainAttackDisplaySide = deadSide;
        UpdateTotalATKDEFDisplay();
    }

    public void ClearPostDeathChainAttackDisplay()
    {
        _postDeathChainAttackDisplay = null;
        UpdateTotalATKDEFDisplay();
    }

    /// <summary>
    /// 道連れ1回分の開始前：前戦闘のフェーズを解き StandBy へ（gameEnd ガード中は SetGameState を使わない）。
    /// </summary>
    public void EnterPostDeathChainNeutralPhase()
    {
        CurrentState = GameState.StandByPhase;
        isProcessingUseButton = false;
        _playerDefenseCombatResolving = false;
    }

    /// <summary>
    /// 死亡者の臨時攻撃 → 生存者の防御フェーズへ。HandleStateChange は呼ばない（DeadlyChainFlow が入力を管理）。
    /// </summary>
    public void EnterPostDeathChainCombatPhase(PlayerType deadAttackerSide)
    {
        CurrentTurnOwner = deadAttackerSide;
        CurrentState = GameState.DefensePhase;
        isProcessingUseButton = false;
        _playerDefenseCombatResolving = false;
    }

    /// <summary>
    /// 道連れの鎖1回分：前の戦闘 TOTAL を消し、新しい攻防表示の土台にする。
    /// </summary>
    public void PreparePostDeathChainCombatUi()
    {
        ClearPlayerSelfAttackTargetMode();
        ClearReflectionAttackTotalDisplay();
        ClearPostDeathChainAttackDisplay();
        ClearStatsDisplaySequenceCards();
        SetCurrentAttackCard(null);
        ClearPlayerAttackComboForCombat();
        ClearEnemyAttackComboForCombat();
        ResetPlayerDefenseUseButtonLocks();
        ClearSelectedCards();
        BattleUIManager.I?.ClearAllSelections();
        BattleUIManager.I?.HideAllCardDetails();
        UpdateTotalATKDEFDisplay();
    }

    /// <summary>反射連鎖など <see cref="CardSequenceManager"/> 外のカード表示に、TotalATKDEF を同期させる。</summary>
    public void SetStatsDisplaySequenceCards(List<CardData> cards, string cardType, Side ownerSide)
    {
        cardStatsDisplay?.SetSequenceCards(cards, cardType, ownerSide);
        cardStatsDisplay?.UpdateDisplay();
    }

    /// <summary>演出シーケンス終了時など、TotalATKDEF を通常ロジックへ戻す。</summary>
    public void ClearStatsDisplaySequenceCards()
    {
        cardStatsDisplay?.ClearSequenceCardsAndAttackDisplayLocks();
        cardStatsDisplay?.UpdateDisplay();
    }

    /// <summary>スライドアニメ完了後：元攻撃側の TOTAL ATK を消し、反射した側のパネルに同じ攻撃 ATK を表示する。</summary>
    /// <param name="totalAtkOnPlayerSide">スライド先がプレイヤー側なら true（敵→自分の反射で true、自分→敵なら false）。</param>
    /// <param name="reflectionBlessingsAttacker">加護・カード参照の攻撃側（カード保持者）。</param>
    /// <param name="reflectionBlessingsDefender">跳ね返り後に受ける側視点の抑制（リヴァ等）。プレイヤー攻撃が跳ね返って自分に来るなら自分自身。</param>
    public void SetReflectionAttackTotalDisplayAfterSlide(
        List<CardData> attackCards,
        bool totalAtkOnPlayerSide,
        PlayerStatus reflectionBlessingsAttacker,
        PlayerStatus reflectionBlessingsDefender,
        int? displayStrengthOverride = null)
    {
        _reflectionAtkDisplayStrengthOverride = displayStrengthOverride;
        _reflectionAtkCardsForTotalDisplay.Clear();
        if (attackCards != null)
            _reflectionAtkCardsForTotalDisplay.AddRange(attackCards);
        _reflectionAtkTotalActive = _reflectionAtkCardsForTotalDisplay.Count > 0;
        _reflectionAtkTotalOnPlayerSide = totalAtkOnPlayerSide;
        _reflectionAtkBlessAttacker = reflectionBlessingsAttacker;
        _reflectionAtkBlessDefender = reflectionBlessingsDefender;
        UpdateTotalATKDEFDisplay();
    }

    /// <summary>反射ダメージ解決が終わったあと、TOTAL ATK の一時表示を通常ロジックに戻す。</summary>
    public void ClearReflectionAttackTotalDisplay()
    {
        _reflectionAtkTotalActive = false;
        _reflectionAtkCardsForTotalDisplay.Clear();
        _reflectionAtkBlessAttacker = null;
        _reflectionAtkBlessDefender = null;
        _reflectionAtkDisplayStrengthOverride = null;
        UpdateTotalATKDEFDisplay();
    }

    /// <summary>反射／宝玉反撃用 TOTAL の数値。宝玉はカード表記上 ATK0 のため、実際の反撃力を返す。</summary>
    public int? GetReflectionAttackDisplayStrengthOverride() => _reflectionAtkDisplayStrengthOverride;

    public bool IsSuppressingEnemyStaleAttackerInTotalByOrb() => _suppressEnemyStaleAttackerInTotalByOrb;

    public void SetSuppressEnemyStaleAttackerInTotalByOrb(bool v) => _suppressEnemyStaleAttackerInTotalByOrb = v;

    public PlayerStatus GetReflectionAttackBlessingAttacker() => _reflectionAtkBlessAttacker;

    public PlayerStatus GetReflectionAttackBlessingDefender() => _reflectionAtkBlessDefender;

    public bool IsReflectionAttackTotalDisplayActive()
    {
        return _reflectionAtkTotalActive && _reflectionAtkCardsForTotalDisplay.Count > 0;
    }

    public bool ReflectionAttackTotalOnPlayerSide => _reflectionAtkTotalOnPlayerSide;

    public List<CardData> GetReflectionAttackCardsForTotalDisplay() => _reflectionAtkCardsForTotalDisplay;

    /// <summary>
    /// 手札カードを 1 枚ドローする（MagicPoolManager 経由で手札追加時に使用）
    /// </summary>
    public async void DrawOneCard()
    {
        if (handRefill != null)
        {
            await handRefill.DrawCardAsync(playerHand);
            BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
        }
    }

    /// <summary>
    /// 手札に裏面で 1 枚追加し、ドローしたカードを返す（await 用）
    /// </summary>
    public async Task<CardData> DrawOneCardAsync(int trailingDelayMs = 200, bool playSoundOnDraw = true)
    {
        if (handRefill == null) return null;
        var drawn = await handRefill.DrawCardAsync(playerHand, trailingDelayMs, playSoundOnDraw);
        BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
        return drawn;
    }

    /// <summary>
    /// 手札の上限枚数を返す（MagicPoolManager で手札追加可否の判定に使用）。
    /// カード UI は配布のたびに Instantiate されるため <see cref="handPanel"/> の子数は現在枚数と一致し、上限には使わない。
    /// </summary>
    public int GetHandMaxCount() => MaxHandCards;

    /// <summary>敵手札リストの論理上限（UI スロットは無いが枚数はプレイヤーと同じ 18 を上限とする）。</summary>
    public int GetEnemyHandCapacity() => MaxHandCards;

    public bool IsSellProcessActive()
    {
        return sellFeature != null && sellFeature.IsSellProcessActive();
    }

    public bool IsBuyProcessActive()
    {
        return buyFeature != null && buyFeature.IsBuyProcessActive();
    }

    public bool IsEconomicActionInProgress()
    {
        return IsSellProcessActive() || IsBuyProcessActive() || IsExchangeProcessActive();
    }

    public bool IsExchangeProcessActive()
    {
        return exchangeFeature != null && exchangeFeature.IsExchangeProcessActive();
    }

    /// <summary>
    /// 現在進行中の経済アクション（売る・買う・両替）をキャンセルする
    /// 他の経済アクションを開始する前に呼び出す
    /// </summary>
    public void CancelCurrentEconomicAction()
    {
        if (IsSellProcessActive())
        {
            Debug.Log("[BattleManager] 売るアクションをキャンセル");
            sellFeature.CancelSell();
        }
        // 買う確認ポップアップが表示中なら先に閉じる
        BattleUIManager.I?.CancelBuyPopup();
        if (IsBuyProcessActive())
        {
            Debug.Log("[BattleManager] 買うアクションをキャンセル");
            buyFeature.CancelBuy();
        }
        if (IsExchangeProcessActive())
        {
            Debug.Log("[BattleManager] 両替アクションをキャンセル");
            exchangeFeature.CancelIfActive();
        }
    }

    private SummonData GetRandomEnemySummon()
    {
        var list = SummonSelectionManager.I?.GetAllSummonData();
        if (list == null || list.Length == 0) return null;

        var enemyCandidates = new List<SummonData>(list);
        if (SummonSelectionManager.I != null)
        {
            enemyCandidates.RemoveAt(SummonSelectionManager.I.SelectedIndex);
        }

        return enemyCandidates[UnityEngine.Random.Range(0, enemyCandidates.Count)];
    }

    /// <summary>
    /// 「買う」アクションを実行（BuyFeatureに委譲）
    /// </summary>
    public async void ExecuteBuyAction()
    {
        await buyFeature.ExecuteBuyActionAsync();
    }

    /// <summary>
    /// 「売る」アクションを実行
    /// </summary>
    public void ExecuteSellAction()
    {
        _ = ExecuteSellActionAsync();
    }

    private async Task ExecuteSellActionAsync()
    {
        await sellFeature.ExecuteSellActionAsync();
    }

    /// <summary>
    /// 「両替」アクションを実行（ExchangeFeatureに委譲）
    /// </summary>
    public void ExecuteExchangeAction()
    {
        _ = ExecuteExchangeActionAsync();
    }

    private async Task ExecuteExchangeActionAsync()
    {
        if (exchangeFeature == null)
        {
            Debug.LogError("[BattleManager] ExchangeFeatureがアタッチされていません");
            return;
        }
        await exchangeFeature.ExecuteExchangeActionAsync();
    }


    /// <summary>
    /// カードドロー処理
    /// </summary>
    private async Task ProcessCardDrawAsync()
    {
        Debug.Log("[BattleManager] カードドロー処理開始");
        
        // HandRefillServiceを使用してドロー
        if (handRefill != null)
        {
            await handRefill.DrawCardAsync(playerHand);
            Debug.Log($"[BattleManager] ドロー完了 - 手札枚数: {playerHand.Count}");
        }
        else
        {
            Debug.LogWarning("[BattleManager] HandRefillServiceが設定されていません");
        }
    }

    /// <summary>
    /// 経済アクション後のドロー処理（EndPhase で実行）
    /// </summary>
    private async Task ProcessEconomicActionDrawAsync()
    {
        // 経済アクションが実行されたかどうかをチェック（ダミー攻撃カードで判定）
        if (currentAttackCard != null && currentAttackCard.cardName == "経済アクション")
        {
            // 0.5秒インターバル
            await Task.Delay(500);
            
            // ドロー処理
            await ProcessCardDrawAsync();
            
            // ステータス更新
            BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        }
    }

    /// <summary>
    /// 裏向きカードを表向きにする処理
    /// </summary>
    private async Task RevealFaceDownCardsAsync()
    {
        if (handPanel == null)
        {
            Debug.LogWarning("[BattleManager] handPanelが設定されていません");
            return;
        }

        // 手札のUIを取得して裏向きのカードを表向きにする
        // childCount がループ中に増えると終了しないことがあるため、開始時の子数で固定する
        int childCountSnapshot = handPanel.childCount;
        for (int i = 0; i < childCountSnapshot; i++)
        {
            var child = handPanel.GetChild(i);
            if (child == null) continue;
            var cardUI = child.GetComponent<CardUI>();
            // 介入などで CardData / GameObject が破棄済みのスロットをスキップ
            if (cardUI == null) continue;
            try
            {
                if (!cardUI.IsFaceDown()) continue;

                var data = cardUI.GetCardData();
                if (data != null)
                    CardDealAudio.Play(data, true);
                await Task.Delay(50);
                cardUI.Reveal();
                await Task.Delay(300);
            }
            catch (MissingReferenceException)
            {
                continue;
            }
        }
    }

    // ================ ゲーム終了（HP0 検出 → 往生 → リザルト） ================

    private bool _gameEndTriggered;

    /// <summary>
    /// ゲーム終了シーケンスが起動済みかどうか。以降の通常フロー（フェーズ遷移・演出）をスキップする判定に用いる。
    /// </summary>
    public bool IsGameEndTriggered => _gameEndTriggered;

    /// <summary>宝玉臨時効果。第1段通過分を基準に、DefenseSelect 順で解決。</summary>
    public async Task PresentOrbDefenseReactionsAsync(
        BattleProcessor battleProcessor,
        IReadOnlyList<CardData> orbs,
        int firstPhaseDamageB,
        PlayerStatus originalAttacker,
        PlayerStatus originalDefender,
        CancellationToken cancellationToken = default)
    {
        if (orbs == null || orbs.Count == 0 || battleProcessor == null) return;
        await OrbDefenseReactionFlow.PresentReactionsAsync(
            this, battleProcessor, orbs, firstPhaseDamageB, originalAttacker, originalDefender, cancellationToken);
    }

    /// <summary>
    /// 各ダメージ適用点から呼ぶ共通チェック。HP0 のプレイヤーが居れば 200ms 待って「往生」ポップアップを出し、
    /// UI を片付けて <see cref="GameState.BattleEndPhase"/> へ遷移したところまでを <b>await で完了</b>させる。
    /// リザルト画面の演出は fire-and-forget で別途走らせるため、呼び出し側は戻り値が true なら即座に
    /// 後続処理（ターン送り・闇フォロー等）を抜けて OK。
    /// </summary>
    public async Task<bool> TryHandleDeathIfAnyAsync(CancellationToken ct = default)
    {
        if (_gameEndTriggered) return true;

        bool pDead = playerStatus != null && playerStatus.IsDead();
        bool eDead = enemyStatus != null && enemyStatus.IsDead();
        if (!pDead && !eDead) return false;

        _gameEndTriggered = true;
        bool gameEndPresentationCompleted = false;

        try
        {
            await Task.Delay(200, ct);
            bool hasPostDeathEffects = PostDeathEffectProcessor.HasPendingEffects(this);
            await RunOjyouPopupOnlyAsync(pDead, eDead, startBgmFade: !hasPostDeathEffects, ct);

            IsPostDeathSequenceActive = true;
            try
            {
                await PostDeathEffectProcessor.RunQueueAsync(
                    this, battleProcessor, handRefill, enemyAI, ct);
            }
            finally
            {
                IsPostDeathSequenceActive = false;
            }

            pDead = playerStatus != null && playerStatus.IsDead();
            eDead = enemyStatus != null && enemyStatus.IsDead();
            await RunGameEndPresentationAsync(pDead, eDead, startBgmFade: hasPostDeathEffects, ct);
            gameEndPresentationCompleted = true;
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[BattleManager] ゲーム終了シーケンスがキャンセルされました");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        if (gameEndPresentationCompleted)
            _ = RunGameResultScreenAsync(pDead, eDead);
        return true;
    }

    /// <summary>
    /// 往生ポップアップの表示 → 約 2 秒待ち → バトル UI 片付け → BattleEndPhase 遷移までを行う。
    /// 戻り値 Task が完了した時点でダメージ経路の呼び出し側は安全に抜けられる。
    /// </summary>
    private const string OjyouBellSeAddress = "Assets/SE/お寺の鐘.mp3";

    private async Task RunOjyouPopupOnlyAsync(bool playerDead, bool enemyDead, bool startBgmFade, CancellationToken ct)
    {
        OjyouPopup popupLifetimeRef = null;
        float lifetime = 2.0f;

        if (playerDead)
        {
            if (BattleUIManager.I != null)
                popupLifetimeRef = BattleUIManager.I.ShowOjyouPopup(Side.Player) ?? popupLifetimeRef;
        }
        if (enemyDead)
        {
            if (BattleUIManager.I != null)
                popupLifetimeRef = BattleUIManager.I.ShowOjyouPopup(Side.Enemy) ?? popupLifetimeRef;
        }

        if (popupLifetimeRef != null)
            lifetime = popupLifetimeRef.SequenceLifetimeSeconds;

        SoundEffectPlayer.I?.Play(OjyouBellSeAddress);
        if (startBgmFade)
            _ = BattleBgmController.Instance?.FadeOutBattleBgmAndStopAsync(lifetime);

        await Task.Delay(TimeSpan.FromSeconds(lifetime), ct);
    }

    /// <summary>
    /// 道連れ等の PostDeath キュー完了後：GAMESET・UI 片付け・BattleEndPhase。
    /// PostDeath ありのとき BGM フェードはここで非同期開始（GAMESET まで待たない）。
    /// </summary>
    private async Task RunGameEndPresentationAsync(bool playerDead, bool enemyDead, bool startBgmFade, CancellationToken ct)
    {
        if (startBgmFade)
            _ = BattleBgmController.Instance?.FadeOutBattleBgmAndStopAsync(2.0f);

        if (BattleUIManager.I != null)
        {
            try
            {
                await BattleUIManager.I.ShowPostOjyouFlashAndGameSetAsync(ct);
            }
            catch (OperationCanceledException) { }
        }

        try
        {
            await Task.Delay(500, ct);
        }
        catch (OperationCanceledException) { }

        BattleUIManager.I?.HideBattleUIForGameEnd();
        cardStatsDisplay?.HideAllForGameEnd();

        SetGameState(GameState.BattleEndPhase);
    }

    private async Task RunOjyouAndHideUIAsync(bool playerDead, bool enemyDead, CancellationToken ct)
    {
        await RunOjyouPopupOnlyAsync(playerDead, enemyDead, startBgmFade: true, ct);
        await RunGameEndPresentationAsync(playerDead, enemyDead, startBgmFade: false, ct);
    }

    /// <summary>
    /// リザルト画面 Prefab を生成して演出を走らせる（fire-and-forget）。
    /// </summary>
    private async Task RunGameResultScreenAsync(bool playerDead, bool enemyDead)
    {
        GameObject prefab = gameResultPrefab != null
            ? gameResultPrefab
            : Resources.Load<GameObject>("Prefab/GameResult");
        if (prefab == null)
        {
            Debug.LogWarning("[BattleManager] GameResult プレハブが見つかりません");
            return;
        }

        var battleUi = BattleUIManager.I;
        Transform parentForResult = battleUi != null ? battleUi.transform.root : null;
        GameObject resultGo = parentForResult != null
            ? Instantiate(prefab, parentForResult, false)
            : Instantiate(prefab);

        var controller = resultGo.GetComponent<GameResultController>();
        if (controller == null)
        {
            Debug.LogWarning("[BattleManager] GameResultController が GameResult プレハブにアタッチされていません");
            return;
        }

        GameResultController.ResultKind kind;
        if (playerDead && enemyDead) kind = GameResultController.ResultKind.Stalemate;
        else if (playerDead)         kind = GameResultController.ResultKind.Defeat;
        else                         kind = GameResultController.ResultKind.Victory;

        try
        {
            await controller.ShowAsync(kind, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private void OnDestroy()
    {
        _phaseCts?.Cancel();
        _phaseCts?.Dispose();
    }
}
