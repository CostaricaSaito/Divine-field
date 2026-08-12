using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「売る」機能を管理するクラス
/// </summary>
public class SellFeature
{
    private BattleManager battleManager;
    private PlayerStatus playerStatus;
    private PlayerStatus enemyStatus;
    private List<CardData> playerHand;
    private List<CardData> cpuHand;
    private CardDealer cardDealer;

    private GameObject sellConfirmPopupPrefab;
    private Canvas popupCanvas;
    private SellConfirmPopup currentPopup;
    private bool isSellModeActive = false;
    private bool isProcessingConfirm = false;
    private CardData targetSellCard;
    private CardSellAnimation cardSellAnimation;

    public void Initialize(
        BattleManager battleManager,
        PlayerStatus playerStatus,
        PlayerStatus enemyStatus,
        List<CardData> playerHand,
        List<CardData> cpuHand,
        CardDealer cardDealer,
        GameObject sellConfirmPopupPrefab,
        Canvas popupCanvas,
        CardSellAnimation cardSellAnimation = null,
        HandRefillService handRefillService = null)
    {
        this.battleManager = battleManager;
        this.playerStatus = playerStatus;
        this.enemyStatus = enemyStatus;
        this.playerHand = playerHand;
        this.cpuHand = cpuHand;
        this.cardDealer = cardDealer;
        this.sellConfirmPopupPrefab = sellConfirmPopupPrefab;
        this.popupCanvas = popupCanvas;
        this.cardSellAnimation = cardSellAnimation;
    }

    public async Task<bool> ExecuteSellActionAsync()
    {
        if (battleManager == null
            || battleManager.CurrentState != GameState.AttackPhase
            || !battleManager.PlayerCanUseEconomicActions())
        {
            return false;
        }

        if (EconomicAction.I == null || !EconomicAction.I.CanSell())
        {
            return false;
        }

        if (playerHand == null || playerHand.Count == 0)
        {
            return false;
        }

        ShowSellConfirmPopup();
        isSellModeActive = true;
        CardSelectionManager.I?.ClearAllSelections();
        BattleManager.I?.UpdateTotalATKDEFDisplay();

        // 売却モード時は全てのカードを有効化（グレーアウト解除）
        BattleUIManager.I?.SetIntroModeUI(playerHand);

        await Task.CompletedTask;
        return true;
    }

    public async Task ProcessEconomicActionAsync()
    {
        try
        {
            if (targetSellCard == null) return;

            BattleManager.I?.UpdateTotalATKDEFDisplay();

            bool sellerIsPlayer = battleManager.AttackerPublic == PlayerType.Player;
            var seller = sellerIsPlayer ? playerStatus : enemyStatus;
            var buyer = sellerIsPlayer ? enemyStatus : playerStatus;
            var sellerHand = sellerIsPlayer ? playerHand : cpuHand;
            var buyerHand = sellerIsPlayer ? cpuHand : playerHand;

            if (cardSellAnimation != null && BattleUIManager.I != null)
            {
                int sellAmount = targetSellCard.cardValue;
                GameObject cardSheetPrefab = BattleUIManager.I.GetCardSheetPrefab();
                var fromPanel = sellerIsPlayer
                    ? BattleUIManager.I.GetPlayerCardDisplayPanel()
                    : BattleUIManager.I.GetEnemyCardDisplayPanel();
                var toPanel = sellerIsPlayer
                    ? BattleUIManager.I.GetEnemyCardDisplayPanel()
                    : BattleUIManager.I.GetPlayerCardDisplayPanel();

                await cardSellAnimation.PlaySellAnimation(
                    targetSellCard,
                    sellAmount,
                    fromPanel,
                    toPanel,
                    cardSheetPrefab);
            }

            ProcessGPTheft(seller, buyer);
            await ProcessCardTransferAsync(sellerHand, buyerHand);
            UpdateHandUI();

            BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
            targetSellCard = null;
        }
        finally
        {
            isProcessingConfirm = false;
            BattleManager.I?.UpdateTotalATKDEFDisplay();
        }
    }

