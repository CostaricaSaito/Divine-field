using System.Collections.Generic;
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

        if (battleManager.CurrentState != GameState.AttackSelect)
        {
            Debug.LogWarning("[BuyFeature] 攻撃フェーズ以外では買うアクションは使用できません");
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

        targetBuyCard = cpuHand[Random.Range(0, cpuHand.Count)];
        Debug.Log($"[BuyFeature] 購入対象カード: {targetBuyCard.cardName} (価値: {targetBuyCard.cardValue})");

        // 0.5秒インターバル（承諾後の待機）
        await Task.Delay(500);

        // 相手のカード表示ゾーンに表示
        BattleUIManager.I?.ShowCardDetail(targetBuyCard, Side.Enemy);

        // 経済アクション用のダミー攻撃カードを設定
        var dummyCard = new CardData();
        dummyCard.cardName = "経済アクション";
        dummyCard.cardType = CardType.Attack;
        battleManager.SetCurrentAttackCard(dummyCard);

        // クールダウンを設定
        EconomicAction.I.SetBuyCooldown();

        // 防御フェーズに移行（跳ね返し対応）
        battleManager.SetGameState(GameState.DefenseSelect);

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
            return;
        }

        int cost = targetBuyCard.cardValue;
        Debug.Log($"[BuyFeature] 経済アクション処理開始 - コスト: {cost}GP");

        // 支払い処理
        ProcessPayment(cost);

        // 購入アニメーション実行
        if (cardPurchaseAnimation != null && BattleUIManager.I != null)
        {
            await cardPurchaseAnimation.PlayPurchaseAnimation(
                targetBuyCard,
                cost,
                BattleUIManager.I.GetEnemyCardDisplayPanel(),
                BattleUIManager.I.GetPlayerCardDisplayPanel()
            );
        }

        // カード取得処理（裏向きのまま手札に追加）
        ProcessCardAcquisition();

        // ステータス更新
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);

        Debug.Log("[BuyFeature] 購入処理完了");

        // 処理完了後、targetBuyCardをクリア
        targetBuyCard = null;
    }

    /// <summary>
    /// 支払い処理（GP → MP → HPの順）
    /// </summary>
    private void ProcessPayment(int cost)
    {
        int remainingCost = cost;
        Debug.Log($"[BuyFeature] 支払い開始 - 必要額: {remainingCost}");

        // GPから支払い
        if (remainingCost > 0 && playerStatus.currentGP > 0)
        {
            int gpPayment = Mathf.Min(remainingCost, playerStatus.currentGP);
            playerStatus.currentGP -= gpPayment;
            remainingCost -= gpPayment;
            Debug.Log($"[BuyFeature] GP支払い: {gpPayment} (残りGP: {playerStatus.currentGP}, 残り必要額: {remainingCost})");
        }

        // MPから支払い
        if (remainingCost > 0 && playerStatus.currentMP > 0)
        {
            int mpPayment = Mathf.Min(remainingCost, playerStatus.currentMP);
            playerStatus.currentMP -= mpPayment;
            remainingCost -= mpPayment;
            Debug.Log($"[BuyFeature] MP支払い: {mpPayment} (残りMP: {playerStatus.currentMP}, 残り必要額: {remainingCost})");
        }

        // HPから支払い（HPは0未満にならない）
        if (remainingCost > 0 && playerStatus.currentHP > 0)
        {
            int hpPayment = Mathf.Min(remainingCost, playerStatus.currentHP);
            playerStatus.currentHP -= hpPayment;
            remainingCost -= hpPayment;
            Debug.Log($"[BuyFeature] HP支払い: {hpPayment} (残りHP: {playerStatus.currentHP}, 残り必要額: {remainingCost})");
        }

        // 相手にGPを支払う
        enemyStatus.currentGP += cost;
        Debug.Log($"[BuyFeature] 相手にGP支払い: {cost} (相手のGP: {enemyStatus.currentGP})");

        // 購入・売却ではGP回復のポップアップは表示しない
    }

    /// <summary>
    /// カード取得処理（裏向きのまま手札に追加して表向きにする）
    /// </summary>
    private void ProcessCardAcquisition()
    {
        if (targetBuyCard == null) return;

        // 相手の手札から削除
        if (cpuHand != null && cpuHand.Contains(targetBuyCard))
        {
            cpuHand.Remove(targetBuyCard);
            Debug.Log($"[BuyFeature] 相手の手札から削除: {targetBuyCard.cardName}");
        }

        // 自分の手札に追加
        if (playerHand != null)
        {
            playerHand.Add(targetBuyCard);
            Debug.Log($"[BuyFeature] 自分の手札に追加: {targetBuyCard.cardName}");
        }

        // カードUIを生成（裏向きのまま）
        if (cardDealer != null)
        {
            var ui = cardDealer.CreateCardUIForHand(targetBuyCard);
            if (ui != null)
            {
                // 即座に表向きにする
                ui.Reveal();
                
                // 効果音を再生
                SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            }
        }
    }

    /// <summary>
    /// 購入対象カードを取得（防御フェーズでの使用）
    /// </summary>
    public CardData GetTargetBuyCard()
    {
        return targetBuyCard;
    }
}

