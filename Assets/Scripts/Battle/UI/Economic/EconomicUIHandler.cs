using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 経済アクション（買う／売る／両替）の UI とフロー入口を司るサブマネージャ。
///
/// 【主な責務】
/// - 各経済アクションボタンの可否・クールダウン表示
/// - ボタン押下時のキャンセル／進行中判定とフロー起動
/// - 購入時の確認ポップアップ生成・後始末
///
/// ※ ポップアップ用 Canvas は自身の <see cref="popupCanvas"/> を優先し、未設定時は
/// <see cref="BattleUIManager.GetMainUICanvas"/> へフォールバックする。
/// </summary>
public class EconomicUIHandler : MonoBehaviour
{
    [Header("経済アクション ボタン")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button exchangeButton;

    [Header("クールダウン表示")]
    [SerializeField] private TMP_Text buyCooldownText;
    [SerializeField] private TMP_Text sellCooldownText;
    [SerializeField] private TMP_Text exchangeCooldownText;

    [Header("確認ポップアップ Prefab")]
    [Tooltip("BuyConfirmPopup 用")]
    [SerializeField] private GameObject confirmPopupPrefab;
    [Tooltip("SellConfirmPopup 用")]
    [SerializeField] private GameObject sellConfirmPopupPrefab;
    [Tooltip("ExchangePopup 用")]
    [SerializeField] private GameObject exchangePopupPrefab;
    [Tooltip("ExchangeConfirmPopUP 用（両替確定後の演出）")]
    [SerializeField] private GameObject exchangeConfirmPopupPrefab;
    [SerializeField] private Canvas popupCanvas;

    private bool isBuyPopupOpen = false;
    private GameObject currentBuyPopup = null;

    /// <summary>
    /// 経済アクションボタンの状態を更新
    /// </summary>
    public void UpdateButtons()
    {
        if (EconomicAction.I == null) return;

        if (BattleManager.I != null && BattleManager.I.IsGameEndTriggered)
        {
            ApplyEconomicButton(buyButton, buyCooldownText, false, 0);
            ApplyEconomicButton(sellButton, sellCooldownText, false, 0);
            ApplyEconomicButton(exchangeButton, exchangeCooldownText, false, 0);
            return;
        }

        bool phaseAllowed = BattleManager.I == null || BattleManager.I.PlayerCanUseEconomicActions();

        bool canBuy = phaseAllowed && EconomicAction.I.CanBuy();
        bool canSell = phaseAllowed && EconomicAction.I.CanSell();
        bool canExchange = phaseAllowed && EconomicAction.I.CanExchange();

        if (BattleManager.I != null)
        {
            if (isBuyPopupOpen || BattleManager.I.IsBuyProcessActive())
                canBuy = true;
            if (BattleManager.I.IsSellProcessActive())
                canSell = true;
            if (BattleManager.I.IsExchangeProcessActive())
                canExchange = true;
        }

        ApplyEconomicButton(buyButton, buyCooldownText, canBuy, EconomicAction.I.GetBuyCooldown());
        ApplyEconomicButton(sellButton, sellCooldownText, canSell, EconomicAction.I.GetSellCooldown());
        ApplyEconomicButton(exchangeButton, exchangeCooldownText, canExchange, EconomicAction.I.GetExchangeCooldown());
    }

    /// <summary>ゲーム終了時：各ボタンを非インタラクティブ化。</summary>
    public void DisableAllButtons()
    {
        if (buyButton != null) buyButton.interactable = false;
        if (sellButton != null) sellButton.interactable = false;
        if (exchangeButton != null) exchangeButton.interactable = false;
    }

    /// <summary>買うボタンが押されたときの処理</summary>
    public void OnBuyButtonPressed()
    {
        if (EconomicAction.I == null)
        {
            Debug.LogWarning("[EconomicUIHandler] 買うアクションは使用できません");
            return;
        }

        if (isBuyPopupOpen || (BattleManager.I != null && BattleManager.I.IsBuyProcessActive()))
        {
            Debug.Log("[EconomicUIHandler] 買いアクション進行中 → キャンセル");
            CancelBuyPopup();
            BattleManager.I?.CancelCurrentEconomicAction();
            return;
        }

        if (BattleManager.I != null && !BattleManager.I.PlayerCanUseEconomicActions())
        {
            Debug.LogWarning("[EconomicUIHandler] 買うアクションは AttackSelect 中のみ使用できます");
            return;
        }

        if (!EconomicAction.I.CanBuy())
        {
            Debug.LogWarning("[EconomicUIHandler] 買うアクションは使用できません");
            return;
        }

        if (BattleUIManager.I != null && BattleUIManager.I.GetSelectedCards().Count > 0)
        {
            Debug.Log("[EconomicUIHandler] 既にカードが選択されているため、買いアクションをキャンセルします");
            BattleUIManager.I.ClearAllSelections();
            BattleUIManager.I.HideAllCardDetails();
            return;
        }

        BattleManager.I?.CancelCurrentEconomicAction();

        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す3.mp3");

        Debug.Log("[EconomicUIHandler] 買いアクション確認ポップアップ表示");
        ShowBuyConfirmPopup();
    }

    /// <summary>売るボタンが押されたときの処理</summary>
    public void OnSellButtonPressed()
    {
        if (BattleManager.I != null && BattleManager.I.IsSellProcessActive())
        {
            BattleManager.I.CancelCurrentEconomicAction();
            return;
        }

        if (BattleManager.I != null && !BattleManager.I.PlayerCanUseEconomicActions())
        {
            Debug.LogWarning("[EconomicUIHandler] 売るアクションは AttackSelect 中のみ使用できます");
            return;
        }

        if (EconomicAction.I == null || !EconomicAction.I.CanSell())
        {
            Debug.LogWarning("[EconomicUIHandler] 売るアクションは使用できません");
            return;
        }

        BattleManager.I?.CancelCurrentEconomicAction();

        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す3.mp3");

        Debug.Log("[EconomicUIHandler] 売るアクション実行");
        BattleManager.I?.ExecuteSellAction();
    }

    /// <summary>交換ボタンが押されたときの処理</summary>
    public void OnExchangeButtonPressed()
    {
        if (BattleManager.I != null && BattleManager.I.IsExchangeProcessActive())
        {
            Debug.Log("[EconomicUIHandler] 交換ポップアップ表示中 → キャンセル");
            BattleManager.I.CancelCurrentEconomicAction();
            return;
        }

        if (BattleManager.I != null && !BattleManager.I.PlayerCanUseEconomicActions())
        {
            Debug.LogWarning("[EconomicUIHandler] 両替アクションは AttackSelect 中のみ使用できます");
            return;
        }

        if (EconomicAction.I == null || !EconomicAction.I.CanExchange())
        {
            Debug.LogWarning("[EconomicUIHandler] 交換アクションは使用できません");
            return;
        }

        BattleManager.I?.CancelCurrentEconomicAction();

        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す3.mp3");

        Debug.Log("[EconomicUIHandler] 交換アクション実行");
        BattleManager.I?.ExecuteExchangeAction();
    }

    /// <summary>買いアクションの確認ポップアップを表示</summary>
    private void ShowBuyConfirmPopup()
    {
        if (confirmPopupPrefab == null)
        {
            Debug.LogError("[EconomicUIHandler] 確認ポップアップの Prefab が設定されていません");
            return;
        }

        var canvas = GetResolvedPopupCanvas();
        if (canvas == null)
        {
            Debug.LogError("[EconomicUIHandler] ポップアップ用の Canvas が設定されていません");
            return;
        }

        var popup = Instantiate(confirmPopupPrefab, canvas.transform);
        popup.name = "BuyConfirmPopup";
        currentBuyPopup = popup;

        var confirmPopup = popup.GetComponent<BuyConfirmPopup>();
        if (confirmPopup == null)
        {
            Debug.LogError("[EconomicUIHandler] BuyConfirmPopup コンポーネントが見つかりません");
            Destroy(popup);
            currentBuyPopup = null;
            return;
        }

        isBuyPopupOpen = true;
        UpdateButtons();

        confirmPopup.Setup(
            onConfirm: () => {
                Debug.Log("[EconomicUIHandler] 買いアクション承諾");
                isBuyPopupOpen = false;
                currentBuyPopup = null;
                BattleManager.I?.ExecuteBuyAction();
                Destroy(popup);
            },
            onCancel: () => {
                Debug.Log("[EconomicUIHandler] 買いアクションキャンセル");
                isBuyPopupOpen = false;
                currentBuyPopup = null;
                UpdateButtons();
                Destroy(popup);
            }
        );

        Debug.Log("[EconomicUIHandler] 買いアクション確認ポップアップ表示完了");
    }

    /// <summary>
    /// 購入確認ポップアップを強制クローズする（他の経済アクション開始時に使用）
    /// </summary>
    public void CancelBuyPopup()
    {
        if (!isBuyPopupOpen || currentBuyPopup == null) return;
        Debug.Log("[EconomicUIHandler] 買いポップアップを強制クローズ");
        isBuyPopupOpen = false;
        Destroy(currentBuyPopup);
        currentBuyPopup = null;
        UpdateButtons();
    }

    /// <summary>SellConfirmPopup の Prefab を取得（BattleManager から使用）</summary>
    public GameObject GetSellConfirmPopupPrefab() => sellConfirmPopupPrefab;

    /// <summary>ExchangePopup の Prefab を取得（BattleManager から使用）</summary>
    public GameObject GetExchangePopupPrefab() => exchangePopupPrefab;

    /// <summary>ExchangeConfirmPopUP の Prefab を取得（ExchangeFeature から使用）</summary>
    public GameObject GetExchangeConfirmPopupPrefab() => exchangeConfirmPopupPrefab;

    /// <summary>ポップアップ用の Canvas を取得。未設定時は BattleUIManager の uiCanvas を返す。</summary>
    public Canvas GetResolvedPopupCanvas()
    {
        if (popupCanvas != null) return popupCanvas;
        return BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
    }

    private static void ApplyEconomicButton(Button button, TMP_Text cooldownText, bool interactable, int cooldown)
    {
        if (button == null) return;
        button.interactable = interactable;
        button.image.color = interactable ? Color.white : Color.gray;
        if (cooldownText != null)
            cooldownText.text = interactable ? "" : cooldown.ToString();
    }
}
