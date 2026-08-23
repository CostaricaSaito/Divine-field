using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <c>Assets/LeaderBoard.prefab</c> の TOP100 表示と YourRank フッター。
/// </summary>
[DisallowMultipleComponent]
public sealed class LeaderboardView : MonoBehaviour
{
    const string DefaultListPath = "Scroll View/Viewport/Content/LeaderboardList";

    [SerializeField] private Button backButton;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform leaderboardListRoot;
    [SerializeField] private string leaderboardListPath = DefaultListPath;
    [SerializeField] private LeaderboardItemView itemPrefab;
    [SerializeField] private RectTransform yourRankPanel;
    [SerializeField] private TMP_Text yourRankText;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TMP_Text errorMessageText;
    [SerializeField] private Button retryButton;

    CancellationTokenSource _loadCts;

    void Awake()
    {
        ResolveReferences();
        EnsureListLayout();
        EnsureStatusUi();
        StretchRootToParent();
        DisableOverlayRaycasts();

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(Close);
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(OnRetryClicked);
        }
    }

    void Start()
    {
        ResetScrollToTop();
        _loadCts = new CancellationTokenSource();
        _ = LoadAndRenderAsync(_loadCts.Token);
    }

    void OnDestroy()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }

    void ResolveReferences()
    {
        if (backButton == null)
            backButton = transform.Find("BackButton")?.GetComponent<Button>();
        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (leaderboardListRoot == null && !string.IsNullOrEmpty(leaderboardListPath))
            leaderboardListRoot = transform.Find(leaderboardListPath);
        if (yourRankPanel == null)
            yourRankPanel = transform.Find("YourRank") as RectTransform;
        if (yourRankText == null && yourRankPanel != null)
            yourRankText = yourRankPanel.GetComponentInChildren<TMP_Text>(true);
        if (itemPrefab == null)
        {
            var loaded = Resources.Load<LeaderboardItemView>("Prefab/LeaderboardItem");
            if (loaded != null)
                itemPrefab = loaded;
        }
    }

    void EnsureListLayout()
    {
        if (leaderboardListRoot == null)
            return;

        var fitter = leaderboardListRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = leaderboardListRoot.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void EnsureStatusUi()
    {
        var fontSource = GetReferenceFontSource();

        if (loadingText == null)
            loadingText = FindOrCreateStatusText("LoadingText", "読み込み中…", fontSource);
        if (emptyText == null)
            emptyText = FindOrCreateStatusText("EmptyText", "まだランキングデータがありません", fontSource);
        if (errorMessageText == null && errorPanel == null)
        {
            errorPanel = FindOrCreateStatusRoot("ErrorPanel");
            errorMessageText = errorPanel.transform.Find("ErrorMessageText")?.GetComponent<TMP_Text>();
            if (errorMessageText == null)
            {
                var textGo = new GameObject("ErrorMessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(errorPanel.transform, false);
                errorMessageText = textGo.GetComponent<TextMeshProUGUI>();
                errorMessageText.alignment = TextAlignmentOptions.Center;
                errorMessageText.fontSize = 32f;
                errorMessageText.text = "取得に失敗しました";
                ApplyFont(errorMessageText, fontSource);
            }

            retryButton = errorPanel.transform.Find("RetryButton")?.GetComponent<Button>();
            if (retryButton == null)
            {
                var buttonGo = new GameObject("RetryButton", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonGo.transform.SetParent(errorPanel.transform, false);
                var buttonRect = (RectTransform)buttonGo.transform;
                buttonRect.sizeDelta = new Vector2(260f, 72f);
                buttonRect.anchoredPosition = new Vector2(0f, -80f);

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(buttonGo.transform, false);
                var label = labelGo.GetComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 28f;
                label.text = "再試行";
                ApplyFont(label, fontSource);
                var labelRect = (RectTransform)labelGo.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                retryButton = buttonGo.GetComponent<Button>();
            }
        }

        if (yourRankText == null && yourRankPanel != null)
        {
            var textGo = new GameObject("YourRankText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(yourRankPanel, false);
            yourRankText = textGo.GetComponent<TextMeshProUGUI>();
            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24f, 0f);
            rect.offsetMax = new Vector2(-24f, 0f);
            yourRankText.alignment = TextAlignmentOptions.MidlineLeft;
            yourRankText.fontSize = 32f;
            yourRankText.color = Color.white;
            ApplyFont(yourRankText, fontSource);
        }

        SetLoadingVisible(true);
        SetEmptyVisible(false);
        SetErrorVisible(false);
        SetListVisible(false);
        SetYourRankVisible(false);
    }

    TMP_Text FindOrCreateStatusText(string objectName, string defaultMessage, TMP_Text fontSource)
    {
        var existing = transform.Find(objectName)?.GetComponent<TMP_Text>();
        if (existing != null)
            return existing;

        var scroll = scrollRect != null ? scrollRect.transform : transform;
        var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(scroll, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 120f);
        rect.anchoredPosition = Vector2.zero;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 34f;
        text.text = defaultMessage;
        ApplyFont(text, fontSource);
        return text;
    }

    TMP_Text GetReferenceFontSource()
    {
        var title = transform.Find("LeaderBoardTitle")?.GetComponent<TMP_Text>();
        if (title != null)
            return title;

        return GetComponentInChildren<TMP_Text>(true);
    }

    static void ApplyFont(TMP_Text target, TMP_Text fontSource)
    {
        if (target == null || fontSource == null || fontSource.font == null)
            return;

        target.font = fontSource.font;
    }

    GameObject FindOrCreateStatusRoot(string objectName)
    {
        var existing = transform.Find(objectName)?.gameObject;
        if (existing != null)
            return existing;

        var scroll = scrollRect != null ? scrollRect.transform : transform;
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(scroll, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 220f);
        rect.anchoredPosition = Vector2.zero;
        return go;
    }

    async Task LoadAndRenderAsync(CancellationToken ct)
    {
        SetLoadingVisible(true);
        SetEmptyVisible(false);
        SetErrorVisible(false);
        SetListVisible(false);
        SetYourRankVisible(false);
        ClearListItems();

        try
        {
            var result = await RankLeaderboardService.FetchTopAndLocalAsync(ct);
            ct.ThrowIfCancellationRequested();

            RenderTopEntries(result.TopEntries, RankLeaderboardService.GetLocalPlayerId());
            RenderYourRank(result.LocalPlayerEntry);

            SetLoadingVisible(false);
            if (result.TopEntries.Count == 0)
            {
                SetEmptyVisible(true);
                SetListVisible(false);
            }
            else
            {
                SetEmptyVisible(false);
                SetListVisible(true);
            }

            SetYourRankVisible(result.LocalPlayerEntry.HasValue);
            ResetScrollToTop();
            RebuildListLayout();
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LeaderboardView] Load failed: " + ex.Message);
            SetLoadingVisible(false);
            SetListVisible(false);
            SetEmptyVisible(false);
            SetYourRankVisible(false);
            SetErrorVisible(true);
            if (errorMessageText != null)
                errorMessageText.text = "取得に失敗しました";
        }
    }

    void RenderTopEntries(
        System.Collections.Generic.IReadOnlyList<RankLeaderboardService.RankLeaderboardEntry> entries,
        string localPlayerId)
    {
        if (leaderboardListRoot == null || itemPrefab == null)
        {
            Debug.LogError("[LeaderboardView] leaderboardListRoot or itemPrefab is missing.", this);
            return;
        }

        ClearListItems();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            bool isLocal = !string.IsNullOrEmpty(localPlayerId)
                           && string.Equals(entry.PlayerId, localPlayerId, StringComparison.Ordinal);
            var row = Instantiate(itemPrefab, leaderboardListRoot);
            row.Bind(entry, isLocal);
        }
    }

    void RenderYourRank(RankLeaderboardService.RankLeaderboardEntry? localEntry)
    {
        if (yourRankText == null || !localEntry.HasValue)
            return;

        var entry = localEntry.Value;
        yourRankText.text = RankLeaderboardService.FormatYourRankLine(
            entry.Rank,
            entry.DisplayName,
            entry.RankPoints);
    }

    void ClearListItems()
    {
        if (leaderboardListRoot == null)
            return;

        for (var i = leaderboardListRoot.childCount - 1; i >= 0; i--)
            Destroy(leaderboardListRoot.GetChild(i).gameObject);
    }

    void RebuildListLayout()
    {
        if (leaderboardListRoot is RectTransform listRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);

        if (scrollRect != null && scrollRect.content is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    void OnRetryClicked()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        _ = LoadAndRenderAsync(_loadCts.Token);
    }

    void SetLoadingVisible(bool visible)
    {
        if (loadingText != null)
            loadingText.gameObject.SetActive(visible);
    }

    void SetEmptyVisible(bool visible)
    {
        if (emptyText != null)
            emptyText.gameObject.SetActive(visible);
    }

    void SetErrorVisible(bool visible)
    {
        if (errorPanel != null)
            errorPanel.SetActive(visible);
    }

    void SetListVisible(bool visible)
    {
        if (leaderboardListRoot != null)
            leaderboardListRoot.gameObject.SetActive(visible);
    }

    void SetYourRankVisible(bool visible)
    {
        if (yourRankPanel != null)
            yourRankPanel.gameObject.SetActive(visible);
    }

    void StretchRootToParent()
    {
        if (transform is not RectTransform root)
            return;
        if (root.parent is not RectTransform)
            return;

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
    }

    void DisableOverlayRaycasts()
    {
        DisableRaycastOnTransform(transform.Find("Copyright"));
        DisableRaycastOnTransform(transform.Find("Main CinemaScope/Scope1"));
        DisableRaycastOnTransform(transform.Find("Main CinemaScope/Scope2"));
    }

    static void DisableRaycastOnTransform(Transform target)
    {
        if (target == null)
            return;

        var graphics = target.GetComponentsInChildren<Graphic>(true);
        for (var i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }

    void ResetScrollToTop()
    {
        if (scrollRect == null || scrollRect.content == null)
            return;

        Canvas.ForceUpdateCanvases();
        scrollRect.velocity = Vector2.zero;
        scrollRect.verticalNormalizedPosition = 1f;
    }

    void Close()
    {
        _loadCts?.Cancel();
        Destroy(gameObject);
    }
}
