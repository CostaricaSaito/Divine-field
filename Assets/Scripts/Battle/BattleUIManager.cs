using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public enum Side { Player, Enemy }

/// <summary>
/// バトル画面のUI表示・管理を担当するマネージャークラス
/// 
/// 【主な機能】
/// - ステータス表示の更新
/// - カード詳細の表示・非表示
/// - ボタンの状態管理（使用/許可/祈り）
/// - ポップアップの表示（ダメージ、ミス）
/// - 手札の操作制御（選択/キャンセル）
/// 
/// 【責任範囲】
/// - UI要素の表示・非表示
/// - UI要素の状態変更
/// - カード選択の管理
/// - アニメーションの制御
/// 
/// 【他のクラスとの関係】
/// - BattleManager: UI更新の指示を受ける
/// - CardSheetDisplay: カード詳細の表示
/// - DamagePopup: ダメージ表示
/// 
/// 【注意事項】
/// - シングルトンパターンは含まない（必要に応じて使用可否を検討）
/// - エラーの処理は外部に委ねる
/// - マルチスレッドでの更新は行わない
/// </summary>
public class BattleUIManager : MonoBehaviour
{
    public static BattleUIManager I;

    //==== フィールド =====
    [Header("UI 要素")]
    [SerializeField] private BattleStatusUI statusUI;
    [SerializeField] private Button useButton;
    [SerializeField] private TMP_Text useButtonLabelTMP;
    [SerializeField] private Text useButtonLabelUGUI;
    [SerializeField] private Image useButtonImage;

    [Header("ポップアップ")]
    [SerializeField] private GameObject damagePopupPrefab;
    [SerializeField] private Canvas uiCanvas;

    [Header("カード詳細表示")]
    [SerializeField] private GameObject cardSheetPrefab;
    [SerializeField] private Transform playerCardDisplayPanel;
    [SerializeField] private Transform enemyCardDisplayPanel;

    [Header("Use ボタン設定")]
    [SerializeField] private Color useButtonNormalColor = new Color(0.2f, 0.5f, 1f, 1f);
    [SerializeField] private Color useButtonDangerColor = new Color(0.9f, 0.2f, 0.25f, 1f);
    [SerializeField] private Color useButtonPrayColor = new Color(1f, 0.95f, 0.6f, 1f);

    [Header("カード管理")]
    [SerializeField] private CardLayoutManager cardLayoutManager;
    [SerializeField] private CardSelectionManager cardSelectionManager;

    [Header("経済アクション")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button exchangeButton;
    [SerializeField] private TMP_Text buyCooldownText;
    [SerializeField] private TMP_Text sellCooldownText;
    [SerializeField] private TMP_Text exchangeCooldownText;

    [Header("確認ポップアップ")]
    [SerializeField] private GameObject confirmPopupPrefab;
    [SerializeField] private Canvas popupCanvas;

    // プライベート変数
    private readonly List<GameObject> activeCardSheets = new();
    private enum UseButtonMode { Use, Allow, Pray }
    
    // ポップアップ状態管理
    private bool isBuyPopupOpen = false;

    //==== 初期化 =====
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        // ボタンコンポーネントの自動取得
        if (useButton != null)
        {
            if (useButtonLabelTMP == null) useButtonLabelTMP = useButton.GetComponentInChildren<TMP_Text>(true);
            if (useButtonLabelUGUI == null) useButtonLabelUGUI = useButton.GetComponentInChildren<Text>(true);
            if (useButtonImage == null) useButtonImage = useButton.targetGraphic as Image;
        }
    }

    //==== パブリックAPI：ステータス表示 =====
    public void UpdateStatus(PlayerStatus player, PlayerStatus enemy)
    {
        // 手札の枚数を取得（常に現在の手札枚数を参照）
        int playerHandCount = BattleManager.I?.playerHand?.Count ?? 0;
        int enemyHandCount = BattleManager.I?.cpuHand?.Count ?? 0;
        
        Debug.Log($"[BattleUIManager] 手札枚数 - プレイヤー: {playerHandCount}, 敵: {enemyHandCount}");
        
        statusUI?.UpdateStatus(player, enemy, playerHandCount, enemyHandCount);
    }

