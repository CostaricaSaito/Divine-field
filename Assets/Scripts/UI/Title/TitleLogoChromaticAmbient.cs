using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chromatic aberration（色収差）：元のロゴ <b>は一切動かさない</b>。
/// 同じスプライトの <see cref="Image"/> を R 専用／G 専用／B 専用に着色（各チャンネルのみ通過）し、
/// そのコピーだけをランダム方向へずらして重ねます。UI Default は頂点色がテクスチャと要素積になるため、
/// (1,0,0,a) で元テクスチャの R のみが表示されます。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleLogoChromaticAmbient : MonoBehaviour
{
    [Header("参照（コピー側のみ。ベースのロゴはここに入れない）")]
    [Tooltip("着地まで非表示にする親（R/G/B 複製のルート）。")]
    [SerializeField] private GameObject chromaRoot;
    [Tooltip("赤チャンネルのみ通す複製 Image（元と同一スプライト・アンカー一致）")]
    [SerializeField] private RectTransform chromaChannelR;
    [Tooltip("緑チャンネルのみ通す複製")]
    [SerializeField] private RectTransform chromaChannelG;
    [Tooltip("青チャンネルのみ通す複製")]
    [SerializeField] private RectTransform chromaChannelB;

    [Header("チャンネル表示")]
    [SerializeField] [Range(0f, 1f)] private float fringeAlpha = 0.35f;
    [Tooltip("B 用オフセットを R 方向と逆向きにする倍率")]
    [SerializeField] [Range(0.2f, 1.5f)] private float blueOppositeScale = 0.95f;
    [Tooltip("G 用は主方向に対し垂直成分へ掛ける倍率（3 枚あるときだけ効く）")]
    [SerializeField] [Range(0f, 1.5f)] private float greenPerpendicularScale = 0.55f;

    [Header("ランダム間隔（秒）")]
    [SerializeField] [Min(0.05f)] private float minInterval = 2f;
    [SerializeField] [Min(0.05f)] private float maxInterval = 6f;

    [Header("ズレ量（アンカー・px 目安）")]
    [SerializeField] [Min(0f)] private float splitPixelsMin = 2f;
    [SerializeField] [Min(0f)] private float splitPixelsMax = 10f;

    [Header("1回のパルス")]
    [SerializeField] [Min(0.01f)] private float rampInDuration = 0.06f;
    [SerializeField] [Min(0f)] private float holdDuration = 0.04f;
    [SerializeField] [Min(0.01f)] private float rampOutDuration = 0.12f;

    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool disableChromaRootOnAwake = true;

    private Vector2 _baseR;
    private Vector2 _baseG;
    private Vector2 _baseB;
    private Coroutine _loop;

    private void Awake()
    {
        if (!disableChromaRootOnAwake) return;

        if (chromaRoot != null && chromaRoot != gameObject)
        {
            chromaRoot.SetActive(false);
            return;
        }

        SetChannelActive(chromaChannelR, false);
        SetChannelActive(chromaChannelG, false);
        SetChannelActive(chromaChannelB, false);
    }

    private void OnDisable()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }

        RestoreNeutralOffsets();
    }

    /// <summary><see cref="TitleLogoIntroController"/> から：白フラッシュ後などに呼ぶ。</summary>
    public void BeginAfterIntro()
    {
        if (!isActiveAndEnabled || !Application.isPlaying) return;
        if (chromaChannelR == null && chromaChannelG == null && chromaChannelB == null)
            return;

        if (chromaRoot != null)
            chromaRoot.SetActive(true);
        else
        {
            SetChannelActive(chromaChannelR, true);
            SetChannelActive(chromaChannelG, true);
            SetChannelActive(chromaChannelB, true);
        }

        ApplyRgbChannelTints();

        if (_loop != null)
            StopCoroutine(_loop);
        _loop = StartCoroutine(CoAmbientLoop());
    }

    private static void SetChannelActive(RectTransform rt, bool on)
    {
        if (rt != null && rt.gameObject != null)
            rt.gameObject.SetActive(on);
    }

    /// <summary>UI Default で各チャンネル成分のみ残す頂点色を適用する。</summary>
    private void ApplyRgbChannelTints()
    {
        SetImageChannelTint(chromaChannelR, new Color(1f, 0f, 0f, fringeAlpha));
        SetImageChannelTint(chromaChannelG, new Color(0f, 1f, 0f, fringeAlpha));
        SetImageChannelTint(chromaChannelB, new Color(0f, 0f, 1f, fringeAlpha));
    }

    private static void SetImageChannelTint(RectTransform rt, Color tint)
    {
        if (rt == null) return;
        var image = rt.GetComponent<Image>();
        if (image != null)
        {
            image.color = tint;
            return;
        }

        var raw = rt.GetComponent<RawImage>();
        if (raw != null)
            raw.color = tint;
    }

    private IEnumerator CoAmbientLoop()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        CacheNeutralPositions();
        ApplyRgbChannelTints();

        for (;;)
        {
            if (!isActiveAndEnabled) yield break;

            var w = Random.Range(minInterval, maxInterval);
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(w);
            else
                yield return new WaitForSeconds(w);

            if (!isActiveAndEnabled) yield break;

            var angle = Random.Range(0f, Mathf.PI * 2f);
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
            var mag = Random.Range(splitPixelsMin, splitPixelsMax);
            var perp = new Vector2(-dir.y, dir.x);
            var hasG = chromaChannelG != null;

            var peakR = dir * mag;
            var peakB = -dir * (mag * blueOppositeScale);
            var peakG = hasG ? perp * (mag * greenPerpendicularScale) : Vector2.zero;

            yield return CoPulseOffsets(peakR, peakG, peakB);
        }
    }

    private IEnumerator CoPulseOffsets(Vector2 peakR, Vector2 peakG, Vector2 peakB)
    {
        void Apply(float u)
        {
            if (chromaChannelR != null)
                chromaChannelR.anchoredPosition = _baseR + peakR * u;
            if (chromaChannelG != null)
                chromaChannelG.anchoredPosition = _baseG + peakG * u;
            if (chromaChannelB != null)
                chromaChannelB.anchoredPosition = _baseB + peakB * u;
        }

        var e = 0f;
        while (e < rampInDuration)
        {
            e += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            var t = Mathf.Clamp01(e / rampInDuration);
            Apply(Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        Apply(1f);

        if (holdDuration > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(holdDuration);
            else
                yield return new WaitForSeconds(holdDuration);
        }

        e = 0f;
        while (e < rampOutDuration)
        {
            e += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            var t = Mathf.Clamp01(e / rampOutDuration);
            var u = 1f - Mathf.SmoothStep(0f, 1f, t);
            Apply(u);
            yield return null;
        }

        Apply(0f);
    }

    private void CacheNeutralPositions()
    {
        if (chromaChannelR != null)
            _baseR = chromaChannelR.anchoredPosition;
        if (chromaChannelG != null)
            _baseG = chromaChannelG.anchoredPosition;
        if (chromaChannelB != null)
            _baseB = chromaChannelB.anchoredPosition;
    }

    private void RestoreNeutralOffsets()
    {
        if (chromaChannelR != null)
            chromaChannelR.anchoredPosition = _baseR;
        if (chromaChannelG != null)
            chromaChannelG.anchoredPosition = _baseG;
        if (chromaChannelB != null)
            chromaChannelB.anchoredPosition = _baseB;
    }
}