    /// <summary>防御確定フローに入れなかった売却など、異常系でフラグだけ残ったときに呼ぶ。</summary>
    public void ForceEndSellProcessingState()
    {
        isProcessingConfirm = false;
    }

    private void ProcessGPTheft(PlayerStatus seller, PlayerStatus buyer)
    {
        if (targetSellCard == null || seller == null || buyer == null) return;

        int remainingTheft = targetSellCard.cardValue;

        if (remainingTheft > 0 && buyer.currentGP > 0)
        {
            int gpTheft = Mathf.Min(remainingTheft, buyer.currentGP);
            buyer.currentGP -= gpTheft;
            seller.currentGP += gpTheft;
            remainingTheft -= gpTheft;
        }

        if (remainingTheft > 0 && buyer.currentMP > 0)
        {
            int mpTheft = Mathf.Min(remainingTheft, buyer.currentMP);
            buyer.currentMP -= mpTheft;
            seller.currentMP += mpTheft;
            remainingTheft -= mpTheft;
        }

        if (remainingTheft > 0 && buyer.currentHP > 0)
        {
            int hpTheft = Mathf.Min(remainingTheft, buyer.currentHP);
            buyer.currentHP -= hpTheft;
            seller.currentHP += hpTheft;
        }
    }

    private async Task ProcessCardTransferAsync(List<CardData> sellerHand, List<CardData> buyerHand)
    {
        if (targetSellCard == null) return;

        if (targetSellCard.cardUI != null)
        {
            var cardUIObject = targetSellCard.cardUI.gameObject;
            targetSellCard.cardUI = null;

            if (cardUIObject.transform.parent != null)
                cardUIObject.transform.SetParent(null);
            Object.Destroy(cardUIObject);
            await Task.Delay(10);
        }

        if (sellerHand != null && sellerHand.Contains(targetSellCard))
            sellerHand.Remove(targetSellCard);

        if (buyerHand != null)
        {
            buyerHand.Add(targetSellCard);
            cardDealer?.CreateCardUIForHand(targetSellCard);
        }

        CleanupHandPanel();
        await Task.CompletedTask;
    }

    /// <summary>オンライン：相手の売却をミラー（攻撃者手札のカードを指定）。</summary>
    public bool SetupMirroredSell(CardData soldCardInAttackerHand)
    {
        if (soldCardInAttackerHand == null)
        {
            Debug.LogWarning("[SellFeature] Mirrored sell card is null");
            return false;
        }

        targetSellCard = soldCardInAttackerHand;
        isProcessingConfirm = true;
        isSellModeActive = false;
        battleManager.SetCurrentAttackCard(EconomicActionNames.CreateSellDummy());
        return true;
    }

    private void CleanupHandPanel()
    {
        if (battleManager?.handPanel == null || playerHand == null) return;

        var handPanelSet = new HashSet<CardData>(playerHand);
        var toDestroy = new List<GameObject>();

        for (int i = battleManager.handPanel.childCount - 1; i >= 0; i--)
        {
            var child = battleManager.handPanel.GetChild(i);
            var childCardUI = child.GetComponent<CardUI>();
            if (childCardUI != null)
            {
                var childCardData = childCardUI.GetCardData();
                if (childCardData == null || !handPanelSet.Contains(childCardData))
                {
                    toDestroy.Add(child.gameObject);
                }
            }
        }

        foreach (var obj in toDestroy)
        {
            if (obj != null) Object.Destroy(obj);
        }

        if (battleManager.handPanel is RectTransform rectTransform)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }

    private void UpdateHandUI()
    {
        if (BattleUIManager.I == null || battleManager == null) return;

        var currentTurnOwner = battleManager.CurrentTurnOwner;
        var isPlayerAttacker = (currentTurnOwner == PlayerType.Player);
        var isPlayerDefender = (currentTurnOwner != PlayerType.Player);

        if (battleManager.CurrentState == GameState.AttackPhase && isPlayerAttacker)
        {
            BattleUIManager.I.RefreshAttackInteractivity(playerHand, CardRules.GetAttackChoices(playerHand));
        }
        else if (battleManager.CurrentState == GameState.DefensePhase && isPlayerDefender)
        {
            battleManager.RefreshPlayerDefensePhaseInteractivity();
        }
        else
        {
            BattleUIManager.I.SetIntroModeUI(playerHand);
        }
    }

