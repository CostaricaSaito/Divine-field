using System.Threading;
using System.Threading.Tasks;
using Coffee.UIEffects;
using TMPro;
using UnityEngine;

/// <summary>
/// Drives <see cref="Assets/BattleBGM.prefab"/> slide-in, hold, and fade-out at battle start.
/// </summary>
public sealed class BattleBgmTitleView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private RectTransform backImage;
    [SerializeField] private float slideInSeconds = 0.5f;
    [SerializeField] private float holdSeconds = 3f;
    [SerializeField] private float fadeOutSeconds = 1f;
    [SerializeField] private float slideInOffsetX = 600f;
    [Tooltip("BGMBack のスライド開始 X（anchoredPosition.x）。左画面外から入る。")]
    [SerializeField] private float backSlideInStartX = -1100f;
    [Header("Post slide-in nudge")]
    [SerializeField] private float postSlideNudgePixels = 30f;
    [SerializeField] private float postSlideNudgeSeconds = 0.4f;

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        if (titleText == null)
            titleText = transform.Find("BGMTitle")?.GetComponent<TMP_Text>();

        if (titleRect == null && titleText != null)
            titleRect = titleText.rectTransform;

        if (backImage == null)
        {
            var backTransform = transform.Find("BGMBack") ?? transform.Find("BGMback");
            backImage = backTransform as RectTransform;
        }

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public async Task PlayAsync(string trackTitle, CancellationToken ct = default)
    {
        if (titleRect == null && backImage == null) return;

        if (titleText != null)
            titleText.text = string.IsNullOrEmpty(trackTitle) ? "BGM" : $"BGM : {trackTitle}";

        DisableUiEffectTweeners();
        _canvasGroup.alpha = 1f;

        await WaitForNextFrameAsync(ct);

        var slideTasks = BuildSlideInTasks(ct);
        if (slideTasks.Length == 0) return;

        await Task.WhenAll(slideTasks);
        if (ct.IsCancellationRequested) return;

        _ = AnimatePostSlideNudgeAsync(ct);

        await Task.Delay(Mathf.RoundToInt(holdSeconds * 1000f), ct);
        if (ct.IsCancellationRequested) return;

        await AnimateFadeOutAsync(ct);
        if (ct.IsCancellationRequested) return;

        Destroy(gameObject);
    }

    private Task[] BuildSlideInTasks(CancellationToken ct)
    {
        int count = 0;
        if (titleRect != null) count++;
        if (backImage != null) count++;
        if (count == 0) return System.Array.Empty<Task>();

        var tasks = new Task[count];
        int index = 0;

        if (titleRect != null)
        {
            var targetPos = titleRect.anchoredPosition;
            titleRect.anchoredPosition = targetPos + new Vector2(slideInOffsetX, 0f);
            tasks[index++] = AnimatePositionAsync(titleRect, targetPos, slideInSeconds, EaseInCubic, ct);
        }

        if (backImage != null)
        {
            var targetPos = backImage.anchoredPosition;
            var startPos = new Vector2(backSlideInStartX, targetPos.y);
            backImage.anchoredPosition = startPos;
            tasks[index++] = AnimatePositionAsync(backImage, targetPos, slideInSeconds, EaseInCubic, ct);
        }

        return tasks;
    }

    private async Task AnimatePostSlideNudgeAsync(CancellationToken ct)
    {
        if (postSlideNudgePixels <= 0f || postSlideNudgeSeconds <= 0f) return;

        int count = 0;
        if (titleRect != null) count++;
        if (backImage != null) count++;
        if (count == 0) return;

        var tasks = new Task[count];
        int index = 0;

        if (titleRect != null)
        {
            var startPos = titleRect.anchoredPosition;
            var targetPos = startPos + new Vector2(-postSlideNudgePixels, 0f);
            tasks[index++] = AnimatePositionAsync(titleRect, targetPos, postSlideNudgeSeconds, EaseOutCubic, ct);
        }

        if (backImage != null)
        {
            var startPos = backImage.anchoredPosition;
            var targetPos = startPos + new Vector2(postSlideNudgePixels, 0f);
            tasks[index++] = AnimatePositionAsync(backImage, targetPos, postSlideNudgeSeconds, EaseOutCubic, ct);
        }

        await Task.WhenAll(tasks);
    }

    private static async Task AnimatePositionAsync(
        RectTransform rect,
        Vector2 targetPos,
        float durationSeconds,
        System.Func<float, float> ease,
        CancellationToken ct)
    {
        if (rect == null) return;

        float duration = Mathf.Max(0.05f, durationSeconds);
        var startPos = rect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, ease(t));
            await WaitForNextFrameAsync(ct);
        }

        rect.anchoredPosition = targetPos;
    }

    private async Task AnimateFadeOutAsync(CancellationToken ct)
    {
        float duration = Mathf.Max(0.05f, fadeOutSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            await WaitForNextFrameAsync(ct);
        }

        _canvasGroup.alpha = 0f;
    }

    private static async Task WaitForNextFrameAsync(CancellationToken ct)
    {
        int frame = Time.frameCount;
        while (Time.frameCount == frame)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    private void DisableUiEffectTweeners()
    {
        var tweeners = GetComponentsInChildren<UIEffectTweener>(true);
        for (int i = 0; i < tweeners.Length; i++)
            tweeners[i].enabled = false;
    }

    private static float EaseInCubic(float t) => t * t * t;

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }
}
