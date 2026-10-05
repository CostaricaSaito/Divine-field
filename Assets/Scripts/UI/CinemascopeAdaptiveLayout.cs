using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adjusts Scope1/Scope2 and outer gap fills for devices whose aspect ratio differs from 1080x1920.
/// Keeps gameplay UI layout unchanged (CanvasScaler match width) while extending cinemascope bars.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class CinemascopeAdaptiveLayout : MonoBehaviour
{
    public static CinemascopeAdaptiveLayout Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Canvas gameplayCanvas;
    [SerializeField] private RectTransform scope2Top;
    [SerializeField] private RectTransform scope1Bottom;
    [SerializeField] private RectTransform topGapFill;
    [SerializeField] private RectTransform bottomGapFill;

    [Header("Overlay")]
    [SerializeField] private bool autoCreateOverlay = true;
    [SerializeField] private string overlayResourcePath = "Prefab/CinemascopeOuterGapOverlay";
    [SerializeField] private int overlaySortOrder = 100;

    private struct ScopeDesign
    {
        public Vector2 Position;
        public Vector2 Size;
    }

    private ScopeDesign _designTop;
    private ScopeDesign _designBottom;
    private bool _initialized;
    private int _lastScreenWidth;
    private int _lastScreenHeight;
    private float _lastScaleFactor;

    public Vector2 AdaptedTopPosition => scope2Top != null ? scope2Top.anchoredPosition : Vector2.zero;
    public Vector2 AdaptedBottomPosition => scope1Bottom != null ? scope1Bottom.anchoredPosition : Vector2.zero;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("CinemascopeAdaptiveLayout: duplicate instance destroyed.", this);
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveReferences();
        CacheDesignValues();

        if (autoCreateOverlay && (topGapFill == null || bottomGapFill == null))
            TryCreateOverlay();

        ApplyLayout(force: true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        Canvas.willRenderCanvases -= OnWillRenderCanvases;
    }

    private void OnEnable()
    {
        Canvas.willRenderCanvases += OnWillRenderCanvases;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= OnWillRenderCanvases;
    }

    private void OnWillRenderCanvases()
    {
        ApplyLayout(force: false);
    }

    public void ApplyLayout(bool force = false)
    {
        if (!_initialized || gameplayCanvas == null)
            return;

        var scaleFactor = Mathf.Max(0.0001f, gameplayCanvas.scaleFactor);
        if (!force &&
            Screen.width == _lastScreenWidth &&
            Screen.height == _lastScreenHeight &&
            Mathf.Approximately(scaleFactor, _lastScaleFactor))
        {
            return;
        }

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        _lastScaleFactor = scaleFactor;

        ComputeOuterGaps(out var topGapPx, out var bottomGapPx);

        var topGapUnits = topGapPx / scaleFactor;
        var bottomGapUnits = bottomGapPx / scaleFactor;

        ApplyScope(scope2Top, _designTop, topGapUnits, isTop: true);
        ApplyScope(scope1Bottom, _designBottom, bottomGapUnits, isTop: false);
        ApplyGapFill(topGapFill, topGapPx);
        ApplyGapFill(bottomGapFill, bottomGapPx);
    }

    private void ResolveReferences()
    {
        if (gameplayCanvas == null)
            gameplayCanvas = GetComponent<Canvas>();

        if (scope2Top == null)
            scope2Top = FindScopeRect("Scope2");

        if (scope1Bottom == null)
            scope1Bottom = FindScopeRect("Scope1");

        if (gameplayCanvas == null || scope2Top == null || scope1Bottom == null)
        {
            Debug.LogWarning(
                "CinemascopeAdaptiveLayout: gameplay Canvas and Scope1/Scope2 are required.",
                this);
            return;
        }

        _initialized = true;
    }

    private void CacheDesignValues()
    {
        if (scope2Top == null || scope1Bottom == null)
            return;

        _designTop = new ScopeDesign
        {
            Position = scope2Top.anchoredPosition,
            Size = scope2Top.sizeDelta
        };

        _designBottom = new ScopeDesign
        {
            Position = scope1Bottom.anchoredPosition,
            Size = scope1Bottom.sizeDelta
        };
    }

    private void TryCreateOverlay()
    {
        GameObject root = null;

        if (!string.IsNullOrEmpty(overlayResourcePath))
        {
            var prefab = Resources.Load<GameObject>(overlayResourcePath);
            if (prefab != null)
                root = Instantiate(prefab);
        }

        if (root == null)
            root = BuildOverlayHierarchy();

        root.name = "CinemascopeOuterGapOverlay";

        var overlayCanvas = root.GetComponent<Canvas>();
        if (overlayCanvas != null)
            overlayCanvas.sortingOrder = overlaySortOrder;

        BindGapFills(root.transform);
    }

    private void BindGapFills(Transform overlayRoot)
    {
        if (overlayRoot == null)
            return;

        if (topGapFill == null)
            topGapFill = overlayRoot.Find("TopGapFill") as RectTransform;

        if (bottomGapFill == null)
            bottomGapFill = overlayRoot.Find("BottomGapFill") as RectTransform;
    }

    private static GameObject BuildOverlayHierarchy()
    {
        var root = new GameObject("CinemascopeOuterGapOverlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        CreateGapFill(rootRect, "TopGapFill", isTop: true);
        CreateGapFill(rootRect, "BottomGapFill", isTop: false);
        return root;
    }

    private static void CreateGapFill(RectTransform parent, string name, bool isTop)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        if (isTop)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
        }

        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 0f);

        var image = go.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
    }

    private static RectTransform FindScopeRect(string scopeName)
    {
        var go = GameObject.Find(scopeName);
        return go == null ? null : go.GetComponent<RectTransform>();
    }

    private void ComputeOuterGaps(out float topGapPx, out float bottomGapPx)
    {
        var pixelRect = gameplayCanvas.pixelRect;
        bottomGapPx = Mathf.Max(0f, pixelRect.yMin);
        topGapPx = Mathf.Max(0f, Screen.height - pixelRect.yMax);
    }

    private static void ApplyScope(RectTransform scope, ScopeDesign design, float gapUnits, bool isTop)
    {
        if (scope == null)
            return;

        var size = design.Size;
        size.y += gapUnits;
        scope.sizeDelta = size;

        var pos = design.Position;
        pos.y += isTop ? gapUnits * 0.5f : -gapUnits * 0.5f;
        scope.anchoredPosition = pos;
    }

    private static void ApplyGapFill(RectTransform fill, float gapPx)
    {
        if (fill == null)
            return;

        var height = Mathf.Max(0f, gapPx);
        var active = height > 0.5f;
        if (fill.gameObject.activeSelf != active)
            fill.gameObject.SetActive(active);

        if (!active)
            return;

        fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
}
