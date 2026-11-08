using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <c>Resources/Prefab/DiscardPopup</c> bindings.
/// </summary>
public class DiscardPopupView : MonoBehaviour
{
    [SerializeField] private Transform discardCardsParent;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text subMessageText;
    [SerializeField] private Vector2 thumbnailSize = new Vector2(72f, 104f);
    [SerializeField] private float panelPaddingLeft = 8f;
    [SerializeField] private float panelPaddingRight = 8f;

    public void AutoWireFromHierarchy()
    {
        if (discardCardsParent == null)
        {
            var t = transform.Find("DiscardCards");
            if (t != null) discardCardsParent = t;
        }

        if (confirmButton == null)
        {
            var t = transform.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i].name == "DiscardConfirmButton")
                {
                    confirmButton = t[i].GetComponent<Button>();
                    break;
                }
            }
        }

        if (cancelButton == null)
        {
            var t = transform.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i].name == "DiscardCancelButton")
                {
                    cancelButton = t[i].GetComponent<Button>();
                    break;
                }
            }
        }

        WireTextByName("DiscardMessageTitle", ref titleText);
        WireTextByName("DiscardMessageSubtext", ref messageText);
        WireTextByName("DiscardMessageText", ref subMessageText);
    }

    private void WireTextByName(string objectName, ref TMP_Text field)
    {
        if (field != null) return;
        var t = transform.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < t.Length; i++)
        {
            if (t[i].name != objectName) continue;
            field = t[i].GetComponent<TMP_Text>();
            return;
        }
    }

    public void Bind(Action onConfirm, Action onCancel)
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => onConfirm?.Invoke());
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => onCancel?.Invoke());
        }
    }

    public void ApplyRequest(HandDiscardSelectionRequest request)
    {
        if (request == null) return;
        if (titleText != null && !string.IsNullOrEmpty(request.Title))
            titleText.text = request.Title;
        if (messageText != null && !string.IsNullOrEmpty(request.Message))
            messageText.text = request.Message;
        if (subMessageText != null && !string.IsNullOrEmpty(request.SubMessage))
            subMessageText.text = request.SubMessage;

        if (cancelButton != null)
            cancelButton.gameObject.SetActive(request.AllowCancel);
    }

    public void SetConfirmInteractable(bool interactable)
    {
        if (confirmButton != null)
            confirmButton.interactable = interactable;
    }

    public void RefreshDiscardCardsThumbnails(IReadOnlyList<CardData> cardsInSelectionOrder)
    {
        if (discardCardsParent == null) return;
        for (int i = discardCardsParent.childCount - 1; i >= 0; i--)
            Destroy(discardCardsParent.GetChild(i).gameObject);

        if (cardsInSelectionOrder == null || cardsInSelectionOrder.Count == 0) return;

        var hlg = discardCardsParent.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;
        var csf = discardCardsParent.GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;

        var parentRt = discardCardsParent as RectTransform;
        if (parentRt == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);

        float cardW = thumbnailSize.x;
        float panelW = parentRt.rect.width;
        if (panelW < 0.1f) panelW = parentRt.sizeDelta.x;
        float availW = Mathf.Max(0f, panelW - panelPaddingLeft - panelPaddingRight);

        var valid = new List<CardData>(cardsInSelectionOrder.Count);
        for (int i = 0; i < cardsInSelectionOrder.Count; i++)
        {
            var c = cardsInSelectionOrder[i];
            if (c != null && c.cardImage != null) valid.Add(c);
        }

        int n = valid.Count;
        if (n == 0) return;

        for (int shown = 0; shown < n; shown++)
        {
            var c = valid[shown];
            var go = new GameObject("DiscardThumb", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(discardCardsParent, false);
            var img = go.GetComponent<Image>();
            img.sprite = c.cardImage;
            img.preserveAspect = true;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = thumbnailSize;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);

            float x;
            if (n == 1)
                x = panelPaddingLeft + (availW - cardW) * 0.5f;
            else
            {
                float interval = (availW - cardW) / (n - 1);
                x = panelPaddingLeft + shown * interval;
            }

            rt.anchoredPosition = new Vector2(x, 0f);
            go.transform.SetSiblingIndex(shown);
        }
    }
}
