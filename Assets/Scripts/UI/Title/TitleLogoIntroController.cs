using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title シーン：ロゴ4分割（Main / MagicCircle / Sword / Sub）の登場演出。
/// 参照未設定時は「TitleLogo(Main)」等の名前で <see cref="RectTransform"/> を検索します。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleLogoIntroController : MonoBehaviour
{
    private const string NameMain = "TitleLogo(Main)";
    private const string NameMagicCircle = "TitleLogo(MagicCircle)";
    private const string NameSword = "TitleLogo(Sword)";
    private const string NameSub = "TitleLogo(Sub)";
    private const string DefaultBattleStartSeAddress = "Assets/SE/バトル開始.mp3";

    [Header("参照（空なら名前検索）")]
    [SerializeField] private RectTransform mainRect;
    [SerializeField] private Graphic mainGraphic;
    [SerializeField] private RectTransform magicCircleRect;
    [SerializeField] private Graphic magicCircleGraphic;
    [SerializeField] private RectTransform swordRect;
    [SerializeField] private GameObject subRoot;

    [Header("フェード・間隔（秒） — デザイナー向け")]
    [SerializeField] [Min(0.01f)] private float mainFadeDuration = 0.5f;
    [Tooltip("Main フェード開始からの遅延。ここから MagicCircle をフェード＋発光。")]
    [SerializeField] [Min(0f)] private float magicCircleDelayFromMainStart = 0.2f;
    [SerializeField] [Min(0.01f)] private float magicCircleFadeDuration = 1f;
    [SerializeField] [Min(0.01f)] private float swordSlideDuration = 0.2f;
    [SerializeField] [Min(1f)] private float whiteFlashDurationMs = 50f;

    [Header("MagicCircle 発光（色の立ち上がり）")]
    [SerializeField] private Color magicCircleColorStart = new Color(0.35f, 0.38f, 0.5f, 0f);
    [SerializeField] private Color magicCircleColorEnd = new Color(1f, 1f, 1f, 1f);

    [Header("Sword スライド・回転")]
    [SerializeField] private Vector2 swordStartAnchoredPosition = new Vector2(1200f, 820f);
    [Tooltip("移動中に「余分に」回す Z 回転量（度）。5回転≒1800。Mathf.Lerp で直線補間するため多周回がそのまま反映されます。")]
    [SerializeField] private float swordSpinDegreesDuringSlide = 1800f;
    [SerializeField] private LeanTweenType swordMoveEase = LeanTweenType.easeOutQuad;

    [Header("モーションブラー風（任意・Graphic 複数）")]
    [Tooltip("剣と同一親の兄弟として Image を置き、同じスプライトでここへ割り当て（アンカー座標系が剣と一致するように）。")]
    [SerializeField] private Graphic[] swordBlurLayers;
    [SerializeField] [Range(0f, 1f)] private float motionBlurMaxAlpha = 0.35f;

    [Header("終了時")]
    [SerializeField] private string battleStartSeAddress = DefaultBattleStartSeAddress;
    [Tooltip("白フラッシュを載せる Canvas。空ならシーン内の最初のアクティブな Canvas。")]
    [SerializeField] private Canvas fullscreenFlashCanvas;
    [Tooltip("剣着弾時の画面揺れ・色収差（任意）")]
    [SerializeField] private TitleLogoImpactFeedback impactFeedback;
    [Tooltip("ロゴのランダム点滅。登場後に基準色を取り直すために参照しておく。")]
    [SerializeField] private TitleLogoRandomBlink titleLogoRandomBlink;
    [Tooltip("色収差レイヤーを着地後に表示し、ランダムなズレを継続させる。")]
    [SerializeField] private TitleLogoChromaticAmbient titleLogoChromaticAmbient;
    [Tooltip("剣着地後から、ランダム間隔で縦揺れを繰り返す。")]
    [SerializeField] private TitleLogoRandomImpact titleLogoRandomImpact;

    [Header("その他")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool playOnEnable = true;

    private Vector2 _swordEndAnchored;
    private float _swordEndRotationZ;
    private Color _mainColorEnd = Color.white;
    private GameObject _fullscreenFlashGo;
    private bool _resolved;

    private void Awake()
    {
        ResolveTargets();
        if (mainGraphic != null)
            _mainColorEnd = mainGraphic.color;
        CaptureSwordRestPose();
        if (_resolved)
            ApplyIntroInitialState();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || !playOnEnable) return;
        if (!_resolved) return;
        PlayIntro();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        CancelAllTweens();
    }

    private void OnDestroy()
    {
        CancelAllTweens();
    }

    /// <summary>手動再生・リピート用。</summary>
    public void PlayIntro()
    {
        if (!Application.isPlaying) return;
        if (!_resolved) return;

        CancelAllTweens();
        LeanTween.init();
        ApplyIntroInitialState();

        if (mainGraphic != null)
        {
            var cStart = _mainColorEnd;
            cStart.a = 0f;
            mainGraphic.color = cStart;
            var tMain = LeanTween.value(mainGraphic.gameObject, 0f, 1f, mainFadeDuration)
                .setEase(LeanTweenType.linear)
                .setOnUpdate(t =>
                {
                    if (mainGraphic == null) return;
                    mainGraphic.color = Color.Lerp(cStart, _mainColorEnd, t);
                });
            if (useUnscaledTime) tMain.setIgnoreTimeScale(true);
        }

        var dcMc = LeanTween.delayedCall(gameObject, magicCircleDelayFromMainStart, PlayMagicCircleIntro);
        if (useUnscaledTime) dcMc.setIgnoreTimeScale(true);

        var dcSword = LeanTween.delayedCall(gameObject, mainFadeDuration, PlaySwordIntro);
        if (useUnscaledTime) dcSword.setIgnoreTimeScale(true);
    }

    private void PlayMagicCircleIntro()
    {
        if (magicCircleGraphic == null) return;

        magicCircleGraphic.color = magicCircleColorStart;
        var lt = LeanTween.value(magicCircleGraphic.gameObject, 0f, 1f, magicCircleFadeDuration)
            .setEase(LeanTweenType.linear)
            .setOnUpdate(t =>
            {
                if (magicCircleGraphic == null) return;
                magicCircleGraphic.color = Color.Lerp(magicCircleColorStart, magicCircleColorEnd, t);
            });
        if (useUnscaledTime) lt.setIgnoreTimeScale(true);
    }

    private void PlaySwordIntro()
    {
        if (swordRect == null) return;

        swordRect.anchoredPosition = swordStartAnchoredPosition;
        var startEuler = swordRect.localEulerAngles;
        startEuler.z = _swordEndRotationZ + swordSpinDegreesDuringSlide;
        swordRect.localEulerAngles = startEuler;

        SetBlurLayersForStart();

        var hasBlur = swordBlurLayers != null && swordBlurLayers.Length > 0 && motionBlurMaxAlpha > 0.01f;
        var lt = LeanTween.value(swordRect.gameObject, 0f, 1f, swordSlideDuration)
            .setEase(swordMoveEase)
            .setOnUpdate(t =>
            {
                swordRect.anchoredPosition =
                    Vector2.Lerp(swordStartAnchoredPosition, _swordEndAnchored, t);
                var e = swordRect.localEulerAngles;
                // LerpAngle は最短角のみのため多周回しない。オイラーZを Lerp で直線補間する。
                e.z = Mathf.Lerp(_swordEndRotationZ + swordSpinDegreesDuringSlide, _swordEndRotationZ, t);
                swordRect.localEulerAngles = e;
                if (hasBlur) UpdateBlurLayersDuringSlide(t);
            })
            .setOnComplete(OnSwordArrived);
        if (useUnscaledTime) lt.setIgnoreTimeScale(true);
    }

    private void OnSwordArrived()
    {
        impactFeedback?.PlayImpact();

        ClearBlurLayers();
        if (SoundEffectPlayer.I != null && !string.IsNullOrEmpty(battleStartSeAddress))
            SoundEffectPlayer.I.Play(battleStartSeAddress);
        else
            Debug.LogWarning("[TitleLogoIntroController] SoundEffectPlayer または SE パスが無効です。", this);

        if (subRoot != null)
            subRoot.SetActive(true);

        StartCoroutine(CoFullscreenWhiteFlash());
        StartCoroutine(CoRefreshBlinkAfterSwordLanding());

        if (swordRect != null)
        {
            swordRect.anchoredPosition = _swordEndAnchored;
            var e = swordRect.localEulerAngles;
            e.z = _swordEndRotationZ;
            swordRect.localEulerAngles = e;
        }
    }

    private IEnumerator CoFullscreenWhiteFlash()
    {
        var canvas = fullscreenFlashCanvas != null
            ? fullscreenFlashCanvas
            : Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[TitleLogoIntroController] Canvas が見つかりません。白フラッシュをスキップします。", this);
            yield break;
        }

        if (_fullscreenFlashGo == null)
        {
            var go = new GameObject("TitleFullscreenWhiteFlash");
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<Image>();
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            _fullscreenFlashGo = go;
        }

        var img = _fullscreenFlashGo.GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = false;
            img.color = Color.white;
        }

        _fullscreenFlashGo.transform.SetAsLastSibling();
        _fullscreenFlashGo.SetActive(true);

        var waitSec = whiteFlashDurationMs * 0.001f;
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(waitSec);
        else
            yield return new WaitForSeconds(waitSec);

        if (_fullscreenFlashGo != null)
        {
            _fullscreenFlashGo.SetActive(false);
            if (img != null)
                img.color = Color.white;
        }
    }

    private IEnumerator CoRefreshBlinkAfterSwordLanding()
    {
        var waitSec = whiteFlashDurationMs * 0.001f;
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(waitSec);
        else
            yield return new WaitForSeconds(waitSec);
        yield return null;
        titleLogoRandomBlink?.RefreshBaseColorsFromCurrent();
        titleLogoChromaticAmbient?.BeginAfterIntro();
        titleLogoRandomImpact?.BeginAfterIntro();
    }

    private void ResolveTargets()
    {
        if (mainRect == null) mainRect = FindRectByName(NameMain);
        if (mainGraphic == null && mainRect != null)
        {
            mainGraphic = mainRect.GetComponent<Graphic>();
            if (mainGraphic == null) mainGraphic = mainRect.GetComponentInChildren<Graphic>(true);
        }

        if (magicCircleRect == null) magicCircleRect = FindRectByName(NameMagicCircle);
        if (magicCircleGraphic == null && magicCircleRect != null)
        {
            magicCircleGraphic = magicCircleRect.GetComponent<Graphic>();
            if (magicCircleGraphic == null) magicCircleGraphic = magicCircleRect.GetComponentInChildren<Graphic>(true);
        }

        if (swordRect == null) swordRect = FindRectByName(NameSword);

        if (subRoot == null)
        {
            var t = FindRectByName(NameSub);
            if (t != null) subRoot = t.gameObject;
        }

        _resolved = mainGraphic != null && magicCircleGraphic != null && swordRect != null && subRoot != null;

        if (!_resolved)
        {
            Debug.LogWarning(
                "[TitleLogoIntroController] 必須参照が足りません。TitleLogo(Main/MagicCircle/Sword/Sub) を RectTransform（＋各 Main/MagicCircle に Graphic）で用意するか、Inspector で割り当ててください。",
                this);
        }
    }

    private static RectTransform FindRectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;
        var rects = FindObjectsOfType<RectTransform>(true);
        foreach (var r in rects)
        {
            if (r.gameObject.name == objectName)
                return r;
        }

        return null;
    }

    private void CaptureSwordRestPose()
    {
        if (swordRect == null) return;
        _swordEndAnchored = swordRect.anchoredPosition;
        _swordEndRotationZ = swordRect.localEulerAngles.z;
    }

    private void ApplyIntroInitialState()
    {
        if (mainGraphic != null)
        {
            var c = _mainColorEnd;
            c.a = 0f;
            mainGraphic.color = c;
        }

        if (magicCircleGraphic != null)
            magicCircleGraphic.color = magicCircleColorStart;

        if (swordRect != null)
        {
            swordRect.anchoredPosition = swordStartAnchoredPosition;
            var e = swordRect.localEulerAngles;
            e.z = _swordEndRotationZ + swordSpinDegreesDuringSlide;
            swordRect.localEulerAngles = e;
        }

        SetBlurLayersForStart();

        if (subRoot != null)
            subRoot.SetActive(false);
    }

    private void SetBlurLayersForStart()
    {
        if (swordBlurLayers == null) return;
        var startZ = _swordEndRotationZ + swordSpinDegreesDuringSlide;
        foreach (var blur in swordBlurLayers)
        {
            if (blur == null) continue;
            blur.rectTransform.anchoredPosition = swordStartAnchoredPosition;
            var be = blur.rectTransform.localEulerAngles;
            be.z = startZ;
            blur.rectTransform.localEulerAngles = be;
            var bc = blur.color;
            bc.a = 0f;
            blur.color = bc;
        }
    }

    private void UpdateBlurLayersDuringSlide(float swordT)
    {
        if (swordBlurLayers == null || swordRect == null) return;
        var peak = Mathf.Sin(swordT * Mathf.PI);

        for (var i = 0; i < swordBlurLayers.Length; i++)
        {
            var blur = swordBlurLayers[i];
            if (blur == null) continue;

            var f = (i + 1f) / (swordBlurLayers.Length + 1f);
            blur.rectTransform.anchoredPosition =
                Vector2.Lerp(swordStartAnchoredPosition, swordRect.anchoredPosition, f);

            var bc = blur.color;
            bc.a = motionBlurMaxAlpha * peak * (1f - f * 0.5f);
            blur.color = bc;

            var be = blur.rectTransform.localEulerAngles;
            be.z = swordRect.localEulerAngles.z;
            blur.rectTransform.localEulerAngles = be;
        }
    }

    private void ClearBlurLayers()
    {
        if (swordBlurLayers == null) return;
        foreach (var blur in swordBlurLayers)
        {
            if (blur == null) continue;
            var bc = blur.color;
            bc.a = 0f;
            blur.color = bc;
        }
    }

    private void CancelAllTweens()
    {
        LeanTween.cancel(gameObject);
        if (mainGraphic != null) LeanTween.cancel(mainGraphic.gameObject);
        if (magicCircleGraphic != null) LeanTween.cancel(magicCircleGraphic.gameObject);
        if (swordRect != null)
        {
            LeanTween.cancel(swordRect);
            LeanTween.cancel(swordRect.gameObject);
        }
    }
}
