using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
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
/// - EnemyAI: 敵の行動決定
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager I;

    // グレーアウト制御フラグ
    private bool shouldGrayOutCards = false;
    
    // 演出中のカードリスト（TotalATKDEF表示用）
    private List<CardData> currentSequenceCards = new List<CardData>();
    private string currentSequenceType = "";

    [Header("バトルUI")]
    public BattleStatusUI statusUI;
    public CutInController cutInController;

    [Header("音響(効果音)")]
    public AudioSource audioSource;
    public AudioClip cardDealSE;
    public AudioClip cardRevealSE;

    [Header("カードUI")]
    public Transform handPanel;
    public GameObject cardUIPrefab;
    public Sprite cardBackSprite;

    [Header("システム")]
    public CardDealer cardDealer;
    public BattleProcessor battleProcessor;

    [Header("UI/演出")]
    public SummonSkillButton summonSkillButton;
    
    [Header("TotalATKDEF表示")]
    public GameObject totalATKDEFButton; // TotalATKDEFボタン（デフォルト非表示）
    public TMP_Text atkdefText; // ATKDEFtextテキストボックス

    // --- 追加: 依存関係の管理 ---
    [SerializeField] private HandRefillService handRefill; // 手札補充用の参照
    private EnemyAI enemyAI = new EnemyAI();

    // バトルデータ
    private PlayerStatus playerStatus, enemyStatus;
    public List<CardData> playerHand = new();
    public List<CardData> cpuHand = new();

    public GameState CurrentState { get; private set; } = GameState.Intro;
    public PlayerType CurrentTurnOwner { get; private set; } = PlayerType.Player;

    private CardData currentAttackCard;
    private CardData selectedCard;
    private CardData selectedDefenseCard;

    private TaskCompletionSource<CardData> defenseCardTCS;
    private CancellationTokenSource _phaseCts;

    [SerializeField] private float cutInDelay = 0.5f;

    private PlayerType Attacker => CurrentTurnOwner;
    private PlayerType Defender => (CurrentTurnOwner == PlayerType.Player) ? PlayerType.Enemy : PlayerType.Player;

    public PlayerStatus GetPlayerStatus() => playerStatus;
    public PlayerStatus GetEnemyStatus() => enemyStatus;

    private void Awake()
    {
        I = this;
    }

    void Start()
    {
        // ステータス初期化
        playerStatus = new PlayerStatus();
        enemyStatus = new PlayerStatus();
        playerStatus.InitializeAsPlayer();
        enemyStatus.InitializeAsEnemy();

        // 召喚データ（プレイヤー：選択済み、敵：ランダム）
        if (SummonSelectionManager.I != null)
        {
            playerStatus.summonData = SummonSelectionManager.I.GetSelectedSummonData();
            enemyStatus.summonData = GetRandomEnemySummon();
        }
        else
        {
            playerStatus.summonData = Resources.Load<SummonData>("Summons/Ifrit");
            enemyStatus.summonData = Resources.Load<SummonData>("Summons/Ifrit");
        }

        summonSkillButton.SetStatus(playerStatus, enemyStatus);

        // システム初期化
        cardDealer.Initialize(playerStatus, enemyStatus, handPanel, cardUIPrefab, cardBackSprite,
                              audioSource, cardDealSE, cardRevealSE);
        battleProcessor.Initialize(playerStatus, enemyStatus, statusUI, cardDealer);

        if (handRefill != null)
            handRefill.Initialize(handPanel, cardUIPrefab, cardBackSprite, audioSource, cardDealSE, cardDealer);

        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        
        // ゲーム開始時はTotalATKDEFを非表示にする
        UpdateTotalATKDEFDisplay();
        
        StartCoroutine(BattleStartSequence());
    }

    //================ 状態遷移 ================
    public void SetGameState(GameState newState)
    {
        if (CurrentState == newState) { Debug.Log($"[State] noop {newState}"); return; }

        _phaseCts?.Cancel(); _phaseCts?.Dispose();
        _phaseCts = new CancellationTokenSource();

        Debug.Log($"[State]{CurrentState} → {newState}(Turn: {CurrentTurnOwner})");
        CurrentState = newState;
        HandleStateChange();
    }

    private void HandleStateChange()
    {
        switch (CurrentState)
        {
            case GameState.Intro:
                break;

            case GameState.TurnStart:
                OnTurnStart();
                break;

            case GameState.AttackSelect:
                EnterAttackSelect();
                break;

            case GameState.AttackConfirm:
                SetGameState(GameState.DefenseSelect);
                break;

            case GameState.DefenseSelect:
                _ = RunDefenseSelectAsync();
                break;

            case GameState.DefenseConfirm:
                _ = RunDefenseConfirmAsync();
                break;

            case GameState.TurnEnd:
                _ = RunTurnEndAsync();
                break;

            case GameState.BattleEnd:
                break;
        }
    }

    //================ バトル開始 ================
    IEnumerator BattleStartSequence()
    {
        yield return StartCoroutine(cardDealer.DealCards(playerHand, cpuHand, 10));

        yield return new WaitForSeconds(cutInDelay);
        bool done = false;
        if (cutInController != null)
        {
            cutInController.OnCutInComplete = () => done = true;
            cutInController.PlayCutIn();
            yield return new WaitUntil(() => done);
        }
        
        // Intro時点ではカードをグレーアウトしない
        BattleUIManager.I?.SetIntroModeUI(playerHand);
        
        SetGameState(GameState.TurnStart);
    }

    private void OnTurnStart()
    {
        if (CurrentTurnOwner == PlayerType.Player)
        {
            
                SoundEffectPlayer.I.Play("Assets/SE/決定ボタンを押す13.mp3");

        }

        if (CurrentTurnOwner == PlayerType.Player) playerStatus.OnTurnStart();
        else enemyStatus.OnTurnStart();

        // ターン開始時にカード詳細表示を非表示
        BattleUIManager.I?.HideAllCardDetails();
        // TotalATKDEFも非表示にする
        UpdateTotalATKDEFDisplay();

        // TurnStart時点ではグレーアウトしない
        BattleUIManager.I?.SetIntroModeUI(playerHand);
        
        // グレーアウト制御フラグを設定（AttackSelectではグレーアウトを有効にする）
        shouldGrayOutCards = true;

        if (CurrentTurnOwner == PlayerType.Player)
        {
            SetGameState(GameState.AttackSelect);
        }
        else
        {
            _ = RunEnemyTurnAsync();
        }
    }

    private void EnterAttackSelect()
    {
        if (Attacker == PlayerType.Player)
        {
            // ターンプレイヤー（攻撃側）の処理
            var attackables = CardRules.GetAttackChoices(playerHand);
            if (attackables.Count == 0 || attackables.TrueForAll(c => c.cardType == CardType.Magic))
            {
                BattleUIManager.I?.SetPrayModeUI(playerHand);
            }
            else
            {
                BattleUIManager.I?.SetUseButtonLabel("使用");
                
                // グレーアウト制御フラグをチェック
                if (shouldGrayOutCards)
                {
                    BattleUIManager.I?.RefreshAttackInteractivity(playerHand, CardRules.GetAttackChoices(playerHand));
                }
                else
                {
                    BattleUIManager.I?.SetIntroModeUI(playerHand);
                }
            }
        }
        else
        {
            // 非ターンプレイヤー（防御側）の処理
            BattleUIManager.I?.SetUseButtonLabel("使用");
            
            // グレーアウト制御フラグをチェック
            if (shouldGrayOutCards)
            {
                BattleUIManager.I?.RefreshDefenseInteractivity(playerHand, CardRules.GetDefenseChoices(playerHand));
            }
            else
            {
                BattleUIManager.I?.SetIntroModeUI(playerHand);
            }
        }
    }

    private async Task RunDefenseSelectAsync()
    {
        // 攻撃カード確定後のインターバルと効果音
        await Task.Delay(1000);
        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す13.mp3");
        Debug.Log("[BattleManager] 攻撃カード確定、防御カード選択開始");

        if (Defender == PlayerType.Enemy)
        {
            // 敵の防御選択（AI）
            selectedDefenseCard = enemyAI.SelectDefenseCard(cpuHand);

            // 相手の防御カード選択完了時の効果音
            if (selectedDefenseCard != null)
            {
                SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
                Debug.Log($"[BattleManager] 相手の防御カード選択完了: {selectedDefenseCard.cardName}");
            }

            // ③防御カード選択後の0.5秒インターバル
            await Task.Delay(500);
            Debug.Log("[BattleManager] 防御カード選択完了、0.5秒待機");
            
            SetGameState(GameState.DefenseConfirm);
        }
        else
        {
            // プレイヤー選択（複数選択対応）
            BattleUIManager.I?.SetUseButtonLabel("許す"); // 初期状態は「許す」
            BattleUIManager.I?.RefreshDefenseInteractivity(playerHand, CardRules.GetDefenseChoices(playerHand));
            
            // プレイヤーが防御カードを選択するまで待機
            // OnUseButtonPressedでHandleDefenseUseが呼ばれるまで待つ
        }
    }

    private async Task RunDefenseConfirmAsync()
    {
        if (currentAttackCard == null)
        {
            Debug.LogWarning("攻撃カードが設定されていません");
            SetGameState(GameState.AttackSelect);
            return;
        }

        // プレイヤーの複数防御カード選択の場合は、StartDefenseCardSequenceで処理済み
        if (Defender == PlayerType.Player)
        {
            Debug.Log("[BattleManager] プレイヤーの防御カード選択は既にStartDefenseCardSequenceで処理済み");
            return;
        }

        // 敵の単一防御カードの処理
        var defenseCardToDisplay = selectedDefenseCard;
        if (defenseCardToDisplay != null)
        {
            var side = (Defender == PlayerType.Player) ? Side.Player : Side.Enemy;
            
            // プレイヤーの防御カードの場合、選択状態をクリアしてから表示
            if (Defender == PlayerType.Player)
            {
                BattleUIManager.I?.ClearAllSelections();
                BattleUIManager.I?.HideAllCardDetails();
            }
            
            BattleUIManager.I?.ShowCardDetail(defenseCardToDisplay, side);
            
            // 防御カード表示時の効果音
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            
            Debug.Log($"[BattleManager] 防御カード表示: {defenseCardToDisplay.cardName}");
            
            // 0.5秒待機
            await Task.Delay(500);
            Debug.Log("[BattleManager] 防御カード表示完了、0.5秒待機");
        }

        var atk = (Attacker == PlayerType.Player) ? playerStatus : enemyStatus;
        var def = (Defender == PlayerType.Player) ? playerStatus : enemyStatus;
        var defHand = (Defender == PlayerType.Player) ? playerHand : cpuHand;

        // 攻撃カードを取得（複数選択対応）
        List<CardData> attackCards = GetAttackCardsForCombat();

        await battleProcessor.ResolveCombatAsync(attackCards, selectedDefenseCard, atk, def, defHand);

        if (_phaseCts.Token.IsCancellationRequested) return;

        // 防御カード使用処理（裏向きにする）
        if (defenseCardToDisplay != null)
        {
            battleProcessor.UseCard(defenseCardToDisplay, defHand);
            
            // HandRefillServiceに使用を記録
            if (handRefill != null)
            {
                if (Defender == PlayerType.Player && defenseCardToDisplay.cardUI != null)
                {
                    int idx = defenseCardToDisplay.cardUI.transform.GetSiblingIndex();
                    handRefill.RecordPlayerUseSlot(idx);
                    Debug.Log($"[BattleManager] プレイヤー防御カード使用記録: {defenseCardToDisplay.cardName} (スロット {idx})");
                }
                else if (Defender == PlayerType.Enemy)
                {
                    handRefill.RecordEnemyUse();
                    Debug.Log($"[BattleManager] 敵防御カード使用記録: {defenseCardToDisplay.cardName}");
                }
            }
            else
            {
                Debug.LogWarning("[BattleManager] handRefillがnullです");
            }
        }

        SetGameState(GameState.TurnEnd);
    }

    private async Task RunTurnEndAsync()
    {
        if (handRefill != null)
        {
            await handRefill.RefillAtTurnEndAsync(playerHand, cpuHand, _phaseCts.Token);
        }

        if (_phaseCts.Token.IsCancellationRequested) return;

        // ⑥相手の攻撃ターン前の0.5秒インターバル
        await Task.Delay(500);
        Debug.Log("[BattleManager] 相手の攻撃ターン前、0.5秒待機");

        // 2ターン目以降はグレーアウトを有効にする
        shouldGrayOutCards = true;

        ToggleTurnOwner();
        SetGameState(GameState.TurnStart);
    }

    //================ 敵のターン ================
    private async Task RunEnemyTurnAsync()
    {
        // 相手の攻撃フェーズ開始時の効果音
        SoundEffectPlayer.I?.Play("Assets/SE/鳩時計1.mp3");
        Debug.Log("[BattleManager] 相手の攻撃フェーズ開始");
        
        // 鳩時計効果音後のインターバル
        await Task.Delay(500);
        Debug.Log("[BattleManager] 鳩時計効果音後、0.5秒待機");

        var attack = enemyAI.SelectAttackCard(cpuHand);
        if (attack == null)
        {
            SetGameState(GameState.TurnEnd);
            return;
        }

        battleProcessor.UseCard(attack, cpuHand);
        handRefill?.RecordEnemyUse();

        currentAttackCard = attack;

        // 敵の攻撃カードを表示
        BattleUIManager.I?.ShowCardDetail(attack, Side.Enemy);
        
        // 相手のカード決定時の効果音
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        Debug.Log($"[BattleManager] 相手のカード決定: {attack.cardName}");

        await Task.Delay(1000);
        SetGameState(GameState.DefenseSelect);
    }

    //================ カード選択 ================
    public void SetSelectedCard(CardUI ui)
    {
        if (ui == null) return;
        var card = ui.GetCardData();

        if (CurrentState == GameState.AttackSelect && Attacker == PlayerType.Player)
        {
            if (!CardRules.IsUsableInAttackPhase(card))
            {
                Debug.LogWarning($"このカードは攻撃フェーズでは使えません: {card.cardName} ({card.cardType})");
                return;
            }
            selectedCard = card;
            BattleUIManager.I?.ShowCardDetail(card, Side.Player);
            // カード選択音を再生
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            // TotalATKDEF表示を更新
            UpdateTotalATKDEFDisplay();
            return;
        }

        if (CurrentState == GameState.DefenseSelect && Defender == PlayerType.Player)
        {
            if (!CardRules.IsUsableInDefensePhase(card))
            {
                Debug.LogWarning($"このカードは防御フェーズでは使えません: {card.cardName} ({card.cardType})");
                return;
            }
            selectedDefenseCard = card;
            BattleUIManager.I?.ShowCardDetail(card, Side.Player);
            // カード選択音を再生
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            // TotalATKDEF表示を更新
            UpdateTotalATKDEFDisplay();
            // 防御フェーズのボタンラベルを更新
            BattleUIManager.I?.UpdateDefenseButtonLabel();
            return;
        }

        Debug.Log("カード選択は現在できません");
    }

    public void OnUseButtonPressed()
    {
        switch (CurrentState)
        {
            case GameState.AttackSelect:
                if (Attacker == PlayerType.Player)
                    HandleAttackUse();
                break;

            case GameState.DefenseSelect:
                if (Defender == PlayerType.Player)
                    HandleDefenseUse();
                break;
        }
    }

    private async Task ResolveImmediateEffectAsync(CardData card, int slotIndex)
    {
        // カード表示後、ポップアップ表示前に0.5秒のインターバル
        await Task.Delay(500);
        Debug.Log("[BattleManager] 回復カード表示後、0.5秒インターバル完了");

        await battleProcessor.ResolveImmediateEffectAsync(card, playerStatus, enemyStatus);

        if (slotIndex >= 0) handRefill?.RecordPlayerUseSlot(slotIndex);

        selectedCard = null;
        BattleUIManager.I?.HideAllCardDetails();
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        // TotalATKDEF表示を更新
        UpdateTotalATKDEFDisplay();

        // ポップアップ表示後、ターン終了前に0.5秒のインターバル
        await Task.Delay(500);
        Debug.Log("[BattleManager] 回復ポップアップ表示後、0.5秒インターバル完了");

        // 回復カード（即時効果）の場合は防御フェーズをスキップして直接ターン終了
        SetGameState(GameState.TurnEnd);
    }

    //================ カード使用処理 ================
    private void HandleAttackUse()
    {
        // BattleUIManagerから選択中の攻撃カードを取得
        var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
        if (selectedAttackCards == null || selectedAttackCards.Count == 0)
        {
            Debug.LogWarning("攻撃カードが選択されていません");
            return;
        }

        // 即時効果（回復など）の場合は通常処理
        if (selectedAttackCards.Count == 1 && CardRules.IsImmediateAction(selectedAttackCards[0]))
        {
            var card = selectedAttackCards[0];
            int slotIndex = (card.cardUI != null) ? card.cardUI.transform.GetSiblingIndex() : -1;
            battleProcessor.UseCard(card, playerHand);
            BattleUIManager.I?.ShowCardDetail(card, Side.Player);
            
            // 選択状態をクリア
            selectedCard = null;
            BattleUIManager.I?.ClearAllSelections();
            UpdateTotalATKDEFDisplay();
            
            _ = ResolveImmediateEffectAsync(card, slotIndex);
            return;
        }

        // 攻撃カードの演出フロー開始
        _ = StartCardSequence(selectedAttackCards, "攻撃", Side.Player);
    }

    private void HandleDefenseUse()
    {
        var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
        if (selectedDefenseCards == null || selectedDefenseCards.Count == 0)
        {
            // 防御カードを1枚も使わない場合（「許す」）
            Debug.Log("[BattleManager] 防御カードを使用せずにダメージを受ける（許す）");
            HandleNoDefenseCard();
            return;
        }

        // 防御カードの演出フロー開始
        _ = StartCardSequence(selectedDefenseCards, "防御", Side.Player);
    }

    /// <summary>
    /// 防御カードを1枚も使わない場合の処理（「許す」）
    /// </summary>
    private async void HandleNoDefenseCard()
    {
        // 選択状態をクリア
        BattleUIManager.I?.ClearAllSelections();
        UpdateTotalATKDEFDisplay();

        // 戦闘解決処理（防御カードなし）
        var atk = (Attacker == PlayerType.Player) ? playerStatus : enemyStatus;
        var def = (Defender == PlayerType.Player) ? playerStatus : enemyStatus;
        var defHand = (Defender == PlayerType.Player) ? playerHand : cpuHand;

        // 攻撃カードを取得（複数選択対応）
        List<CardData> attackCards = GetAttackCardsForCombat();

        // 防御カードなしで戦闘解決
        await battleProcessor.ResolveCombatAsync(attackCards, (CardData)null, atk, def, defHand);

        if (_phaseCts.Token.IsCancellationRequested) return;

        // 防御カード確定後の処理
        SetGameState(GameState.TurnEnd);
    }


    /// <summary>
    /// カード処理（攻撃・防御共通）
    /// </summary>
    private void ProcessCards(List<CardData> cards, string cardType)
    {
        if (cards.Count > 1)
        {
            Debug.Log($"[BattleManager] 複数{cardType}カード選択中: {cards.Count}枚。全てのカードを処理します。");
            ProcessMultipleCards(cards, cardType);
        }
        else
        {
            Debug.Log($"[BattleManager] 単一{cardType}カード選択中。カードを処理します。");
            ProcessSingleCard(cards[0], cardType);
        }
    }

    /// <summary>
    /// 複数カードの処理（攻撃・防御共通）
    /// </summary>
    private void ProcessMultipleCards(List<CardData> cards, string cardType)
    {
        // 攻撃カードの場合は最初のカードをcurrentAttackCardに設定
        if (cardType == "攻撃" && cards.Count > 0)
        {
            currentAttackCard = cards[0];
        }
        
        foreach (var card in cards)
        {
            if (card?.cardUI == null) continue;
            
            int slotIndex = card.cardUI.transform.GetSiblingIndex();
            battleProcessor.UseCard(card, playerHand);
            handRefill?.RecordPlayerUseSlot(slotIndex);
            Debug.Log($"[BattleManager] {cardType}カード処理: {card.cardName} (スロット: {slotIndex})");
        }
    }

    /// <summary>
    /// 単一カードの処理（攻撃・防御共通）
    /// </summary>
    private void ProcessSingleCard(CardData card, string cardType)
    {
        if (cardType == "防御")
        {
            selectedDefenseCard = card;
        }
        else
        {
            selectedCard = card;
            currentAttackCard = card; // 攻撃カードの場合はcurrentAttackCardも設定
        }
        
        int slotIndex = (card.cardUI != null) ? card.cardUI.transform.GetSiblingIndex() : -1;
        battleProcessor.UseCard(card, playerHand);
        handRefill?.RecordPlayerUseSlot(slotIndex);
        Debug.Log($"[BattleManager] 単一{cardType}カード処理: {card.cardName} (スロット: {slotIndex})");
    }

    /// <summary>
    /// カード演出シーケンス（攻撃・防御共通）
    /// ①表示ゾーンクリア → ②カード順次表示（0.5秒インターバル）
    /// </summary>
    private async Task StartCardSequence(List<CardData> selectedCards, string cardType, Side side)
    {
        Debug.Log($"[BattleManager] {cardType}カード演出開始: {selectedCards.Count}枚");

        // 演出中のカードリストを初期化
        currentSequenceCards.Clear();
        currentSequenceType = cardType;

        // ①表示ゾーンをクリア
        BattleUIManager.I?.ClearAllSelections();
        BattleUIManager.I?.HideAllCardDetails();
        Debug.Log("[BattleManager] 表示ゾーンをクリアしました");

        // クリア後のインターバル（まっさらな状態を維持）
        await Task.Delay(300);
        Debug.Log("[BattleManager] クリア後インターバル完了");

        // ②カードを順次表示（0.5秒インターバル）
        for (int i = 0; i < selectedCards.Count; i++)
        {
            var card = selectedCards[i];
            BattleUIManager.I?.ShowCardDetail(card, side);
            
            // 演出中のカードリストに追加
            currentSequenceCards.Add(card);
            
            // TotalATKDEF表示を更新
            UpdateTotalATKDEFDisplay();
            
            // カード表示効果音を再生（Addressables使用）
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            
            Debug.Log($"[BattleManager] {cardType}カード表示: {card.cardName} ({i + 1}/{selectedCards.Count})");
            
            // すべてのカード表示後に0.5秒待機（最後のカードも選択枠を表示）
            await Task.Delay(500);
        }

        // カードの処理
        ProcessCards(selectedCards, cardType);

        // 選択状態をクリア（ProcessCardsで既に設定済み）
        BattleUIManager.I?.ClearAllSelections();
        UpdateTotalATKDEFDisplay();

        // 戦闘解決処理
        var atk = (Attacker == PlayerType.Player) ? playerStatus : enemyStatus;
        var def = (Defender == PlayerType.Player) ? playerStatus : enemyStatus;
        var defHand = (Defender == PlayerType.Player) ? playerHand : cpuHand;

        // 攻撃カードを取得（selectedCardsパラメータを直接使用）
        List<CardData> attackCards = GetAttackCardsForCombat(selectedCards);

        // 戦闘解決を呼び出し
        if (cardType == "攻撃")
        {
            // 攻撃カードの場合、防御カードは単一またはnull
            await battleProcessor.ResolveCombatAsync(attackCards, selectedDefenseCard, atk, def, defHand);
        }
        else
        {
            // 防御カードの場合、複数防御カード対応
            await battleProcessor.ResolveCombatAsync(attackCards, selectedCards, atk, def, defHand);
        }

        if (_phaseCts.Token.IsCancellationRequested) return;

        // ダメージ処理完了後、演出中のカードリストをクリア
        currentSequenceCards.Clear();
        currentSequenceType = "";
        UpdateTotalATKDEFDisplay();

        // カード確定後の処理
        SetGameState(GameState.TurnEnd);
    }

    private List<CardData> GetAttackCardsForCombat(List<CardData> selectedCards = null)
    {
        if (Attacker == PlayerType.Player)
        {
            Debug.Log("[BattleManager] プレイヤーの攻撃カードを取得中...");
            
            // selectedCardsパラメータが提供されている場合はそれを使用
            if (selectedCards != null)
            {
                var attackCards = new List<CardData>();
                foreach (var card in selectedCards)
                {
                    if (card.cardType == CardType.Attack || card.isPrimaryAttack || card.isAdditionalAttack)
                    {
                        attackCards.Add(card);
                    }
                }
                Debug.Log($"[BattleManager] selectedCardsから取得した攻撃カード数: {attackCards.Count}");
                return attackCards;
            }
            
            // フォールバック: UIから取得
            var uiAttackCards = BattleUIManager.I?.GetSelectedAttackCards() ?? new List<CardData>();
            if (uiAttackCards.Count == 0 && currentAttackCard != null)
            {
                Debug.Log($"[BattleManager] フォールバック: 単一カード {currentAttackCard.cardName} を使用");
                uiAttackCards = new List<CardData> { currentAttackCard };
            }
            Debug.Log($"[BattleManager] UIから取得した攻撃カード数: {uiAttackCards.Count}");
            return uiAttackCards;
        }
        else
        {
            Debug.Log($"[BattleManager] 敵の攻撃カード: {currentAttackCard?.cardName ?? "なし"}");
            return new List<CardData> { currentAttackCard };
        }
    }

    //================ 手番管理 ================
    public void ToggleTurnOwner()
    {
        CurrentTurnOwner = (CurrentTurnOwner == PlayerType.Player) ? PlayerType.Enemy : PlayerType.Player;
        Debug.Log($"[Turn] 手番変更: {CurrentTurnOwner}");
    }

    //================ カード選択クリア ================
    public void ClearSelectedCards()
    {
        selectedCard = null;
        selectedDefenseCard = null;
        UpdateTotalATKDEFDisplay();
    }

    //================ TotalATKDEF表示 ================
    public void UpdateTotalATKDEFDisplay()
    {
        if (totalATKDEFButton == null) 
        {
            Debug.LogWarning("[BattleManager] totalATKDEFButtonが設定されていません");
            return;
        }

        // ボタンを非表示にする条件をチェック
        bool shouldHide = ShouldHideTotalATKDEF();
        totalATKDEFButton.SetActive(!shouldHide);
        
        Debug.Log($"[BattleManager] TotalATKDEF表示更新: 非表示={shouldHide}, 状態={CurrentState}, 選択カード={selectedCard?.cardName ?? "なし"}");

        if (shouldHide) return;

        // ATKDEFtextテキストボックスを更新
        if (atkdefText != null)
        {
            string displayText = GetTotalATKDEFText();
            atkdefText.text = displayText;
            Debug.Log($"[BattleManager] TotalATKDEFテキスト更新: {displayText}");
        }
        else
        {
            Debug.LogWarning("[BattleManager] ATKDEFtextが設定されていません");
        }
    }

    private bool ShouldHideTotalATKDEF()
    {
        // 演出中のカードがある場合
        if (currentSequenceCards.Count > 0)
        {
            if (currentSequenceType == "攻撃")
            {
                int totalAttack = CalculateTotalAttackPower(currentSequenceCards);
                if (totalAttack <= 0) return true;
                return false;
            }
            else if (currentSequenceType == "防御")
            {
                int totalDefense = CalculateTotalDefensePower(currentSequenceCards);
                if (totalDefense <= 0) return true;
                return false;
            }
        }

        // 攻撃フェーズの場合
        if (CurrentState == GameState.AttackSelect)
        {
            // BattleUIManagerの選択中カードをチェック
            var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
            if (selectedAttackCards != null && selectedAttackCards.Count > 0)
            {
                // 複数カード選択時は合計攻撃力が0以下の場合は非表示
                int totalAttack = CalculateTotalAttackPower(selectedAttackCards);
                if (totalAttack <= 0) return true;
                
                // 表示する
                return false;
            }
            
            // 単一カード選択の場合
            if (selectedCard == null) return true;
            
            // 回復カードや特殊カード（即時効果）の場合は非表示
            if (CardRules.IsImmediateAction(selectedCard)) return true;
            
            // 攻撃力が0以下の場合は非表示
            if (selectedCard.attackPower <= 0) return true;
            
            // 表示する
            return false;
        }

        // 防御フェーズの場合
        if (CurrentState == GameState.DefenseSelect)
        {
            // BattleUIManagerの選択中カードをチェック
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
            {
                // 複数カード選択時は合計防御力が0以下の場合は非表示
                int totalDefense = CalculateTotalDefensePower(selectedDefenseCards);
                if (totalDefense <= 0) return true;
                
                // 表示する
                return false;
            }
            
            // 防御カードが選択されていない場合は非表示
            return true;
        }

        // その他の状態では非表示
        return true;
    }

    private string GetTotalATKDEFText()
    {
        // 演出中のカードがある場合
        if (currentSequenceCards.Count > 0)
        {
            if (currentSequenceType == "攻撃")
            {
                int totalAttack = CalculateTotalAttackPower(currentSequenceCards);
                return $"ATK {totalAttack}";
            }
            else if (currentSequenceType == "防御")
            {
                int totalDefense = CalculateTotalDefensePower(currentSequenceCards);
                return $"DEF {totalDefense}";
            }
        }

        if (CurrentState == GameState.AttackSelect)
        {
            // 攻撃フェーズ：選択中の攻撃カードの合計攻撃力を計算
            var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
            if (selectedAttackCards != null && selectedAttackCards.Count > 0)
            {
                int totalAttack = CalculateTotalAttackPower(selectedAttackCards);
                return $"ATK {totalAttack}";
            }
            else if (selectedCard != null)
            {
                return $"ATK {selectedCard.attackPower}";
            }
        }
        else if (CurrentState == GameState.DefenseSelect)
        {
            // 防御フェーズ：選択中の防御カードの合計防御力を表示
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
            {
                int totalDefense = CalculateTotalDefensePower(selectedDefenseCards);
                return $"DEF {totalDefense}";
            }
            else if (selectedDefenseCard != null)
            {
                return $"DEF {selectedDefenseCard.defensePower}";
            }
        }

        return "";
    }

    /// <summary>
    /// カードリストの合計攻撃力・防御力を計算（統一メソッド）
    /// </summary>
    private int CalculateTotalPower(List<CardData> cards, bool isAttack)
    {
        int total = 0;
        foreach (var card in cards)
        {
            if (card != null)
            {
                total += isAttack ? card.attackPower : card.defensePower;
            }
        }
        return total;
    }

    // 後方互換性のためのラッパーメソッド
    private int CalculateTotalAttackPower(List<CardData> attackCards)
    {
        return CalculateTotalPower(attackCards, true);
    }

    private int CalculateTotalDefensePower(List<CardData> defenseCards)
    {
        return CalculateTotalPower(defenseCards, false);
    }

    //================ ユーティリティ ================
    private SummonData GetRandomEnemySummon()
    {
        var list = SummonSelectionManager.I?.GetAllSummonData();
        if (list == null || list.Length == 0) return null;

        var enemyCandidates = new List<SummonData>(list);
        if (SummonSelectionManager.I != null)
        {
            enemyCandidates.RemoveAt(SummonSelectionManager.I.SelectedIndex);
        }

        return enemyCandidates[Random.Range(0, enemyCandidates.Count)];
    }

    private void OnDestroy()
    {
        _phaseCts?.Cancel();
        _phaseCts?.Dispose();
    }
}
