using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Title 用：2 枚以上のスライドをパン切替。クロスフェード中は手前がフェードアウトしつつパン、
/// 次画像はフェードインと同時にパンを開始（LeanTween のみ）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public sealed class TitleBackgroundPanController : MonoBehaviour
{
    private const int MinSlides = 2;

    [Header("表示")]
    [Tooltip("手前（パンさせる）Image。未指定なら同じ GameObject。")]
    [SerializeField] private Image image;

    [Tooltip("上に重ねる Image（メインの Image と別 GameObject 必須）。未指定・同一オブジェクト指定時はクロスフェードしません。")]
    [SerializeField] private Image crossFadeImage;

    [Tooltip("窓。未指定なら親の RectTransform。RectMask2D を付ける想定。")]
    [SerializeField] private RectTransform clipViewport;

    [Tooltip("スライドの backgroundSize が (0,0) のときの既定サイズ。")]
    [SerializeField] private Vector2 backgroundSize = new Vector2(3413f, 2200f);

    [Header("フェードイン（開始時・メイン Image のみ）")]
    [SerializeField] [Min(0f)] private float fadeInFromBlackDuration = 1.5f;

    [Header("クロスフェード")]
    [Tooltip("オン＋Cross Fade Image 割当時のみ、手前画像をフェードアウトしながら次画像をフェードインします。オフなら瞬間切り替えです。")]
    [SerializeField] private bool useCrossFade = true;

    [FormerlySerializedAs("crossFadeLeadSeconds")]
    [Tooltip("いまのパン 1 レグが終わる「何秒前」から、手前画像のフェードアウトと次画像のフェードインを同時に始めます。" +
             "フェードに使える時間は、その瞬間からレグ終了まで（＝レグが十分長ければこの秒数と同じ）。レグが短いときはレグ全長になります。")]
    [SerializeField] [Min(0f)] private float crossFadeStartBeforeEndSeconds = 5f;

    [Tooltip("クロスフェード中の不透明度のカーブ。通常はそのままでよいです。")]
    [SerializeField] private LeanTweenType crossFadeEase = LeanTweenType.easeInOutQuad;

    [Header("スライド（2 枚以上必須）")]
    [SerializeField] private List<TitleBackgroundPanSlide> slides = new();

    [SerializeField] private TitleBackgroundSlideAdvanceMode slideAdvanceMode = TitleBackgroundSlideAdvanceMode.AfterFullRoundTrip;

    [Header("共通（パン移動）")]
    [Tooltip("スライドの片道時間が 0 以下のときの既定（秒）。")]
    [SerializeField] [Min(0.01f)] private float defaultOneWayDurationSeconds = 48f;

    [Tooltip("各スライドの「位置パン」Start→End のイージングの既定。スライドで Override Ease をオンにするとそちらが優先。クロスフェードとは無関係。")]
    [SerializeField] private LeanTweenType defaultEaseType = LeanTweenType.easeInOutQuad;

    [Tooltip("オフなら Start→End のみ。AfterFullRoundTrip では End→Start の後に次画像へ。")]
    [SerializeField] private bool pingPong = true;

    [SerializeField] private bool useUnscaledTime = true;

    private RectTransform _imgRt;
    private RectTransform _crossRt;
    private int _slideIndex;

    private bool _crossFadeActiveForCurrentLeg;
    private float _crossFadeOverlapSecondsForLeg;
    private int _crossFadeTargetSlideIndex;
    private GameObject _crossFadeAlphaDriver;

    private void Reset()
    {
        image = GetComponent<Image>();
        if (image != null) image.preserveAspect = false;
    }

    private void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        _imgRt = image != null ? image.rectTransform : null;
        _crossRt = crossFadeImage != null ? crossFadeImage.rectTransform : null;

        if (clipViewport == null && transform.parent != null)
            clipViewport = transform.parent as RectTransform;

        if (clipViewport != null && clipViewport.GetComponent<RectMask2D>() == null)
            clipViewport.gameObject.AddComponent<RectMask2D>();

        ApplyImageLayout();
        HideCrossFadeLayerVisual();
        EnsureCrossFadeAlphaDriver();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || _imgRt == null || image == null) return;

        if (slides == null || slides.Count < MinSlides)
        {
            Debug.LogWarning($"[TitleBackgroundPanController] slides は {MinSlides} 枚以上設定してください。", this);
            return;
        }

        LeanTween.init();
        ApplyImageLayout();
        HideCrossFadeLayerVisual();
        Canvas.ForceUpdateCanvases();

        CancelBackgroundTweens();
        _slideIndex = 0;

        ApplySlideSpriteAndLayout();

        var start = CurrentPanStart();
        _imgRt.anchoredPosition = start;

        image.color = Color.black;

        if (fadeInFromBlackDuration > 0.0001f)
        {
            var cTw = LeanTween.value(gameObject, 0f, 1f, fadeInFromBlackDuration)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnUpdate((float u) =>
                {
                    if (image != null)
                        image.color = Color.Lerp(Color.black, Color.white, u);
                })
                .setOnComplete(() =>
                {
                    if (image != null) image.color = Color.white;
                });
            if (useUnscaledTime) cTw.setIgnoreTimeScale(true);
        }
        else
            image.color = Color.white;

        PlayFromCurrentSlide();
    }

    private void OnDisable()
    {
        CancelBackgroundTweens();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (image == null) image = GetComponent<Image>();
        if (image != null) image.preserveAspect = false;
        if (crossFadeImage != null) crossFadeImage.preserveAspect = false;
    }
