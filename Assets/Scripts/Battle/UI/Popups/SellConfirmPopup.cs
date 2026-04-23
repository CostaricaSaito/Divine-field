using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 売るアクションの確認ポップアップ
/// </summary>
public class SellConfirmPopup : MonoBehaviour
{
    [Header("UI要素")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Image cardThumbnailImage;
    [SerializeField] private TMP_Text gpText;
    [SerializeField] private GameObject cardThumbnailContainer;
    [SerializeField] private GameObject gpContainer;

    private Action onConfirm;
    private Action onCancel;
    private CardData selectedCard;
    private bool isDestroying = false; // 破棄処理中かどうか

    private void Awake()
    {
        // ボタンのイベントを設定
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        // タイトルとメッセージテキストを設定
        if (titleText != null)
            titleText.text = "売却";
        
        if (messageText != null)
            messageText.text = "高いものを売りつけろ。";

        // 初期状態ではカードサムネイルとGPを非表示
        if (cardThumbnailContainer != null)
            cardThumbnailContainer.SetActive(false);
        
        if (gpContainer != null)
            gpContainer.SetActive(false);
    }

    private void Start()
    {
        Debug.Log($"[SellConfirmPopup] Start呼び出し - activeSelf: {gameObject.activeSelf}, activeInHierarchy: {gameObject.activeInHierarchy}");
    }

    /// <summary>
    /// ポップアップの設定
    /// </summary>
    /// <param name="onConfirm">承諾時のコールバック</param>
    /// <param name="onCancel">キャンセル時のコールバック</param>
    public void Setup(Action onConfirm, Action onCancel)
    {
        this.onConfirm = onConfirm;
        this.onCancel = onCancel;
        this.selectedCard = null;
        this.isDestroying = false; // フラグをリセット
        
        // ポップアップが確実に表示されるように設定
        gameObject.SetActive(true);
        
        // 選択状態をリセット
        UpdateDisplay();
        
        Debug.Log("[SellConfirmPopup] Setup完了 - isDestroying: false, activeSelf: " + gameObject.activeSelf);
    }

    /// <summary>
    /// 選択されたカードを設定
    /// </summary>
    public void SetSelectedCard(CardData card)
    {
        selectedCard = card;
        UpdateDisplay();
    }

    /// <summary>
    /// 表示を更新
    /// </summary>
    private void UpdateDisplay()
    {
        if (selectedCard != null)
        {
            // カードサムネイルとGPを表示
            if (cardThumbnailContainer != null)
                cardThumbnailContainer.SetActive(true);
            
            if (gpContainer != null)
                gpContainer.SetActive(true);

            // カードサムネイルを設定
            if (cardThumbnailImage != null && selectedCard.cardImage != null)
            {
                cardThumbnailImage.sprite = selectedCard.cardImage;
            }

            // GPを表示
            if (gpText != null)
            {
                gpText.text = $"{selectedCard.cardValue}GP";
            }

            // 承諾ボタンを有効化
            if (confirmButton != null)
                confirmButton.interactable = true;
        }
        else
        {
            // カードサムネイルとGPを非表示
            if (cardThumbnailContainer != null)
                cardThumbnailContainer.SetActive(false);
            
            if (gpContainer != null)
                gpContainer.SetActive(false);

            // 承諾ボタンを無効化
            if (confirmButton != null)
                confirmButton.interactable = false;
        }
    }

    private void OnConfirmClicked()
    {
        if (selectedCard == null)
        {
            Debug.LogWarning("[SellConfirmPopup] カードが選択されていません");
            return;
        }

        // 破棄処理中フラグを設定（OnDestroyでキャンセルを呼ばないようにする）
        isDestroying = true;

        // 承諾時の音効果（後で実装）
        SoundEffectPlayer.I?.Play("Assets/SE/金額をお確かめください.mp3");
        
        onConfirm?.Invoke();
    }

    private void OnCancelClicked()
    {
        // 破棄処理中フラグを設定（OnDestroyでキャンセルを呼ばないようにする）
        isDestroying = true;

        // キャンセル時の音効果（後で実装）
        SoundEffectPlayer.I?.Play("Assets/SE/キャンセル4.mp3");
        
        Debug.Log("[SellConfirmPopup] キャンセルボタンが押されました");
        onCancel?.Invoke();
    }

    private void OnDestroy()
    {
        Debug.Log($"[SellConfirmPopup] OnDestroy呼び出し - isDestroying: {isDestroying}, activeSelf: {gameObject.activeSelf}, activeInHierarchy: {gameObject.activeInHierarchy}");
        
        // イベントをクリア
        if (confirmButton != null)
            confirmButton.onClick.RemoveAllListeners();
        
        if (cancelButton != null)
            cancelButton.onClick.RemoveAllListeners();

        // ボタンが押されて破棄された場合は、OnDestroyでキャンセルを呼ばない
        if (!isDestroying)
        {
            Debug.Log("[SellConfirmPopup] OnDestroy: ボタンが押されずに破棄されたため、キャンセル処理を実行");
            // ポップアップが破棄された場合の安全策としてキャンセル処理を実行
            // SellFeature側でisProcessingConfirmフラグで制御している
            onCancel?.Invoke();
        }
        else
        {
            Debug.Log("[SellConfirmPopup] OnDestroy: ボタンが押された後の破棄のため、キャンセル処理をスキップ");
        }
    }
}

