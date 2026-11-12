using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Layout and async motion for still sprites on <see cref="RawImage"/>.</summary>
public static class BattleBackgroundStillImageAnimation
{
    public static void ResetToFullScreen(RawImage raw)
    {
        if (raw == null)
            return;

        var rt = raw.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    /// <summary>Configures <paramref name="raw"/> for pan/zoom modes. Full-screen reset when mode is <see cref="BattleBackgroundStillAnimationMode.Off"/>.</summary>
    public static void ApplyLayout(RawImage raw, RectTransform viewport, Sprite sprite, BattleBackgroundStillAnimationMode mode)
    {
        if (raw == null)
            return;

        if (mode == BattleBackgroundStillAnimationMode.Off || sprite == null)
        {
            ResetToFullScreen(raw);
            return;
        }

        if (mode == BattleBackgroundStillAnimationMode.Zoom300Percent)
        {
            ResetToFullScreen(raw);
            raw.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            raw.rectTransform.localScale = Vector3.one;
            return;
        }

        if (viewport == null)
        {
            ResetToFullScreen(raw);
            return;
        }

        Canvas.ForceUpdateCanvases();
        float viewportWidth = Mathf.Max(1f, viewport.rect.width);
        float viewportHeight = Mathf.Max(1f, viewport.rect.height);
        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        var rt = raw.rectTransform;

        switch (mode)
        {
            case BattleBackgroundStillAnimationMode.PanLeftToRight:
            case BattleBackgroundStillAnimationMode.PanRightToLeft:
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                float imageWidth = viewportHeight * aspect;
                rt.sizeDelta = new Vector2(imageWidth, 0f);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
                break;

            case BattleBackgroundStillAnimationMode.PanTopToBottom:
            case BattleBackgroundStillAnimationMode.PanBottomToTop:
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                float imageHeight = viewportWidth / aspect;
                rt.sizeDelta = new Vector2(0f, imageHeight);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
                break;

            default:
                ResetToFullScreen(raw);
                break;
        }
    }

    public static async Task RunLoopAsync(
        RawImage raw,
        RectTransform viewport,
        BattleBackgroundStillAnimationMode mode,
        float durationSeconds,
        CancellationToken ct)
    {
        if (raw == null || mode == BattleBackgroundStillAnimationMode.Off)
            return;

        durationSeconds = Mathf.Max(0.01f, durationSeconds);

        try
        {
            while (!ct.IsCancellationRequested)
                await RunOneCycleAsync(raw, viewport, mode, durationSeconds, ct);
        }
        catch (OperationCanceledException)
        {
            // expected on entry switch / stop
        }
    }

    private static async Task RunOneCycleAsync(
        RawImage raw,
        RectTransform viewport,
        BattleBackgroundStillAnimationMode mode,
        float durationSeconds,
        CancellationToken ct)
    {
        var rt = raw.rectTransform;
        float elapsed = 0f;

        GetPanRange(viewport, rt, mode, out float startPrimary, out float endPrimary);
        ApplyMotionSample(rt, mode, startPrimary, 0f);

        while (elapsed < durationSeconds)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(elapsed / durationSeconds);
            float primary = Mathf.Lerp(startPrimary, endPrimary, u);
            ApplyMotionSample(rt, mode, primary, u);
            await Task.Yield();
        }

        ApplyMotionSample(rt, mode, endPrimary, 1f);

        if (mode == BattleBackgroundStillAnimationMode.Zoom300Percent)
            rt.localScale = Vector3.one;
    }

    private static void GetPanRange(
        RectTransform viewport,
        RectTransform imageRt,
        BattleBackgroundStillAnimationMode mode,
        out float start,
        out float end)
    {
        Canvas.ForceUpdateCanvases();
        float viewportWidth = viewport != null ? Mathf.Max(1f, viewport.rect.width) : 0f;
        float viewportHeight = viewport != null ? Mathf.Max(1f, viewport.rect.height) : 0f;
        float imageWidth = Mathf.Max(1f, imageRt.rect.width);
        float imageHeight = Mathf.Max(1f, imageRt.rect.height);

        switch (mode)
        {
            case BattleBackgroundStillAnimationMode.PanLeftToRight:
                start = 0f;
                end = viewportWidth - imageWidth;
                break;
            case BattleBackgroundStillAnimationMode.PanRightToLeft:
                start = viewportWidth - imageWidth;
                end = 0f;
                break;
            case BattleBackgroundStillAnimationMode.PanTopToBottom:
                start = 0f;
                end = viewportHeight - imageHeight;
                break;
            case BattleBackgroundStillAnimationMode.PanBottomToTop:
                start = viewportHeight - imageHeight;
                end = 0f;
                break;
            default:
                start = 0f;
                end = 0f;
                break;
        }
    }

    private static void ApplyMotionSample(RectTransform rt, BattleBackgroundStillAnimationMode mode, float primary, float zoomU)
    {
        switch (mode)
        {
            case BattleBackgroundStillAnimationMode.PanLeftToRight:
            case BattleBackgroundStillAnimationMode.PanRightToLeft:
                rt.anchoredPosition = new Vector2(primary, rt.anchoredPosition.y);
                break;
            case BattleBackgroundStillAnimationMode.PanTopToBottom:
            case BattleBackgroundStillAnimationMode.PanBottomToTop:
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, primary);
                break;
            case BattleBackgroundStillAnimationMode.Zoom300Percent:
                float scale = Mathf.Lerp(1f, 3f, zoomU);
                rt.localScale = new Vector3(scale, scale, 1f);
                break;
        }
    }
}