    //==== パブリックAPI：カード詳細表示 =====
    public void ShowCardDetail(CardData card, Side side)
    {
        if (card == null)
        {
            Debug.LogWarning("[BattleUIManager] ShowCardDetail: card is null");
            return;
        }

        // 既に選択されているカードの場合は選択解除
        if (cardSelectionManager.IsCardSelected(card))
        {
            // カード選択をキャンセル
            Debug.Log($"[BattleUIManager] カード選択をキャンセル: {card.cardName}");
            CancelCardSelection(card);
            return;
        }

        // カード選択を追加（制限チェックは内部で実行）
        if (cardSelectionManager.AddCardSelection(card))
        {
            // カード表示
            DisplayCard(card, side);
        }
    }

    public void HideAllCardDetails()
    {
        foreach (var go in activeCardSheets)
        {
            if (go != null) Destroy(go);
        }
        activeCardSheets.Clear();
        cardSelectionManager.ClearAllSelections();
        UpdateHandCardHighlights();
        BattleManager.I?.ClearSelectedCards();
    }

    //==== パブリックAPI：カード選択管理 =====
    public List<CardData> GetSelectedCards()
    {
        return cardSelectionManager.GetSelectedCards();
    }

    public List<CardData> GetSelectedAttackCards()
    {
        return cardSelectionManager.GetSelectedAttackCards();
    }

    public List<CardData> GetSelectedDefenseCards()
    {
        return cardSelectionManager.GetSelectedDefenseCards();
    }

    //==== パブリックAPI：ボタン管理 =====
    public void SetUseButtonLabel(string text)
    {
        if (useButton == null) return;

        if (useButtonLabelTMP != null) useButtonLabelTMP.text = text;
        if (useButtonLabelUGUI != null) useButtonLabelUGUI.text = text;

        var mode = text == "許可" ? UseButtonMode.Allow
                 : text == "祈り" ? UseButtonMode.Pray
                 : UseButtonMode.Use;
        ApplyUseButtonMode(mode);

        useButton.interactable = true;
    }

    public void SetUseButtonInteractable(bool interactable)
    {
        if (useButton != null) useButton.interactable = interactable;
    }

    //==== パブリックAPI：手札管理 =====
    public void SetHandInteractivity(List<CardData> hand, bool interactable)
    {
        if (hand == null) return;
        foreach (var c in hand) SetCardInteractable(c, interactable);
    }

    public void SetCardInteractable(CardData card, bool interactable)
    {
        if (card?.cardUI == null) return;

        var btn = card.cardUI.button;
        if (btn != null) btn.interactable = interactable;

        var cg = card.cardUI.GetComponent<CanvasGroup>();
        if (cg == null) cg = card.cardUI.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = interactable ? 1f : 0.5f;
        cg.blocksRaycasts = interactable;
    }

    public void UpdateHandInteractivity(List<CardData> hand, List<CardData> allowedCards = null)
    {
        if (hand == null) return;
        
        // allowedCardsがnullの場合は全てのカードを使用可能にする
        if (allowedCards == null)
        {
            foreach (var card in hand)
            {
                if (card?.cardUI == null) continue;
                SetCardInteractable(card, true);
            }
            return;
        }
        
        // 参照比較ではなく、カードのcardUIを基準に比較する
        var allowedCardUIs = new HashSet<CardUI>();
        foreach (var allowedCard in allowedCards)
        {
            if (allowedCard?.cardUI != null)
            {
                allowedCardUIs.Add(allowedCard.cardUI);
            }
        }
        
        foreach (var card in hand)
        {
            if (card?.cardUI == null) continue;
            // cardUIを基準に比較（新しいカードが置き換えられても、cardUIが同じなら一致する）
            bool canUse = allowedCardUIs.Contains(card.cardUI);
            SetCardInteractable(card, canUse);
        }
    }
    
    public void SetPrayModeUI(List<CardData> hand)
    {
        SetUseButtonLabel("祈り");
        SetHandInteractivity(hand, false);
    }
    
    public void RefreshAttackInteractivity(List<CardData> hand, List<CardData> attackableCards)
    {
        UpdateHandInteractivity(hand, attackableCards);
        SetUseButtonLabel("使用");
    }
    
    public void RefreshDefenseInteractivity(List<CardData> hand, List<CardData> defenseCards)
    {
        UpdateHandInteractivity(hand, defenseCards);
        SetUseButtonLabel("許可");
    }

