using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Battle facade: wires coordinators/services and keeps the public API stable.
/// Phase logic lives in <see cref="BattlePhaseController"/>; snapshots in <see cref="CombatSnapshotStore"/>.
/// </summary>
public partial class BattleManager : MonoBehaviour, IBattleContext, IBattlePhaseHost, IBattlePhaseControllerHost,
    IAdHocDefenseHost, IDualBladeDefenseHost, IBattleOpeningHost, ISummonSkillHost, IBattleBootstrapHost,
    IGameEndOrchestratorHost, IOnlineBattleSyncHost, IEnemyTurnHost, IEnemyDefenseHost, IPlayerInputHost,
    IPlayerDefenseInteractivityHost
{
    public static BattleManager I;

    private readonly CombatSnapshotStore _combatSnapshots = new();
    private readonly BattleBootstrap _bootstrap = new();
    private GameEndOrchestrator _gameEnd;
    private OnlineBattleSyncService _onlineSync;
    private EnemyTurnRunner _enemyTurn;
    private EnemyDefenseResolver _enemyDefense;
    private PlayerInputController _playerInput;
    private BattlePhaseController _phaseController;
    private PlayerDefenseInteractivityService _defenseInteractivity;
    private AdHocDefenseCoordinator _adHocDefense;
    private DualBladeDefenseCoordinator _dualBladeDefense;
    private BattleOpeningCoordinator _battleOpening;
    private SummonSkillCoordinator _summonSkills;

    public CombatSnapshotStore CombatSnapshots => _combatSnapshots;
    public AdHocDefenseCoordinator AdHocDefense => _adHocDefense;
    public DualBladeDefenseCoordinator DualBladeDefense => _dualBladeDefense;
    public BattleOpeningCoordinator BattleOpening => _battleOpening;
    public SummonSkillCoordinator SummonSkills => _summonSkills;
    public GameEndOrchestrator GameEnd => _gameEnd;
    public OnlineBattleSyncService OnlineSync => _onlineSync;
    public EnemyTurnRunner EnemyTurn => _enemyTurn;
    public EnemyDefenseResolver EnemyDefense => _enemyDefense;
    public PlayerInputController PlayerInput => _playerInput;
    public BattlePhaseController PhaseController => _phaseController;

    PlayerStatus IBattleContext.PlayerStatus => playerStatus;
    PlayerStatus IBattleContext.EnemyStatus => enemyStatus;
    PlayerType IBattleContext.Attacker => Attacker;
    IReadOnlyList<CardData> IBattleContext.PlayerHand => playerHand;
    IReadOnlyList<CardData> IBattleContext.CpuHand => cpuHand;

    PlayerStatus IAdHocDefenseHost.PlayerStatus => playerStatus;
    IReadOnlyList<CardData> IAdHocDefenseHost.PlayerHand => playerHand;
    PlayerType IAdHocDefenseHost.Defender => Defender;
    bool IAdHocDefenseHost.IsProcessingUseButton
    {
        get => _playerInput.IsProcessingUseButton;
        set => _playerInput.IsProcessingUseButton = value;
    }

    private CardData currentAttackCard
    {
        get => _combatSnapshots.CurrentAttackCard;
        set => _combatSnapshots.CurrentAttackCard = value;
    }

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

    [Header("Startup")]
    [SerializeField] private BattleBgmController battleBgmController;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] private BattleDebugTools battleDebugTools;
#endif

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
    [Tooltip("未設定時は Resources.Load(\"Prefab/BahamutPopup\")")]
    [SerializeField] private GameObject bahamutPopupPrefab;
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

    internal BuyFeature BuyFeatureInternal => buyFeature;
    internal SellFeature SellFeatureInternal => sellFeature;
    internal ExchangeFeature ExchangeFeatureInternal => exchangeFeature;

    // バトルデータ
    private PlayerStatus playerStatus, enemyStatus;
    public List<CardData> playerHand = new();
    public List<CardData> cpuHand = new();
    

    public GameState CurrentState { get; private set; } = GameState.OpeningPhase;
    public PlayerType CurrentTurnOwner { get; private set; } = PlayerType.Player;

    /// <summary>Opening first-turn owner (for summon turn-end ordering).</summary>
    public PlayerType OpeningTurnOwner => _battleOpening?.OpeningTurnOwner ?? PlayerType.Player;

    /// <summary>Resources の SummonSkillPopup が開いている間、手札・経済・重複表示を防ぐ。</summary>
    public bool IsSummonSkillPopupOpen => _summonSkills?.IsPopupOpen ?? false;

    /// <summary>顕現／メガフレア等、召喚スキル演出が走っている間。</summary>
    public bool IsAnySummonSkillFlowRunning =>
        _summonSkills != null && (_summonSkills.IsManifestationFlowRunning || _summonSkills.IsMegaFlareFlowRunning);

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
        if (IsAdHocDefenseWaitActive(AdHocDefenseKind.ReflectionChain)) return;

        var cards = BattleUIManager.I?.GetSelectedAttackCards();
        if (cards == null || cards.Count == 0)
        {
            ClearPlayerSelfAttackTargetMode();
            return;
        }

        if (cardStatsDisplay != null && cardStatsDisplay.IsPlayerAttackSelectionNumericAtkZero(cards))
            ClearPlayerSelfAttackTargetMode();
    }

    /// <summary>Records near-death card consumption for the next host ResolveState sync.</summary>
    public void RecordNearDeathConsumptionForOnlineSync(PlayerType ownerSide, string cardName)
        => _onlineSync.RecordNearDeathConsumption(ownerSide, cardName);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// オンラインデバッグ：両クライアントで同じ手札へカードを注入する。
    /// targetIsHostPlayer=true → ホスト側プレイヤーの手札（ホスト: playerHand / クライアント: cpuHand）。
    /// </summary>
    public bool TryApplyOnlineDebugCardInject(string cardName, bool targetIsHostPlayer)
        => _onlineSync.TryApplyDebugCardInject(cardName, targetIsHostPlayer);

    /// <summary>ホスト：注入してクライアントへ転送（Development のみ）。</summary>
    public bool HostBroadcastOnlineDebugCardInject(string cardName, bool targetIsHostPlayer)
        => _onlineSync.HostBroadcastDebugCardInject(cardName, targetIsHostPlayer);

    /// <summary>クライアント：ホストへ注入を依頼（Development のみ）。</summary>
    public bool RequestOnlineDebugCardInject(string cardName, bool targetIsHostPlayer)
        => _onlineSync.RequestDebugCardInject(cardName, targetIsHostPlayer);
