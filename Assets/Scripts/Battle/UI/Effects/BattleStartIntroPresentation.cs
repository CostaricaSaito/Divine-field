using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Battle-start intro timeline.
///
/// t=0.00  white flash + SE + diagonal band opens (0.05s) + both captions slide in (2s)
///         + "一閃(白縦)" additive video
/// t=0.10  "斬撃" additive video
/// t=0.30  "斬撃" again, flipped upside down
/// t=2.50  captions exit (JA right / EN left, 0.25s), band narrows and fades out (0.5s)
/// </summary>
public static class BattleStartIntroPresentation
{
    public const string StartSeAddress = "Assets/SE/アルティメットレディ.mp3";
    public const string FlashVideoAddress = "Assets/Videos/\u4E00\u9583(\u767D\u7E26).mp4";
    public const string SlashVideoAddress = "Assets/Videos/赤斬撃.mp4";

    private const int FirstSlashDelayMs = 100;
    private const int FlippedSlashDelayMs = 300;

    public static async Task RunAsync(CancellationToken ct)
    {
        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
        if (canvas == null)
        {
            Debug.LogWarning("[BattleStartIntroPresentation] Main UI canvas is missing.");
            return;
        }

        // Everything is loaded and prepared first so all cues land on time.
        BattleStartIntroVideoOverlay flash = null;
        BattleStartIntroVideoOverlay slash = null;
        BattleStartIntroVideoOverlay flippedSlash = null;
        BattleStartIntroView view = null;
        bool overlaysScheduled = false;

        try
        {
            var captionFont = await BattleStartIntroView.LoadCaptionFontAsync(ct);
            if (captionFont == null)
            {
                Debug.LogWarning("[BattleStartIntroPresentation] Caption font is missing.");
                return;
            }

            view = BattleStartIntroView.Build(canvas, captionFont);
            if (view == null)
                return;

            var flashClip = await BattleStartIntroVideoOverlay.LoadClipAsync(FlashVideoAddress, ct);
            var slashClip = await BattleStartIntroVideoOverlay.LoadClipAsync(SlashVideoAddress, ct);

            flash = await BattleStartIntroVideoOverlay.PrepareAsync(canvas, flashClip, false, ct);
            slash = await BattleStartIntroVideoOverlay.PrepareAsync(canvas, slashClip, false, ct);
            flippedSlash = await BattleStartIntroVideoOverlay.PrepareAsync(canvas, slashClip, true, ct);

            // Bake dynamic glyphs first, then swap to outline-only materials.
            await view.WarmupCaptionCharactersAsync(ct);
            view.ApplyOutlinedTextMaterials();

            SoundEffectPlayer.I?.Play(StartSeAddress);
            view.PlayDimBackground(ct);
            view.PlayWhiteFlash(ct);
            _ = PlayOverlayAsync(flash, 0, ct);
            _ = PlayOverlayAsync(slash, FirstSlashDelayMs, ct);
            _ = PlayOverlayAsync(flippedSlash, FlippedSlashDelayMs, ct);
            overlaysScheduled = true;

            await Task.WhenAll(view.OpenBandAsync(ct), view.SlideInCaptionsAsync(ct));
            await Task.Delay(TimeSpan.FromSeconds(BattleStartIntroView.CaptionHoldSeconds), ct);
            await Task.WhenAll(view.ExitCaptionsAsync(ct), view.CloseBandAsync(ct));
        }
        finally
        {
            view?.Dispose();
            if (!overlaysScheduled)
            {
                // Aborted before the cues fired: nothing else will clean these up.
                flash?.Dispose();
                slash?.Dispose();
                flippedSlash?.Dispose();
            }
        }
    }

    /// <summary>Fire-and-forget: waits for the cue, plays once, then disposes the overlay.</summary>
    private static async Task PlayOverlayAsync(BattleStartIntroVideoOverlay overlay, int delayMs, CancellationToken ct)
    {
        if (overlay == null)
            return;

        try
        {
            if (delayMs > 0)
                await Task.Delay(delayMs, ct);
            await overlay.PlayAsync(ct);
        }
        catch (OperationCanceledException)
        {
            overlay.Dispose();
        }
    }

}
