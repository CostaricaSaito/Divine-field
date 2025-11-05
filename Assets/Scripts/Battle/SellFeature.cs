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

    public void Initialize(
        BattleManager battleManager,
        PlayerStatus playerStatus,
        PlayerStatus enemyStatus,
        List<CardData> playerHand,
        List<CardData> cpuHand,
        CardDealer cardDealer,
        GameObject sellConfirmPopupPrefab,
        Canvas popupCanvas,
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
    }

    public async Task<bool> ExecuteSellActionAsync()
    {
        if (battleManager == null || battleManager.CurrentState != GameState.AttackSelect)
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

        await Task.CompletedTask;
        return true;
    }

    public async Task ProcessEconomicActionAsync()
    {
        if (targetSellCard == null) return;

        isProcessingConfirm = false;
        ProcessGPTheft();
        await ProcessCardTransferAsync();
        UpdateHandUI();
        
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        targetSellCard = null;
    }

    private void ProcessGPTheft()
    {
        if (targetSellCard == null) return;

        int remainingTheft = targetSellCard.cardValue;

        // GPから奪取
        if (remainingTheft > 0 && enemyStatus.currentGP > 0)
        {
            int gpTheft = Mathf.Min(remainingTheft, enemyStatus.currentGP);
            enemyStatus.currentGP -= gpTheft;
            playerStatus.currentGP += gpTheft;
            remainingTheft -= gpTheft;
        }

        // MPから奪取
        if (remainingTheft > 0 && enemyStatus.currentMP > 0)
        {
            int mpTheft = Mathf.Min(remainingTheft, enemyStatus.currentMP);
            enemyStatus.currentMP -= mpTheft;
            playerStatus.currentMP += mpTheft;
            remainingTheft -= mpTheft;
        }

        // HPから奪取
        if (remainingTheft > 0 && enemyStatus.currentHP > 0)
        {
            int hpTheft = Mathf.Min(remainingTheft, enemyStatus.currentHP);
            enemyStatus.currentHP -= hpTheft;
            playerStatus.currentHP += hpTheft;
        }
    }

    private async Task ProcessCardTransferAsync()
    {
        if (targetSellCard == null) return;

        // カードUIを削除
        if (targetSellCard.cardUI != null)
        {
            var cardUIObject = targetSellCard.cardUI.gameObject;
            targetSellCard.cardUI = null;
            
            if (cardUIObject.transform.parent != null)
            {
                cardUIObject.transform.SetParent(null);
            }
            Object.Destroy(cardUIObject);
            await Task.Delay(10);
        }

        // プレイヤー手札から削除
        if (playerHand != null && playerHand.Contains(targetSellCard))
        {
            playerHand.Remove(targetSellCard);
        }

        // 敵手札に追加
        if (cpuHand != null)
        {
            cpuHand.Add(targetSellCard);
            cardDealer?.CreateCardUIForHand(targetSellCard);
        }

        // 手札パネルのクリーンアップ（データとUIの同期）
        CleanupHandPanel();
        await Task.CompletedTask;
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

        if (battleManager.CurrentState == GameState.AttackSelect && isPlayerAttacker)
        {
            BattleUIManager.I.RefreshAttackInteractivity(playerHand, CardRules.GetAttackChoices(playerHand));
        }
        else if (battleManager.CurrentState == GameState.DefenseSelect && isPlayerDefender)
        {
            BattleUIManager.I.RefreshDefenseInteractivity(playerHand, CardRules.GetDefenseChoices(playerHand));
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
    }

    private void ShowSellConfirmPopup()
    {
        if (sellConfirmPopupPrefab == null || popupCanvas == null)
        {
            Debug.LogError("[SellFeature] ポップアップのPrefabまたはCanvasがnullです");
            return;
        }

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
        isProcessingConfirm = true;
        ClosePopup();
        isSellModeActive = false;

        // 経済アクション用のダミー攻撃カードを設定
        var dummyCard = ScriptableObject.CreateInstance<CardData>();
        dummyCard.cardName = "経済アクション（売却）";
        dummyCard.cardType = CardType.Attack;
        battleManager.SetCurrentAttackCard(dummyCard);
        battleManager.SetGameState(GameState.DefenseSelect);
    }

    private void OnCancelSell()
    {
        if (isProcessingConfirm) return;

        ClosePopup();
        isSellModeActive = false;
        targetSellCard = null;
        CardSelectionManager.I?.ClearAllSelections();
        battleManager.SetCurrentAttackCard(null);
        battleManager.SetGameState(GameState.AttackSelect);
        BattleUIManager.I?.UpdateEconomicActionButtons();
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
}