#endif

    /// <summary>敵攻撃＋双剣：2回目の「許す／使用」入力待ち中（<see cref="GameState.CombatResolvePhase"/> かつ専用インデックス）。</summary>
    public bool IsPlayerDualBladeSecondDefenseWaitActive()
        => _dualBladeDefense?.IsSecondDefenseWaitActive() ?? false;

    public Task<bool> TryPreparePlayerDualBladeSecondDefenseIfNeededAsync(CancellationToken cancellationToken = default)
        => _dualBladeDefense.TryPrepareSecondDefenseIfNeededAsync(cancellationToken);

    PlayerType IDualBladeDefenseHost.Attacker => Attacker;
    PlayerType IDualBladeDefenseHost.Defender => Defender;
    PlayerStatus IDualBladeDefenseHost.PlayerStatus => playerStatus;
    PlayerStatus IDualBladeDefenseHost.EnemyStatus => enemyStatus;
    List<CardData> IDualBladeDefenseHost.GetAttackCardsForCombat() => GetAttackCardsForCombat();

    CardData IDualBladeDefenseHost.CurrentAttackCard => currentAttackCard;
    List<CardData> IDualBladeDefenseHost.PlayerHand => playerHand;
    bool IDualBladeDefenseHost.IsProcessingUseButton
    {
        get => _playerInput.IsProcessingUseButton;
        set => _playerInput.IsProcessingUseButton = value;
    }

    void IDualBladeDefenseHost.ClearCardStatsSequence()
        => cardStatsDisplay?.ClearSequenceCards();

    void IDualBladeDefenseHost.SetEnemyAttackSequenceDisplay(List<CardData> attackCards)
    {
        BattleUIManager.I?.ShowCardSheetsVisualOnlyBatch(attackCards, Side.Enemy);
        cardStatsDisplay?.SetSequenceCards(attackCards, "攻撃", Side.Enemy);
        cardStatsDisplay?.UpdateDisplay();
    }

    void IDualBladeDefenseHost.UpdateCardStatsDisplay()
        => cardStatsDisplay?.UpdateDisplay();

    void IDualBladeDefenseHost.SetSelectedDefenseCard(CardData card)
        => SetSelectedDefenseCard(card);

    void IDualBladeDefenseHost.TryAutoPassPlayerDefenseIfChantingArchMagic()
        => TryAutoPassPlayerDefenseIfChantingArchMagic();

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
        => _playerInput.SelectMagicPoolCard(card);
    private CardData selectedCard;
    private CardData selectedDefenseCard;

    private bool _postDeathSequenceActive;
    public bool IsPostDeathSequenceActive => _postDeathSequenceActive;
    public bool IsPostDeathPlayerDefender => _adHocDefense?.IsPostDeathPlayerDefender ?? false;
    public bool IsUseButtonLocked => _playerInput.IsUseButtonLocked;

    /// <summary>CardSequence 例外で中断したとき、UseButton / 手札の入力ロックを戻す。</summary>
    public void ReleaseCardSequenceInputLocks()
        => _playerInput.ReleaseCardSequenceInputLocks();

    public bool IsPlayerDefenseCombatResolving => _playerInput.IsPlayerDefenseCombatResolving;

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
        => await _enemyDefense.PickAndDisplayAfterPlayerHitAsync(playerAttackCards);

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

    public PlayerStatus GetPlayerStatus() => playerStatus;
    public PlayerStatus GetEnemyStatus() => enemyStatus;

    // ===== Ad-hoc defense (reflection / parry / intervention / disaster / post-death) =====

    public bool IsAdHocDefenseWaitActive() => _adHocDefense.IsWaitActive();

    public bool IsAdHocDefenseWaitActive(AdHocDefenseKind kind) => _adHocDefense.IsWaitActive(kind);

    public bool ShouldKeepOpponentAttackPanelOnSelectionClear()
        => _adHocDefense.ShouldKeepOpponentAttackPanelOnSelectionClear();

    public bool IsInterventionDefenseWaitActive() => _adHocDefense.IsInterventionWaitActive();

    public bool IsDisasterPlayerDefenseWaitActive() => _adHocDefense.IsDisasterPlayerWaitActive();

    public bool IsPostDeathDefenseWaitActive() => _adHocDefense.IsPostDeathWaitActive();

    public bool IsReflectionChainDefensePending() => _adHocDefense.IsReflectionChainPending();

    public bool IsParryRerunDefensePending() => _adHocDefense.IsParryRerunPending();

    public bool IsReactiveAdHocDefenseWaitActive() => _adHocDefense.IsReactiveWaitActive();

    public void BeginInterventionPlayerDefensePhase(List<CardData> attackCardsForElement)
        => _adHocDefense.BeginInterventionPlayerDefensePhase(attackCardsForElement);

    public void BeginDisasterPlayerDefensePhase(List<CardData> attackCardsForElement)
        => _adHocDefense.BeginDisasterPlayerDefensePhase(attackCardsForElement);

    public void BeginPostDeathPlayerDefenseWait(List<CardData> attackCardsForElement)
        => _adHocDefense.BeginPostDeathPlayerDefenseWait(attackCardsForElement);

    public Task<List<CardData>> WaitForAdHocDefenseSubmitAsync(CancellationToken cancellationToken)
        => _adHocDefense.WaitForSubmitAsync(cancellationToken);

    public Task<List<CardData>> WaitForInterventionPlayerDefenseSubmitAsync(CancellationToken ct)
        => _adHocDefense.WaitForSubmitAsync(ct);

    public Task<List<CardData>> WaitForDisasterPlayerDefenseSubmitAsync(CancellationToken ct)
        => _adHocDefense.WaitForSubmitAsync(ct);

    public Task<List<CardData>> WaitForPostDeathPlayerDefenseSubmitAsync(CancellationToken ct)
        => _adHocDefense.WaitForSubmitAsync(ct);

    public Task<List<CardData>> WaitForReflectionChainDefenseAsync(
        List<CardData> attackSnapshot,
        CancellationToken cancellationToken)
        => _adHocDefense.WaitForReflectionChainDefenseAsync(attackSnapshot, cancellationToken);

    public Task<List<CardData>> WaitForParryRerunDefenseSubmitAsync(CancellationToken cancellationToken)
        => _adHocDefense.WaitForParryRerunDefenseSubmitAsync(cancellationToken);

    public void ClearInterventionDefenseWait() => _adHocDefense.ClearInterventionWait();

    public void ClearDisasterPlayerDefenseWait() => _adHocDefense.ClearDisasterPlayerWait();

    public void ClearPostDeathDefenseWait() => _adHocDefense.ClearPostDeathWait();

    public void ClearAdHocDefense() => _adHocDefense.Clear();

    /// <summary>
    /// 自プレイヤーが防御カードを選ぶ入力待ちか（通常防御・介入・PostDeath・反射連鎖・打ち払い再防御・双剣2回目）。
    /// </summary>
    public bool IsPlayerDefenseInputActive()
    {
        if (_adHocDefense.IsAdHocPlayerDefenseInputActive())
            return true;

        if (CurrentState == GameState.CombatResolvePhase && IsPlayerDualBladeSecondDefenseWaitActive()
            && Defender == PlayerType.Player)
            return true;
        return CurrentState == GameState.DefensePhase && Defender == PlayerType.Player;
    }

    /// <summary>防御入力開始時：UseButton ロック解除と相手側 yurusu 装飾の非表示。</summary>
    public void ResetPlayerDefenseUseButtonLocks()
        => _playerInput.ResetDefenseUseButtonLocks();

    private void TrySubmitAdHocPlayerDefense() => _adHocDefense.TrySubmitPlayerDefense();

    bool IAdHocDefenseHost.TryAutoPassPlayerDefenseIfChantingArchMagic()
        => _defenseInteractivity.TryAutoPassPlayerDefenseIfChantingArchMagic();

    List<CardData> IAdHocDefenseHost.GetAttackCardsForCombat() => GetAttackCardsForCombat();

    /// <summary>詠唱中プレイヤーは防御不可。「許す」相当で即進行する。</summary>
    public bool TryAutoPassPlayerDefenseIfChantingArchMagic()
        => _defenseInteractivity.TryAutoPassPlayerDefenseIfChantingArchMagic();

    /// <summary>
    /// 防御フェーズ等でプレイヤーが防御側のとき、手札グレーアウト（拘束時は選択済み1枚のみ）と「体が重い」オーバーレイを更新。
    /// </summary>
    public void RefreshPlayerDefensePhaseInteractivity()
        => _defenseInteractivity.RefreshPlayerDefensePhaseInteractivity();

    /// <summary>全プレイヤー手札の Card Status Text を再適用。手札操作可否は <see cref="BattleUIManager.IsHandInputBlocked"/> を参照（REFLECT 等の切替）。</summary>
    public void RefreshPlayerHandStatusTextForDefenseSnapshot()
        => _defenseInteractivity.RefreshPlayerHandStatusTextForDefenseSnapshot();

    public void RefreshReflectionChainInteractivity(List<CardData> attackSnapshot)
        => _defenseInteractivity.RefreshReflectionChainInteractivity(attackSnapshot);

    void IPlayerDefenseInteractivityHost.HandleNoDefenseCard()
        => _playerInput.HandleNoDefenseCard();

    void IPlayerDefenseInteractivityHost.CompleteAdHocDefenseSubmit(List<CardData> selectedDefenseCards)
        => _adHocDefense.CompleteSubmit(selectedDefenseCards);

    PlayerStatus IPlayerDefenseInteractivityHost.PlayerStatus => playerStatus;
    List<CardData> IPlayerDefenseInteractivityHost.PlayerHand => playerHand;
    bool IPlayerDefenseInteractivityHost.IsOnlineMatch => IsOnlineMatch;
    AdHocDefenseCoordinator IPlayerDefenseInteractivityHost.AdHocDefense => _adHocDefense;
    PlayerInputController IPlayerDefenseInteractivityHost.PlayerInput => _playerInput;

    /// <summary>連鎖反射時の攻撃カードスナップショット（可否判定用）。</summary>
    public List<CardData> GetReflectionChainAttackSnapshot()
        => _adHocDefense.GetReflectionChainAttackSnapshot();

    /// <summary>介入の防御入力待ち時の攻撃カード（UI 用）。</summary>
    public List<CardData> GetInterventionDefenseAttackSnapshot()
        => _adHocDefense.GetInterventionDefenseAttackSnapshot();

    /// <summary>天変地異の防御入力待ち時の攻撃カード（UI 用）。</summary>
    public List<CardData> GetDisasterDefenseAttackSnapshot()
        => _adHocDefense.GetDisasterDefenseAttackSnapshot();

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
        if (_adHocDefense.IsWaitActive())
            return _adHocDefense.CurrentBattleStep;

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

    /// <summary>選択変更後に連鎖反射の手札グレーアウトを更新（<see cref="BattleUIManager"/> から呼ぶ）。</summary>
    public void RefreshReflectionChainInteractivityIfPending()
        => _adHocDefense.RefreshReflectionChainInteractivityIfPending();

    /// <summary>
    /// 防御 UI・併用不可判定用の現在の攻撃スナップショット（反射連鎖／介入／通常防御）。</summary>
    public List<CardData> GetIncomingAttackSnapshotForDefenseUi()
    {
        var adHocSnapshot = _adHocDefense.GetIncomingAttackSnapshotForDefenseUi();
        if (adHocSnapshot != null)
            return adHocSnapshot;

        if (CurrentState == GameState.CombatResolvePhase && IsPlayerDualBladeSecondDefenseWaitActive()
            && DefenderPublic == PlayerType.Player)
            return GetAttackCardsForCombat();

        if ((CurrentState == GameState.DefensePhase || CurrentState == GameState.DefenseConfirmPhase)
            && DefenderPublic == PlayerType.Player)
            return GetAttackCardsForCombat();

        return null;
    }

    private void Awake()
    {
        I = this;
        ResolveBattleDebugToolsReference();
        _adHocDefense = new AdHocDefenseCoordinator(this);
        _dualBladeDefense = new DualBladeDefenseCoordinator(this);
        _battleOpening = new BattleOpeningCoordinator(this);
        _summonSkills = new SummonSkillCoordinator(this);
        _gameEnd = new GameEndOrchestrator(this);
        _onlineSync = new OnlineBattleSyncService(this);
        _enemyTurn = new EnemyTurnRunner(this);
        _enemyDefense = new EnemyDefenseResolver(this);
        _playerInput = new PlayerInputController(this);
        _phaseController = new BattlePhaseController(this);
        _defenseInteractivity = new PlayerDefenseInteractivityService(this);
    }

    private void EnsureBattleBgmController()
    {
        if (battleBgmController != null)
            return;

        Debug.LogError(
            "[BattleManager] BattleBgmController is missing. Wire it on BattleManager or attach it to the BGM object in the Battle scene.");
    }

    private void ResolveBattleDebugToolsReference()
    {
        if (battleDebugTools != null) return;

        battleDebugTools = GetComponent<BattleDebugTools>();
        if (battleDebugTools == null)
            battleDebugTools = FindObjectOfType<BattleDebugTools>();

        if (battleDebugTools == null)
            Debug.LogWarning("[BattleManager] BattleDebugTools not found. Debug summon overrides will not apply.");
    }

    void Start() => _bootstrap.RunStartup(this);

    BattleManager IBattleBootstrapHost.Manager => this;
    List<CardData> IBattleBootstrapHost.PlayerHand => playerHand;
    List<CardData> IBattleBootstrapHost.CpuHand => cpuHand;
    void IBattleBootstrapHost.SetPlayerStatus(PlayerStatus value) => playerStatus = value;
    void IBattleBootstrapHost.SetEnemyStatus(PlayerStatus value) => enemyStatus = value;
    PlayerStatus IBattleBootstrapHost.PlayerStatus => playerStatus;
    PlayerStatus IBattleBootstrapHost.EnemyStatus => enemyStatus;
    EnemyAI IBattleBootstrapHost.EnemyAI
    {
        get => enemyAI;
        set => enemyAI = value;
    }
    Transform IBattleBootstrapHost.HandPanel => handPanel;
    GameObject IBattleBootstrapHost.CardUiPrefab => cardUIPrefab;
    Sprite IBattleBootstrapHost.CardBackSprite => cardBackSprite;
    CardDealer IBattleBootstrapHost.CardDealer => cardDealer;
    BattleProcessor IBattleBootstrapHost.BattleProcessor => battleProcessor;
    BattleStatusUI IBattleBootstrapHost.StatusUI => statusUI;
    HandRefillService IBattleBootstrapHost.HandRefill => handRefill;
    CardSequenceManager IBattleBootstrapHost.CardSequenceManager => cardSequenceManager;
    CardStatsDisplay IBattleBootstrapHost.CardStatsDisplay => cardStatsDisplay;
    MagicPoolManager IBattleBootstrapHost.MagicPoolManager => magicPoolManager;
    CardPurchaseAnimation IBattleBootstrapHost.CardPurchaseAnimation => cardPurchaseAnimation;
    CardSellAnimation IBattleBootstrapHost.CardSellAnimation => cardSellAnimation;
    ExchangeFeature IBattleBootstrapHost.ExchangeFeature => exchangeFeature;
    SummonSkillButton IBattleBootstrapHost.SummonSkillButton => summonSkillButton;
    SummonSkillButton IBattleBootstrapHost.EnemySummonSkillButton => enemySummonSkillButton;
    StatusProgressionConfig IBattleBootstrapHost.StatusProgressionConfig => statusProgressionConfig;
    DiseaseTurnEndSettings IBattleBootstrapHost.DiseaseTurnEndSettings => diseaseTurnEndSettings;
    ShivaDirectAttackFreezeSettings IBattleBootstrapHost.ShivaDirectAttackFreezeSettings
        => shivaDirectAttackFreezeSettings;
    BuyFeature IBattleBootstrapHost.BuyFeature => buyFeature;
    SellFeature IBattleBootstrapHost.SellFeature => sellFeature;
    void IBattleBootstrapHost.BeginOpeningSequence() => _ = BattleStartSequenceAsync();
    void IBattleBootstrapHost.EnsureBattleBgmController() => EnsureBattleBgmController();
    void IBattleBootstrapHost.RefreshEnemyMagicPoolSnapshot() => RefreshEnemyMagicPoolSnapshot();
    BattleBgmController IBattleBootstrapHost.BattleBgmController => battleBgmController;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    BattleDebugTools IBattleBootstrapHost.BattleDebugTools => battleDebugTools;
    void IBattleBootstrapHost.SubscribeOnlineDebugInject()
        => NetworkBattleBridge.OnlineDebugInjectReceived += _onlineSync.HandleDebugInjectReceived;
    void IBattleBootstrapHost.UnsubscribeOnlineDebugInject()
        => NetworkBattleBridge.OnlineDebugInjectReceived -= _onlineSync.HandleDebugInjectReceived;
