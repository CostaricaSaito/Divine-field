using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional dynamic detail filler (body text / illustrations from catalog).
/// Attach only when you want catalog-driven content; omit for fully custom prefab layouts.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuleDetailView : MonoBehaviour
{
    const float IllustrationHeight = 280f;
    const float BodyMinHeight = 120f;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField] private int bodyFontSize = 40;

    bool _wired;

    public void Show(HowToPlayRuleEntry entry)
    {
        EnsureWired();

        if (titleText != null)
            titleText.text = string.IsNullOrEmpty(entry.menuLabel) ? entry.kind.ToString() : entry.menuLabel;

        RebuildContent(entry);

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;

        EnsureModalBlocker();
        RaiseCanvasSortOrder();
        gameObject.SetActive(true);
    }

    void Awake()
    {
        if (titleText == null)
            titleText = FindChildTmp("SelectedRuleText");
        EnsureWired();
    }

    void EnsureWired()
    {
        if (_wired) return;
        _wired = true;

        if (titleText == null)
            titleText = FindChildTmp("SelectedRuleText");

        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (contentRoot == null && scrollRect != null)
            contentRoot = scrollRect.content;

        HidePrototypeViewportContent();
        EnsureContentLayout();
    }

    TMP_Text FindChildTmp(string objectName)
    {
        var tmps = GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < tmps.Length; i++)
        {
            if (tmps[i].name == objectName)
                return tmps[i];
        }
        return null;
    }

    void EnsureModalBlocker()
    {
        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        var canvasTransform = canvas.transform;
        var existing = canvasTransform.Find("ModalBlocker");
        if (existing != null) return;

        var go = new GameObject("ModalBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvasTransform, false);
        go.transform.SetAsFirstSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
    }

    void HidePrototypeViewportContent()
    {
        if (scrollRect == null || scrollRect.viewport == null) return;

        var viewport = scrollRect.viewport;
        for (var i = 0; i < viewport.childCount; i++)
        {
            var child = viewport.GetChild(i);
            if (contentRoot != null && child == contentRoot) continue;
            child.gameObject.SetActive(false);
        }
    }

    void EnsureContentLayout()
    {
        if (contentRoot == null) return;

        if (contentRoot.GetComponent<VerticalLayoutGroup>() == null)
        {
            var vlg = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 24f;
            vlg.padding = new RectOffset(16, 16, 16, 16);
        }

        if (contentRoot.GetComponent<ContentSizeFitter>() == null)
        {
            var fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    void RebuildContent(HowToPlayRuleEntry entry)
    {
        if (contentRoot == null) return;

        for (var i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        if (entry.illustrations != null)
        {
            for (var i = 0; i < entry.illustrations.Length; i++)
            {
                var sprite = entry.illustrations[i];
                if (sprite == null) continue;
                CreateIllustrationImage(sprite);
            }
        }

        if (!string.IsNullOrEmpty(entry.body))
            CreateBodyText(entry.body);

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    void CreateIllustrationImage(Sprite sprite)
    {
        var go = new GameObject("Illustration", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(contentRoot, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, IllustrationHeight);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = IllustrationHeight;
        le.minHeight = IllustrationHeight;
        le.flexibleWidth = 1f;
    }

    void CreateBodyText(string body)
    {
        var go = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(contentRoot, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, BodyMinHeight);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (bodyFont != null)
            tmp.font = bodyFont;
        tmp.fontSize = bodyFontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.richText = true;
        tmp.raycastTarget = false;
        tmp.text = body;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = BodyMinHeight;
        le.flexibleWidth = 1f;
    }

    void RaiseCanvasSortOrder()
    {
        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 200;
    }
}
