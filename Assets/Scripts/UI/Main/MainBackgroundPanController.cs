using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// メイン画面用：指定スプライトを <see cref="backgroundSize"/>（既定 3413×1920）に引き伸ばし、
/// 親の <see cref="clipViewport"/> より大きい領域をパンして切り替えます。
/// 親には <see cref="RectMask2D"/> があり、子の Image だけがマスクされます（Unity の仕様）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public sealed class MainBackgroundPanController : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("子の Image。未指定なら同じ GameObject から取得します。")]
    [SerializeField] private Image image;

    [Tooltip("窓（画面全体想定）。未指定の場合は parent の RectTransform。ここに RectMask2D を付ける想定。")]
    [SerializeField] private RectTransform clipViewport;

    [Tooltip("引き伸ばし後の大きさ（UI 上の基準解像度に対する座標系）。")]
    [SerializeField] private Vector2 backgroundSize = new Vector2(3413f, 1920f);

    [Header("スライド")]
    [Tooltip("上から順に切り替え。空なら何もしません。")]
    [SerializeField] private List<MainBackgroundSlide> slides = new();

    [Tooltip("オンなら全スライドの秒数に defaultDurationSeconds を使い、各要素の durationSeconds を無視します。")]
    [SerializeField] private bool overrideSlideDuration = false;

    [SerializeField] private float defaultDurationSeconds = 60f;

    private int _index;
    private Coroutine _panRoutine;

    private void Reset()
    {
        image = GetComponent<Image>();
        if (image != null) image.preserveAspect = false;
    }

    private void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        if (clipViewport == null && transform.parent != null)
        {
            clipViewport = transform.parent as RectTransform;
        }

        if (clipViewport != null && clipViewport.GetComponent<RectMask2D>() == null)
        {
            clipViewport.gameObject.AddComponent<RectMask2D>();
        }
    }

    private void OnEnable()
    {
        ApplyImageLayout();
        if (_panRoutine != null) StopCoroutine(_panRoutine);
        _index = 0;
        if (slides != null && slides.Count > 0) _panRoutine = StartCoroutine(PanSequence());
    }

    private void OnDisable()
    {
        if (_panRoutine != null)
        {
            StopCoroutine(_panRoutine);
            _panRoutine = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (image == null) image = GetComponent<Image>();
        if (image != null) image.preserveAspect = false;
    }
#endif

    public void SetSlides(IReadOnlyList<MainBackgroundSlide> list)
    {
        slides = list != null ? new List<MainBackgroundSlide>(list) : new List<MainBackgroundSlide>();
        if (isActiveAndEnabled)
        {
            if (_panRoutine != null) StopCoroutine(_panRoutine);
            _index = 0;
            if (slides.Count > 0) _panRoutine = StartCoroutine(PanSequence());
        }
    }

    private void ApplyImageLayout()
    {
        if (image == null) return;
        image.raycastTarget = false;
        image.preserveAspect = false;
        var rt = (RectTransform)image.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = backgroundSize;
    }

    private IEnumerator PanSequence()
    {
        for (;;)
        {
            if (slides == null || slides.Count == 0) yield break;

            var s = slides[_index % slides.Count];
            if (s.sprite == null)
            {
                _index = (_index + 1) % slides.Count;
                yield return null;
                continue;
            }

            if (image != null) image.sprite = s.sprite;
            float duration = overrideSlideDuration ? defaultDurationSeconds : (s.durationSeconds > 0f ? s.durationSeconds : defaultDurationSeconds);
            if (clipViewport == null) yield return new WaitForSeconds(duration);
            else yield return AnimateOneSlide(s, duration);

            _index = (_index + 1) % slides.Count;
        }
    }

    private IEnumerator AnimateOneSlide(MainBackgroundSlide s, float duration)
    {
        var rt = (RectTransform)image.transform;
        var view = clipViewport;
        var viewW = view.rect.width;
        var viewH = view.rect.height;
        var imgW = backgroundSize.x;
        var imgH = backgroundSize.y;
        var ax = GetHalfSlack(imgW, viewW);
        var ay = GetHalfSlack(imgH, viewH);
        if (ax < 0.1f && ay < 0.1f)
        {
            rt.anchoredPosition = Vector2.zero;
            yield return new WaitForSeconds(duration);
            yield break;
        }

        if (!s.ComputeAnchoredPosRange(ax, ay, out var start, out var end, out _))
        {
            rt.anchoredPosition = Vector2.zero;
            yield return new WaitForSeconds(duration);
            yield break;
        }

        if (duration <= 0.01f)
        {
            rt.anchoredPosition = end;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var u = Mathf.Clamp01(elapsed / duration);
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, u);
            yield return null;
        }
        rt.anchoredPosition = end;
    }

    private static float GetHalfSlack(float image, float view)
    {
        return Math.Max(0f, 0.5f * (image - view));
    }
}