#endif

    /// <summary>
    /// 配布・カットイン等の開幕 <see cref="BattleStartSequenceAsync"/> が完了し、<see cref="GameState.StandByPhase"/> 直前の時点で true になる。
    /// 手札リロード UI はこれが true かつ攻撃プレイヤーの <see cref="GameState.AttackPhase"/> などで有効化する。
    /// </summary>
    public bool IsBattleOpeningSequenceComplete => _battleOpening?.IsBattleOpeningSequenceComplete ?? false;

    //================ 状態遷移 ================
    public void SetGameState(GameState newState) => _phaseController.SetGameState(newState);

    private async Task BattleStartSequenceAsync()
        => await _battleOpening.RunBattleStartSequenceAsync();

    MonoBehaviour IBattleOpeningHost.CoroutineRunner => this;
    CardDealer IBattleOpeningHost.CardDealer => cardDealer;
    List<CardData> IBattleOpeningHost.PlayerHand => playerHand;
    List<CardData> IBattleOpeningHost.CpuHand => cpuHand;
    PlayerStatus IBattleOpeningHost.PlayerStatus => playerStatus;
    PlayerStatus IBattleOpeningHost.EnemyStatus => enemyStatus;
    CutInController IBattleOpeningHost.CutInController => cutInController;
    float IBattleOpeningHost.CutInDelaySeconds => cutInDelay;
    PlayerType IBattleOpeningHost.GetCurrentTurnOwner() => CurrentTurnOwner;
    void IBattleOpeningHost.SetCurrentTurnOwner(PlayerType owner) => CurrentTurnOwner = owner;
    void IBattleOpeningHost.SetGameState(GameState state) => SetGameState(state);
    void IBattleOpeningHost.UpdateBattleStatusUi()
        => BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
    void IBattleOpeningHost.SetIntroModeUi()
        => BattleUIManager.I?.SetIntroModeUI(playerHand);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    BattleDebugTools IBattleOpeningHost.BattleDebugTools => battleDebugTools;
