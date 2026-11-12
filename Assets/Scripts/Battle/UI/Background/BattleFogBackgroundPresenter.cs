using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dense fog: replaces the normal battle video background with a wide panorama
/// that pans left-to-right, fades out, and loops. Restores video immediately when fog ends.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleFogBackgroundPresenter : MonoBehaviour
{
    public static BattleFogBackgroundPresenter Instance { get; private set; }

    private const float PanDurationSeconds = 60f;
    private const float FogFadeInDurationSeconds = 2f;
    private const float FadeOutDurationSeconds = 3f;

    [SerializeField] private Sprite fogPanoramaSprite;
    [SerializeField] private BattleBackgroundVideoController backgroundVideoController;

    private RectTransform _viewportRect;
    private Image _fogImage;
    private RectTransform _fogRect;
    private CanvasGroup _fogCanvasGroup;
    private CancellationTokenSource _loopCts;
    private bool _fogActive;
    private float _cachedViewportWidth;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (backgroundVideoController == null)
            backgroundVideoController = GetComponent<BattleBackgroundVideoController>();
    }

    private void OnDestroy()
    {
        StopLoop();
        if (Instance == this)
            Instance = null;
    }

    public bool TryGetActiveShakeRect(out RectTransform rect)
    {
        rect = null;
        if (!_fogActive || _fogRect == null || !_fogRect.gameObject.activeInHierarchy)
            return false;
        rect = _fogRect;
        return true;
    }

    /// <summary>Called when human player fog vision toggles (respects defer in BattleStatusUI).</summary>
    public void SetViewerFogBackgroundActive(bool active)
    {
        if (active == _fogActive) return;
        _fogActive = active;

        if (active)
        {
            if (!EnsureFogLayer())
                return;
            backgroundVideoController?.SetPlaybackSuppressedByFog(true);
            _fogImage.gameObject.SetActive(true);
            SetFogAlpha(0f);
            RestartPanLoop();
        }
        else
        {
            StopLoop();
            if (_fogImage != null)
                _fogImage.gameObject.SetActive(false);
            backgroundVideoController?.SetPlaybackSuppressedByFog(false);
        }
    }

    private void RestartPanLoop()
    {
        StopLoop();
        _loopCts = new CancellationTokenSource();
        _ = RunPanLoopAsync(_loopCts.Token);
    }

    private void StopLoop()
    {
        if (_loopCts == null) return;
        _loopCts.Cancel();
        _loopCts.Dispose();
        _loopCts = null;
    }

    private async Task RunPanLoopAsync(CancellationToken ct)
    {
        if (_fogRect == null || _viewportRect == null) return;

        Canvas.ForceUpdateCanvases();
        float viewportWidth = _cachedViewportWidth > 1f
            ? _cachedViewportWidth
            : Mathf.Max(1f, _viewportRect.rect.width);
        float imageWidth = _fogRect.rect.width;
        float startX = 0f;
        float endX = viewportWidth - imageWidth;

        try
        {
            while (!ct.IsCancellationRequested)
                await RunPanCycleAsync(startX, endX, ct);
        }
        catch (OperationCanceledException)
        {
            // expected when fog ends
        }
    }

    /// <summary>Fade-in overlaps pan start; fade-out begins 3s before pan end.</summary>
    private async Task RunPanCycleAsync(float startX, float endX, CancellationToken ct)
    {
        float elapsed = 0f;
        _fogRect.anchoredPosition = new Vector2(startX, 0f);
        SetFogAlpha(0f);

        while (elapsed < PanDurationSeconds)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.deltaTime;

            float panU = Mathf.Clamp01(elapsed / PanDurationSeconds);
            float x = Mathf.Lerp(startX, endX, panU);
            _fogRect.anchoredPosition = new Vector2(x, 0f);
            SetFogAlpha(EvaluateFogAlphaForPanElapsed(elapsed));
            await Task.Yield();
        }

        _fogRect.anchoredPosition = new Vector2(endX, 0f);
        SetFogAlpha(0f);
    }

    private float EvaluateFogAlphaForPanElapsed(float elapsed)
    {
        float fadeOutStart = PanDurationSeconds - FadeOutDurationSeconds;
        if (elapsed < FogFadeInDurationSeconds)
            return Mathf.Clamp01(elapsed / FogFadeInDurationSeconds);
        if (elapsed >= fadeOutStart)
        {
            float u = (elapsed - fadeOutStart) / FadeOutDurationSeconds;
            return Mathf.Clamp01(1f - u);
        }

        return 1f;
    }

    private void SetFogAlpha(float alpha)
    {
        if (_fogCanvasGroup != null)
            _fogCanvasGroup.alpha = Mathf.Clamp01(alpha);
        else if (_fogImage != null)
        {
            var c = _fogImage.color;
            c.a = Mathf.Clamp01(alpha);
            _fogImage.color = c;
        }
    }

    private bool EnsureFogLayer()
    {
        if (fogPanoramaSprite == null)
        {
            Debug.LogWarning("[BattleFogBackgroundPresenter] fogPanoramaSprite is not assigned.");
            return false;
        }

        if (_fogImage != null) return true;

        if (!EnsureViewport())
            return false;

        var go = new GameObject(
            "FogPanoramaBackground",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        _fogRect = go.GetComponent<RectTransform>();
        _fogRect.SetParent(_viewportRect, false);
        PlaceFogBehindVideoLayer();

        _fogRect.anchorMin = new Vector2(0f, 0f);
        _fogRect.anchorMax = new Vector2(0f, 1f);
        _fogRect.pivot = new Vector2(0f, 0.5f);

        Canvas.ForceUpdateCanvases();
        float viewportHeight = Mathf.Max(1f, _viewportRect.rect.height);
        float viewportWidth = Mathf.Max(1f, _viewportRect.rect.width);
        float aspect = fogPanoramaSprite.rect.width / Mathf.Max(1f, fogPanoramaSprite.rect.height);
        float imageWidth = viewportHeight * aspect;
        _fogRect.sizeDelta = new Vector2(imageWidth, 0f);
        _fogRect.anchoredPosition = Vector2.zero;
        _cachedViewportWidth = viewportWidth;

        _fogImage = go.GetComponent<Image>();
        _fogImage.sprite = fogPanoramaSprite;
        _fogImage.preserveAspect = false;
        _fogImage.raycastTarget = false;
        _fogImage.color = Color.white;

        _fogCanvasGroup = go.GetComponent<CanvasGroup>();
        _fogCanvasGroup.interactable = false;
        _fogCanvasGroup.blocksRaycasts = false;
        _fogCanvasGroup.alpha = 0f;

        go.SetActive(false);
        return true;
    }

    private bool EnsureViewport()
    {
        if (_viewportRect != null) return true;

        if (backgroundVideoController == null)
            backgroundVideoController = BattleBackgroundVideoController.Instance;
        var backdrop = backgroundVideoController != null
            ? backgroundVideoController.GetBackdropRootRectTransform()
            : null;

        if (backdrop != null)
        {
            _viewportRect = backdrop;
            return true;
        }

        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
        if (canvas == null)
        {
            Debug.LogWarning("[BattleFogBackgroundPresenter] Could not resolve backdrop viewport.");
            return false;
        }

        var found = canvas.transform.Find("BackGroundImage");
        _viewportRect = found != null ? found.GetComponent<RectTransform>() : canvas.GetComponent<RectTransform>();
        return _viewportRect != null;
    }

    private void PlaceFogBehindVideoLayer()
    {
        if (_fogRect == null || _viewportRect == null) return;

        var video = _viewportRect.Find("BackGroundVideo");
        if (video != null)
            _fogRect.SetSiblingIndex(video.GetSiblingIndex() + 1);
        else
        {
            var still = _viewportRect.Find("BackGroundImage");
            int idx = still != null ? still.GetSiblingIndex() + 1 : 0;
            _fogRect.SetSiblingIndex(idx);
        }
    }
}
