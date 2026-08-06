using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CardDisplayPanel-centered styled message popup (float up + fade).
/// Text is single-line and horizontally compressed to fit <see cref="MaxTextWidthPx"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class MessagePopup : MonoBehaviour
{
    public const float MaxTextWidthPx = 450f;
    public const float PanelWidthPx = 500f;
    public const float PanelHeightPx = 200f;

    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private float floatSpeed = 30f;
    [SerializeField] private float fadeDuration = 1f;

    private CanvasGroup _canvasGroup;
    private float _timer;
    private PopupRunMode _runMode = PopupRunMode.Normal;
    private float _diseasePhase1Duration;
    private TaskCompletionSource<bool> _diseasePhase1Tcs;

    private Sprite _defaultBackgroundSprite;
    private Image.Type _defaultImageType;
    private float _defaultPixelsPerUnitMultiplier;
    private bool _defaultBackgroundCached;

    private enum PopupRunMode
    {
        Normal,
        DiseaseWorsenPhase1Float,
        DiseaseWorsenSequenceManual,
    }

    public float FadeDuration => fadeDuration;

    private void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        if (messageText == null)
            messageText = GetComponentInChildren<TMP_Text>(true);

        CacheDefaultBackgroundState();
    }

    private void CacheDefaultBackgroundState()
    {
        if (backgroundImage == null || _defaultBackgroundCached) return;
        _defaultBackgroundSprite = backgroundImage.sprite;
        _defaultImageType = backgroundImage.type;
        _defaultPixelsPerUnitMultiplier = backgroundImage.pixelsPerUnitMultiplier;
        _defaultBackgroundCached = true;
    }

    private void Start()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Setup(MessagePopupStyleEntry entry)
    {
        entry = MessagePopupSettings.ResolveRuntimeFields(entry);
        ApplyBackground(entry);
        ApplyText(entry.message, entry.textColor, entry.outlineColor);
    }

    public void Setup(string message, Color backgroundColor, Color textColor, Color outlineColor)
    {
        ApplyBackground(new MessagePopupStyleEntry
        {
            backgroundMode = MessagePopupBackgroundMode.SolidColor,
            backgroundColor = backgroundColor,
        });
        ApplyText(message, textColor, outlineColor);
    }

    private void ApplyBackground(MessagePopupStyleEntry entry)
    {
        if (backgroundImage == null) return;
        CacheDefaultBackgroundState();

        if (entry.UsesSpriteBackground)
        {
            backgroundImage.sprite = entry.backgroundSprite;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = false;
            backgroundImage.color = Color.white;
            return;
        }

        if (_defaultBackgroundSprite != null)
            backgroundImage.sprite = _defaultBackgroundSprite;
        backgroundImage.type = _defaultImageType;
        backgroundImage.pixelsPerUnitMultiplier = _defaultPixelsPerUnitMultiplier;
        backgroundImage.preserveAspect = false;
        backgroundImage.color = entry.backgroundColor.a > 0.001f
            ? entry.backgroundColor
            : Color.white;
    }

    private void ApplyText(string message, Color textColor, Color outlineColor)
    {
        if (messageText == null) return;

        messageText.richText = false;
        messageText.text = StripLineBreaks(message);
        messageText.fontStyle = FontStyles.Bold;
        messageText.color = textColor;
        messageText.outlineWidth = Mathf.Max(messageText.outlineWidth, 0.22f);
        messageText.outlineColor = outlineColor;
        messageText.enableWordWrapping = false;
        messageText.overflowMode = TextOverflowModes.Overflow;
        messageText.enableAutoSizing = false;
        messageText.alignment = TextAlignmentOptions.Center;

        ApplySingleLineHorizontalFit(messageText, MaxTextWidthPx);
    }

    public static void ApplySingleLineHorizontalFit(TMP_Text text, float maxWidthPx)
    {
        if (text == null) return;

        var rt = text.rectTransform;
        rt.localScale = Vector3.one;
        text.ForceMeshUpdate();

        float preferred = text.preferredWidth;
        if (preferred > maxWidthPx && preferred > 0.01f)
            rt.localScale = new Vector3(maxWidthPx / preferred, 1f, 1f);
    }

    public Task BeginDiseaseWorsenPhase1AndGetTask(MessagePopupStyleEntry entry, float phase1FloatSeconds)
    {
        EnsureDiseaseReelClippingMask();
        Setup(entry);
        _runMode = PopupRunMode.DiseaseWorsenPhase1Float;
        _timer = 0f;
        _diseasePhase1Duration = Mathf.Max(0.02f, phase1FloatSeconds);
        _diseasePhase1Tcs = new TaskCompletionSource<bool>();
        return _diseasePhase1Tcs.Task;
    }

    public Task RunDiseaseReelSecondLinePostIntervalAndDestroyAsync(
        MessagePopupStyleEntry secondLineEntry,
        float reelDurationSeconds,
        float postIntervalSeconds,
        CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(CoDiseaseReelSecondThenIntervalDestroy(
            secondLineEntry, reelDurationSeconds, postIntervalSeconds, ct, tcs));
        return tcs.Task;
    }

    private void EnsureDiseaseReelClippingMask()
    {
        if (GetComponent<RectMask2D>() != null) return;
        gameObject.AddComponent<RectMask2D>();
    }

    private IEnumerator CoDiseaseReelSecondThenIntervalDestroy(
        MessagePopupStyleEntry secondLineEntry,
        float reelDurationSeconds,
        float postIntervalSeconds,
        CancellationToken ct,
        TaskCompletionSource<bool> tcs)
    {
        EnsureDiseaseReelClippingMask();
        _runMode = PopupRunMode.DiseaseWorsenSequenceManual;

        if (messageText == null)
        {
            Setup(secondLineEntry);
            yield return WaitForSecondsOrCancel(postIntervalSeconds, ct);
            tcs?.TrySetResult(true);
            Destroy(gameObject);
            yield break;
        }

        var rt = messageText.rectTransform;
        Vector2 basePos = rt.anchoredPosition;
        float half = Mathf.Max(0.04f, reelDurationSeconds * 0.5f);
        float el = 0f;
        while (el < half)
        {
            if (ct.IsCancellationRequested) { tcs?.TrySetCanceled(); Destroy(gameObject); yield break; }
            el += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(el / half);
            rt.anchoredPosition = basePos + Vector2.down * (72f * p);
            yield return null;
        }

        Setup(secondLineEntry);
        rt.anchoredPosition = basePos + Vector2.up * 72f;
        el = 0f;
        while (el < half)
        {
            if (ct.IsCancellationRequested) { tcs?.TrySetCanceled(); Destroy(gameObject); yield break; }
            el += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(el / half);
            rt.anchoredPosition = Vector2.Lerp(basePos + Vector2.up * 72f, basePos, p);
            yield return null;
        }
        rt.anchoredPosition = basePos;

        yield return WaitForSecondsOrCancel(postIntervalSeconds, ct);
        if (ct.IsCancellationRequested) { tcs?.TrySetCanceled(); Destroy(gameObject); yield break; }

        tcs?.TrySetResult(true);
        Destroy(gameObject);
    }

    private static IEnumerator WaitForSecondsOrCancel(float seconds, CancellationToken ct)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (ct.IsCancellationRequested) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static string StripLineBreaks(string message)
    {
        if (string.IsNullOrEmpty(message)) return string.Empty;
        return message.Replace("\r\n", string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private void Update()
    {
        if (_runMode == PopupRunMode.DiseaseWorsenPhase1Float)
        {
            _timer += Time.deltaTime;
            transform.Translate(Vector3.up * (floatSpeed * Time.deltaTime));
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;

            if (_timer >= _diseasePhase1Duration)
            {
                floatSpeed = 0f;
                _runMode = PopupRunMode.DiseaseWorsenSequenceManual;
                _diseasePhase1Tcs?.TrySetResult(true);
                _diseasePhase1Tcs = null;
            }
            return;
        }

        if (_runMode == PopupRunMode.DiseaseWorsenSequenceManual)
            return;

        _timer += Time.deltaTime;
        transform.Translate(Vector3.up * (floatSpeed * Time.deltaTime));

        if (_canvasGroup != null)
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, _timer / fadeDuration);

        if (_timer >= fadeDuration)
            Destroy(gameObject);
    }
}
