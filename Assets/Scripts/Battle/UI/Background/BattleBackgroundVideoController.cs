using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays battle background videos and still images on <see cref="RawImage"/> with black-key friendly blend modes.
/// Works with scene objects named BackGroundVideo / VideoPlayer when references are not wired.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleBackgroundVideoController : MonoBehaviour
{
    public static BattleBackgroundVideoController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage backgroundVideo;
    [SerializeField] private Image staticBackgroundImage;
    [SerializeField] private RenderTexture targetTexture;

    [Header("Playlist")]
    [Tooltip("Fallback: Resources/BattleBackgroundVideoPlaylist")]
    [SerializeField] private BattleBackgroundVideoPlaylistSO playlist;

    [Header("Session Fade")]
    [SerializeField] private float sessionFadeInSeconds = 0.75f;

    [Header("Render Texture")]
    [SerializeField] private int renderTextureWidth = 1920;
    [SerializeField] private int renderTextureHeight = 1080;

    [Header("Blend Materials (template)")]
    [SerializeField] private Material additiveMaterial;
    [SerializeField] private Material screenMaterial;
    [SerializeField] private Material additiveGlowMaterial;
    [SerializeField] private Material alphaMaterial;

    private Material _activeMaterial;
    private CanvasGroup _displayFadeGroup;
    private BattleBackgroundVideoPlaylistSO _resolvedPlaylist;
    private int _entryIndex;
    private float _entryElapsedSeconds;
    private bool _sessionStarted;
    private bool _isSwitching;
    private CancellationTokenSource _sessionCts;

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        if (Instance == this)
            Instance = null;
        if (_activeMaterial != null)
            Destroy(_activeMaterial);
    }

    private void Update()
    {
        if (!_sessionStarted || _resolvedPlaylist == null || _isSwitching)
            return;

        var entries = _resolvedPlaylist.Entries;
        if (entries == null || entries.Length == 0)
            return;

        var entry = entries[_entryIndex];
        if (entry == null || entry.durationSeconds <= 0f)
            return;

        _entryElapsedSeconds += Time.unscaledDeltaTime;
        if (_entryElapsedSeconds < entry.durationSeconds)
            return;

        _ = AdvanceEntryAsync(_sessionCts?.Token ?? CancellationToken.None);
    }

    public void StartBattleSession()
    {
        if (_sessionStarted)
            return;

        EnsureWired();
        if (videoPlayer == null || backgroundVideo == null)
        {
            Debug.LogWarning("[BattleBackgroundVideoController] VideoPlayer or BackGroundVideo is missing.");
            return;
        }

        _resolvedPlaylist = ResolvePlaylist();
        var entries = _resolvedPlaylist?.Entries;
        if (entries == null || entries.Length == 0 || !HasAnyValidEntry(entries))
        {
            Debug.LogWarning("[BattleBackgroundVideoController] Playlist has no valid video or still-image entries.");
            return;
        }

        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _sessionCts = new CancellationTokenSource();

        ConfigureVideoPlayer();
        EnsureDisplayFadeGroup();
        backgroundVideo.gameObject.SetActive(true);

        _entryIndex = 0;
        _entryElapsedSeconds = 0f;
        _sessionStarted = true;

        videoPlayer.loopPointReached -= OnVideoLoopPointReached;
        videoPlayer.loopPointReached += OnVideoLoopPointReached;

        ApplyEntryImmediate(entries[_entryIndex]);
        SetDisplayAlpha(0f);
        PlayCurrentEntryMedia();

        float fadeIn = sessionFadeInSeconds;
        if (_resolvedPlaylist != null && _resolvedPlaylist.FadeInSeconds > 0f)
            fadeIn = _resolvedPlaylist.FadeInSeconds;

        _ = FadeDisplayAlphaAsync(1f, fadeIn, _sessionCts.Token);
    }

    public async Task StopAsync(float fadeSeconds, CancellationToken ct = default)
    {
        if (!_sessionStarted && backgroundVideo == null)
            return;

        _sessionStarted = false;
        videoPlayer.loopPointReached -= OnVideoLoopPointReached;

        if (backgroundVideo != null)
            await FadeDisplayAlphaAsync(0f, fadeSeconds, ct);

        if (videoPlayer != null)
            videoPlayer.Stop();
    }

    private void OnVideoLoopPointReached(VideoPlayer source)
    {
        if (!_sessionStarted || _isSwitching || _resolvedPlaylist == null)
            return;

        if (!ShouldAdvanceOnLoopEnd())
            return;

        _ = AdvanceEntryAsync(_sessionCts?.Token ?? CancellationToken.None);
    }

    private bool ShouldAdvanceOnLoopEnd()
    {
        var entries = _resolvedPlaylist.Entries;
        if (entries == null || entries.Length == 0)
            return false;

        var entry = entries[_entryIndex];
        if (entry == null || entry.IsStill)
            return false;

        if (entry.durationSeconds > 0f)
            return false;

        if (!entry.loopClip)
            return true;

        return entries.Length > 1 || _resolvedPlaylist.LoopPlaylist;
    }

    private async Task AdvanceEntryAsync(CancellationToken ct)
    {
        if (_isSwitching || _resolvedPlaylist == null)
            return;

        var entries = _resolvedPlaylist.Entries;
        if (entries == null || entries.Length == 0)
            return;

        int nextIndex = _entryIndex + 1;
        if (nextIndex >= entries.Length)
        {
            if (!_resolvedPlaylist.LoopPlaylist)
                return;
            nextIndex = 0;
        }

        if (nextIndex == _entryIndex)
            return;

        _isSwitching = true;
        try
        {
            float fadeOut = Mathf.Max(0.01f, _resolvedPlaylist.FadeOutSeconds);
            float fadeIn = Mathf.Max(0.01f, _resolvedPlaylist.FadeInSeconds);

            await FadeDisplayAlphaAsync(0f, fadeOut, ct);
            ct.ThrowIfCancellationRequested();

            _entryIndex = nextIndex;
            _entryElapsedSeconds = 0f;
            ApplyEntryImmediate(entries[_entryIndex]);
            PlayCurrentEntryMedia();

            await FadeDisplayAlphaAsync(1f, fadeIn, ct);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        finally
        {
            _isSwitching = false;
        }
    }

    private void ApplyEntryImmediate(BattleBackgroundVideoEntry entry)
    {
        if (entry == null || !entry.IsValid || backgroundVideo == null)
            return;

        ApplyBlendMode(entry.blendMode, entry.intensity);

        if (entry.IsVideo)
            ShowVideoEntry(entry);
        else
            ShowStillEntry(entry);
    }

    private void ShowVideoEntry(BattleBackgroundVideoEntry entry)
    {
        if (videoPlayer == null || entry.clip == null)
            return;

        videoPlayer.Stop();
        videoPlayer.clip = entry.clip;
        videoPlayer.isLooping = entry.loopClip;
        EnsureRenderTexture();
        backgroundVideo.texture = targetTexture;
        backgroundVideo.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    private void ShowStillEntry(BattleBackgroundVideoEntry entry)
    {
        if (entry.stillImage == null)
            return;

        if (videoPlayer != null)
            videoPlayer.Stop();

        backgroundVideo.texture = entry.stillImage.texture;
        var outerUv = UnityEngine.Sprites.DataUtility.GetOuterUV(entry.stillImage);
        backgroundVideo.uvRect = new Rect(outerUv.x, outerUv.y, outerUv.z - outerUv.x, outerUv.w - outerUv.y);
    }

    private void PlayCurrentEntryMedia()
    {
        var entries = _resolvedPlaylist?.Entries;
        if (entries == null || _entryIndex < 0 || _entryIndex >= entries.Length)
            return;

        var entry = entries[_entryIndex];
        if (entry != null && entry.IsVideo && videoPlayer != null)
            videoPlayer.Play();
    }

    private static bool HasAnyValidEntry(BattleBackgroundVideoEntry[] entries)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].IsValid)
                return true;
        }

        return false;
    }

    private void ApplyBlendMode(BattleBackgroundVideoBlendMode mode, float intensity)
    {
        if (backgroundVideo == null)
            return;

        var template = ResolveMaterialTemplate(mode);
        if (template == null)
        {
            Debug.LogWarning($"[BattleBackgroundVideoController] Material template missing for {mode}.");
            return;
        }

        if (_activeMaterial == null || _activeMaterial.shader != template.shader)
        {
            if (_activeMaterial != null)
                Destroy(_activeMaterial);
            _activeMaterial = new Material(template);
            backgroundVideo.material = _activeMaterial;
        }

        _activeMaterial.SetFloat(IntensityId, intensity);
    }

    private Material ResolveMaterialTemplate(BattleBackgroundVideoBlendMode mode)
    {
        return mode switch
        {
            BattleBackgroundVideoBlendMode.Screen => screenMaterial,
            BattleBackgroundVideoBlendMode.AdditiveGlow => additiveGlowMaterial,
            BattleBackgroundVideoBlendMode.Alpha => alphaMaterial,
            _ => additiveMaterial,
        };
    }

    private async Task FadeDisplayAlphaAsync(float targetAlpha, float durationSeconds, CancellationToken ct)
    {
        EnsureDisplayFadeGroup();
        if (_displayFadeGroup == null)
            return;

        float start = _displayFadeGroup.alpha;
        if (durationSeconds <= 0f)
        {
            SetDisplayAlpha(targetAlpha);
            return;
        }

        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(start, targetAlpha, elapsed / durationSeconds);
            SetDisplayAlpha(alpha);
            await Task.Yield();
        }

        SetDisplayAlpha(targetAlpha);
    }

    private void EnsureDisplayFadeGroup()
    {
        if (backgroundVideo == null)
            return;

        _displayFadeGroup = backgroundVideo.GetComponent<CanvasGroup>();
        if (_displayFadeGroup == null)
            _displayFadeGroup = backgroundVideo.gameObject.AddComponent<CanvasGroup>();

        _displayFadeGroup.interactable = false;
        _displayFadeGroup.blocksRaycasts = false;
    }

    private void SetDisplayAlpha(float alpha)
    {
        EnsureDisplayFadeGroup();
        if (_displayFadeGroup != null)
            _displayFadeGroup.alpha = alpha;
    }

    private void ConfigureVideoPlayer()
    {
        EnsureRenderTexture();
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = targetTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.aspectRatio = VideoAspectRatio.FitHorizontally;
        backgroundVideo.texture = targetTexture;
        backgroundVideo.raycastTarget = false;
        backgroundVideo.color = Color.white;
    }

    private void EnsureRenderTexture()
    {
        if (targetTexture == null)
        {
            int width = Mathf.Max(320, renderTextureWidth);
            int height = Mathf.Max(240, renderTextureHeight);
            targetTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "BattleBackgroundVideoRT",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            targetTexture.Create();
        }

        if (videoPlayer != null)
            videoPlayer.targetTexture = targetTexture;
        if (backgroundVideo != null)
            backgroundVideo.texture = targetTexture;
    }

    private void EnsureWired()
    {
        if (videoPlayer == null)
        {
            var playerGo = GameObject.Find("VideoPlayer");
            if (playerGo != null)
                videoPlayer = playerGo.GetComponent<VideoPlayer>();
            if (videoPlayer == null)
                videoPlayer = GetComponent<VideoPlayer>();
        }

        EnsureMaterialTemplates();

        if (staticBackgroundImage == null)
        {
            if (BattleBgmController.Instance != null)
                staticBackgroundImage = BattleBgmController.Instance.BattleBackgroundImage;
            if (staticBackgroundImage == null)
            {
                var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
                var found = canvas != null ? canvas.transform.Find("BackGroundImage") : null;
                if (found != null)
                    staticBackgroundImage = found.GetComponent<Image>();
            }
        }

        if (backgroundVideo == null)
        {
            Transform parent = staticBackgroundImage != null
                ? staticBackgroundImage.transform.parent
                : BattleUIManager.I != null
                    ? BattleUIManager.I.GetMainUICanvas()?.transform
                    : null;

            if (parent != null)
            {
                var found = parent.Find("BackGroundVideo");
                if (found != null)
                    backgroundVideo = found.GetComponent<RawImage>();
            }

            if (backgroundVideo == null && parent is RectTransform canvasRoot && staticBackgroundImage != null)
                backgroundVideo = CreateBackgroundVideoLayer(canvasRoot, staticBackgroundImage.rectTransform);
        }
    }

    private static RawImage CreateBackgroundVideoLayer(RectTransform canvasRoot, RectTransform staticBackground)
    {
        var go = new GameObject(
            "BackGroundVideo",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(CanvasGroup));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(canvasRoot, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetSiblingIndex(staticBackground.GetSiblingIndex() + 1);

        var raw = go.GetComponent<RawImage>();
        raw.raycastTarget = false;
        raw.color = Color.white;

        var fadeGroup = go.GetComponent<CanvasGroup>();
        fadeGroup.interactable = false;
        fadeGroup.blocksRaycasts = false;
        return raw;
    }

    private BattleBackgroundVideoPlaylistSO ResolvePlaylist()
    {
        if (playlist != null)
            return playlist;
        return Resources.Load<BattleBackgroundVideoPlaylistSO>("BattleBackgroundVideoPlaylist");
    }

    private void EnsureMaterialTemplates()
    {
        if (additiveMaterial == null || screenMaterial == null || additiveGlowMaterial == null || alphaMaterial == null)
            Debug.LogWarning("[BattleBackgroundVideoController] Assign all blend materials on BattleBackgroundVideoSystem.");
    }
}
