using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    public static BattleManager I;

    [Header("初期アサイン")]
    public BattleStatusUI statusUI;
    public CutInController cutInController;

    [Header("効果音(従来)")]
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

    [Header("UI/操作")]
    public SummonSkillButton summonSkillButton;

    // --- 追加: 分割した責務 ---
    [SerializeField] private HandRefillService handRefill; // シーンに配置して参照
    private EnemyAI enemyAI = new EnemyAI();

    // ランタイム
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
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    void Start()
    {
        // ステータス
        playerStatus = new PlayerStatus();
        enemyStatus = new PlayerStatus();
        playerStatus.InitializeAsPlayer();
        enemyStatus.InitializeAsEnemy();

        // 召喚獣（省略：既存ロジックそのまま）
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

        // 依存初期化
        cardDealer.Initialize(playerStatus, enemyStatus, handPanel, cardUIPrefab, cardBackSprite,
                              audioSource, cardDealSE, cardRevealSE);
        battleProcessor.Initialize(playerStatus, enemyStatus, statusUI, this, cardDealer);

        if (handRefill != null)
            handRefill.Initialize(handPanel, cardUIPrefab, cardBackSprite, audioSource, cardDealSE, cardDealer);

        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        StartCoroutine(BattleStartSequence());
    }

    //================ 状態遷移 ================
    public void SetGameState(GameState newState)
    {
        if (CurrentState == newState) { Debug.Log($"[State] noop {newState}"); return; }

        _phaseCts?.Cancel(); _phaseCts?.Dispose();
        _phaseCts = new CancellationTokenSource();

        Debug.Log($"【State】{CurrentState} → {newState}（Turn: {CurrentTurnOwner}）");
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

    //================ フェーズ ================
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
        SetGameState(GameState.TurnStart);
    }

    private void OnTurnStart()
    {
        if (CurrentTurnOwner == PlayerType.Player)
            SoundEffectPlayer.I?.Play("SE/決定ボタンを押す13");

        if (CurrentTurnOwner == PlayerType.Player) playerStatus.OnTurnStart();
        else enemyStatus.OnTurnStart();

        // ★ ターン開始時にカード詳細表示をクリア
        BattleUIManager.I?.HideAllCardDetails();

        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        SetGameState(GameState.AttackSelect);
    }

    private void EnterAttackSelect()
    {
        var attackables = CardRules.GetAttackChoices(playerHand);
        if (Attacker == PlayerType.Player &&
            (attackables.Count == 0 || attackables.TrueForAll(c => c.cardType == CardType.Magic)))
        {
            BattleUIManager.I?.SetPrayModeUI(playerHand);
        }
        else
        {
            BattleUIManager.I?.SetUseButtonLabel("使用");
            BattleUIManager.I?.RefreshAttackInteractivity(playerHand);
        }

        if (CurrentTurnOwner == PlayerType.Enemy)
            _ = RunEnemyTurnAsync();
    }

    private async Task RunDefenseSelectAsync()
    {
        var ct = _phaseCts?.Token ?? default;

        if (Defender == PlayerType.Enemy)
        {
            // 敵が防御（プレイヤー攻撃）
            selectedDefenseCard = enemyAI.SelectDefenseCard(cpuHand);

            try { await Task.Delay(1000, ct); } catch { return; }
            if (!ct.IsCancellationRequested) SetGameState(GameState.DefenseConfirm);
        }
        else
        {
            // プレイヤー防御
            BattleUIManager.I?.SetUseButtonLabel("許す");
            BattleUIManager.I?.RefreshDefenseInteractivity(CardRules.GetDefenseChoices(playerHand));

            defenseCardTCS = new TaskCompletionSource<CardData>();
            using (ct.Register(() => defenseCardTCS.TrySetCanceled())) { }

            try { selectedDefenseCard = await defenseCardTCS.Task; } catch { return; }
            if (!ct.IsCancellationRequested) SetGameState(GameState.DefenseConfirm);
        }
    }

    private async Task RunDefenseConfirmAsync()
    {
        if (currentAttackCard == null)
        {
            Debug.LogWarning("攻撃カード未設定のためスキップ");
            SetGameState(GameState.AttackSelect);
            return;
        }

        // プレイヤーが防御でカードを使った場合：裏スロット作成
        if (Defender == PlayerType.Player && selectedDefenseCard?.cardUI != null)
        {
            int idx = selectedDefenseCard.cardUI.transform.GetSiblingIndex();
            handRefill?.RecordPlayerUseSlot(idx);
        }
        // 敵が防御でカードを使った場合：枚数カウント
        if (Defender == PlayerType.Enemy && selectedDefenseCard != null)
        {
            handRefill?.RecordEnemyUse();
        }

        // ★ 防御カードはこのタイミングで表示
        if (selectedDefenseCard != null)
        {
            BattleUIManager.I?.ShowCardDetail(selectedDefenseCard,
                (Defender == PlayerType.Player) ? Side.Player : Side.Enemy);
        }

        var atk = (Attacker == PlayerType.Player) ? playerStatus : enemyStatus;
        var def = (Attacker == PlayerType.Player) ? enemyStatus : playerStatus;
        var defHand = (Attacker == PlayerType.Player) ? cpuHand : playerHand;

        await battleProcessor.ResolveCombatAsync(currentAttackCard, selectedDefenseCard, atk, def, defHand);

        // ★ 解決後はクリア
        BattleUIManager.I?.HideAllCardDetails();

        currentAttackCard = null;
        selectedDefenseCard = null;
        defenseCardTCS = null;

        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);

        if (IsDead(playerStatus) || IsDead(enemyStatus))
        {
            SetGameState(GameState.BattleEnd);
            return;
        }

        SetGameState(GameState.TurnEnd);
    }

    private async Task RunTurnEndAsync()
    {
        var ct = _phaseCts?.Token ?? default;

        if (handRefill != null)
            await handRefill.RefillAtTurnEndAsync(playerHand, cpuHand, ct);

        if (ct.IsCancellationRequested) return;

        // ★ ターン跨ぎ前にクリア
        BattleUIManager.I?.HideAllCardDetails();

        ToggleTurnOwner();
        SetGameState(GameState.TurnStart);
    }

    //================ 敵ターン ================
    private async Task RunEnemyTurnAsync()
    {
        var ct = _phaseCts?.Token ?? default;

        var attack = enemyAI.SelectAttackCard(cpuHand);
        if (attack == null)
        {
            SetGameState(GameState.TurnEnd);
            return;
        }

        battleProcessor.UseCard(attack, cpuHand);
        handRefill?.RecordEnemyUse();

        currentAttackCard = attack;

        // ★ 敵の攻撃カードを表示
        BattleUIManager.I?.ShowCardDetail(attack, Side.Enemy);

        if (!ct.IsCancellationRequested)
            SetGameState(GameState.DefenseSelect);
    }

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
            BattleUIManager.I?.SetUseButtonLabel("使用");
            return;
        }

        Debug.Log("カード選択は現在できません");
    }

    public void OnUseButtonPressed()
    {
        switch (CurrentState)
        {
            case GameState.AttackSelect:
                _ = HandleAttackConfirmAsync();
                break;

            case GameState.DefenseSelect:
                if (Defender == PlayerType.Player && defenseCardTCS != null && !defenseCardTCS.Task.IsCompleted)
                    defenseCardTCS.TrySetResult(selectedDefenseCard); // null=許す
                break;
        }
    }

    private async Task HandleAttackConfirmAsync()
    {
        if (CurrentState != GameState.AttackSelect || Attacker != PlayerType.Player) return;
        if (selectedCard == null) { Debug.LogWarning("攻撃カードが選択されていません"); return; }

        var card = selectedCard;

        // 即時（回復など）
        if (CardRules.IsImmediateAction(card))
        {
            int idx = (card.cardUI != null) ? card.cardUI.transform.GetSiblingIndex() : -1;

            battleProcessor.UseCard(card, playerHand);

            BattleUIManager.I?.ShowCardDetail(card, Side.Player);

            await battleProcessor.ResolveImmediateEffectAsync(card, playerStatus, enemyStatus);

            if (idx >= 0) handRefill?.RecordPlayerUseSlot(idx);

            selectedCard = null;

            BattleUIManager.I?.HideAllCardDetails();
            BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);

            SetGameState(GameState.TurnEnd);
            return;
        }

        // 通常攻撃
        int slot = (card.cardUI != null) ? card.cardUI.transform.GetSiblingIndex() : -1;

        currentAttackCard = card;
        battleProcessor.UseCard(card, playerHand);

        if (slot >= 0) handRefill?.RecordPlayerUseSlot(slot);

        BattleUIManager.I?.ShowCardDetail(card, Side.Player);

        selectedCard = null;

        SetGameState(GameState.AttackConfirm);
    }

    //================ ヘルパ ================
    public void ToggleTurnOwner()
    {
        CurrentTurnOwner = (CurrentTurnOwner == PlayerType.Player) ? PlayerType.Enemy : PlayerType.Player;
    }

    private SummonData GetRandomEnemySummon()
    {
        var list = SummonSelectionManager.I.GetAllSummonData();
        var enemyCandidates = new List<SummonData>(list);
        enemyCandidates.RemoveAt(SummonSelectionManager.I.SelectedIndex);
        return enemyCandidates[UnityEngine.Random.Range(0, enemyCandidates.Count)];
    }

    private bool IsDead(PlayerStatus s) => s != null && s.currentHP <= 0;
}
