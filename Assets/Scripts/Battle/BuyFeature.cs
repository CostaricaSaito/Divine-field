using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 「買う」機能を管理するクラス
/// BattleManagerから購入関連の処理を移設
/// </summary>
public class BuyFeature
{
    private BattleManager battleManager;
    private PlayerStatus playerStatus;
    private PlayerStatus enemyStatus;
    private List<CardData> playerHand;
    private List<CardData> cpuHand;
    private CardDealer cardDealer;
    private CardPurchaseAnimation cardPurchaseAnimation;

    private CardData targetBuyCard; // 購入対象カード
    private bool isBuyModeActive = false;
    private bool isProcessingBuy = false;

    /// <summary>
    /// 初期化
    /// </summary>
    public void Initialize(
        BattleManager battleManager,
        PlayerStatus playerStatus,
        PlayerStatus enemyStatus,
        List<CardData> playerHand,
        List<CardData> cpuHand,
        CardDealer cardDealer,
        CardPurchaseAnimation cardPurchaseAnimation)
    {
        this.battleManager = battleManager;
        this.playerStatus = playerStatus;
        this.enemyStatus = enemyStatus;
        this.playerHand = playerHand;
        this.cpuHand = cpuHand;
        this.cardDealer = cardDealer;
        this.cardPurchaseAnimation = cardPurchaseAnimation;
    }

    /// <summary>
    /// 「買う」アクションを実行
    /// </summary>
    public async Task<bool> ExecuteBuyActionAsync()
    {
        if (battleManager == null)
        {
            Debug.LogWarning("[BuyFeature] BattleManagerがnullです");
            return false;
        }

        if (battleManager.CurrentState != GameState.AttackPhase
            || !battleManager.PlayerCanUseEconomicActions())
        {
            Debug.LogWarning("[BuyFeature] AttackSelect 中以外では買うアクションは使用できません");
            return false;
        }

        if (EconomicAction.I == null || !EconomicAction.I.CanBuy())
        {
            Debug.LogWarning("[BuyFeature] 買うアクションはクールダウン中です");
            return false;
        }

        Debug.Log("[BuyFeature] 買うアクション開始");

        // 相手の手札からランダムに1枚選択
        if (cpuHand == null || cpuHand.Count == 0)
        {
            Debug.LogWarning("[BuyFeature] 相手の手札が空のため、買うアクションは実行できません");
            return false;
        }

        targetBuyCard = PickBuyableCardFromEnemyHand();
        if (targetBuyCard == null)
        {
            Debug.LogWarning("[BuyFeature] 相手の手札に購入可能なカードがありません");
            return false;
        }
        Debug.Log($"[BuyFeature] 購入対象カード: {targetBuyCard.cardName} (価値: {targetBuyCard.cardValue})");

        isBuyModeActive = true;
        BattleManager.I?.ClearPlayerSelfAttackTargetMode();
        BattleManager.I?.UpdateTotalATKDEFDisplay();

        // 0.5秒インターバル（承諾後の待機）
        await Task.Delay(500);

        // 相手のカード表示ゾーンに表示
        BattleUIManager.I?.ShowCardDetail(targetBuyCard, Side.Enemy);

        // 経済アクション用のダミー攻撃カードを設定
        var dummyCard = EconomicActionNames.CreateBuyDummy();
        battleManager.SetCurrentAttackCard(dummyCard);

        // クールダウンを設定
        EconomicAction.I.SetBuyCooldown();
        // クールダウン設定後にUIを即座に更新
        BattleUIManager.I?.UpdateEconomicActionButtons();

        if (battleManager.IsOnlineMatch)
            NetworkBattleBridge.SendEconomicBuy(targetBuyCard.cardName);

        // 防御フェーズに移行（跳ね返し対応）
        battleManager.SetGameState(GameState.DefensePhase);

        return true;
    }

    /// <summary>オンライン：相手の購入をローカルにミラー（防御側の手札カードを指定）。</summary>
    public bool SetupMirroredBuy(CardData targetInDefenderHand)
    {
        if (targetInDefenderHand == null)
        {
            Debug.LogWarning("[BuyFeature] Mirrored buy target is null");
            return false;
        }

        targetBuyCard = targetInDefenderHand;
        isBuyModeActive = true;
        BattleUIManager.I?.ShowCardDetail(targetInDefenderHand, Side.Player);
        battleManager.SetCurrentAttackCard(EconomicActionNames.CreateBuyDummy());
        return true;
    }