#endif

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

    public List<CardData> GetEnemyDefenseCardsForCombat()
        => _enemyDefense.GetDefenseCardsForCombat();

    public void SetSelectedCard(CardUI ui)
        => _playerInput.SetSelectedCard(ui);

    public void OnUseButtonPressed()
        => _playerInput.OnUseButtonPressed();

    /// <summary>
    /// 戦闘用攻撃カードを取得（RunDefenseConfirmAsync、PlayerInputController から使用）
    /// </summary>
    private List<CardData> GetAttackCardsForCombat()
    {
        var uiAttackCards = BattleUIManager.I?.GetSelectedAttackCards() ?? new List<CardData>();
        IReadOnlyList<CardData> remoteLast = null;
        if (IsOnlineMatch && enemyAI is RemotePlayerAgent remote)
            remoteLast = remote.LastAttackSelection;
        return _combatSnapshots.ResolveAttackCardsForCombat(
            Attacker, uiAttackCards, IsOnlineMatch, remoteLast);
    }

    public void RefreshSummonSkillButtonInteractables()
        => _summonSkills.RefreshButtonInteractables();

    public bool CanActivateBahamutSummonButton(bool isLocalPlayerSide)
    {
        var self = isLocalPlayerSide ? playerStatus : enemyStatus;
        return _summonSkills != null && _summonSkills.CanActivateBahamutSummonButton(self, isLocalPlayerSide);
    }

    public bool TryOpenSummonSkillPopup(PlayerStatus summoner, PlayerStatus opponent)
        => _summonSkills.TryOpenPopup(summoner, opponent);

    public Task PresentEnemyManifestationAttackToPlayerDefenseAsync(
        List<CardData> atkList,
        CancellationToken cancellationToken)
        => _summonSkills.PresentEnemyManifestationAttackToPlayerDefenseAsync(atkList, cancellationToken);

    MonoBehaviour ISummonSkillHost.HostBehaviour => this;
    List<CardData> ISummonSkillHost.PlayerHand => playerHand;
    List<CardData> ISummonSkillHost.CpuHand => cpuHand;
    BattleStatusUI ISummonSkillHost.StatusUI => statusUI;
    SummonSkillButton ISummonSkillHost.PlayerSummonButton => summonSkillButton;
    SummonSkillButton ISummonSkillHost.EnemySummonButton => enemySummonSkillButton;
    PlayerStatus ISummonSkillHost.PlayerStatus => playerStatus;
    PlayerStatus ISummonSkillHost.EnemyStatus => enemyStatus;
    PlayerType ISummonSkillHost.Defender => Defender;
    SummonTurnCounterState ISummonSkillHost.SummonTurnCounters => _summonTurnCounters;
    GameObject ISummonSkillHost.BahamutPopupPrefab => bahamutPopupPrefab;

    bool ISummonSkillHost.IsHandReloadPopupOpen() => IsHandReloadPopupOpen;

    void ISummonSkillHost.ClearAttackSelectionNeutral()
    {
        BattleUIManager.I?.ClearAllCardDisplaysAndSelectionImmediate();
        BattleUIManager.I?.ClearAllSelections();
        cardStatsDisplay?.ClearSequenceCards();
        cardStatsDisplay?.UpdateDisplay();
        SetCurrentAttackCard(null);
        ClearPlayerSelfAttackTargetMode();
    }

    void ISummonSkillHost.EnterAttackPhase() => _phaseController.EnterAttackPhase();

    void ISummonSkillHost.UpdateCardStatsDisplay() => cardStatsDisplay?.UpdateDisplay();

    void ISummonSkillHost.ClearCardStatsSequence()
        => cardStatsDisplay?.ClearSequenceCards();

    async Task ISummonSkillHost.RunAfterCombatSharedCleanupAsync(CancellationToken cancellationToken)
    {
        if (cardSequenceManager != null)
            await cardSequenceManager.RunAfterCombatSharedCleanupAsync(cancellationToken);
    }

    async Task<bool> ISummonSkillHost.ResolveConfusionSelfAttackAsync(
        List<CardData> atkList, CancellationToken cancellationToken)
    {
        if (cardSequenceManager == null) return false;
        bool finished = await cardSequenceManager.ResolvePlayerAttackCombatAsync(
            atkList, enemyStatus, enemyStatus, cpuHand, cancellationToken);
        BattleUIManager.I?.HideAllCardDetails();
        currentAttackCard = null;
        cardStatsDisplay?.UpdateDisplay();
        return finished;
    }

    public void ToggleTurnOwner()
    {
        CurrentTurnOwner = (CurrentTurnOwner == PlayerType.Player) ? PlayerType.Enemy : PlayerType.Player;
        Debug.Log($"[Turn] 手番変更: {CurrentTurnOwner}");
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

    public void EnterPostDeathChainNeutralPhase()
        => _gameEnd.EnterPostDeathChainNeutralPhase();

    public void EnterPostDeathChainCombatPhase(PlayerType deadAttackerSide)
        => _gameEnd.EnterPostDeathChainCombatPhase(deadAttackerSide);

    public void PreparePostDeathChainCombatUi()
        => _gameEnd.PreparePostDeathChainCombatUi();

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


    // ================ ゲーム終了（HP0 検出 → 往生 → リザルト） ================

    public bool IsGameEndTriggered => _gameEnd?.IsGameEndTriggered ?? false;

    public Task<bool> TryHandleDeathIfAnyAsync(CancellationToken ct = default)
        => _gameEnd.TryHandleDeathIfAnyAsync(ct);

    BattleManager IGameEndOrchestratorHost.Manager => this;
    MonoBehaviour IGameEndOrchestratorHost.HostBehaviour => this;
    PlayerStatus IGameEndOrchestratorHost.PlayerStatus => playerStatus;
    PlayerStatus IGameEndOrchestratorHost.EnemyStatus => enemyStatus;
    BattleProcessor IGameEndOrchestratorHost.BattleProcessor => battleProcessor;
    HandRefillService IGameEndOrchestratorHost.HandRefill => handRefill;
    EnemyAI IGameEndOrchestratorHost.EnemyAI => enemyAI;
    CardStatsDisplay IGameEndOrchestratorHost.CardStatsDisplay => cardStatsDisplay;
    GameObject IGameEndOrchestratorHost.GameResultPrefab => gameResultPrefab;
    bool IGameEndOrchestratorHost.IsPostDeathSequenceActive
    {
        get => _postDeathSequenceActive;
        set => _postDeathSequenceActive = value;
    }
    void IGameEndOrchestratorHost.SetCurrentStateDirect(GameState state) => CurrentState = state;
    void IGameEndOrchestratorHost.SetCurrentTurnOwner(PlayerType owner) => CurrentTurnOwner = owner;
    void IGameEndOrchestratorHost.ResetDefenseInputFlags()
        => _playerInput.ResetAllLocks();
    void IGameEndOrchestratorHost.ClearPlayerSelfAttackTargetMode() => ClearPlayerSelfAttackTargetMode();
    void IGameEndOrchestratorHost.ClearReflectionAttackTotalDisplay() => ClearReflectionAttackTotalDisplay();
    void IGameEndOrchestratorHost.ClearPostDeathChainAttackDisplay() => ClearPostDeathChainAttackDisplay();
    void IGameEndOrchestratorHost.ClearStatsDisplaySequenceCards() => ClearStatsDisplaySequenceCards();
    void IGameEndOrchestratorHost.SetCurrentAttackCard(CardData card) => SetCurrentAttackCard(card);
    void IGameEndOrchestratorHost.ClearPlayerAttackComboForCombat() => ClearPlayerAttackComboForCombat();
    void IGameEndOrchestratorHost.ClearEnemyAttackComboForCombat() => ClearEnemyAttackComboForCombat();
    void IGameEndOrchestratorHost.ResetPlayerDefenseUseButtonLocks() => ResetPlayerDefenseUseButtonLocks();
    void IGameEndOrchestratorHost.ClearSelectedCards() => ClearSelectedCards();
    void IGameEndOrchestratorHost.UpdateTotalATKDEFDisplay() => UpdateTotalATKDEFDisplay();

    BattleManager IOnlineBattleSyncHost.Manager => this;
    bool IOnlineBattleSyncHost.IsOnlineMatch => IsOnlineMatch;
    bool IOnlineBattleSyncHost.IsGameEndTriggered => _gameEnd?.IsGameEndTriggered ?? false;
    int IOnlineBattleSyncHost.GetOnlineTurnTag()
        => _summonTurnCounters.PlayerOwnTurnsEnded + _summonTurnCounters.EnemyOwnTurnsEnded;
    PlayerStatus IOnlineBattleSyncHost.PlayerStatus => playerStatus;
    PlayerStatus IOnlineBattleSyncHost.EnemyStatus => enemyStatus;
    List<CardData> IOnlineBattleSyncHost.PlayerHand => playerHand;
    List<CardData> IOnlineBattleSyncHost.CpuHand => cpuHand;
    BattleProcessor IOnlineBattleSyncHost.BattleProcessor => battleProcessor;
    HandRefillService IOnlineBattleSyncHost.HandRefill => handRefill;
    CardDealer IOnlineBattleSyncHost.CardDealer => cardDealer;
    SummonTurnCounterState IOnlineBattleSyncHost.SummonTurnCounters => _summonTurnCounters;
    PlayerType IOnlineBattleSyncHost.CurrentTurnOwner
    {
        get => CurrentTurnOwner;
        set => CurrentTurnOwner = value;
    }
    int IOnlineBattleSyncHost.MaxHandCards => MaxHandCards;
    Task<bool> IOnlineBattleSyncHost.TryHandleDeathIfAnyAsync(CancellationToken ct)
        => TryHandleDeathIfAnyAsync(ct);
    void IOnlineBattleSyncHost.UpdateBattleStatusUi()
        => BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
    void IOnlineBattleSyncHost.RefreshTurnCountDisplay()
        => BattleUIManager.I?.RefreshTurnCountDisplay(_summonTurnCounters, CurrentTurnOwner);
    void IOnlineBattleSyncHost.RefreshArchMagicBarrierUi(PlayerStatus status)
    {
        if (status == null) return;
        Side side = ReferenceEquals(status, playerStatus) ? Side.Player : Side.Enemy;
        if (status.IsCastingArchMagic)
            BattleUIManager.I?.ShowArchMagicBarrier(side, status.archMagicBarrierRemaining);
        else
            BattleUIManager.I?.HideArchMagicBarrier(side);
    }
    void IOnlineBattleSyncHost.UpdateTotalATKDefDisplay() => UpdateTotalATKDEFDisplay();
    void IOnlineBattleSyncHost.RefreshPlayerDefensePhaseInteractivity() => RefreshPlayerDefensePhaseInteractivity();
    void IOnlineBattleSyncHost.SetIntroModeUi() => BattleUIManager.I?.SetIntroModeUI(playerHand);
    PlayerStatus IOnlineBattleSyncHost.ResolveArchMagicEffectTarget(PlayerStatus status, bool targetSelf)
        => targetSelf ? status : (ReferenceEquals(status, playerStatus) ? enemyStatus : playerStatus);

    BattleManager IEnemyTurnHost.Manager => this;
    GameState IEnemyTurnHost.CurrentState => CurrentState;
    bool IEnemyTurnHost.IsOnlineMatch => IsOnlineMatch;
    PlayerStatus IEnemyTurnHost.PlayerStatus => playerStatus;
    PlayerStatus IEnemyTurnHost.EnemyStatus => enemyStatus;
    List<CardData> IEnemyTurnHost.CpuHand => cpuHand;
    EnemyAI IEnemyTurnHost.EnemyAI => enemyAI;
    BattleProcessor IEnemyTurnHost.BattleProcessor => battleProcessor;
    HandRefillService IEnemyTurnHost.HandRefill => handRefill;
    CardSequenceManager IEnemyTurnHost.CardSequenceManager => cardSequenceManager;
    CardStatsDisplay IEnemyTurnHost.CardStatsDisplay => cardStatsDisplay;
    CombatSnapshotStore IEnemyTurnHost.CombatSnapshots => _combatSnapshots;
    DualBladeDefenseCoordinator IEnemyTurnHost.DualBladeDefense => _dualBladeDefense;
    CancellationToken IEnemyTurnHost.GetPhaseToken() => _phaseController.GetPhaseToken();
    CardData IEnemyTurnHost.CurrentAttackCard
    {
        get => currentAttackCard;
        set => currentAttackCard = value;
    }
    void IEnemyTurnHost.SetGameState(GameState state) => SetGameState(state);
    Task IEnemyTurnHost.PlayAttackConfirmPresentationAsync(CardData card, Side side, CancellationToken ct)
        => cardSequenceManager.PlayAttackConfirmPresentationAsync(card, side, ct);
    async Task IEnemyTurnHost.RunAfterCombatSharedCleanupAsync(CancellationToken ct)
    {
        if (cardSequenceManager != null)
            await cardSequenceManager.RunAfterCombatSharedCleanupAsync(ct);
    }
    Task<bool> IEnemyTurnHost.ResolveSelfTargetAttackAsync(List<CardData> atkList, CancellationToken ct)
        => ((ISummonSkillHost)this).ResolveConfusionSelfAttackAsync(atkList, ct);
    void IEnemyTurnHost.SetConfusionAttackTargetResolvedForDisplay(bool targetsSelf)
        => SetConfusionAttackTargetResolvedForDisplay(targetsSelf);
    void IEnemyTurnHost.ClearMagicalExplosionComboMpPoolSnapshot() => ClearMagicalExplosionComboMpPoolSnapshot();
    void IEnemyTurnHost.ClearMillionDollarBazookaComboGpPoolSnapshot() => ClearMillionDollarBazookaComboGpPoolSnapshot();
    void IEnemyTurnHost.ClearTributeBloodHpPaidSnapshot() => ClearTributeBloodHpPaidSnapshot();
    void IEnemyTurnHost.ClearHammadnessRollSnapshot() => ClearHammadnessRollSnapshot();
    void IEnemyTurnHost.ClearMagicalSwordEnemyAttackState() => ClearMagicalSwordEnemyAttackState();
    void IEnemyTurnHost.UpdateBattleStatusUi()
        => BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
    void IEnemyTurnHost.UpdateCardStatsDisplay() => cardStatsDisplay?.UpdateDisplay();
    void IEnemyTurnHost.ClearCardStatsSequenceAndAttackLocks()
        => cardStatsDisplay?.ClearSequenceCardsAndAttackDisplayLocks();

    BattleManager IEnemyDefenseHost.Manager => this;
    PlayerType IEnemyDefenseHost.Attacker => Attacker;
    PlayerType IEnemyDefenseHost.Defender => Defender;
    PlayerStatus IEnemyDefenseHost.PlayerStatus => playerStatus;
    PlayerStatus IEnemyDefenseHost.EnemyStatus => enemyStatus;
    List<CardData> IEnemyDefenseHost.PlayerHand => playerHand;
    List<CardData> IEnemyDefenseHost.CpuHand => cpuHand;
    EnemyAI IEnemyDefenseHost.EnemyAI => enemyAI;
    BattleProcessor IEnemyDefenseHost.BattleProcessor => battleProcessor;
    HandRefillService IEnemyDefenseHost.HandRefill => handRefill;
    CardSequenceManager IEnemyDefenseHost.CardSequenceManager => cardSequenceManager;
    CardStatsDisplay IEnemyDefenseHost.CardStatsDisplay => cardStatsDisplay;
    CardData IEnemyDefenseHost.SelectedDefenseCard
    {
        get => selectedDefenseCard;
        set => selectedDefenseCard = value;
    }
    CardData IEnemyDefenseHost.CurrentAttackCard
    {
        get => currentAttackCard;
        set => currentAttackCard = value;
    }
    bool IEnemyDefenseHost.IsOnlineMatch => IsOnlineMatch;
    CancellationToken IEnemyDefenseHost.GetPhaseToken() => _phaseController.GetPhaseToken();
    List<CardData> IEnemyDefenseHost.GetAttackCardsForCombat() => GetAttackCardsForCombat();
    Task<bool> IEnemyDefenseHost.TryHandleDeathIfAnyAsync(CancellationToken ct)
        => TryHandleDeathIfAnyAsync(ct);
    void IEnemyDefenseHost.SetGameState(GameState state) => SetGameState(state);
    void IEnemyDefenseHost.ClearMagicalExplosionComboMpPoolSnapshot() => ClearMagicalExplosionComboMpPoolSnapshot();
    void IEnemyDefenseHost.ClearMillionDollarBazookaComboGpPoolSnapshot() => ClearMillionDollarBazookaComboGpPoolSnapshot();
    void IEnemyDefenseHost.ClearTributeBloodHpPaidSnapshot() => ClearTributeBloodHpPaidSnapshot();
    void IEnemyDefenseHost.ClearHammadnessRollSnapshot() => ClearHammadnessRollSnapshot();
    void IEnemyDefenseHost.SetSuppressEnemyStaleAttackerInTotalByOrb(bool value)
        => SetSuppressEnemyStaleAttackerInTotalByOrb(value);
    void IEnemyDefenseHost.UpdateCardStatsDisplay() => cardStatsDisplay?.UpdateDisplay();
    void IEnemyDefenseHost.ClearCardStatsSequenceAndAttackLocks()
        => cardStatsDisplay?.ClearSequenceCardsAndAttackDisplayLocks();

    BattleManager IPlayerInputHost.Manager => this;
    GameState IPlayerInputHost.CurrentState => CurrentState;
    PlayerType IPlayerInputHost.Attacker => Attacker;
    PlayerType IPlayerInputHost.Defender => Defender;
    bool IPlayerInputHost.IsOnlineMatch => IsOnlineMatch;
    bool IPlayerInputHost.IsPlayerSelfAttackTargetMode => IsPlayerSelfAttackTargetMode;
    PlayerStatus IPlayerInputHost.PlayerStatus => playerStatus;
    PlayerStatus IPlayerInputHost.EnemyStatus => enemyStatus;
    List<CardData> IPlayerInputHost.PlayerHand => playerHand;
    List<CardData> IPlayerInputHost.CpuHand => cpuHand;
    BattleProcessor IPlayerInputHost.BattleProcessor => battleProcessor;
    HandRefillService IPlayerInputHost.HandRefill => handRefill;
    CardSequenceManager IPlayerInputHost.CardSequenceManager => cardSequenceManager;
    CardStatsDisplay IPlayerInputHost.CardStatsDisplay => cardStatsDisplay;
    SellFeature IPlayerInputHost.SellFeature => sellFeature;
    CardData IPlayerInputHost.SelectedCard
    {
        get => selectedCard;
        set => selectedCard = value;
    }
    CardData IPlayerInputHost.SelectedDefenseCard
    {
        get => selectedDefenseCard;
        set => selectedDefenseCard = value;
    }
    CardData IPlayerInputHost.CurrentAttackCard
    {
        get => currentAttackCard;
        set => currentAttackCard = value;
    }
    CancellationToken IPlayerInputHost.GetPhaseToken() => _phaseController.GetPhaseToken();
    bool IPlayerInputHost.IsPlayerDefenseInputActive() => IsPlayerDefenseInputActive();
    bool IPlayerInputHost.IsReactiveAdHocDefenseWaitActive() => IsReactiveAdHocDefenseWaitActive();
    bool IPlayerInputHost.IsAdHocDefenseWaitActive() => IsAdHocDefenseWaitActive();
    void IPlayerInputHost.TrySubmitAdHocPlayerDefense() => TrySubmitAdHocPlayerDefense();
    bool IPlayerInputHost.IsPlayerChantingArchMagicWhileDefending()
        => _defenseInteractivity.IsPlayerChantingArchMagicWhileDefending();
    bool IPlayerInputHost.TryAutoPassPlayerDefenseIfChantingArchMagic()
        => TryAutoPassPlayerDefenseIfChantingArchMagic();
    IReadOnlyList<CardData> IPlayerInputHost.GetIncomingAttackSnapshotForDefenseUi()
        => GetIncomingAttackSnapshotForDefenseUi();
    List<CardData> IPlayerInputHost.GetAttackCardsForCombat() => GetAttackCardsForCombat();
    void IPlayerInputHost.SetGameState(GameState state) => SetGameState(state);
    Task<bool> IPlayerInputHost.TryHandleDeathIfAnyAsync(CancellationToken ct)
        => TryHandleDeathIfAnyAsync(ct);
    Task<bool> IPlayerInputHost.TryPreparePlayerDualBladeSecondDefenseIfNeededAsync(CancellationToken ct)
        => TryPreparePlayerDualBladeSecondDefenseIfNeededAsync(ct);
    void IPlayerInputHost.ClearPlayerSelfAttackTargetMode() => ClearPlayerSelfAttackTargetMode();
    Task IPlayerInputHost.PlayAttackConfirmPresentationAsync(CardData card, Side side, CancellationToken ct)
        => cardSequenceManager.PlayAttackConfirmPresentationAsync(card, side, ct);
    void IPlayerInputHost.SetStatsDisplaySequenceCards(List<CardData> cards, string cardType, Side ownerSide)
        => SetStatsDisplaySequenceCards(cards, cardType, ownerSide);
    async Task IPlayerInputHost.RunAfterCombatSharedCleanupAsync(CancellationToken ct)
    {
        if (cardSequenceManager != null)
            await cardSequenceManager.RunAfterCombatSharedCleanupAsync(ct);
    }
    Task<CardData> IPlayerInputHost.DrawOneCardAsync(int trailingDelayMs, bool playSoundOnDraw)
        => DrawOneCardAsync(trailingDelayMs, playSoundOnDraw);
    void IPlayerInputHost.DrawOneCard() => DrawOneCard();
    int IPlayerInputHost.GetHandMaxCount() => GetHandMaxCount();
    void IPlayerInputHost.UpdateTotalATKDefDisplay() => UpdateTotalATKDEFDisplay();
    void IPlayerInputHost.UpdateBattleStatusUi()
        => BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
    void IPlayerInputHost.ClearMagicalExplosionComboMpPoolSnapshot() => ClearMagicalExplosionComboMpPoolSnapshot();
    void IPlayerInputHost.ClearMillionDollarBazookaComboGpPoolSnapshot() => ClearMillionDollarBazookaComboGpPoolSnapshot();
    void IPlayerInputHost.ClearTributeBloodHpPaidSnapshot() => ClearTributeBloodHpPaidSnapshot();
    void IPlayerInputHost.ClearHammadnessRollSnapshot() => ClearHammadnessRollSnapshot();
    void IPlayerInputHost.ClearCardStatsSequenceAndAttackLocks()
        => cardStatsDisplay?.ClearSequenceCardsAndAttackDisplayLocks();

    BattleManager IBattlePhaseControllerHost.Manager => this;
    GameState IBattlePhaseControllerHost.CurrentState => CurrentState;
    void IBattlePhaseControllerHost.SetCurrentState(GameState state) => CurrentState = state;
    PlayerType IBattlePhaseControllerHost.CurrentTurnOwner
    {
        get => CurrentTurnOwner;
        set => CurrentTurnOwner = value;
    }
    PlayerType IBattlePhaseControllerHost.Attacker => Attacker;
    PlayerType IBattlePhaseControllerHost.Defender => Defender;
    PlayerStatus IBattlePhaseControllerHost.PlayerStatus => playerStatus;
    PlayerStatus IBattlePhaseControllerHost.EnemyStatus => enemyStatus;
    List<CardData> IBattlePhaseControllerHost.PlayerHand => playerHand;
    List<CardData> IBattlePhaseControllerHost.CpuHand => cpuHand;
    Transform IBattlePhaseControllerHost.HandPanel => handPanel;
    bool IBattlePhaseControllerHost.IsOnlineMatch => IsOnlineMatch;
    bool IBattlePhaseControllerHost.ShouldGrayOutCards
    {
        get => shouldGrayOutCards;
        set => shouldGrayOutCards = value;
    }
    bool IBattlePhaseControllerHost.IsProcessingUseButton
    {
        get => _playerInput.IsProcessingUseButton;
        set => _playerInput.IsProcessingUseButton = value;
    }
    CardData IBattlePhaseControllerHost.CurrentAttackCard
    {
        get => currentAttackCard;
        set => currentAttackCard = value;
    }
    CardData IBattlePhaseControllerHost.SelectedDefenseCard
    {
        get => selectedDefenseCard;
        set => selectedDefenseCard = value;
    }
    EnemyAI IBattlePhaseControllerHost.EnemyAI => enemyAI;
    EnemyTurnRunner IBattlePhaseControllerHost.EnemyTurn => _enemyTurn;
    EnemyDefenseResolver IBattlePhaseControllerHost.EnemyDefense => _enemyDefense;
    OnlineBattleSyncService IBattlePhaseControllerHost.OnlineSync => _onlineSync;
    GameEndOrchestrator IBattlePhaseControllerHost.GameEnd => _gameEnd;
    CardSequenceManager IBattlePhaseControllerHost.CardSequenceManager => cardSequenceManager;
    CardStatsDisplay IBattlePhaseControllerHost.CardStatsDisplay => cardStatsDisplay;
    HandRefillService IBattlePhaseControllerHost.HandRefill => handRefill;
    BuyFeature IBattlePhaseControllerHost.BuyFeature => buyFeature;
    SellFeature IBattlePhaseControllerHost.SellFeature => sellFeature;
    SummonTurnCounterState IBattlePhaseControllerHost.SummonTurnCounters => _summonTurnCounters;
    void IBattlePhaseControllerHost.ClearPlayerSelfAttackTargetMode() => ClearPlayerSelfAttackTargetMode();
    void IBattlePhaseControllerHost.ClearConfusionAttackTargetResolvedForDisplay()
        => ClearConfusionAttackTargetResolvedForDisplay();
    void IBattlePhaseControllerHost.ClearOnlineEnemyAttackCombo() => ClearOnlineEnemyAttackCombo();
    void IBattlePhaseControllerHost.ClearMagicalSwordEnemyAttackState() => ClearMagicalSwordEnemyAttackState();
    void IBattlePhaseControllerHost.SetSuppressEnemyStaleAttackerInTotalByOrb(bool value)
        => SetSuppressEnemyStaleAttackerInTotalByOrb(value);
    void IBattlePhaseControllerHost.ClearReflectionAttackTotalDisplay() => ClearReflectionAttackTotalDisplay();
    void IBattlePhaseControllerHost.UpdateTotalATKDEFDisplay() => UpdateTotalATKDEFDisplay();
    void IBattlePhaseControllerHost.RefreshPlayerDefensePhaseInteractivity() => RefreshPlayerDefensePhaseInteractivity();
    void IBattlePhaseControllerHost.RefreshSummonSkillButtonInteractables() => RefreshSummonSkillButtonInteractables();
    bool IBattlePhaseControllerHost.TryAutoPassPlayerDefenseIfChantingArchMagic()
        => TryAutoPassPlayerDefenseIfChantingArchMagic();
    List<CardData> IBattlePhaseControllerHost.GetAttackCardsForCombat() => GetAttackCardsForCombat();
    void IBattlePhaseControllerHost.ToggleTurnOwner() => ToggleTurnOwner();
    Task<bool> IBattlePhaseControllerHost.TryHandleDeathIfAnyAsync(CancellationToken ct)
        => TryHandleDeathIfAnyAsync(ct);

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

    private void OnDestroy()
    {
        _bootstrap.Shutdown(this);
        _phaseController?.Dispose();
    }
}
