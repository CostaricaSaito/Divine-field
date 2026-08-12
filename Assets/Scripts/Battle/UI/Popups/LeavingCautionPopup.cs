using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Online battle leave confirmation (<c>LeavingCaution.prefab</c>).
/// </summary>
public sealed class LeavingCautionPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action _onConfirm;
    private Action _onCancel;
    private bool _closed;

    void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
            titleText.text = "逃げるのか？";
        if (messageText != null && string.IsNullOrWhiteSpace(messageText.text))
            messageText.text = "対戦放棄と見なされ自動的に敗北となります";
    }

    public void Setup(Action onConfirm, Action onCancel)
    {
        _closed = false;
        _onConfirm = onConfirm;
        _onCancel = onCancel;
    }

    private void OnConfirmClicked()
    {
        if (_closed) return;
        _closed = true;
        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す3.mp3");
        _onConfirm?.Invoke();
        Destroy(gameObject);
    }

    private void OnCancelClicked()
    {
        if (_closed) return;
        _closed = true;
        SoundEffectPlayer.I?.Play("Assets/SE/キャンセル4.mp3");
        _onCancel?.Invoke();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveAllListeners();
        if (cancelButton != null)
            cancelButton.onClick.RemoveAllListeners();
    }
}
