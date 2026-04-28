using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// メイン画面起動時：黒帯（シネマスコープ風）を 0.5 秒でフェードインしつつ、
/// 上帯（Scope2）は画面上外から、下帯（Scope1）は画面下外から、それぞれシーン上の anchoredPosition へ合わせます（Pos X=835 / -835 等は配置のまま）。
/// 未割当なら "Scope2" / "Scope1" を名前で探索します（アクティブのみ GameObject.Find）。
/// </summary>
/// <remarks>
/// DOTween を Plugins の DLL だけ入れていると、デフォルトの Assembly-CSharp から参照が解決されないことがあります。
/// 同プロジェクト同梱の LeanTween で同じ挙動にしています。DOTween を使うなら専用 asmdef でその DLL を参照するか、UPM 版で参照を揃えてください。
/// </remarks>
public sealed class MainCinemascopeScopeIntroController : MonoBehaviour
{
    [Header("ターゲット（未割当で名前検索: Scope2=上, Scope1=下）")]
    [SerializeField] private RectTransform scope2Top;
    [SerializeField] private RectTransform scope1Bottom;
    [SerializeField] private Graphic graphicScope2;
    [SerializeField] private Graphic graphicScope1;

    [Header("演出")]
    [SerializeField] [Min(0.01f)] private float introDuration = 0.5f;
    [SerializeField] private LeanTweenType positionEase = LeanTweenType.easeOutCubic;
    [SerializeField] [Min(0f)] private float offscreenOffset = 1500f;
    [Tooltip("Time.timeScale=0 でも入場したい場合オンに。")]
    [SerializeField] private bool useUnscaledTime;

    private Vector2 _endTop;
    private Vector2 _endBottom;

    private void Awake()
    {
        ResolveTargets();
        if (scope2Top == null || scope1Bottom == null)
        {
            Debug.LogWarning(
                "MainCinemascopeScopeIntroController: Scope2 / Scope1 の RectTransform を参照できません。名前または Inspector で割り当ててください。",
                this);
            return;
        }

        if (graphicScope2 == null) graphicScope2 = scope2Top.GetComponent<Graphic>();
        if (graphicScope2 == null) graphicScope2 = scope2Top.GetComponentInChildren<Graphic>(true);
        if (graphicScope1 == null) graphicScope1 = scope1Bottom.GetComponent<Graphic>();
        if (graphicScope1 == null) graphicScope1 = scope1Bottom.GetComponentInChildren<Graphic>(true);
        if (graphicScope2 == null || graphicScope1 == null)
        {
            Debug.LogWarning(
                "MainCinemascopeScopeIntroController: Scope1/2 に Image 等の Graphic が同一オブジェクトまたは子にありません。フェードしません。移動のみ試みます。",
                this);
        }

        _endTop = scope2Top.anchoredPosition;
        _endBottom = scope1Bottom.anchoredPosition;
        ApplyInitialHiddenState();
    }

    /// <summary>Play 毎回・Enter Play オプション（ドメイン/シーン再読み込みの制御）の影響で、Start だけに依存すると 2 回目以降に再生されないことがあるため、有効化のたびに再生する。</summary>
    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (scope2Top == null || scope1Bottom == null) return;
        PlayIntro();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        CancelScopeTweens();
    }

    private void OnDestroy()
    {
        CancelScopeTweens();
    }

    private void ResolveTargets()
    {
        if (scope2Top == null) scope2Top = FindRect("Scope2");
        if (scope1Bottom == null) scope1Bottom = FindRect("Scope1");
    }

    private static RectTransform FindRect(string goName)
    {
        if (string.IsNullOrEmpty(goName)) return null;
        var go = GameObject.Find(goName);
        return go == null ? null : go.GetComponent<RectTransform>();
    }

    private void ApplyInitialHiddenState()
    {
        var c2 = graphicScope2 != null ? graphicScope2.color : Color.white;
        c2.a = 0f;
        if (graphicScope2 != null) graphicScope2.color = c2;
        var c1 = graphicScope1 != null ? graphicScope1.color : Color.white;
        c1.a = 0f;
        if (graphicScope1 != null) graphicScope1.color = c1;

        scope2Top.anchoredPosition = _endTop + new Vector2(0f, offscreenOffset);
        scope1Bottom.anchoredPosition = _endBottom - new Vector2(0f, offscreenOffset);
    }

    private void CancelScopeTweens()
    {
        if (scope2Top != null) LeanTween.cancel(scope2Top);
        if (scope1Bottom != null) LeanTween.cancel(scope1Bottom);
        if (graphicScope2 != null) LeanTween.cancel(graphicScope2.rectTransform);
        if (graphicScope1 != null) LeanTween.cancel(graphicScope1.rectTransform);
    }

    private void PlayIntro()
    {
        if (scope2Top == null || scope1Bottom == null) return;

        CancelScopeTweens();
        LeanTween.init();

        var endTop3 = new Vector3(_endTop.x, _endTop.y, 0f);
        var endBottom3 = new Vector3(_endBottom.x, _endBottom.y, 0f);

        var moveTop = LeanTween.move(scope2Top, endTop3, introDuration).setEase(positionEase);
        var moveBottom = LeanTween.move(scope1Bottom, endBottom3, introDuration).setEase(positionEase);
        if (useUnscaledTime)
        {
            moveTop.setIgnoreTimeScale(true);
            moveBottom.setIgnoreTimeScale(true);
        }

        if (graphicScope2 != null)
        {
            var t = LeanTween.alpha(graphicScope2.rectTransform, 1f, introDuration);
            if (useUnscaledTime) t.setIgnoreTimeScale(true);
        }
        if (graphicScope1 != null)
        {
            var b = LeanTween.alpha(graphicScope1.rectTransform, 1f, introDuration);
            if (useUnscaledTime) b.setIgnoreTimeScale(true);
        }
    }
}
