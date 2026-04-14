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

    // グレーアウト制御フラグ
    private bool shouldGrayOutCards = false;

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
    public CardPurchaseAnimation cardPurchaseAnimation;
    [SerializeField] private CardSellAnimation cardSellAnimation;
    
    [SerializeField] private HandRefillService handRefill;
    [SerializeField] private CardStatsDisplay cardStatsDisplay;
    [SerializeField] private CardSequenceManager cardSequenceManager;
    [SerializeField] private MagicPoolManager magicPoolManager;
    private EnemyAI enemyAI = new EnemyAI();
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
        yield return StartCoroutine(cardDealer.DealCards(playerHand, cpuHand, 10));

        // 手札が配られた後にステータスを更新
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);

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
        // 攻撃カード確定後のインターバルと効果音
        await Task.Delay(1000);
        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す13.mp3");
        Debug.Log("[BattleManager] 攻撃カード確定、防御カード選択開始");

        if (Defender == PlayerType.Enemy)
        {
            // EnemyAIで防御選択を実行
            selectedDefenseCard = await enemyAI.ExecuteDefenseSelectAsync(cpuHand);
            
            cardStatsDisplay?.UpdateDisplay();
            
            SetGameState(GameState.DefenseConfirm);
        }
        else
        {
            BattleUIManager.I?.HidePlayerCardDetails();
            BattleUIManager.I?.SetHandClickable(true);
            BattleUIManager.I?.SetUseButtonLabel("許す");

            var attackElement = ElementHelper.GetCombinedElement(GetAttackCardsForCombat());
            var defenseChoices = CardRules.GetDefenseChoicesForElement(playerHand, attackElement);
            BattleUIManager.I?.RefreshDefenseInteractivity(playerHand, defenseChoices);

            BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
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

        // 経済アクションの場合は特別処理
        if (CurrentState == GameState.DefenseConfirm && currentAttackCard != null)
        {
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
            else if (currentAttackCard.cardName == "経済アクション（売却）")
            {
                Debug.Log("[BattleManager] 経済アクション（売却）の防御フェーズ処理");
                await sellFeature.ProcessEconomicActionAsync();
                currentAttackCard = null;
                selectedDefenseCard = null;
                UpdateTotalATKDEFDisplay();
                SetGameState(GameState.TurnEnd);
                return;
            }
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

        await battleProcessor.ResolveCombatAsync(attackCards, selectedDefenseCard, atk, def, defHand);

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

    private async Task RunTurnEndAsync()
    {
        // 攻撃フェーズ終了直後：攻撃側の病系処理（補充・ドローより先）
        PlayerStatus attackerStatus = CurrentTurnOwner == PlayerType.Player ? playerStatus : enemyStatus;
        try
        {
            await DiseaseTurnEndProcessor.ProcessForAttackerAsync(attackerStatus, _phaseCts.Token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        if (_phaseCts.Token.IsCancellationRequested) return;

        if (handRefill != null)
        {
            await handRefill.RefillAtTurnEndAsync(playerHand, cpuHand, _phaseCts.Token);
        }

        if (_phaseCts.Token.IsCancellationRequested) return;

        // 経済アクション後のドロー処理
        await ProcessEconomicActionDrawAsync();

        // 裏向きカードを表向きにする処理
        await RevealFaceDownCardsAsync();

        // 手札枚数が正しく更新された後にステータスを更新
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);

        // ターン切り替えのインターバル中はグレーアウトしない（全てのカードを表示）
        // グレーアウト状態の更新は次のターン開始時にEnterAttackSelectで行う
        BattleUIManager.I?.SetIntroModeUI(playerHand);

        // 相手の攻撃ターン前のインターバル
        await Task.Delay(500);

        // 2ターン目以降はグレーアウトを有効にする
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

        // 即時効果（回復など）の場合は通常処理
        if (selectedAttackCards.Count == 1 && CardRules.IsImmediateAction(selectedAttackCards[0]))
        {
            var card = selectedAttackCards[0];
            int slotIndex = (card.cardUI != null) ? card.cardUI.transform.GetSiblingIndex() : -1;
            
            // RecordPlayerUseSlotはUseCardの前に呼ぶ必要がある（UseCardでcardDataがnullになるため）
            if (slotIndex >= 0) handRefill?.RecordPlayerUseSlot(slotIndex);
            
            battleProcessor.UseCard(card, playerHand);
            BattleUIManager.I?.ShowCardDetail(card, Side.Player);
            
            // 選択状態をクリア
            selectedCard = null;
            BattleUIManager.I?.ClearAllSelections();
            UpdateTotalATKDEFDisplay();
            
            _ = ResolveImmediateEffectAsync(card, slotIndex);
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
            // 防御カードを1枚も使わない場合（「許す」）
            Debug.Log("[BattleManager] 防御カードを使用せずにダメージを受ける（許す）");
            HandleNoDefenseCard();
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

        // 防御カードなしで戦闘解決
        await battleProcessor.ResolveCombatAsync(attackCards, (CardData)null, atk, def, defHand);

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
    /// 手札の上限枚数を返す（MagicPoolManager で手札追加可否の判定に使用）
    /// </summary>
    public int GetHandMaxCount()
    {
        // handPanel の子オブジェクト数 = スロット数（構造上の最大手札杖数）
        if (handPanel != null) return handPanel.childCount;
        return 10; // デフォルト値
    }

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
        for (int i = 0; i < handPanel.childCount; i++)
        {
            var child = handPanel.GetChild(i);
            var cardUI = child.GetComponent<CardUI>();
            
            if (cardUI != null && cardUI.IsFaceDown())
            {
                cardUI.Reveal();
                
                // 効果音を再生（Addressables使用）
                SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
                
                // カードごとに短い間隔を空ける
                await Task.Delay(300);
            }
        }
    }

    private void OnDestroy()
    {
        _phaseCts?.Cancel();
        _phaseCts?.Dispose();
    }
}