/// <summary>1 枚分の表示とパン向き。Inspector から指定する。</summary>
[Serializable]
public struct MainBackgroundSlide
{
    [Tooltip("引き伸ばしで backgroundSize へフィット。")]
    public Sprite sprite;

    [Tooltip("このスライドの秒数。0 以下のときは defaultDurationSeconds。")]
    public float durationSeconds;

    [Tooltip("CustomAngle 以外のときは無視されます。")]
    public MainBackgroundPanPreset preset;

    [Tooltip("CustomAngle: 0=右、90=上、180=左、270=下（度・反時計回り）。")]
    public float panAngleDegrees;

    public bool ComputeAnchoredPosRange(float ax, float ay, out Vector2 start, out Vector2 end, out float resolvedAngle)
    {
        var mode = preset;
        if (mode == MainBackgroundPanPreset.CustomAngle)
        {
            var rad = panAngleDegrees * Mathf.Deg2Rad;
            var u = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            if (u.sqrMagnitude < 0.0001f) u = Vector2.right;
            u.Normalize();
            if (!TryGetSymmetricSpanAlongUnit(ax, ay, u, out var t, out var angleRes))
            {
                start = end = Vector2.zero;
                resolvedAngle = panAngleDegrees;
                return false;
            }
            start = -t * u;
            end = t * u;
            resolvedAngle = angleRes;
            return true;
        }

        start = end = Vector2.zero;
        if (mode == MainBackgroundPanPreset.ToBottomRight)
        {
            start = new Vector2(ax, -ay);
            end = new Vector2(-ax, ay);
        }
        else if (mode == MainBackgroundPanPreset.ToTopLeft)
        {
            start = new Vector2(-ax, ay);
            end = new Vector2(ax, -ay);
        }
        else if (mode == MainBackgroundPanPreset.TopToBottom)
        {
            start = new Vector2(0f, ay);
            end = new Vector2(0f, -ay);
        }
        else if (mode == MainBackgroundPanPreset.LeftToRight)
        {
            start = new Vector2(ax, 0f);
            end = new Vector2(-ax, 0f);
        }
        else
        {
            resolvedAngle = 0f;
            return false;
        }

        resolvedAngle = (end - start).magnitude < 0.1f
            ? 0f
            : Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
        return (end - start).sqrMagnitude > 0.01f;
    }

    private static bool TryGetSymmetricSpanAlongUnit(float ax, float ay, Vector2 u, out float t, out float angleDeg)
    {
        t = 0f;
        angleDeg = Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg;
        if (ax < 0.0001f && ay < 0.0001f) return false;

        var tX = float.PositiveInfinity;
        var tY = float.PositiveInfinity;
        if (Mathf.Abs(u.x) > 1e-5f) tX = ax / Mathf.Abs(u.x);
        if (Mathf.Abs(u.y) > 1e-5f) tY = ay / Mathf.Abs(u.y);

        t = float.IsPositiveInfinity(tX) ? tY : float.IsPositiveInfinity(tY) ? tX : Mathf.Min(tX, tY);
        if (float.IsPositiveInfinity(t) || t < 0.0001f) return false;
        return true;
    }
}

public enum MainBackgroundPanPreset
{
    /// <summary>角→対角。左上の見え方から右下方向へ</summary>
    ToBottomRight,
    /// <summary>反対。右下の見え方から左上方向へ</summary>
    ToTopLeft,
    /// <summary>上寄せから下寄せ</summary>
    TopToBottom,
    /// <summary>左端寄せから右端寄せ</summary>
    LeftToRight,
    /// <summary><see cref="MainBackgroundSlide.panAngleDegrees"/> 使用</summary>
    CustomAngle,
}
