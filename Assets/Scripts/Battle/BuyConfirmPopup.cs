using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 買うアクションの確認ポップアップ
/// </summary>
public class BuyConfirmPopup : MonoBehaviour
{
    [Header("UI要素")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action onConfirm;
    private Action onCancel;

    private void Awake()
    {
        // ボタンのイベントを設定
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        // タイトルとメッセージテキストを設定
        if (titleText != null)
            titleText.text = "購入";
        
        if (messageText != null)
            messageText.text = "ノークレーム・ノーリターン";
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
    }

    private void OnConfirmClicked()
    {
        // 承諾時の音効果
        SoundEffectPlayer.I?.Play("Assets/SE/「お金を入れてね」.mp3");
        
        onConfirm?.Invoke();
    }

    private void OnCancelClicked()
    {
        // キャンセル時の音効果
        SoundEffectPlayer.I?.Play("Assets/SE/キャンセル4.mp3");
        
        Debug.Log("[BuyConfirmPopup] やめとくボタンが押されました");
        onCancel?.Invoke();
    }

    private void OnDestroy()
    {
        // イベントをクリア
        if (confirmButton != null)
            confirmButton.onClick.RemoveAllListeners();
        
        if (cancelButton != null)
            cancelButton.onClick.RemoveAllListeners();

        // ポップアップが破棄された場合の安全策としてキャンセル処理を実行
        onCancel?.Invoke();
    }
}