#endif

    private void CancelBackgroundTweens()
    {
        LeanTween.cancel(gameObject);
        if (_crossFadeAlphaDriver != null) LeanTween.cancel(_crossFadeAlphaDriver);
        if (_imgRt != null) LeanTween.cancel(_imgRt.gameObject);
        if (_crossRt != null) LeanTween.cancel(_crossRt.gameObject);
    }

    private void EnsureCrossFadeAlphaDriver()
    {
        if (_crossFadeAlphaDriver != null) return;

        _crossFadeAlphaDriver = new GameObject("CrossFadeAlphaDriver", typeof(RectTransform));
        _crossFadeAlphaDriver.transform.SetParent(transform, false);
        _crossFadeAlphaDriver.SetActive(true);
        var rt = _crossFadeAlphaDriver.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private void HideCrossFadeLayerVisual()
    {
        if (crossFadeImage == null) return;
        crossFadeImage.raycastTarget = false;
        var c = crossFadeImage.color;
        c.r = c.g = c.b = 1f;
        c.a = 0f;
        crossFadeImage.color = c;
    }

    private void ApplySlideSpriteAndLayout()
    {
        if (image == null || slides == null || slides.Count == 0) return;

        var s = slides[_slideIndex % slides.Count];
        if (s.sprite != null)
            image.sprite = s.sprite;
        ApplyImageLayout();
    }

    private Vector2 CurrentPanStart() => slides[_slideIndex % slides.Count].panAnchoredStart;

    private Vector2 CurrentPanEnd() => slides[_slideIndex % slides.Count].panAnchoredEnd;

    private float CurrentOneWayDuration()
    {
        var d = slides[_slideIndex % slides.Count].oneWayDurationSeconds;
        return d > 0.0001f ? d : defaultOneWayDurationSeconds;
    }

    private LeanTweenType CurrentEase()
    {
        if (slides[_slideIndex % slides.Count].overrideEase)
            return slides[_slideIndex % slides.Count].easeType;
        return defaultEaseType;
    }

    private Vector2 BackgroundSizeForSlideIndex(int idx)
    {
        var sz = slides[idx % slides.Count].backgroundSize;
        if (sz.sqrMagnitude > 0.0001f) return sz;
        return backgroundSize;
    }

    private Vector2 CurrentBackgroundSize() => BackgroundSizeForSlideIndex(_slideIndex);

    private float CurrentCrossFadeLead() => crossFadeStartBeforeEndSeconds;

    private float OneWayDurationForSlideIndex(int idx)
    {
        var d = slides[idx % slides.Count].oneWayDurationSeconds;
        return d > 0.0001f ? d : defaultOneWayDurationSeconds;
    }

    private LeanTweenType EaseForSlideIndex(int idx)
    {
        var s = slides[idx % slides.Count];
        return s.overrideEase ? s.easeType : defaultEaseType;
    }

    /// <summary>現在スライドの「行き」レグ終了後：次へ進むか、戻りレグへ。</summary>
    private void BranchAfterForwardLeg()
    {
        var advanceForward = slideAdvanceMode == TitleBackgroundSlideAdvanceMode.AfterForwardLeg;
        if (advanceForward)
        {
            AdvanceSlideInstantOrFromBlack();
            return;
        }

        if (!pingPong)
        {
            AdvanceSlideInstantOrFromBlack();
            return;
        }

        var start = CurrentPanStart();
        var dur = CurrentOneWayDuration();
        var ease = CurrentEase();
        PlayLegTo(start, dur, ease, AdvanceSlideInstantOrFromBlack, scheduleCrossFadeBeforeAdvance: true);
    }

    private void PlayFromCurrentSlide()
    {
        if (!isActiveAndEnabled || _imgRt == null) return;
        if (slides == null || slides.Count < MinSlides) return;

        var end = CurrentPanEnd();
        var dur = CurrentOneWayDuration();
        var ease = CurrentEase();

        _imgRt.anchoredPosition = CurrentPanStart();

        var advanceForward = slideAdvanceMode == TitleBackgroundSlideAdvanceMode.AfterForwardLeg;
        var scheduleFirstLeg = advanceForward || !pingPong;

        PlayLegTo(end, dur, ease, BranchAfterForwardLeg, scheduleCrossFadeBeforeAdvance: scheduleFirstLeg);
    }

    /// <summary>次スライドへ指数進行し、先頭からパン（クロスフェードなしの切替）。</summary>
    private void AdvanceSlideInstantOrFromBlack()
    {
        if (slides == null || slides.Count < MinSlides) return;

        LeanTween.cancel(gameObject);

        _slideIndex = (_slideIndex + 1) % slides.Count;

        for (var guard = 0; guard < slides.Count; guard++)
        {
            if (slides[_slideIndex].sprite != null) break;
            _slideIndex = (_slideIndex + 1) % slides.Count;
        }

        FinishCrossFadeForSlideChange();

        ApplySlideSpriteAndLayout();

        var c = image.color;
        c.r = c.g = c.b = 1f;
        c.a = 1f;
        image.color = c;

        PlayFromCurrentSlide();
    }

    private void FinishCrossFadeForSlideChange()
    {
        HideCrossFadeLayerVisual();
    }

    private int PeekNextSlideIndex()
    {
        var n = (_slideIndex + 1) % slides.Count;
        for (var guard = 0; guard < slides.Count; guard++)
        {
            if (slides[n].sprite != null) break;
            n = (n + 1) % slides.Count;
        }

        return n;
    }

    private void PrepareCrossFadeLayer(int nextSlideIdx)
    {
        if (crossFadeImage == null) return;

        var s = slides[nextSlideIdx % slides.Count];
        if (s.sprite != null)
            crossFadeImage.sprite = s.sprite;

        crossFadeImage.raycastTarget = false;
        crossFadeImage.preserveAspect = false;

        var rt = crossFadeImage.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = BackgroundSizeForSlideIndex(nextSlideIdx);
        rt.anchoredPosition = s.panAnchoredStart;

        var c = crossFadeImage.color;
        c.r = c.g = c.b = 1f;
        c.a = 0f;
        crossFadeImage.color = c;
    }

    private bool BeginCrossFadeTransition(int nextSlideIdx, float overlapSeconds)
    {
        if (crossFadeImage == null || image != null && crossFadeImage == image)
        {
            Debug.LogWarning("[TitleBackgroundPanController] Cross Fade Image は Image と別オブジェクトにしてください。", this);
            return false;
        }

        if (_crossRt == null || overlapSeconds < 0.01f) return false;

        EnsureCrossFadeAlphaDriver();

        _crossFadeTargetSlideIndex = nextSlideIdx;
        _crossFadeOverlapSecondsForLeg = overlapSeconds;

        LeanTween.cancel(_crossRt.gameObject);
        LeanTween.cancel(_crossFadeAlphaDriver);

        _crossRt.SetAsLastSibling();

        PrepareCrossFadeLayer(nextSlideIdx);

        var nextEnd = slides[nextSlideIdx % slides.Count].panAnchoredEnd;
        var nextStart = slides[nextSlideIdx % slides.Count].panAnchoredStart;
        var dNext = OneWayDurationForSlideIndex(nextSlideIdx);
        var easeNext = EaseForSlideIndex(nextSlideIdx);

        var panTw = LeanTween.value(_crossRt.gameObject, nextStart, nextEnd, Mathf.Max(0.01f, dNext))
            .setEase(easeNext)
            .setOnUpdate((Vector2 p) =>
            {
                if (_crossRt != null) _crossRt.anchoredPosition = p;
            });

        if (useUnscaledTime) panTw.setIgnoreTimeScale(true);

        var bottom = image;
        var top = crossFadeImage;
        var startBottomA = bottom != null ? bottom.color.a : 1f;

        var alphaTw = LeanTween.value(_crossFadeAlphaDriver, 0f, 1f, overlapSeconds)
            .setEase(crossFadeEase)
            .setOnUpdate((float u) =>
            {
                if (bottom != null)
                {
                    var bc = bottom.color;
                    bc.a = Mathf.Lerp(startBottomA, 0f, u);
                    bottom.color = bc;
                }

                if (top != null)
                {
                    var tc = top.color;
                    tc.a = Mathf.Lerp(0f, 1f, u);
                    top.color = tc;
                }
            })
            .setOnComplete(() =>
            {
                if (top != null)
                {
                    var tc = top.color;
                    tc.a = 1f;
                    top.color = tc;
                }
            });

        if (useUnscaledTime) alphaTw.setIgnoreTimeScale(true);
        return true;
    }

    private void CompleteCrossfadeHandoffAndContinuePan()
    {
        if (_crossRt == null || crossFadeImage == null || image == null || _imgRt == null) return;

        LeanTween.cancel(gameObject);
        if (_crossFadeAlphaDriver != null) LeanTween.cancel(_crossFadeAlphaDriver);
        LeanTween.cancel(_crossRt.gameObject);

        var handoffPos = _crossRt.anchoredPosition;
        var overlap = _crossFadeOverlapSecondsForLeg;
        _slideIndex = _crossFadeTargetSlideIndex;

        ApplySlideSpriteAndLayout();
        _imgRt.anchoredPosition = handoffPos;

        HideCrossFadeLayerVisual();

        var c = image.color;
        c.r = c.g = c.b = 1f;
        c.a = 1f;
        image.color = c;

        var end = CurrentPanEnd();
        var D = CurrentOneWayDuration();
        var ease = CurrentEase();
        var remaining = Mathf.Max(0.01f, D - overlap);

        var advanceForward = slideAdvanceMode == TitleBackgroundSlideAdvanceMode.AfterForwardLeg;
        var scheduleNext = advanceForward || !pingPong;

        PlayLegTo(end, remaining, ease, BranchAfterForwardLeg, scheduleCrossFadeBeforeAdvance: scheduleNext);
    }

    private void PlayLegTo(Vector2 target, float duration, LeanTweenType ease, Action onComplete, bool scheduleCrossFadeBeforeAdvance)
    {
        if (_imgRt == null) return;

        _crossFadeActiveForCurrentLeg = false;

        if (scheduleCrossFadeBeforeAdvance && useCrossFade && crossFadeImage != null && _crossRt != null && slides != null && slides.Count >= MinSlides)
        {
            var lead = CurrentCrossFadeLead();
            var delay = Mathf.Max(0f, duration - lead);
            var overlapDuration = Mathf.Max(0.01f, duration - delay);
            var nextIdx = PeekNextSlideIndex();

            void StartBlend()
            {
                if (!isActiveAndEnabled) return;
                if (BeginCrossFadeTransition(nextIdx, overlapDuration))
                    _crossFadeActiveForCurrentLeg = true;
            }

            if (delay < 0.001f)
                StartBlend();
            else
            {
                var dc = LeanTween.delayedCall(gameObject, delay, StartBlend);
                if (useUnscaledTime) dc.setIgnoreTimeScale(true);
            }
        }

        var from = _imgRt.anchoredPosition;
        var tw = LeanTween.value(_imgRt.gameObject, from, target, Mathf.Max(0.01f, duration))
            .setEase(ease)
            .setOnUpdate((Vector2 p) =>
            {
                if (_imgRt != null) _imgRt.anchoredPosition = p;
            })
            .setOnComplete(() =>
            {
                if (!isActiveAndEnabled) return;
                if (_crossFadeActiveForCurrentLeg && useCrossFade && crossFadeImage != null)
                    CompleteCrossfadeHandoffAndContinuePan();
                else
                    onComplete?.Invoke();
            });

        if (useUnscaledTime) tw.setIgnoreTimeScale(true);
    }

    private void ApplyImageLayout()
    {
        if (image == null) return;
        image.raycastTarget = false;
        image.preserveAspect = false;
        var rt = image.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = CurrentBackgroundSize();
    }
}

/// <summary>1 枚分：スプライト・サイズ・パン。</summary>
[Serializable]
public struct TitleBackgroundPanSlide
{
    [Tooltip("null のときは直前のスプライトを維持します。")]
    public Sprite sprite;

    [Tooltip("(0,0) のときはコントローラの backgroundSize を使用。")]
    public Vector2 backgroundSize;

    public Vector2 panAnchoredStart;
    public Vector2 panAnchoredEnd;

    [Tooltip("0 以下で defaultOneWayDurationSeconds を使用。")]
    public float oneWayDurationSeconds;

    [Tooltip("オンにすると、このスライドの「位置パン」だけ別のイージング（LeanTweenType）にします。画像のクロスフェードとは無関係です。")]
    public bool overrideEase;

    [Tooltip("overrideEase がオンのとき、Start→End の移動に使うカーブ（例: easeInOutQuad）。")]
    public LeanTweenType easeType;
}

public enum TitleBackgroundSlideAdvanceMode
{
    /// <summary>Start→End だけで次の画像へ（往復なし）</summary>
    AfterForwardLeg,

    /// <summary>往復オン時は Start→End→Start の後に次の画像へ</summary>
    AfterFullRoundTrip,
}