    /// <summary>
    /// Intro時点でのカード表示（グレーアウトなし）
    /// </summary>
    public void SetIntroModeUI(List<CardData> hand)
    {
        SetUseButtonLabel("使用");
        SetHandInteractivity(hand, true); // すべてのカードを有効にする（グレーアウトなし）
    }

    //==== パブリックAPI：ポップアップ =====
    public void ShowDamagePopup(int amount, PlayerStatus target)
    {
        Debug.Log($"[BattleUIManager] ダメージポップアップ表示: {amount}ダメージ 対象 {target?.DisplayName ?? "null"}");
        
        var popup = SpawnPopupFor(target);
        if (popup == null) 
        {
            Debug.LogWarning("[BattleUIManager] ポップアップの生成に失敗しました");
            return;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            bool hitPlayer = (target == BattleManager.I.GetPlayerStatus());
            string displayText = amount > 0 ? $"{amount} ダメージ！" : "ダメージなし！";
            Color displayColor = amount > 0 ? (hitPlayer ? Color.cyan : Color.red) : Color.yellow;
            damageText.Setup(displayText, displayColor);
            Debug.Log($"[BattleUIManager] ダメージポップアップ設定完了: {amount}ダメージ");
        }
        else
        {
            Debug.LogWarning("[BattleUIManager] DamagePopupコンポーネントが見つかりません");
        }
    }

    /// <summary>
    /// 回復ポップアップを表示
    /// </summary>
    public void ShowHealPopup(int amount, string statType, PlayerStatus target)
    {
        Debug.Log($"[BattleUIManager] 回復ポップアップ表示: {statType}{amount}回復 対象 {target?.DisplayName ?? "null"}");
        
        var popup = SpawnPopupFor(target);
        if (popup == null) 
        {
            Debug.LogWarning("[BattleUIManager] ポップアップの生成に失敗しました");
            return;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            bool hitPlayer = (target == BattleManager.I.GetPlayerStatus());
            string displayText = $"{statType}{amount}回復！";
            Color displayColor = Color.green; // 回復は緑色
            damageText.Setup(displayText, displayColor);
            Debug.Log($"[BattleUIManager] 回復ポップアップ設定完了: {statType}{amount}回復");
        }
        else
        {
            Debug.LogWarning("[BattleUIManager] DamagePopupコンポーネントが見つかりません");
        }
    }

    public void ShowMissPopup(PlayerStatus target)
    {
        Debug.Log($"[BattleUIManager] ミスポップアップ表示 対象 {target?.DisplayName ?? "null"}");
        
        var popup = SpawnPopupFor(target);
        if (popup == null) 
        {
            Debug.LogWarning("[BattleUIManager] ミスポップアップの生成に失敗しました");
            return;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.Setup("ミス！", Color.yellow);
            Debug.Log("[BattleUIManager] ミスポップアップ設定完了");
        }
        else
        {
            Debug.LogWarning("[BattleUIManager] DamagePopupコンポーネントが見つかりません");
        }
    }

    //==== プライベートメソッド：カード選択管理 =====
    private void CancelCardSelection(CardData card)
    {
        bool removed = cardSelectionManager.CancelCardSelection(card);
        Debug.Log($"[BattleUIManager] カード選択をキャンセル: {card.cardName} (削除成功: {removed}, selectedCards数: {cardSelectionManager.SelectedCardCount})");
        
        // 表示されているカードシートを削除
        RemoveCardFromDisplay(card);
        
        // 手札のハイライト更新
        UpdateHandCardHighlights();
        
        // カードレイアウトの更新
        cardLayoutManager.SetActiveCardSheets(activeCardSheets);
        cardLayoutManager.SetSelectedCards(cardSelectionManager.GetSelectedCards());
        cardLayoutManager.HandleCardCancellation();
        
        // BattleManagerの更新
        UpdateBattleManagerAfterCancel();
        
        // TotalATKDEF表示を更新（選択が空の場合は非表示になる）
        BattleManager.I?.UpdateTotalATKDEFDisplay();
    }

    public void ClearAllSelections()
    {
        cardSelectionManager.ClearAllSelections();
        UpdateHandCardHighlights();
        BattleManager.I?.ClearSelectedCards();
    }

