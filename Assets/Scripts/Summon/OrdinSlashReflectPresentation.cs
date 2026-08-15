using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Odin 切り払い: fullscreen slash video + dedicated reflect SE (before bounce popup).
/// </summary>
public static class OrdinSlashReflectPresentation
{
    private const int OverlayWidth = 1080;
    private const int OverlayHeight = 1920;

    public static async Task RunCutInAsync(CancellationToken ct)
    {
        var settings = OrdinSlashReflectFlow.ActiveSettings;
        SoundEffectPlayer.I?.Play(settings.reflectSoundEffectPath);
        await PlayFullscreenVideoAsync(settings, ct);
    }

    private static async Task PlayFullscreenVideoAsync(OrdinSlashReflectSettings settings, CancellationToken ct)
    {
        VideoClip clip = settings.slashVideoClip;
        if (clip == null && !string.IsNullOrEmpty(settings.slashVideoAddress))
            clip = await OrdinSlashReflectFlow.LoadVideoClipAsync(settings.slashVideoAddress, ct);

        if (clip == null)
        {
            Debug.LogWarning("[OrdinSlashReflectPresentation] Slash video clip is missing.");
            return;
        }

        Canvas canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
        if (canvas == null)
        {
            Debug.LogWarning("[OrdinSlashReflectPresentation] Main UI canvas is missing.");
            return;
        }

        var overlayGo = new GameObject("OrdinSlashReflectVideoOverlay");
        overlayGo.transform.SetParent(canvas.transform, false);

        var overlayRect = overlayGo.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var renderTexture = new RenderTexture(OverlayWidth, OverlayHeight, 0, RenderTextureFormat.ARGB32)
        {
            name = "OrdinSlashReflectRT",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        renderTexture.Create();

        var rawImage = overlayGo.AddComponent<RawImage>();
        rawImage.raycastTarget = true;
        rawImage.texture = renderTexture;
        rawImage.color = Color.white;

        var playerGo = new GameObject("VideoPlayer");
        playerGo.transform.SetParent(overlayGo.transform, false);
        var videoPlayer = playerGo.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.clip = clip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.aspectRatio = VideoAspectRatio.Stretch;

        var tcs = new TaskCompletionSource<bool>();
        videoPlayer.loopPointReached += _ => tcs.TrySetResult(true);
        videoPlayer.errorReceived += (_, message) =>
        {
            Debug.LogWarning($"[OrdinSlashReflectPresentation] Video error: {message}");
            tcs.TrySetResult(true);
        };

        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared && !ct.IsCancellationRequested)
            await Task.Yield();

        ct.ThrowIfCancellationRequested();
        videoPlayer.Play();

        try
        {
            using (ct.Register(() => tcs.TrySetCanceled()))
                await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            if (videoPlayer.isPlaying)
                videoPlayer.Stop();
            throw;
        }
        finally
        {
            UnityEngine.Object.Destroy(overlayGo);
            renderTexture.Release();
            UnityEngine.Object.Destroy(renderTexture);
        }
    }
}
