using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Fullscreen video overlay with additive blending, used by the battle-start intro.
///
/// The overlay is prepared up front and stays invisible until <see cref="PlayAsync"/>,
/// so the intro can hit its 0.1s / 0.3s cues without waiting on VideoPlayer.Prepare.
/// </summary>
public sealed class BattleStartIntroVideoOverlay
{
    private const int OverlayWidth = 1080;
    private const int OverlayHeight = 1920;
    private const string AdditiveShaderName = "DivineField/UI/BattleVideoAdditive";

    private static readonly Dictionary<string, VideoClip> ClipCache = new();
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private GameObject _overlayGo;
    private RawImage _rawImage;
    private RenderTexture _renderTexture;
    private Material _additiveMaterial;
    private VideoPlayer _videoPlayer;
    private TaskCompletionSource<bool> _finished;

    /// <summary>Loads (and caches) a clip by Addressables key. Uses AssetDatabase in the editor.</summary>
    public static async Task<VideoClip> LoadClipAsync(string address, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(address))
            return null;

        if (ClipCache.TryGetValue(address, out var cached) && cached != null)
            return cached;

#if UNITY_EDITOR
        var fromDatabase = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(address);
        if (fromDatabase != null)
        {
            ClipCache[address] = fromDatabase;
            return fromDatabase;
        }
#endif

        AsyncOperationHandle<VideoClip> handle;
        try
        {
            handle = Addressables.LoadAssetAsync<VideoClip>(address);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BattleStartIntroVideoOverlay] Addressables load failed: {address} - {ex.Message}");
            return null;
        }

        while (!handle.IsDone)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogWarning($"[BattleStartIntroVideoOverlay] Failed to load video: {address}");
            return null;
        }

        ClipCache[address] = handle.Result;
        return handle.Result;
    }

    /// <summary>
    /// Builds a hidden fullscreen overlay for <paramref name="clip"/> and waits until the
    /// VideoPlayer is prepared. <paramref name="flipVertically"/> mirrors the picture upside down.
    /// </summary>
    public static async Task<BattleStartIntroVideoOverlay> PrepareAsync(
        Canvas canvas, VideoClip clip, bool flipVertically, CancellationToken ct)
    {
        if (canvas == null || clip == null)
            return null;

        var overlay = new BattleStartIntroVideoOverlay();
        overlay.Build(canvas, clip, flipVertically);

        try
        {
            overlay._videoPlayer.Prepare();
            while (!overlay._videoPlayer.isPrepared && !overlay._finished.Task.IsCompleted)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }
        catch (OperationCanceledException)
        {
            overlay.Dispose();
            throw;
        }

        return overlay;
    }

    /// <summary>Shows the overlay, plays the clip once and disposes everything afterwards.</summary>
    public async Task PlayAsync(CancellationToken ct)
    {
        if (_videoPlayer == null || _rawImage == null)
            return;

        try
        {
            _rawImage.enabled = true;
            _rawImage.rectTransform.SetAsLastSibling();
            _videoPlayer.Play();

            using (ct.Register(() => _finished.TrySetCanceled()))
                await _finished.Task;
        }
        catch (OperationCanceledException)
        {
            // The battle scene is going away; just clean up.
        }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        if (_videoPlayer != null && _videoPlayer.isPlaying)
            _videoPlayer.Stop();
        _videoPlayer = null;
        _rawImage = null;

        if (_overlayGo != null)
            UnityEngine.Object.Destroy(_overlayGo);
        _overlayGo = null;

        if (_additiveMaterial != null)
            UnityEngine.Object.Destroy(_additiveMaterial);
        _additiveMaterial = null;

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            UnityEngine.Object.Destroy(_renderTexture);
        }
        _renderTexture = null;
    }

    private void Build(Canvas canvas, VideoClip clip, bool flipVertically)
    {
        _overlayGo = new GameObject(flipVertically ? "BattleStartIntroVideoFlipped" : "BattleStartIntroVideo");
        _overlayGo.transform.SetParent(canvas.transform, false);

        var overlayRect = _overlayGo.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.localScale = flipVertically ? new Vector3(1f, -1f, 1f) : Vector3.one;

        _renderTexture = new RenderTexture(OverlayWidth, OverlayHeight, 0, RenderTextureFormat.ARGB32)
        {
            name = "BattleStartIntroRT",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        _renderTexture.Create();

        _rawImage = _overlayGo.AddComponent<RawImage>();
        _rawImage.raycastTarget = false;
        _rawImage.texture = _renderTexture;
        _rawImage.color = Color.white;
        _rawImage.enabled = false;

        _additiveMaterial = CreateAdditiveMaterial();
        if (_additiveMaterial != null)
            _rawImage.material = _additiveMaterial;

        var playerGo = new GameObject("VideoPlayer");
        playerGo.transform.SetParent(_overlayGo.transform, false);
        _videoPlayer = playerGo.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = false;
        _videoPlayer.clip = clip;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _renderTexture;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        _videoPlayer.aspectRatio = VideoAspectRatio.Stretch;

        _finished = new TaskCompletionSource<bool>();
        _videoPlayer.loopPointReached += _ => _finished.TrySetResult(true);
        _videoPlayer.errorReceived += (_, message) =>
        {
            Debug.LogWarning($"[BattleStartIntroVideoOverlay] Video error: {message}");
            _finished.TrySetResult(true);
        };
    }

    private static Material CreateAdditiveMaterial()
    {
        var template = BattleBackgroundVideoController.Instance != null
            ? BattleBackgroundVideoController.Instance.AdditiveMaterialTemplate
            : null;

        Material material = template != null ? new Material(template) : null;
        if (material == null)
        {
            var shader = Shader.Find(AdditiveShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[BattleStartIntroVideoOverlay] Additive shader not found: {AdditiveShaderName}");
                return null;
            }
            material = new Material(shader);
        }

        if (material.HasProperty(IntensityId))
            material.SetFloat(IntensityId, 1f);
        return material;
    }
}