    private void UpdateHandCardHighlights()
    {
        var handCards = FindObjectsOfType<CardUI>();
        
        foreach (var cardUI in handCards)
        {
            if (cardUI == null) continue;
            
            var cardData = cardUI.GetCardData();
            if (cardData == null) continue;
            
            bool isSelected = cardSelectionManager.IsCardSelected(cardData);
            cardUI.SetHighlight(isSelected);
        }
    }

    //==== プライベートメソッド：カード表示 =====
    private void DisplayCard(CardData card, Side side)
    {
        Transform parent = (side == Side.Player) ? playerCardDisplayPanel : enemyCardDisplayPanel;

        if (cardSheetPrefab != null && parent != null)
        {
            var go = Instantiate(cardSheetPrefab, parent);
            if (!parent.gameObject.activeSelf) parent.gameObject.SetActive(true);
            if (!go.activeSelf) go.SetActive(true);

            var display = go.GetComponent<CardSheetDisplay>();
            if (display != null)
            {
                display.Setup(card);
            }
            
            activeCardSheets.Add(go);
            
            // レイアウトマネージャーの更新
            cardLayoutManager.SetActiveCardSheets(activeCardSheets);
            cardLayoutManager.SetSelectedCards(cardSelectionManager.GetSelectedCards());
            
            // カード位置の設定
            cardLayoutManager.SetupCardPosition(go, parent);
            UpdateHandCardHighlights();
            return;
        }

        // フォールバック処理
        HandleCardDisplayFallback(card, side);
    }




    //==== プライベートメソッド：アニメーション =====
    private System.Collections.IEnumerator StackCardAnimation(GameObject cardObj, float targetX, float targetY)
    {
        var rt = cardObj.transform as RectTransform;
        if (rt == null) yield break;
        
        Vector3 startPos = new Vector3(0, 0, 0);
        Vector3 endPos = new Vector3(targetX, targetY, 0);
        Vector3 startScale = Vector3.one;
        Vector3 endScale = Vector3.one * 0.9f; // cardScaleの固定値
        
        float duration = 0.3f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);
            
            rt.anchoredPosition = Vector3.Lerp(startPos, endPos, t);
            rt.localScale = Vector3.Lerp(startScale, endScale, t);
            
