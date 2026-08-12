using UnityEngine;

/// <summary>
/// Windows/macOS standalone: open a portrait 9:16 window that fits the display.
/// A literal 1080x1920 window cannot fit on typical 1080p monitors and gets clipped;
/// this picks the largest fitting size and keeps the Title/Main UI aspect correct.
/// Mobile builds are unaffected.
/// </summary>
public static class StandalonePortraitBootstrap
{
    const float PortraitAspect = 1080f / 1920f; // width / height
    const int MarginPixels = 96; // title bar + taskbar breathing room

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    static void ApplyBeforeSplash() => ApplyPortraitWindow("BeforeSplashScreen");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyAfterSceneLoad() => ApplyPortraitWindow("AfterSceneLoad");

    static void ApplyPortraitWindow(string phase)
    {
#if UNITY_STANDALONE || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
        if (!TryComputeFittedPortraitSize(out int width, out int height))
            return;

        bool alreadyOk = Screen.width == width
            && Screen.height == height
            && Screen.fullScreenMode == FullScreenMode.Windowed;
        if (alreadyOk)
            return;

        Debug.Log(
            $"[StandalonePortraitBootstrap] {phase}: SetResolution {width}x{height} Windowed "
            + $"(display {Screen.currentResolution.width}x{Screen.currentResolution.height}, "
            + $"was {Screen.width}x{Screen.height} mode={Screen.fullScreenMode})");

        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(width, height, FullScreenMode.Windowed);
#endif
    }

    static bool TryComputeFittedPortraitSize(out int width, out int height)
    {
        width = 0;
        height = 0;

        var display = Screen.currentResolution;
        int maxWidth = Mathf.Max(320, display.width - MarginPixels);
        int maxHeight = Mathf.Max(568, display.height - MarginPixels);

        // Prefer max height, then clamp width to display.
        height = maxHeight;
        width = Mathf.RoundToInt(height * PortraitAspect);
        if (width > maxWidth)
        {
            width = maxWidth;
            height = Mathf.RoundToInt(width / PortraitAspect);
        }

        // Keep even sizes (some GPUs prefer it).
        width -= width % 2;
        height -= height % 2;

        return width >= 320 && height >= 568;
    }
}
