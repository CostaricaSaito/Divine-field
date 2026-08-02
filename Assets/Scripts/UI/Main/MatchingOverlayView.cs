using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Matching..." overlay shown on top of the rank match popup while the
/// online matchmaking flow runs. Built entirely at runtime (no prefab needed).
/// </summary>
public sealed class MatchingOverlayView : MonoBehaviour
{
    TMP_Text _statusText;
    TMP_Text _dotsText;
    Button _cancelButton;
    Action _onCancel;
    float _dotTimer;
    int _dotCount;

    /// <summary>Create the overlay as a full-stretch child of <paramref name="parent"/>.</summary>
    public static MatchingOverlayView Show(Transform parent, Action onCancel)
    {
        var go = new GameObject("MatchingOverlay", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.transform.SetAsLastSibling();

        var view = go.AddComponent<MatchingOverlayView>();
        view._onCancel = onCancel;
        view.BuildUi();
        return view;
    }

    public void SetStatus(string text)
    {
        if (_statusText != null)
            _statusText.text = text;
    }

    public void SetCancelInteractable(bool interactable)
    {
        if (_cancelButton != null)
            _cancelButton.interactable = interactable;
    }

    public void Close()
    {
        if (this != null && gameObject != null)
            Destroy(gameObject);
    }

    void Update()
    {
        // Simple animated "..." so the screen does not look frozen.
        _dotTimer += Time.unscaledDeltaTime;
        if (_dotTimer >= 0.5f)
        {
            _dotTimer = 0f;
            _dotCount = (_dotCount + 1) % 4;
            if (_dotsText != null)
                _dotsText.text = new string('.', _dotCount);
        }
    }

    void BuildUi()
    {
        // Dim blocker (swallows all clicks behind the overlay)
        var dim = CreateStretchImage(transform, "Dim", new Color(0f, 0f, 0f, 0.82f));
        dim.raycastTarget = true;

        var font = ResolveFont();

        _statusText = CreateText(transform, "StatusText", font, 52f, new Vector2(0f, 90f), new Vector2(900f, 140f));
        _statusText.text = "サーバに接続しています";

        _dotsText = CreateText(transform, "DotsText", font, 52f, new Vector2(0f, 20f), new Vector2(900f, 70f));
        _dotsText.text = "";

        // Cancel button
        var btnGo = new GameObject("CancelButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var btnRt = (RectTransform)btnGo.transform;
        btnRt.SetParent(transform, false);
        btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = new Vector2(0f, -180f);
        btnRt.sizeDelta = new Vector2(360f, 110f);

        var btnImage = btnGo.GetComponent<Image>();
        btnImage.color = new Color(0.75f, 0.2f, 0.25f, 1f);

        _cancelButton = btnGo.GetComponent<Button>();
        _cancelButton.targetGraphic = btnImage;
        _cancelButton.onClick.AddListener(OnCancelClicked);

        var btnLabel = CreateText(btnGo.transform, "Label", font, 44f, Vector2.zero, new Vector2(360f, 110f));
        btnLabel.text = "キャンセル";
    }

    void OnCancelClicked()
    {
        SetCancelInteractable(false);
        SetStatus("キャンセル中...");
        _onCancel?.Invoke();
    }

    static Image CreateStretchImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    static TMP_Text CreateText(
        Transform parent, string name, TMP_FontAsset font, float size, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static TMP_FontAsset ResolveFont()
    {
        // Borrow a font already used in the scene so Japanese glyphs render.
        var anyText = FindObjectOfType<TextMeshProUGUI>();
        if (anyText != null && anyText.font != null)
            return anyText.font;
        return TMP_Settings.defaultFontAsset;
    }
}