            yield return null;
        }
        
        rt.anchoredPosition = endPos;
        rt.localScale = endScale;
    }

    //==== プライベートメソッド：ボタン管理 =====
    private void ApplyUseButtonMode(UseButtonMode mode)
    {
        if (useButton == null) return;
        var img = useButtonImage ?? (useButton.targetGraphic as Image);
        if (img == null) return;

        img.color = mode == UseButtonMode.Allow ? useButtonDangerColor
                 : mode == UseButtonMode.Pray ? useButtonPrayColor
                 : useButtonNormalColor;
    }

    //==== プライベートメソッド：ポップアップ =====
    private GameObject SpawnPopupFor(PlayerStatus target)
    {
        Debug.Log($"[BattleUIManager] ポップアップ生成 - damagePopupPrefab: {damagePopupPrefab != null}, uiCanvas: {uiCanvas != null}");
        
        if (damagePopupPrefab == null || uiCanvas == null)
        {
            Debug.LogWarning("[BattleUIManager] DamagePopup / Canvas が設定されていません");
            return null;
        }

        var go = Instantiate(damagePopupPrefab, uiCanvas.transform);
        var rt = go.transform as RectTransform;
        if (rt != null)
        {
            rt.anchoredPosition = GetPopupAnchor(target);
            rt.localScale = Vector3.one;
            Debug.Log($"[BattleUIManager] ポップアップ位置設定 - 位置: {rt.anchoredPosition}");
        }
        return go;
    }

    private Vector2 GetPopupAnchor(PlayerStatus target)
    {
        bool isPlayer = (target == BattleManager.I.GetPlayerStatus());
        return isPlayer ? new Vector2(-300, -200) : new Vector2(300, 200);
    }

    //==== プライベートメソッド：ヘルパー =====
    private void RemoveCardFromDisplay(CardData card)
    {
        for (int i = activeCardSheets.Count - 1; i >= 0; i--)
        {
            var cardObj = activeCardSheets[i];
            if (cardObj == null) continue;
            
            var cardDisplay = cardObj.GetComponent<CardSheetDisplay>();
            if (cardDisplay != null && cardDisplay.GetCardData() == card)
            {
                Destroy(cardObj);
                activeCardSheets.RemoveAt(i);
                break;
            }
        }
    }

    private void UpdateBattleManagerAfterCancel()
    {
        if (cardSelectionManager.HasNoSelectedCards())
        {
            BattleManager.I?.ClearSelectedCards();
        }
        else if (BattleManager.I != null)
        {
            if (BattleManager.I.CurrentState == GameState.AttackSelect)
            {
                var selectedAttackCards = GetSelectedAttackCards();
                if (selectedAttackCards.Count == 0)
                {
                    BattleManager.I.ClearSelectedCards();
                }
                else
                {
                    BattleManager.I.UpdateTotalATKDEFDisplay();
                }
            }
            else if (BattleManager.I.CurrentState == GameState.DefenseSelect)
            {
                BattleManager.I.UpdateTotalATKDEFDisplay();
                UpdateDefenseButtonLabel();
            }
        }
    }

    /// <summary>
    /// 防御フェーズのボタンラベルを更新
    /// </summary>
    public void UpdateDefenseButtonLabel()
    {
        if (BattleManager.I?.CurrentState != GameState.DefenseSelect) return;

        var selectedDefenseCards = GetSelectedDefenseCards();
        if (selectedDefenseCards.Count > 0)
        {
            SetUseButtonLabel("使用");
        }
        else
        {
            SetUseButtonLabel("許可");
        }
    }

    private void HandleCardDisplayFallback(CardData card, Side side)
    {
        if (cardSheetPrefab == null)
        {
            Debug.LogWarning("[BattleUIManager] cardSheetPrefab が設定されていません。CardDisplayController へのフォールバック処理を実行します。");
        }
        if ((side == Side.Player ? playerCardDisplayPanel : enemyCardDisplayPanel) == null)
        {
            Debug.LogWarning("[BattleUIManager] CardDisplayPanel が設定されていません。CardDisplayController へのフォールバック処理を実行します。side=" + side);
        }

        var controller = FindObjectOfType<CardDisplayController>(true);
        if (controller != null)
        {
            controller.ShowCard(card);
        }
        else
        {
            Debug.LogError("[BattleUIManager] すべての表示方法が利用できません。cardSheetPrefab / panel が設定されていない、CardDisplayController も見つかりません。");
        }
    }

    //==== 経済アクション =====
    
    /// <summary>
    /// 経済アクションボタンの状態を更新
    /// </summary>
    public void UpdateEconomicActionButtons()
    {
        if (EconomicAction.I == null) return;

        // 買うボタン
        if (buyButton != null)
        {
            bool canBuy = EconomicAction.I.CanBuy();
            buyButton.interactable = canBuy;
            if (buyCooldownText != null)
                buyCooldownText.text = canBuy ? "" : EconomicAction.I.GetBuyCooldown().ToString();
            buyButton.image.color = canBuy ? Color.white : Color.gray;
        }

        // 売るボタン
        if (sellButton != null)
        {
            bool canSell = EconomicAction.I.CanSell();
            sellButton.interactable = canSell;
            if (sellCooldownText != null)
                sellCooldownText.text = canSell ? "" : EconomicAction.I.GetSellCooldown().ToString();
            sellButton.image.color = canSell ? Color.white : Color.gray;
        }

        // 両替ボタン
        if (exchangeButton != null)
        {
            bool canExchange = EconomicAction.I.CanExchange();
            exchangeButton.interactable = canExchange;
            if (exchangeCooldownText != null)
                exchangeCooldownText.text = canExchange ? "" : EconomicAction.I.GetExchangeCooldown().ToString();
            exchangeButton.image.color = canExchange ? Color.white : Color.gray;
        }

        Debug.Log($"[BattleUIManager] 経済アクションボタン更新完了");
    }

    /// <summary>
    /// 買うボタンが押された時の処理
    /// </summary>
    public void OnBuyButtonPressed()
    {
        if (EconomicAction.I == null || !EconomicAction.I.CanBuy())
        {
            Debug.LogWarning("[BattleUIManager] 買うアクションは使用できません");
            return;
        }

        // ポップアップが既に開いている場合は無視
        if (isBuyPopupOpen)
        {
            Debug.Log("[BattleUIManager] 買うポップアップが既に開いているため、無視します");
            return;
        }

        // 既にカードが選択されている場合はキャンセル
        if (cardSelectionManager != null && cardSelectionManager.SelectedCardCount > 0)
        {
            Debug.Log("[BattleUIManager] 既にカードが選択されているため、買うアクションをキャンセルします");
            cardSelectionManager.ClearAllSelections();
            BattleUIManager.I?.HideAllCardDetails();
            return;
        }

        // 購入ボタン押下時の音効果
        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す3.mp3");

        Debug.Log("[BattleUIManager] 買うアクション確認ポップアップ表示");
        ShowBuyConfirmPopup();
    }

    /// <summary>
    /// 売るボタンが押された時の処理
    /// </summary>
    public void OnSellButtonPressed()
    {
        if (EconomicAction.I == null || !EconomicAction.I.CanSell())
        {
            Debug.LogWarning("[BattleUIManager] 売るアクションは使用できません");
            return;
        }

        Debug.Log("[BattleUIManager] 売るアクション実行");
        BattleManager.I?.ExecuteSellAction();
    }

    /// <summary>
    /// 両替ボタンが押された時の処理
    /// </summary>
    public void OnExchangeButtonPressed()
    {
        if (EconomicAction.I == null || !EconomicAction.I.CanExchange())
        {
            Debug.LogWarning("[BattleUIManager] 両替アクションは使用できません");
            return;
        }

        Debug.Log("[BattleUIManager] 両替アクション実行");
        BattleManager.I?.ExecuteExchangeAction();
    }

    /// <summary>
    /// 買うアクションの確認ポップアップを表示
    /// </summary>
    private void ShowBuyConfirmPopup()
    {
        if (confirmPopupPrefab == null)
        {
            Debug.LogError("[BattleUIManager] 確認ポップアップのPrefabが設定されていません");
            return;
        }

        var canvas = popupCanvas != null ? popupCanvas : uiCanvas;
        if (canvas == null)
        {
            Debug.LogError("[BattleUIManager] ポップアップ用のCanvasが設定されていません");
            return;
        }

        // ポップアップを生成
        var popup = Instantiate(confirmPopupPrefab, canvas.transform);
        popup.name = "BuyConfirmPopup";

        // ポップアップのコンポーネントを取得
        var confirmPopup = popup.GetComponent<BuyConfirmPopup>();
        if (confirmPopup == null)
        {
            Debug.LogError("[BattleUIManager] BuyConfirmPopupコンポーネントが見つかりません");
            Destroy(popup);
            return;
        }

        // ポップアップ状態を設定
        isBuyPopupOpen = true;

        // コールバックを設定
        confirmPopup.Setup(
            onConfirm: () => {
                Debug.Log("[BattleUIManager] 買うアクション承諾");
                isBuyPopupOpen = false; // ポップアップ状態をリセット
                BattleManager.I?.ExecuteBuyAction();
                Destroy(popup);
            },
            onCancel: () => {
                Debug.Log("[BattleUIManager] 買うアクションキャンセル");
                isBuyPopupOpen = false; // ポップアップ状態をリセット
                Destroy(popup);
            }
        );

        Debug.Log("[BattleUIManager] 買うアクション確認ポップアップ表示完了");
    }

    /// <summary>
    /// プレイヤーのカード表示エリアの中心位置を取得
    /// </summary>
    public Vector3 GetPlayerCardDisplayCenter()
    {
        if (playerCardDisplayPanel != null)
        {
            return playerCardDisplayPanel.position;
        }
        return Vector3.zero;
    }

    /// <summary>
    /// 敵のカード表示エリアの中心位置を取得
    /// </summary>
    public Vector3 GetEnemyCardDisplayCenter()
    {
        if (enemyCardDisplayPanel != null)
        {
            return enemyCardDisplayPanel.position;
        }
        return Vector3.zero;
    }

    /// <summary>
    /// プレイヤーのカード表示エリアのTransformを取得
    /// </summary>
    public Transform GetPlayerCardDisplayPanel()
    {
        return playerCardDisplayPanel;
    }

    /// <summary>
    /// 敵のカード表示エリアのTransformを取得
    /// </summary>
    public Transform GetEnemyCardDisplayPanel()
    {
        return enemyCardDisplayPanel;
    }
}