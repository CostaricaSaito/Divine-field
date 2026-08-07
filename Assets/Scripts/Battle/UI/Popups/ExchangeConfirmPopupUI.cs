using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Post-exchange confirmation popup: shows before values, counts HP/MP/GP to after values, then auto-closes via slider.
/// </summary>
public sealed class ExchangeConfirmPopupUI : MonoBehaviour
{
    private const string SeRouletteLoop = "Assets/SE/電子ルーレット回転中.mp3";
    private const string SeRouletteStop = "Assets/SE/電子ルーレット停止ボタンを押す.mp3";

    private const float InitialIntervalSeconds = 1f;
    private const float StatCountSeconds = 0.5f;
    private const float BetweenStatIntervalSeconds = 0.5f;
    private const float CloseProgressSeconds = 3.5f;
    private const float AfterCloseProgressIntervalSeconds = 1f;

    [SerializeField] private TMP_Text hpValueText;
    [SerializeField] private TMP_Text mpValueText;
    [SerializeField] private TMP_Text gpValueText;
    [SerializeField] private Slider closeProgressSlider;
    [SerializeField] private GameObject progress1;
    [SerializeField] private GameObject progress2;
    [SerializeField] private GameObject progress3;

    public async Task PlayConfirmSequenceAsync(
        int beforeHp, int beforeMp, int beforeGp,
        int afterHp, int afterMp, int afterGp,
        CancellationToken cancellationToken = default)
    {
        PrepareInitialDisplay(beforeHp, beforeMp, beforeGp);

        await DelaySecondsAsync(InitialIntervalSeconds, cancellationToken);

        await AnimateStatIfChangedAsync(hpValueText, beforeHp, afterHp, progress1, cancellationToken);
        await DelaySecondsAsync(BetweenStatIntervalSeconds, cancellationToken);

        await AnimateStatIfChangedAsync(mpValueText, beforeMp, afterMp, progress2, cancellationToken);
        await DelaySecondsAsync(BetweenStatIntervalSeconds, cancellationToken);

        await AnimateStatIfChangedAsync(gpValueText, beforeGp, afterGp, progress3, cancellationToken);

        await AnimateCloseProgressAsync(cancellationToken);
        await DelaySecondsAsync(AfterCloseProgressIntervalSeconds, cancellationToken);
    }

    private void PrepareInitialDisplay(int hp, int mp, int gp)
    {
        SetStatText(hpValueText, hp);
        SetStatText(mpValueText, mp);
        SetStatText(gpValueText, gp);
        SetProgressVisible(progress1, false);
        SetProgressVisible(progress2, false);
        SetProgressVisible(progress3, false);

        if (closeProgressSlider != null)
        {
            closeProgressSlider.minValue = 0f;
            closeProgressSlider.maxValue = 1f;
            closeProgressSlider.value = 0f;
        }
    }

    private async Task AnimateStatIfChangedAsync(
        TMP_Text text,
        int from,
        int to,
        GameObject progressMarker,
        CancellationToken ct)
    {
        if (from == to) return;

        try
        {
            if (SoundEffectPlayer.I != null)
                await SoundEffectPlayer.I.StartLoopingAsync(SeRouletteLoop);
        }
        catch (Exception e)
        {
            Debug.LogWarning(e.Message);
        }

        try
        {
            await CountUpTextAsync(text, from, to, StatCountSeconds, ct);
        }
        finally
        {
            SoundEffectPlayer.I?.StopLooping();
        }

        SoundEffectPlayer.I?.Play(SeRouletteStop);
        SetProgressVisible(progressMarker, true);
    }

    private static async Task CountUpTextAsync(
        TMP_Text target,
        int from,
        int to,
        float seconds,
        CancellationToken ct)
    {
        if (target == null) return;

        float dur = Mathf.Max(0.01f, seconds);
        float elapsed = 0f;
        target.text = from.ToString();

        while (elapsed < dur)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            int current = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            target.text = current.ToString();
            await Task.Yield();
        }

        target.text = to.ToString();
    }

    private async Task AnimateCloseProgressAsync(CancellationToken ct)
    {
        float elapsed = 0f;
        if (closeProgressSlider != null)
            closeProgressSlider.value = 0f;

        while (elapsed < CloseProgressSeconds)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / CloseProgressSeconds);
            if (closeProgressSlider != null)
                closeProgressSlider.value = t;
            await Task.Yield();
        }

        if (closeProgressSlider != null)
            closeProgressSlider.value = 1f;
    }

    private static void SetStatText(TMP_Text text, int value)
    {
        if (text != null) text.text = value.ToString();
    }

    private static void SetProgressVisible(GameObject go, bool visible)
    {
        if (go != null) go.SetActive(visible);
    }

    private static Task DelaySecondsAsync(float seconds, CancellationToken ct)
    {
        int ms = Mathf.Max(0, Mathf.RoundToInt(seconds * 1000f));
        return Task.Delay(ms, ct);
    }
}