    public void OnCardSelected(CardData card)
    {
        if (!isSellModeActive || card == null) return;
        if (playerHand == null || !playerHand.Contains(card)) return;

        targetSellCard = card;
        currentPopup?.SetSelectedCard(card);
        SoundEffectPlayer.I?.Play("Assets/SE/カーソル移動1.mp3");
        BattleManager.I?.UpdateTotalATKDEFDisplay();
    }

    private void ShowSellConfirmPopup()
    {
        if (sellConfirmPopupPrefab == null || popupCanvas == null)
        {
            Debug.LogError("[SellFeature] ポップアップのPrefabまたはCanvasがnullです");
            return;
        }

        BattleManager.I?.ClearPlayerSelfAttackTargetMode();

        // 既存のポップアップを破棄
        if (currentPopup != null)
        {
            Object.Destroy(currentPopup.gameObject);
            currentPopup = null;
        }

        // ポップアップを生成
        var popupObj = Object.Instantiate(sellConfirmPopupPrefab, popupCanvas.transform);
        currentPopup = popupObj.GetComponent<SellConfirmPopup>();

        if (currentPopup == null)
        {
            Debug.LogError("[SellFeature] SellConfirmPopupコンポーネントが見つかりません");
            Object.Destroy(popupObj);
            return;
        }

        popupObj.SetActive(true);
        var popupRect = popupObj.GetComponent<RectTransform>();
        if (popupRect != null)
        {
            popupRect.localScale = Vector3.one;
        }

        isProcessingConfirm = false;
        currentPopup.Setup(OnConfirmSell, OnCancelSell);
    }

    private void OnConfirmSell()
    {
        if (targetSellCard == null) return;

        EconomicAction.I?.SetSellCooldown();
        // クールダウン設定後にUIを即座に更新
        BattleUIManager.I?.UpdateEconomicActionButtons();
        
        isProcessingConfirm = true;
        ClosePopup();
        isSellModeActive = false;
        BattleManager.I?.UpdateTotalATKDEFDisplay();

        var dummyCard = EconomicActionNames.CreateSellDummy();
        battleManager.SetCurrentAttackCard(dummyCard);

        if (battleManager.IsOnlineMatch)
            NetworkBattleBridge.SendEconomicSell(targetSellCard.cardName);

        battleManager.SetGameState(GameState.DefensePhase);
    }

    private void OnCancelSell()
    {
        if (isProcessingConfirm) return;

        ClosePopup();
        isSellModeActive = false;
        targetSellCard = null;
        CardSelectionManager.I?.ClearAllSelections();
        battleManager.SetCurrentAttackCard(null);
        battleManager.SetGameState(GameState.AttackPhase);
        BattleManager.I?.UpdateTotalATKDEFDisplay();

        // 攻撃フェーズの状態に戻す（攻撃可能なカード以外はグレーアウト）
        if (BattleUIManager.I != null && battleManager != null && playerHand != null)
        {
            var attackables = CardRules.GetAttackChoices(playerHand);
            BattleUIManager.I.RefreshAttackInteractivity(playerHand, attackables);
        }

        BattleUIManager.I?.UpdateEconomicActionButtons();
    }

    /// <summary>
    /// 外部から売るアクションをキャンセルする（他の経済アクション開始時に使用）
    /// </summary>
    public void CancelSell()
    {
        if (!IsSellProcessActive()) return;
        Debug.Log("[SellFeature] 外部からキャンセル");
        OnCancelSell();
    }

    private void ClosePopup()
    {
        if (currentPopup != null)
        {
            var popup = currentPopup;
            currentPopup = null;
            Object.Destroy(popup.gameObject);
        }
    }

    public CardData GetTargetSellCard() => targetSellCard;
    public bool IsSellModeActive() => isSellModeActive;
    public bool IsSellProcessActive() => isSellModeActive || isProcessingConfirm;
}