    /// <summary>
    /// 経済アクションの処理（支払い、カード取得）
    /// </summary>
    public async Task ProcessEconomicActionAsync()
    {
        if (targetBuyCard == null)
        {
            Debug.LogWarning("[BuyFeature] 購入対象カードが設定されていません");
            ResetBuyProcessState();
            return;
        }

        isProcessingBuy = true;
        BattleManager.I?.UpdateTotalATKDEFDisplay();

        try
        {
            bool buyerIsPlayer = battleManager.AttackerPublic == PlayerType.Player;
            var buyer = buyerIsPlayer ? playerStatus : enemyStatus;
            var seller = buyerIsPlayer ? enemyStatus : playerStatus;
            var buyerHand = buyerIsPlayer ? playerHand : cpuHand;
            var sellerHand = buyerIsPlayer ? cpuHand : playerHand;

            int cost = targetBuyCard.cardValue;
            Debug.Log($"[BuyFeature] 経済アクション処理開始 - コスト: {cost}GP");

            ProcessPayment(cost, buyer, seller);

            if (cardPurchaseAnimation != null && BattleUIManager.I != null)
            {
                var fromPanel = buyerIsPlayer
                    ? BattleUIManager.I.GetEnemyCardDisplayPanel()
                    : BattleUIManager.I.GetPlayerCardDisplayPanel();
                var toPanel = buyerIsPlayer
                    ? BattleUIManager.I.GetPlayerCardDisplayPanel()
                    : BattleUIManager.I.GetEnemyCardDisplayPanel();
                await cardPurchaseAnimation.PlayPurchaseAnimation(targetBuyCard, cost, fromPanel, toPanel);
            }

            ProcessCardAcquisition(buyerHand, sellerHand, buyerIsPlayer);

            BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);

            Debug.Log("[BuyFeature] 購入処理完了");
        }
        finally
        {
            targetBuyCard = null;
            ResetBuyProcessState();
        }
    }

    /// <summary>
    /// 支払い処理（GP → MP → HPの順）
    /// </summary>
    private void ProcessPayment(int cost, PlayerStatus buyer, PlayerStatus seller)
    {
        if (buyer == null || seller == null) return;

        int remainingCost = cost;
        Debug.Log($"[BuyFeature] 支払い開始 - 必要額: {remainingCost}");

        if (remainingCost > 0 && buyer.currentGP > 0)
        {
            int gpPayment = Mathf.Min(remainingCost, buyer.currentGP);
            buyer.currentGP -= gpPayment;
            remainingCost -= gpPayment;
        }

        if (remainingCost > 0 && buyer.currentMP > 0)
        {
            int mpPayment = Mathf.Min(remainingCost, buyer.currentMP);
            buyer.currentMP -= mpPayment;
            remainingCost -= mpPayment;
        }

        if (remainingCost > 0 && buyer.currentHP > 0)
        {
            int hpPayment = Mathf.Min(remainingCost, buyer.currentHP);
            buyer.currentHP -= hpPayment;
            remainingCost -= hpPayment;
        }

        seller.currentGP += cost;
        Debug.Log($"[BuyFeature] 相手にGP支払い: {cost} (相手のGP: {seller.currentGP})");
    }

    /// <summary>
    /// カード取得処理（裏向きのまま手札に追加して表向きにする）
    /// </summary>
    private void ProcessCardAcquisition(List<CardData> buyerHand, List<CardData> sellerHand, bool buyerIsPlayer)
    {
        if (targetBuyCard == null) return;

        var released = targetBuyCard;
        if (sellerHand != null && sellerHand.Contains(released))
        {
            if (released.cardUI != null)
            {
                Object.Destroy(released.cardUI.gameObject);
                released.cardUI = null;
            }
            sellerHand.Remove(released);
            Debug.Log($"[BuyFeature] 相手の手札から削除: {released.cardName}");
        }

        if (cardDealer == null)
        {
            Debug.LogError("[BuyFeature] CardDealer が null のため購入を完了できません");
            return;
        }

        var acquired = cardDealer.InstantiateCardFromTemplate(released);
        if (acquired == null)
        {
            Debug.LogError("[BuyFeature] 購入カードの複製に失敗しました");
            return;
        }

        if (buyerHand != null)
        {
            buyerHand.Add(acquired);
            Debug.Log($"[BuyFeature] 購入者の手札に追加: {acquired.cardName}");
        }

        if (released is ScriptableObject soReleased)
            Object.Destroy(soReleased);

        var ui = cardDealer.CreateCardUIForHand(acquired);
        if (ui != null)
            _ = HandRevealPresentation.RevealCardAsync(acquired, ui, new HandRevealBatchContext(), CancellationToken.None);
    }

    public bool IsBuyProcessActive() => isBuyModeActive || isProcessingBuy;

    private void ResetBuyProcessState()
    {
        isProcessingBuy = false;
        isBuyModeActive = false;
        BattleManager.I?.UpdateTotalATKDEFDisplay();
    }

    /// <summary>
    /// 外部から買うアクションをキャンセルする（他の経済アクション開始時に使用）
    /// </summary>
    public void CancelBuy()
    {
        if (!IsBuyProcessActive()) return;
        Debug.Log("[BuyFeature] 外部からキャンセル");
        ResetBuyProcessState();
        targetBuyCard = null;
        // カード表示をリセット
        BattleUIManager.I?.HideAllCardDetails();
        // AttackSelect 状態に戻す
        if (battleManager != null && battleManager.CurrentState != GameState.AttackPhase)
        {
            battleManager.SetCurrentAttackCard(null);
            battleManager.SetGameState(GameState.AttackPhase);
        }
        BattleUIManager.I?.UpdateEconomicActionButtons();
    }

    /// <summary>
    /// 購入対象カードを取得（防御フェーズでの使用）
    /// </summary>
    public CardData GetTargetBuyCard()
    {
        return targetBuyCard;
    }

    private CardData PickBuyableCardFromEnemyHand()
    {
        if (cpuHand == null || cpuHand.Count == 0) return null;

        var candidates = new List<CardData>();
        for (int i = 0; i < cpuHand.Count; i++)
        {
            var c = cpuHand[i];
            if (c == null || c.cardType == CardType.Disaster || c.cardType == CardType.Ultimate) continue;
            candidates.Add(c);
        }

        if (candidates.Count == 0) return null;
        return candidates[BattleRandom.Range(0, candidates.Count)];
    }
}

