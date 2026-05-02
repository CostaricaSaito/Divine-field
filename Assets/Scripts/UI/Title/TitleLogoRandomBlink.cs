using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title ロゴ：ランダムな間隔で白フラッシュ。
/// <b>推奨</b>：<see cref="whiteFlashOverlay"/> または <see cref="whiteFlashGraphic"/>（TitleLogo(White)）で上に重ね、
/// 下のロゴの <see cref="Graphic.color"/> は触らず LeanTween 等と競合しないようにします。
/// 未設定時は <see cref="blinkTargets"/> の色を Lerp します。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleLogoRandomBlink : MonoBehaviour
{
    [Header("白オーバーレイ（推奨・TitleLogo White）")]
    [Tooltip("点滅中以外は非アクティブにするルート。未指定なら CanvasGroup / Graphic の GameObject を使います。")]
    [SerializeField] private GameObject whiteFlashOverlayRoot;
    [Tooltip("TitleLogo(White) に CanvasGroup を付けて割り当て。alpha のみ点滅します。")]
    [SerializeField] private CanvasGroup whiteFlashOverlay;
    [Tooltip("CanvasGroup が無いとき。白画像 Image など。RGB は 1 にし、A のみ点滅します。")]
    [SerializeField] private Graphic whiteFlashGraphic;

    [Header("対象（オーバーレイ未使用時）")]
    [SerializeField] private Graphic[] blinkTargets;

    [Header("間隔（秒）")]
    [SerializeField] [Min(0.1f)] private float minInterval = 2.5f;
    [SerializeField] [Min(0.1f)] private float maxInterval = 6f;

    [Header("白フラッシュ")]
    [SerializeField] [Min(0.01f)] private float fadeToWhiteDuration = 0.04f;
    [SerializeField] [Min(0f)] private float holdWhiteDuration = 0.02f;
    [SerializeField] [Min(0.01f)] private float fadeToBaseDuration = 0.07f;

    [Header("その他")]
    [SerializeField] private bool useUnscaledTime = true;
    [Tooltip("全対象を同じタイミングでフラッシュ。オフなら対象ごとに独立（オーバーレイ時は毎回同じオーバーレイを点滅）。")]
    [SerializeField] private bool syncAllTargetsTogether = true;

    private Color[] _baseColors;
    private Coroutine _loop;

    private bool UsesWhiteOverlay =>
        whiteFlashOverlay != null || whiteFlashGraphic != null;

    private GameObject ResolveWhiteFlashOverlayRoot()
    {
        if (whiteFlashOverlayRoot != null) return whiteFlashOverlayRoot;
        if (whiteFlashOverlay != null) return whiteFlashOverlay.gameObject;
        if (whiteFlashGraphic != null) return whiteFlashGraphic.gameObject;
        return null;
    }

    private void SetWhiteOverlayRootActive(bool active)
    {
        var root = ResolveWhiteFlashOverlayRoot();
        if (root != null) root.SetActive(active);
    }

    private void Awake()
    {
        CacheBaseColors();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        CacheBaseColors();
        if (_loop != null) StopCoroutine(_loop);

        HideWhiteOverlayStrength();

        if (UsesWhiteOverlay)
            SetWhiteOverlayRootActive(false);

        if (!CanRunBlinkLoop()) return;

        if (syncAllTargetsTogether || UsesWhiteOverlay)
            _loop = StartCoroutine(CoBlinkLoopAll());
        else
            _loop = StartCoroutine(CoBlinkLoopStaggered());
    }

    private bool CanRunBlinkLoop() =>
        UsesWhiteOverlay || (blinkTargets != null && blinkTargets.Length > 0);

    /// <summary>登場演出などの後で呼び出し、現在の色を「通常色」として記憶し直します。</summary>
    public void RefreshBaseColorsFromCurrent()
    {
        CacheBaseColors();
    }

    private void OnDisable()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }

        HideWhiteOverlayStrength();
        RestoreAllColors();
        SetWhiteOverlayRootActive(false);
    }

    private void CacheBaseColors()
    {
        if (blinkTargets == null || blinkTargets.Length == 0)
        {
            _baseColors = null;
            return;
        }

        if (_baseColors == null || _baseColors.Length != blinkTargets.Length)
            _baseColors = new Color[blinkTargets.Length];

        for (var i = 0; i < blinkTargets.Length; i++)
        {
            var g = blinkTargets[i];
            _baseColors[i] = g != null ? g.color : Color.white;
        }
    }

    private IEnumerator CoBlinkLoopAll()
    {
        for (;;)
        {
            yield return WaitInterval();
            if (UsesWhiteOverlay)
                yield return FlashWhiteOverlay();
            else
                yield return FlashOnceAll();
        }
    }

    private IEnumerator CoBlinkLoopStaggered()
    {
        for (;;)
        {
            if (UsesWhiteOverlay)
            {
                if (!isActiveAndEnabled) yield break;
                yield return WaitInterval();
                yield return FlashWhiteOverlay();
                continue;
            }

            for (var i = 0; i < blinkTargets.Length; i++)
            {
                if (!isActiveAndEnabled) yield break;
                yield return WaitInterval();
                var g = blinkTargets[i];
                if (g != null && i < _baseColors.Length)
                    yield return FlashOneGraphic(g, _baseColors[i]);
            }
        }
    }

    private IEnumerator WaitInterval()
    {
        var w = Random.Range(minInterval, maxInterval);
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(w);
        else
            yield return new WaitForSeconds(w);
    }

    private IEnumerator FlashWhiteOverlay()
    {
        SetWhiteOverlayRootActive(true);

        float Delta() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        var e = 0f;
        while (e < fadeToWhiteDuration)
        {
            e += Delta();
            var u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / fadeToWhiteDuration));
            SetWhiteOverlayStrength(u);
            yield return null;
        }

        SetWhiteOverlayStrength(1f);
        if (holdWhiteDuration > 0f)
            yield return WaitForSecondsOrRealtime(holdWhiteDuration);

        e = 0f;
        while (e < fadeToBaseDuration)
        {
            e += Delta();
            var u = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / fadeToBaseDuration));
            SetWhiteOverlayStrength(u);
            yield return null;
        }

        SetWhiteOverlayStrength(0f);
        SetWhiteOverlayRootActive(false);
    }

    private void SetWhiteOverlayStrength(float alpha01)
    {
        if (whiteFlashOverlay != null)
        {
            whiteFlashOverlay.alpha = alpha01;
            return;
        }

        if (whiteFlashGraphic == null) return;

        var c = whiteFlashGraphic.color;
        c.r = 1f;
        c.g = 1f;
        c.b = 1f;
        c.a = alpha01;
        whiteFlashGraphic.color = c;
    }

    private void HideWhiteOverlayStrength() => SetWhiteOverlayStrength(0f);

    private IEnumerator FlashOnceAll()
    {
        if (blinkTargets == null) yield break;

        float Delta() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        var e = 0f;
        while (e < fadeToWhiteDuration)
        {
            e += Delta();
            var u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / fadeToWhiteDuration));
            for (var i = 0; i < blinkTargets.Length; i++)
            {
                var g = blinkTargets[i];
                if (g == null || i >= _baseColors.Length) continue;
                var b = _baseColors[i];
                g.color = Color.Lerp(b, Color.white, u);
            }

            yield return null;
        }

        for (var i = 0; i < blinkTargets.Length; i++)
        {
            var g = blinkTargets[i];
            if (g == null || i >= _baseColors.Length) continue;
            g.color = Color.white;
        }

        if (holdWhiteDuration > 0f)
            yield return WaitForSecondsOrRealtime(holdWhiteDuration);

        e = 0f;
        while (e < fadeToBaseDuration)
        {
            e += Delta();
            var u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / fadeToBaseDuration));
            for (var i = 0; i < blinkTargets.Length; i++)
            {
                var g = blinkTargets[i];
                if (g == null || i >= _baseColors.Length) continue;
                var b = _baseColors[i];
                g.color = Color.Lerp(Color.white, b, u);
            }

            yield return null;
        }

        for (var i = 0; i < blinkTargets.Length; i++)
        {
            var g = blinkTargets[i];
            if (g == null || i >= _baseColors.Length) continue;
            g.color = _baseColors[i];
        }
    }

    private IEnumerator FlashOneGraphic(Graphic g, Color baseCol)
    {
        float Delta() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        var e = 0f;
        while (e < fadeToWhiteDuration)
        {
            e += Delta();
            var u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / fadeToWhiteDuration));
            g.color = Color.Lerp(baseCol, Color.white, u);
            yield return null;
        }

        g.color = Color.white;
        if (holdWhiteDuration > 0f)
            yield return WaitForSecondsOrRealtime(holdWhiteDuration);

        e = 0f;
        while (e < fadeToBaseDuration)
        {
            e += Delta();
            var u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / fadeToBaseDuration));
            g.color = Color.Lerp(Color.white, baseCol, u);
            yield return null;
        }

        g.color = baseCol;
    }

    private IEnumerator WaitForSecondsOrRealtime(float sec)
    {
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(sec);
        else
            yield return new WaitForSeconds(sec);
    }

    private void RestoreAllColors()
    {
        if (blinkTargets == null || _baseColors == null) return;
        for (var i = 0; i < blinkTargets.Length && i < _baseColors.Length; i++)
        {
            if (blinkTargets[i] != null)
                blinkTargets[i].color = _baseColors[i];
        }
    }
}
