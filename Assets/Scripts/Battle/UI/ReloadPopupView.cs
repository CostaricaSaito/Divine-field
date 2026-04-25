using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <c>Resources/Prefab/ReloadPopup</c> 用。ReloadCards のサムネイルと確定／キャンセルを受け持つ。
/// 4枚以上は <see cref="CardLayoutManager"/> の上下配置と同様、先頭・末尾を端に固定し中間を等間隔（重なり可）で水平配置する。
/// </summary>
public class ReloadPopupView : MonoBehaviour
{
    [SerializeField] private Transform reloadCardsParent;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Vector2 thumbnailSize = new Vector2(72f, 104f);
    [SerializeField] private float panelPaddingLeft = 8f;
    [SerializeField] private float panelPaddingRight = 8f;

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

    public void SetConfirmInteractable(bool interactable)
    {
        if (confirmButton != null)
            confirmButton.interactable = interactable;
    }

    /// <param name="cardsInSelectionOrder">選択した順。先頭＝左端、末尾＝右端（<see cref="CardLayoutManager"/> 相当の等間隔重ね）。</param>
    /// <param name="handForIndex">未使用。互換のため残す。</param>
    public void RefreshReloadCardsThumbnails(IReadOnlyList<CardData> cardsInSelectionOrder, List<CardData> handForIndex)
    {
        if (reloadCardsParent == null) return;
        for (int i = reloadCardsParent.childCount - 1; i >= 0; i--)
            Destroy(reloadCardsParent.GetChild(i).gameObject);

        if (cardsInSelectionOrder == null || cardsInSelectionOrder.Count == 0) return;

        var hlg = reloadCardsParent.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;
        var csf = reloadCardsParent.GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;

        var parentRt = reloadCardsParent as RectTransform;
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
            var go = new GameObject("ReloadThumb", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(reloadCardsParent, false);
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
            {
                x = panelPaddingLeft + (availW - cardW) * 0.5f;
            }
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
