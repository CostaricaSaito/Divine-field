using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Sequential hand card reveal with SuperRare+ cut-in video (first card only per batch).
/// </summary>
public sealed class HandRevealBatchContext
{
    public bool SuperRareVideoPlayed { get; set; }
}

public static class HandRevealPresentation
{
    public const string SuperRareRevealVideoPath = "Assets/Videos/ブチュン.mp4";

    public const int PreRevealDelayMs = 150;
    public const int PostRevealDelayMs = 100;

    private const int OverlayWidth = 1080;
    private const int OverlayHeight = 1920;

    private static VideoClip _cachedVideoClip;

    public static async Task RevealCardAsync(
        CardData card,
        CardUI ui,
        HandRevealBatchContext batch,
        CancellationToken ct,
        bool enableInteractable = true)
    {
        if (ui == null) return;
        batch ??= new HandRevealBatchContext();

        await Task.Delay(PreRevealDelayMs, ct);
        if (ct.IsCancellationRequested) return;

        if (enableInteractable && ui.button != null)
            ui.button.interactable = true;

        if (ShouldPlaySuperRareVideo(card, batch))
        {
            batch.SuperRareVideoPlayed = true;
            ui.Reveal();
            await PlaySuperRareRevealVideoAsync(ct);
            CardDealAudio.Play(card, true);
        }
        else
        {
            CardDealAudio.Play(card, true);
            ui.Reveal();
        }

        await Task.Delay(PostRevealDelayMs, ct);
    }

    public static async Task RevealFaceDownCardsLeftToRightAsync(Transform handPanel, CancellationToken ct)
    {
        if (handPanel == null)
        {
            Debug.LogWarning("[HandRevealPresentation] handPanel is null");
            return;
        }

        var batch = new HandRevealBatchContext();
        int childCountSnapshot = handPanel.childCount;
        for (int i = 0; i < childCountSnapshot; i++)
        {
            var child = handPanel.GetChild(i);
            if (child == null) continue;

            var cardUI = child.GetComponent<CardUI>();
            if (cardUI == null || !cardUI.IsFaceDown()) continue;

            try
            {
                await RevealCardAsync(cardUI.GetCardData(), cardUI, batch, ct);
            }
            catch (MissingReferenceException)
            {
                // destroyed during reveal
            }
        }
    }

    private static bool ShouldPlaySuperRareVideo(CardData card, HandRevealBatchContext batch)
    {
        if (batch == null || batch.SuperRareVideoPlayed) return false;
        if (card == null || !card.HasPremiumHandPresentation()) return false;
        if (BattleManager.I == null) return false;
        return DisadvantageRules.IsDisadvantaged(BattleManager.I.GetPlayerStatus());
    }

    private static async Task PlaySuperRareRevealVideoAsync(CancellationToken ct)
    {
        VideoClip clip = await LoadSuperRareVideoClipAsync(ct);
        if (clip == null)
        {
            Debug.LogWarning("[HandRevealPresentation] SuperRare reveal video is missing.");
            return;
        }

        Canvas canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
        if (canvas == null)
        {
            Debug.LogWarning("[HandRevealPresentation] Main UI canvas is missing.");
            return;
        }

        var overlayGo = new GameObject("SuperRareRevealVideoOverlay");
        overlayGo.transform.SetParent(canvas.transform, false);

        var overlayRect = overlayGo.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var renderTexture = new RenderTexture(OverlayWidth, OverlayHeight, 0, RenderTextureFormat.ARGB32)
        {
            name = "SuperRareRevealRT",
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
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.aspectRatio = VideoAspectRatio.Stretch;

        if (clip.audioTrackCount > 0)
        {
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetDirectAudioVolume(0, 1f);
        }

        var tcs = new TaskCompletionSource<bool>();
        videoPlayer.loopPointReached += _ => tcs.TrySetResult(true);
        videoPlayer.errorReceived += (_, message) =>
        {
            Debug.LogWarning($"[HandRevealPresentation] Video error: {message}");
            tcs.TrySetResult(true);
        };

        BattleBgmController.Instance?.PauseBgmInstantForPresentation();

        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared && !ct.IsCancellationRequested)
            await Task.Yield();

        if (ct.IsCancellationRequested)
        {
            BattleBgmController.Instance?.ResumeBgmInstantFromPresentation();
            CleanupVideoOverlay(overlayGo, renderTexture);
            ct.ThrowIfCancellationRequested();
        }

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
            BattleBgmController.Instance?.ResumeBgmInstantFromPresentation();
            CleanupVideoOverlay(overlayGo, renderTexture);
        }
    }

    private static void CleanupVideoOverlay(GameObject overlayGo, RenderTexture renderTexture)
    {
        if (overlayGo != null)
            UnityEngine.Object.Destroy(overlayGo);
        if (renderTexture == null) return;
        renderTexture.Release();
        UnityEngine.Object.Destroy(renderTexture);
    }

    private static async Task<VideoClip> LoadSuperRareVideoClipAsync(CancellationToken ct)
    {
        if (_cachedVideoClip != null)
            return _cachedVideoClip;

#if UNITY_EDITOR
        _cachedVideoClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(SuperRareRevealVideoPath);
        if (_cachedVideoClip != null)
            return _cachedVideoClip;
#endif

        if (string.IsNullOrEmpty(SuperRareRevealVideoPath))
            return null;

        AsyncOperationHandle<VideoClip> handle = default;
        try
        {
            handle = Addressables.LoadAssetAsync<VideoClip>(SuperRareRevealVideoPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HandRevealPresentation] Addressables load failed: {ex.Message}");
            return null;
        }

        while (!handle.IsDone)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogWarning($"[HandRevealPresentation] Failed to load video: {SuperRareRevealVideoPath}");
            return null;
        }

        _cachedVideoClip = handle.Result;
        return _cachedVideoClip;
    }
}
