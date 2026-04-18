using System;
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

    [Header("UI/演出")]
    public SummonSkillButton summonSkillButton;
    public CardPurchaseAnimation cardPurchaseAnimation;
    [SerializeField] private CardSellAnimation cardSellAnimation;
    
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
    private BuyFeature buyFeature = new BuyFeature();
    private SellFeature sellFeature = new SellFeature();
    [SerializeField] private ExchangeFeature exchangeFeature;

    // バトルデータ
    private PlayerStatus playerStatus, enemyStatus;
    public List<CardData> playerHand = new();
    public List<CardData> cpuHand = new();
    

    public GameState CurrentState { get; private set; } = GameState.Intro;
    public PlayerType CurrentTurnOwner { get; private set; } = PlayerType.Player;

    private CardData currentAttackCard;
    
    /// <summary>
    /// 現在の攻撃カードを設定（BuyFeature、CardSequenceManagerから使用）
    /// </summary>
    public void SetCurrentAttackCard(CardData card)
    {
        currentAttackCard = card;
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

        if (CurrentState != GameState.AttackSelect || Attacker != PlayerType.Player)
        {
            Debug.Log($"[BattleManager] MagicPanel カード選択不可: 現在のState={CurrentState}");
            return;
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
    private TaskCompletionSource<bool> _interventionDefenseSubmitTcs;
    private List<CardData> _interventionAttackForDefenseUi;
    private bool isProcessingUseButton;
    public bool IsUseButtonLocked => isProcessingUseButton;

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
        if (selectedDefenseCard != null)
        {
            BattleUIManager.I?.ShowCardDetail(selectedDefenseCard, Side.Enemy);
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            await Task.Delay(500);
        }
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

    /// <summary>
    /// 防御フェーズ・プレイヤー防御側の手札グレーアウト（拘束時は選択済み1枚のみ）と「体が重い」オーバーレイを更新。
    /// </summary>
    /// <summary>TurnEnd 中、敵介入のプレイヤー防御入力待ちか。</summary>
    public bool IsInterventionDefenseWaitActive()
    {
        return _interventionDefenseSubmitTcs != null && !_interventionDefenseSubmitTcs.Task.IsCompleted;
    }

    public void BeginInterventionPlayerDefensePhase(List<CardData> attackCardsForElement)
    {
        _interventionAttackForDefenseUi = attackCardsForElement;
        _interventionDefenseSubmitTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        BattleUIManager.I?.ClearAllSelections();
        BattleUIManager.I?.HidePlayerCardDetails();

        RefreshPlayerDefensePhaseInteractivity();
        BattleUIManager.I?.SetHandClickable(true);
        BattleUIManager.I?.UpdateDefenseButtonLabel();
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

    private void TrySubmitInterventionPlayerDefense()
    {
        var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
        if (selectedDefenseCards == null)
            selectedDefenseCards = new List<CardData>();

        if (playerStatus != null && playerStatus.HasRestraintEffect() && selectedDefenseCards.Count > 1)
        {
            BattleUIManager.I?.ShowInfoPopupOnCardPanel("体が重い", new Color(0.22f, 0.24f, 0.38f));
            isProcessingUseButton = false;
            BattleUIManager.I?.SetUseButtonInteractable(true);
            BattleUIManager.I?.SetHandClickable(true);
            return;
        }

        _interventionDefenseSubmitTcs?.TrySetResult(true);
        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.SetUseButtonInteractable(false);
        isProcessingUseButton = false;
    }

    public void RefreshPlayerDefensePhaseInteractivity()
    {
        bool interventionDefense = CurrentState == GameState.TurnEnd && IsInterventionDefenseWaitActive();

        if (!(CurrentState == GameState.DefenseSelect && Defender == PlayerType.Player) && !interventionDefense)
            return;
        if (BattleUIManager.I == null) return;

        List<CardData> attackSource;
        if (interventionDefense && _interventionAttackForDefenseUi != null)
            attackSource = _interventionAttackForDefenseUi;
        else if (CurrentState == GameState.DefenseSelect)
            attackSource = GetAttackCardsForCombat();
        else
            return;

        ElementType attackElement = ElementHelper.GetCombinedElement(attackSource);
        var defenseChoices = CardRules.GetDefenseChoicesForElement(playerHand, attackElement);
        if (ReflectionRules.CanReflectPhysical(attackSource))
        {
            foreach (var c in playerHand)
            {
                if (c != null && ReflectionRules.IsPhysicalReflectionCard(c) && !defenseChoices.Contains(c))
                    defenseChoices.Add(c);
            }
        }
        else
        {
            defenseChoices.RemoveAll(c => c != null && ReflectionRules.IsPhysicalReflectionCard(c));
        }

        var selectedDefense = BattleUIManager.I.GetSelectedDefenseCards();
        defenseChoices = CardRules.ApplyRestraintDefenseFilter(
            defenseChoices,
            selectedDefense,
            playerStatus != null && playerStatus.HasRestraintEffect());

        BattleUIManager.I.RefreshDefenseInteractivity(playerHand, defenseChoices);
    }

    /// <summary>連鎖反射でプレイヤーが再防御を選ぶまで待つ（許す＝空リスト）。</summary>
    public async Task<List<CardData>> WaitForReflectionChainDefenseAsync(
        List<CardData> attackSnapshot,
        CancellationToken cancellationToken)
    {
        _reflectionChainDefenseTcs = new TaskCompletionSource<List<CardData>>(TaskCreationOptions.RunContinuationsAsynchronously);
        isProcessingUseButton = false;
        BattleUIManager.I?.SetUseButtonInteractable(true);
        BattleUIManager.I?.SetHandClickable(true);
        BattleUIManager.I?.SetUseButtonLabel("許す");
        BattleUIManager.I?.ClearAllSelections();
        ClearSelectedCards();
        RefreshReflectionChainInteractivity(attackSnapshot);
        BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);

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
            _reflectionChainDefenseTcs = null;
            BattleUIManager.I?.SetHandClickable(false);
        }
    }

    private void RefreshReflectionChainInteractivity(List<CardData> attackSnapshot)
    {
        if (BattleUIManager.I == null || attackSnapshot == null) return;

        ElementType attackElement = ElementHelper.GetCombinedElement(attackSnapshot);
        var defenseChoices = CardRules.GetDefenseChoicesForElement(playerHand, attackElement);
        if (ReflectionRules.CanReflectPhysical(attackSnapshot))
        {
            foreach (var c in playerHand)
            {
                if (c != null && ReflectionRules.IsPhysicalReflectionCard(c) && !defenseChoices.Contains(c))
                    defenseChoices.Add(c);
            }
        }
        else
        {
            defenseChoices.RemoveAll(c => c != null && ReflectionRules.IsPhysicalReflectionCard(c));
        }

        var selectedDefense = BattleUIManager.I.GetSelectedDefenseCards();
        defenseChoices = CardRules.ApplyRestraintDefenseFilter(
            defenseChoices,
            selectedDefense,
            playerStatus != null && playerStatus.HasRestraintEffect());

        BattleUIManager.I.RefreshDefenseInteractivity(playerHand, defenseChoices);
    }

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
        var battleDebug = FindObjectOfType<BattleDebugTools>();
        battleDebug?.ApplyInitialSummonOverrides(playerStatus, enemyStatus);
#endif

        summonSkillButton.SetStatus(playerStatus, enemyStatus);

        // システム初期化
        cardDealer.Initialize(playerStatus, enemyStatus, handPanel, cardUIPrefab, cardBackSprite);
        battleProcessor.Initialize(playerStatus, enemyStatus, statusUI, cardDealer);
        DiseaseTurnEndProcessor.BindSettings(diseaseTurnEndSettings);
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
            Debug.Log("[BattleManager] MagicPoolManager初期化完了");
        }

        if (cardStatsDisplay != null)
        {
            cardStatsDisplay?.UpdateDisplay();
        }
        
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
        isProcessingUseButton = false;
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
        SummonGarudaLifecycle.GetOpeningHandTargets(playerStatus, enemyStatus, out int openingPlayer, out int openingCpu);
        yield return StartCoroutine(cardDealer.DealOpeningHands(playerHand, cpuHand, openingPlayer, openingCpu));

        // 手札が配られた後にステータスを更新
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        BattleUIManager.I?.RefreshTurnCountDisplay(_summonTurnCounters);

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
        BattleUIManager.I?.HideYurusuButton();
        BattleUIManager.I?.RefreshTurnCountDisplay(_summonTurnCounters);

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
        cardStatsDisplay?.UpdateDisplay();

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
        BattleUIManager.I?.SetHandClickable(true);

        if (Attacker == PlayerType.Player)
        {
            // ターンプレイヤー（攻撃側）の処理
            var attackables = CardRules.GetAttackChoices(playerHand);
            if (attackables.Count == 0)
            {
                BattleUIManager.I?.SetPrayModeUI(playerHand);
            }
            else
            {
                BattleUIManager.I?.SetUseButtonLabel("使用");
                BattleUIManager.I?.SetUseButtonInteractable(false);
                
                // グレーアウト制御フラグをチェック
                if (shouldGrayOutCards)
                {
                    BattleUIManager.I?.RefreshAttackInteractivity(playerHand, CardRules.GetAttackChoices(playerHand));
                }
                else
                {
                    BattleUIManager.I?.SetIntroModeUI(playerHand);
                }
                
                // 経済アクションボタンの状態を更新
                BattleUIManager.I?.UpdateEconomicActionButtons();
            }

            // MagicPanel のインタラクティブ状態を更新
            BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
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

                SetGameState(GameState.DefenseConfirm);
                reachedDefenseConfirm = true;
            }
            else
            {
                BattleUIManager.I?.HidePlayerCardDetails();
                BattleUIManager.I?.SetHandClickable(true);
                BattleUIManager.I?.SetUseButtonLabel("許す");

                RefreshPlayerDefensePhaseInteractivity();

                BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
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
                SetGameState(GameState.AttackSelect);
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
                SetGameState(GameState.TurnEnd);
                return;
            }

            if (currentAttackCard.cardName == "経済アクション（売却）")
            {
                Debug.Log("[BattleManager] 経済アクション（売却）の防御フェーズ処理");
                await sellFeature.ProcessEconomicActionAsync();
                currentAttackCard = null;
                selectedDefenseCard = null;
                UpdateTotalATKDEFDisplay();
                SetGameState(GameState.TurnEnd);
                return;
            }

            // プレイヤーの防御カード選択はCardSequenceManagerで処理済み（HandleDefenseUse経由）
            if (Defender == PlayerType.Player)
            {
                return;
            }

            // 敵の単一防御カードの処理
            var defenseCardToDisplay = selectedDefenseCard;
            if (defenseCardToDisplay != null)
            {
                // 敵の防御カードを表示
                BattleUIManager.I?.ShowCardDetail(defenseCardToDisplay, Side.Enemy);

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

            List<CardData> attackCards = GetAttackCardsForCombat();

            bool showYurusuDuringCombat =
                Defender == PlayerType.Enemy && selectedDefenseCard == null && BattleUIManager.I != null;
            if (showYurusuDuringCombat)
                BattleUIManager.I.ShowYurusuDisplay();

            try
            {
                await battleProcessor.ResolveCombatAsync(attackCards, selectedDefenseCard, atk, def, defHand);
            }
            finally
            {
                if (showYurusuDuringCombat)
                    BattleUIManager.I?.HideYurusuButton();
            }

            if (_phaseCts.Token.IsCancellationRequested) return;

            // ダメージ処理完了後、全カード表示をクリア
            BattleUIManager.I?.HideAllCardDetails();

            // 敵の防御カード使用処理（裏向きにする）
            if (defenseCardToDisplay != null)
            {
                // HandRefillServiceに使用を記録（UseCardの前に呼ぶ必要がある）
                handRefill?.RecordEnemyUse(defenseCardToDisplay);
                battleProcessor.UseCard(defenseCardToDisplay, defHand);
            }

            SetGameState(GameState.TurnEnd);
        }
        finally
        {
            if (sellFlow && sellFeature != null)
                sellFeature.ForceEndSellProcessingState();
        }
    }

    private async Task RunTurnEndAsync()
    {
        // TurnEnd 突入時のフェーズ専用トークン（途中で SetGameState され _phaseCts が差し替わっても判定に使う）
        CancellationToken phaseToken = _phaseCts != null ? _phaseCts.Token : default;

        try
        {
            if (CurrentState != GameState.TurnEnd) return;

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

            if (CurrentState != GameState.TurnEnd) return;

            // 攻撃フェーズ終了直後：攻撃側の病系処理（補充・ドローより先）
            PlayerStatus attackerStatus = CurrentTurnOwner == PlayerType.Player ? playerStatus : enemyStatus;
            try
            {
                await DiseaseTurnEndProcessor.ProcessForAttackerAsync(attackerStatus, phaseToken);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BattleManager] DiseaseTurnEndProcessor: キャンセル（TurnEnd 続行を試みます）");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (CurrentState != GameState.TurnEnd) return;

            // ガルーダ：5n ターン終了時はメッセージ → インターバル → 裏向きドロー → 表向け（Refill より前）
            try
            {
                await SummonGarudaLifecycle.ProcessTurnEndBonusAsync(this, _summonTurnCounters, phaseToken);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BattleManager] SummonGarudaLifecycle: キャンセル");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (CurrentState != GameState.TurnEnd) return;

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

            if (CurrentState != GameState.TurnEnd) return;

            // 経済アクション後のドロー処理
            await ProcessEconomicActionDrawAsync();

            if (CurrentState != GameState.TurnEnd) return;

            // 裏向きカードを表向きにする処理
            await RevealFaceDownCardsAsync();

            if (CurrentState != GameState.TurnEnd) return;

            // 手札枚数が正しく更新された後にステータスを更新
            BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);

            // ターン切り替えのインターバル中はグレーアウトしない（全てのカードを表示）
            // グレーアウト状態の更新は次のターン開始時にEnterAttackSelectで行う
            BattleUIManager.I?.SetIntroModeUI(playerHand);

            // 相手の攻撃ターン前のインターバル
            await Task.Delay(500);

            if (CurrentState != GameState.TurnEnd) return;

            // 2ターン目以降はグレーアウトを有効にする
            shouldGrayOutCards = true;

            ToggleTurnOwner();
            SetGameState(GameState.TurnStart);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            TryRecoverTurnEndToTurnStart();
        }
    }

    /// <summary>
    /// TurnEnd 処理が例外や中断で TurnStart に進めなかったときの保険。
    /// </summary>
    private void TryRecoverTurnEndToTurnStart()
    {
        if (CurrentState != GameState.TurnEnd) return;

        Debug.LogWarning("[BattleManager] TurnEnd から復帰できなかったため TurnStart に移行します");
        shouldGrayOutCards = true;
        ToggleTurnOwner();
        SetGameState(GameState.TurnStart);
    }

    private async Task RunEnemyTurnAsync()
    {
        // EnemyAIで攻撃ターンを実行（enemyStatusを渡してMP消費・魔法判定を行う）
        var attack = await enemyAI.ExecuteAttackTurnAsync(cpuHand, battleProcessor, handRefill, enemyStatus);
        
        if (attack == null)
        {
            SetGameState(GameState.TurnEnd);
            return;
        }

        currentAttackCard = attack;

        // 敵の攻撃カードを表示
        BattleUIManager.I?.ShowCardDetail(attack, Side.Enemy);
        
        // 相手のカード決定時の効果音
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        Debug.Log($"[BattleManager] 相手のカード決定: {attack.cardName}");

        // ステータスUI更新（MP消費の反映）
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);

        // 敵のTotalATKDEF表示を更新
        cardStatsDisplay.UpdateDisplay();

        await Task.Delay(1000);

        var atkList = new List<CardData> { attack };
        var primary = HitRateRules.GetPrimaryForHitRate(atkList);
        int finalPct = HitRateRules.ComputeFinalHitPercent(primary, enemyStatus, playerStatus);
        bool rolledHit = HitRateRules.RollHit(finalPct);
        if (!rolledHit)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ニュッ1.mp3");
            BattleUIManager.I?.ShowMissPopup(playerStatus);
            await Task.Delay(TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown));
            await Task.Delay(DamagePopup.PostPopupIntervalMs);
            BattleUIManager.I?.HideAllCardDetails();
            currentAttackCard = null;
            SetGameState(GameState.TurnEnd);
            return;
        }

        if (finalPct < 100)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/小パンチ.mp3");
            float popupSec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowCombatHitConfirmedPopup(playerStatus)
                : DamagePopup.DefaultFadeDurationIfUnknown;
            await Task.Delay(TimeSpan.FromSeconds(popupSec));
            await Task.Delay(DamagePopup.PostPopupIntervalMs);
        }

        SetGameState(GameState.DefenseSelect);
    }

    public void SetSelectedCard(CardUI ui)
    {
        if (ui == null) return;
        var card = ui.GetCardData();
        if (card == null) return;

        if (CurrentState == GameState.AttackSelect && Attacker == PlayerType.Player)
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
            UpdateTotalATKDEFDisplay();
            // 防御フェーズのボタンラベルを更新
            BattleUIManager.I?.UpdateDefenseButtonLabel();
            return;
        }

        if (CurrentState == GameState.TurnEnd && IsInterventionDefenseWaitActive() && Defender == PlayerType.Player)
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
            BattleUIManager.I?.UpdateDefenseButtonLabel();
            return;
        }

        if (CurrentState != GameState.AttackSelect && CurrentState != GameState.DefenseSelect)
        {
            Debug.Log($"カード選択は現在できません - State: {CurrentState}, Attacker: {Attacker}, Defender: {Defender}, Card: {card?.cardName}");
        }
    }

    public void OnUseButtonPressed()
    {
        if (isProcessingUseButton) return;
        isProcessingUseButton = true;
        BattleUIManager.I?.SetUseButtonInteractable(false);
        BattleUIManager.I?.SetHandClickable(false);

        switch (CurrentState)
        {
            case GameState.AttackSelect:
                if (Attacker == PlayerType.Player)
                    HandleAttackUse();
                else
                    isProcessingUseButton = false;
                break;

            case GameState.DefenseSelect:
                if (Defender == PlayerType.Player)
                    HandleDefenseUse();
                else
                    isProcessingUseButton = false;
                break;

            case GameState.TurnEnd:
                if (IsInterventionDefenseWaitActive())
                    TrySubmitInterventionPlayerDefense();
                else
                {
                    isProcessingUseButton = false;
                    BattleUIManager.I?.SetUseButtonInteractable(false);
                }
                break;

            default:
                isProcessingUseButton = false;
                break;
        }
    }

    private async Task ResolveImmediateEffectAsync(CardData card, int slotIndex)
    {
        // カード表示後、ポップアップ表示前に0.5秒のインターバル
        await Task.Delay(500);
        Debug.Log("[BattleManager] 回復カード表示後、0.5秒インターバル完了");

        // RecordPlayerUseSlotは既にHandleAttackUseで呼ばれている（UseCardの前）
        // ここでは呼ばない（二重呼び出しを防ぐ）
        
        await battleProcessor.ResolveImmediateEffectAsync(card, playerStatus, enemyStatus);

        selectedCard = null;
        BattleUIManager.I?.HideAllCardDetails();
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        UpdateTotalATKDEFDisplay();

        // ポップアップ表示後、ターン終了前に0.5秒のインターバル
        await Task.Delay(500);
        Debug.Log("[BattleManager] 回復ポップアップ表示後、0.5秒インターバル完了");

        // 回復カード（即時効果）の場合は防御フェーズをスキップして直接ターン終了
        SetGameState(GameState.TurnEnd);
    }

    /// <summary>
    /// 攻撃フェーズで単体の即時カードを処理する。魔法のときは MP 消費・手札→MagicPanel 演出・MagicPool 登録を
    /// <see cref="CardSequenceManager"/> 経由と揃える。
    /// </summary>
    private async Task RunImmediateAttackSingleCardAsync(CardData card, int slotIndex)
    {
        bool isMagic = card != null && card.cardType == CardType.Magic;

        if (isMagic && MagicPoolManager.I != null)
        {
            if (playerStatus != null && card.mpCost > 0)
            {
                int pay = playerStatus.GetEffectiveMagicMpCost(card.mpCost);
                playerStatus.UseMP(pay);
                BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
            }

            RectTransform handRt = card.cardUI != null ? card.cardUI.transform as RectTransform : null;
            if (handRt != null && BattleUIManager.I != null && card.cardImage != null)
            {
                int slot = MagicPoolManager.I.GetPredictedPlayerSlotIndex(card);
                await BattleUIManager.I.PlayMagicFlyHandToPanelAsync(card, handRt, slot);
            }

            battleProcessor.UseCard(card, playerHand);

            System.Action drawCb = () => DrawOneCard();
            MagicPoolManager.I.TryUseMagicCard(card, playerHand, GetHandMaxCount(), drawCb);
        }
        else
        {
            battleProcessor.UseCard(card, playerHand);
        }

        BattleUIManager.I?.ShowCardDetail(card, Side.Player);

        selectedCard = null;
        BattleUIManager.I?.ClearAllSelections();
        UpdateTotalATKDEFDisplay();

        await ResolveImmediateEffectAsync(card, slotIndex);
    }

    private void HandleAttackUse()
    {
        var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
        if (selectedAttackCards == null || selectedAttackCards.Count == 0)
        {
            Debug.LogWarning("攻撃カードが選択されていません");
            isProcessingUseButton = false;
            BattleUIManager.I?.SetUseButtonInteractable(false);
            BattleUIManager.I?.SetHandClickable(true);
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

        int totalMagicMp = playerStatus.GetTotalEffectiveMagicMpForCards(selectedAttackCards);
        if (totalMagicMp > playerStatus.currentMP)
        {
            isProcessingUseButton = false;
            BattleUIManager.I?.ShowInfoPopupOnCardPanel("MPが足りません", new Color(0.95f, 0.25f, 0.2f));
            BattleUIManager.I?.SetHandClickable(true);
            UpdateTotalATKDEFDisplay();
            return;
        }

        // 即時効果（回復・OnCardEffectResolve の状態異常など）の場合は通常処理
        // ※魔法カードはここでも MagicPool へ登録する（従来は即時分岐のみだと CardSequenceManager を経由せずプールに載らなかった）
        if (selectedAttackCards.Count == 1 && CardRules.IsImmediateAction(selectedAttackCards[0]))
        {
            var card = selectedAttackCards[0];
            int slotIndex = (card.cardUI != null) ? card.cardUI.transform.GetSiblingIndex() : -1;

            if (slotIndex >= 0) handRefill?.RecordPlayerUseSlot(slotIndex);

            _ = RunImmediateAttackSingleCardAsync(card, slotIndex);
            return;
        }

        // 攻撃カードの演出フローをCardSequenceManagerに委譲
        if (cardSequenceManager != null)
        {
            _ = cardSequenceManager.StartCardSequenceAsync(selectedAttackCards, "攻撃", Side.Player, _phaseCts.Token);
        }
        else
        {
            Debug.LogError("[BattleManager] CardSequenceManagerが設定されていません");
        }
    }

    private void HandleDefenseUse()
    {
        var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
        if (selectedDefenseCards == null || selectedDefenseCards.Count == 0)
        {
            if (_reflectionChainDefenseTcs != null && !_reflectionChainDefenseTcs.Task.IsCompleted)
            {
                _reflectionChainDefenseTcs.TrySetResult(new List<CardData>());
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
            if (Defender == PlayerType.Player
                && playerStatus != null
                && playerStatus.HasRestraintEffect()
                && selectedDefenseCards.Count > 1)
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("体が重い", new Color(0.22f, 0.24f, 0.38f));
                return;
            }

            _reflectionChainDefenseTcs.TrySetResult(new List<CardData>(selectedDefenseCards));
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

        // 防御カードの演出フローをCardSequenceManagerに委譲
        if (cardSequenceManager != null)
        {
            _ = cardSequenceManager.StartCardSequenceAsync(selectedDefenseCards, "防御", Side.Player, _phaseCts.Token);
        }
        else
        {
            Debug.LogError("[BattleManager] CardSequenceManagerが設定されていません");
        }
    }

    /// <summary>
    /// 防御カードを1枚も使わない場合の処理（「許す」）
    /// </summary>
    private async void HandleNoDefenseCard()
    {
        // キャンセルトークンを先にキャプチャ（await 後に _phaseCts が Dispose される可能性があるため）
        var token = _phaseCts.Token;

        // 選択状態をクリア
        BattleUIManager.I?.ClearAllSelections();
        UpdateTotalATKDEFDisplay();

        // 戦闘解決処理（防御カードなし）
        var atk = (Attacker == PlayerType.Player) ? playerStatus : enemyStatus;
        var def = (Defender == PlayerType.Player) ? playerStatus : enemyStatus;
        var defHand = (Defender == PlayerType.Player) ? playerHand : cpuHand;

        List<CardData> attackCards = GetAttackCardsForCombat();

        // 防御カードなしで戦闘解決（敵の攻撃は RunEnemyTurnAsync で命中済み）
        bool skipHit = Attacker == PlayerType.Enemy;
        await battleProcessor.ResolveCombatAsync(attackCards, (CardData)null, atk, def, defHand, skipHit);

        if (token.IsCancellationRequested) return;

        // ダメージ処理完了後、全カード表示をクリア
        BattleUIManager.I?.HideAllCardDetails();

        // 防御カード確定後の処理
        SetGameState(GameState.TurnEnd);
    }


    /// <summary>
    /// 戦闘用攻撃カードを取得（RunDefenseConfirmAsync、HandleNoDefenseCardから使用）
    /// </summary>
    private List<CardData> GetAttackCardsForCombat()
    {
        if (Attacker == PlayerType.Player)
        {
            Debug.Log("[BattleManager] プレイヤーの攻撃カードを取得中...");
            
            var uiAttackCards = BattleUIManager.I?.GetSelectedAttackCards() ?? new List<CardData>();
            if (uiAttackCards.Count == 0 && currentAttackCard != null)
            {
                uiAttackCards = new List<CardData> { currentAttackCard };
            }
            return uiAttackCards;
        }
        else
        {
            Debug.Log($"[BattleManager] 敵の攻撃カード: {currentAttackCard?.cardName ?? "なし"}");
            return new List<CardData> { currentAttackCard };
        }
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
        BattleUIManager.I?.RefreshUseButtonForMpAndSelection();
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
    /// 経済アクション後のドロー処理（TurnEndフェーズで実行）
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
                    CardDealAudio.Play(data);
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

    private void OnDestroy()
    {
        _phaseCts?.Cancel();
        _phaseCts?.Dispose();
    }
}
