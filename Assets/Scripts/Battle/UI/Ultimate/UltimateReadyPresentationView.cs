using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// Drives the UltimateReady prefab timeline (box expand, text reveal, clone READY motion).
/// </summary>
public sealed class UltimateReadyPresentationView : MonoBehaviour
{
    public const float BoxWidth = 1080f;
    public const float BoxStartHeight = 1f;
    public const float BoxEndHeight = 300f;

    [Header("Refs (auto-resolved when empty)")]
    [SerializeField] private RectTransform boxRect;
    [SerializeField] private RectTransform ultimateRect;
    [SerializeField] private RectTransform readyRect;
    [SerializeField] private RectTransform readyUp1;
    [SerializeField] private RectTransform readyUp2;
    [SerializeField] private RectTransform readyDown1;
    [SerializeField] private RectTransform readyDown2;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("Timing")]
    [SerializeField] private float boxExpandDuration = 0.45f;
    [SerializeField] private float cloneDelayAfterMainSeconds = 0.2f;
    [SerializeField] private float cloneMoveDuration = 0.4f;
    [SerializeField] private float boxHoldDurationSeconds = 3f;
    [SerializeField] private float fadeOutDuration = 0.45f;
    [SerializeField] private LeanTweenType expandEase = LeanTweenType.easeOutCubic;
    [SerializeField] private LeanTweenType cloneMoveEase = LeanTweenType.easeOutCubic;
    [SerializeField] private LeanTweenType fadeOutEase = LeanTweenType.easeOutCubic;

    private Vector2 _readyUp1Target;
    private Vector2 _readyUp2Target;
    private Vector2 _readyDown1Target;
    private Vector2 _readyDown2Target;
    private bool _targetsCached;

    private void Awake()
    {
        ResolveRefs();
        CacheTargetPositions();
    }

    public async Task PlayAsync(CancellationToken ct)
    {
        ResolveRefs();
        CacheTargetPositions();
        ResetVisualState();

        float startTime = Time.unscaledTime;

        var expandTask = AnimateBoxHeightAsync(BoxStartHeight, BoxEndHeight, boxExpandDuration, ct);
        ShowMainTexts();
        await expandTask;
        ct.ThrowIfCancellationRequested();

        if (cloneDelayAfterMainSeconds > 0f)
            await Task.Delay(TimeSpan.FromSeconds(cloneDelayAfterMainSeconds), ct);

        BattleUIManager.I?.PlayFullscreenWhiteFlashMs(UltimateReadyPresentation.PreShowWhiteFlashMs);
        SoundEffectPlayer.I?.Play(UltimateReadyPresentation.SoundEffectPath);
        BattleManager.I?.ReleaseUltimateReadyPlayerSummonGlow();
        await AnimateCloneReadiesAsync(ct);

        float elapsed = Time.unscaledTime - startTime;
        float remaining = boxHoldDurationSeconds - elapsed;
        if (remaining > 0f)
            await Task.Delay(TimeSpan.FromSeconds(remaining), ct);

        await FadeOutAsync(ct);
    }

    private CanvasGroup ResolveRootCanvasGroup()
    {
        if (rootCanvasGroup != null)
            return rootCanvasGroup;

        rootCanvasGroup = GetComponent<CanvasGroup>();
        if (rootCanvasGroup == null)
            rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        return rootCanvasGroup;
    }

    private async Task FadeOutAsync(CancellationToken ct)
    {
        var group = ResolveRootCanvasGroup();
        if (group == null || fadeOutDuration <= 0f)
            return;

        await LeanTweenValueFloatAsync(
            gameObject,
            a => group.alpha = a,
            group.alpha,
            0f,
            fadeOutDuration,
            fadeOutEase,
            ct);

        group.alpha = 0f;
    }

    private void ResolveRefs()
    {
        if (boxRect == null) boxRect = transform.Find("Box") as RectTransform;
        if (ultimateRect == null) ultimateRect = transform.Find("Ultimate") as RectTransform;
        if (readyRect == null) readyRect = transform.Find("Ready") as RectTransform;
        if (readyUp1 == null) readyUp1 = transform.Find("ReadyUp1") as RectTransform;
        if (readyUp2 == null) readyUp2 = transform.Find("ReadyUp2") as RectTransform;
        if (readyDown1 == null) readyDown1 = transform.Find("ReadyDown1") as RectTransform;
        if (readyDown2 == null) readyDown2 = transform.Find("ReadyDown2") as RectTransform;
    }

    private void CacheTargetPositions()
    {
        if (_targetsCached) return;

        _readyUp1Target = readyUp1 != null ? readyUp1.anchoredPosition : Vector2.zero;
        _readyUp2Target = readyUp2 != null ? readyUp2.anchoredPosition : Vector2.zero;
        _readyDown1Target = readyDown1 != null ? readyDown1.anchoredPosition : Vector2.zero;
        _readyDown2Target = readyDown2 != null ? readyDown2.anchoredPosition : Vector2.zero;
        _targetsCached = true;
    }

    private void ResetVisualState()
    {
        var group = ResolveRootCanvasGroup();
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        if (boxRect != null)
            boxRect.sizeDelta = new Vector2(BoxWidth, BoxStartHeight);

        SetTextAlpha(ultimateRect, 0f);
        SetTextAlpha(readyRect, 0f);

        Vector2 readyCenter = readyRect != null ? readyRect.anchoredPosition : Vector2.zero;
        PrepareClone(readyUp1, readyCenter);
        PrepareClone(readyUp2, readyCenter);
        PrepareClone(readyDown1, readyCenter);
        PrepareClone(readyDown2, readyCenter);
    }

    private static void PrepareClone(RectTransform rect, Vector2 startPos)
    {
        if (rect == null) return;
        rect.anchoredPosition = startPos;
        SetTextAlpha(rect, 0f);
    }

    private void ShowMainTexts()
    {
        SetTextAlpha(ultimateRect, 1f);
        SetTextAlpha(readyRect, 1f);
    }

    private async Task AnimateBoxHeightAsync(float from, float to, float duration, CancellationToken ct)
    {
        if (boxRect == null)
        {
            if (duration > 0f)
                await Task.Delay(TimeSpan.FromSeconds(duration), ct);
            return;
        }

        await LeanTweenValueFloatAsync(
            gameObject,
            h => boxRect.sizeDelta = new Vector2(BoxWidth, h),
            from,
            to,
            duration,
            expandEase,
            ct);
    }

    private async Task AnimateCloneReadiesAsync(CancellationToken ct)
    {
        var tasks = new[]
        {
            AnimateCloneAsync(readyUp1, _readyUp1Target, ct),
            AnimateCloneAsync(readyUp2, _readyUp2Target, ct),
            AnimateCloneAsync(readyDown1, _readyDown1Target, ct),
            AnimateCloneAsync(readyDown2, _readyDown2Target, ct),
        };
        await Task.WhenAll(tasks);
    }

    private async Task AnimateCloneAsync(RectTransform rect, Vector2 target, CancellationToken ct)
    {
        if (rect == null) return;

        var tmp = rect.GetComponent<TMP_Text>();
        float startY = rect.anchoredPosition.y;
        float endY = target.y;

        await LeanTweenValueFloatAsync(
            rect.gameObject,
            t =>
            {
                float y = Mathf.Lerp(startY, endY, t);
                rect.anchoredPosition = new Vector2(target.x, y);
                if (tmp != null)
                    tmp.alpha = t;
            },
            0f,
            1f,
            cloneMoveDuration,
            cloneMoveEase,
            ct);

        rect.anchoredPosition = target;
        if (tmp != null)
            tmp.alpha = 1f;
    }

    private static void SetTextAlpha(RectTransform rect, float alpha)
    {
        if (rect == null) return;
        var tmp = rect.GetComponent<TMP_Text>();
        if (tmp != null)
            tmp.alpha = alpha;
    }

    private static async Task LeanTweenValueFloatAsync(
        GameObject go,
        Action<float> onUpdate,
        float from,
        float to,
        float time,
        LeanTweenType ease,
        CancellationToken ct)
    {
        if (go == null || onUpdate == null) return;
        if (time < 0.0001f)
        {
            onUpdate(to);
            return;
        }

        onUpdate(from);
        var tcs = new TaskCompletionSource<bool>();
        var reg = ct.Register(() =>
        {
            if (go != null) LeanTween.cancel(go);
            tcs.TrySetCanceled();
        });

        try
        {
            LeanTween.value(go, onUpdate, from, to, time)
                .setEase(ease)
                .setIgnoreTimeScale(true)
                .setOnComplete(() =>
                {
                    if (!tcs.Task.IsCompleted)
                        tcs.TrySetResult(true);
                });
            await tcs.Task.ConfigureAwait(true);
        }
        finally
        {
            reg.Dispose();
        }

        onUpdate(to);
    }
}
