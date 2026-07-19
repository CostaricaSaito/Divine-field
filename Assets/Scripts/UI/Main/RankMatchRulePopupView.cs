using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ランクマッチルール説明ポップアップ。
/// Prefab 上の Scroll View（または <see cref="scrollContentRoot"/>）を縦スクロール表示します。
/// </summary>
/// <remarks>
/// 推奨階層:
/// RankMatchRule（本コンポーネント・Stretch）
/// └ ContentRoot（中央パネル）
///    ├ Background（枠・固定）
///    ├ ScrollView（ScrollRect + Viewport + Content）
///    │  └ Viewport（RectMask2D）
///    │     └ Content / RuleScrollContent（縦長テキスト・画像）
///    ├ Scrollbar Vertical（任意）
///    └ CloseButton
/// </remarks>
[DisallowMultipleComponent]
public sealed class RankMatchRulePopupView : MonoBehaviour
{
    const string DefaultResourcesPath = "Prefab/RankMatchRule";

    [Header("参照（Prefab で割り当て。未設定時は名前で探索）")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Scrollbar verticalScrollbar;
    [SerializeField] private Button closeButton;
    [Tooltip("Scroll View 未配置時にここを Viewport 配下へ移してスクロールを自動構築します。")]
    [SerializeField] private RectTransform scrollContentRoot;

    [Header("自動構築 Scroll View")]
    [SerializeField] private bool buildScrollViewIfMissing = true;
    [SerializeField] private Vector2 scrollViewPadding = new Vector2(48f, 120f);

    public event Action Closed;

    void Awake()
    {
        ResolveReferences();
        EnsureScrollReady();
        WireCloseButton();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    /// <summary>表示します。親の最前面に出します。</summary>
    public void Open()
    {
        ResolveReferences();
        EnsureScrollReady();
        ResetScrollToTop();
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    /// <summary>非表示にします。</summary>
    public void Close()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    public static RankMatchRulePopupView InstantiateFromResources(Transform parent)
    {
        var prefab = Resources.Load<RankMatchRulePopupView>(DefaultResourcesPath);
        if (prefab == null)
        {
            Debug.LogError($"[RankMatchRulePopupView] Resources/{DefaultResourcesPath} が見つかりません。");
            return null;
        }

        var instance = Instantiate(prefab, parent, false);
        instance.transform.SetAsLastSibling();
        return instance;
    }

    void ResolveReferences()
    {
        if (contentRoot == null)
            contentRoot = transform.Find("ContentRoot") as RectTransform;

        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (verticalScrollbar == null)
        {
            var scrollbars = GetComponentsInChildren<Scrollbar>(true);
            for (var i = 0; i < scrollbars.Length; i++)
            {
                var bar = scrollbars[i];
                if (bar.direction == Scrollbar.Direction.TopToBottom
                    || bar.direction == Scrollbar.Direction.BottomToTop)
                {
                    verticalScrollbar = bar;
                    break;
                }
            }

            if (verticalScrollbar == null && scrollbars.Length > 0)
                verticalScrollbar = scrollbars[0];
        }

        if (closeButton == null)
            closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
        if (closeButton == null && contentRoot != null)
            closeButton = contentRoot.Find("CloseButton")?.GetComponent<Button>();

        if (contentRoot == null)
            contentRoot = transform as RectTransform;

        if (scrollContentRoot == null)
        {
            scrollContentRoot = transform.Find("Scroll View/Viewport/Content") as RectTransform
                ?? transform.Find("Scroll View/Viewport/RuleScrollContent") as RectTransform;
        }

        if (scrollContentRoot == null && contentRoot != null)
        {
            scrollContentRoot = contentRoot.Find("RuleScrollContent") as RectTransform
                ?? contentRoot.Find("ScrollView/Viewport/Content") as RectTransform
                ?? contentRoot.Find("ScrollView/Viewport/RuleScrollContent") as RectTransform;
        }
    }

    void EnsureScrollReady()
    {
        if (scrollRect == null && buildScrollViewIfMissing)
            TryBuildScrollView();

        if (scrollRect == null)
        {
            Debug.LogWarning(
                "[RankMatchRulePopupView] ScrollRect がありません。ContentRoot 下に Scroll View を配置するか、" +
                "scrollContentRoot に縦長コンテンツを指定してください。",
                this);
            return;
        }

        ConfigureScrollRect(scrollRect, verticalScrollbar, buildScrollViewIfMissing);
    }

    void TryBuildScrollView()
    {
        if (contentRoot == null || scrollContentRoot == null) return;

        var scrollViewGo = new GameObject(
            "ScrollView",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ScrollRect));
        scrollViewGo.transform.SetParent(contentRoot, false);
        scrollViewGo.transform.SetSiblingIndex(scrollContentRoot.GetSiblingIndex());

        var scrollViewRt = scrollViewGo.GetComponent<RectTransform>();
        scrollViewRt.anchorMin = Vector2.zero;
        scrollViewRt.anchorMax = Vector2.one;
        scrollViewRt.offsetMin = new Vector2(scrollViewPadding.x, scrollViewPadding.y);
        scrollViewRt.offsetMax = new Vector2(-scrollViewPadding.x, -scrollViewPadding.y);

        var scrollImage = scrollViewGo.GetComponent<Image>();
        scrollImage.color = new Color(1f, 1f, 1f, 0f);
        scrollImage.raycastTarget = true;

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGo.transform.SetParent(scrollViewGo.transform, false);
        var viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;

        scrollContentRoot.SetParent(viewportRt, false);
        PrepareScrollContentRect(scrollContentRoot);

        scrollRect = scrollViewGo.GetComponent<ScrollRect>();
        scrollRect.content = scrollContentRoot;
        scrollRect.viewport = viewportRt;
        scrollRect.verticalScrollbar = verticalScrollbar;

        if (verticalScrollbar != null)
            PrepareVerticalScrollbar(verticalScrollbar);
    }

    static void PrepareScrollContentRect(RectTransform content)
    {
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;

        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    static void PrepareVerticalScrollbar(Scrollbar scrollbar)
    {
        scrollbar.direction = Scrollbar.Direction.TopToBottom;

        var rt = scrollbar.transform as RectTransform;
        if (rt == null) return;

        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-8f, 0f);
        rt.sizeDelta = new Vector2(24f, 0f);
    }

    static void ConfigureScrollRect(ScrollRect rect, Scrollbar verticalBar, bool mutateContentLayout)
    {
        rect.horizontal = false;
        rect.vertical = true;
        rect.movementType = ScrollRect.MovementType.Elastic;
        rect.elasticity = 0.1f;
        rect.inertia = true;
        rect.decelerationRate = 0.135f;
        rect.scrollSensitivity = 24f;

        if (verticalBar != null)
        {
            PrepareVerticalScrollbar(verticalBar);
            rect.verticalScrollbar = verticalBar;
            rect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        }

        if (rect.content != null && mutateContentLayout)
            PrepareScrollContentRect(rect.content);
    }

    void ResetScrollToTop()
    {
        if (scrollRect == null) return;

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
        scrollRect.velocity = Vector2.zero;
    }

    void WireCloseButton()
    {
        if (closeButton == null) return;
        closeButton.onClick.RemoveListener(Close);
        closeButton.onClick.AddListener(Close);
    }
}
