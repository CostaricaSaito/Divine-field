using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NPC（オフライン）戦のリザルト <c>Assets/Resources/Prefab/NPCResult.prefab</c>。
/// フェードインと勝敗テキストのスライドインのみ。RP は変動しない。
/// </summary>
public sealed class NPCResultController : MonoBehaviour
{
    [Header("結果テキスト")]
    [SerializeField] private TMP_Text resultJpText;
    [SerializeField] private TMP_Text resultEnText;

    [Header("操作")]
    [SerializeField] private Button backToMainButton;
    [SerializeField] private Button continueBattleButton;
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string battleSceneName = "Battle";

    [Header("ルートフェード")]
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("演出タイミング（秒）")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float resultTitleEnterOffsetX = 100f;
    [SerializeField] private float resultTitleEnterDuration = 1f;

    [Tooltip("バトル UI より手前に描画するための Canvas.sortingOrder。")]
    [SerializeField] private int resultCanvasSortingOrder = 5000;

    [Header("SE（Address）")]
    [SerializeField] private string seButtonCursorAddress = "Assets/SE/カーソル移動1.mp3";

    Vector2 _resultTitleJpAnchoredEnd;
    Vector2 _resultTitleEnAnchoredEnd;
    bool _resultTitleEndCaptured;

    void Awake()
    {
        if (rootCanvasGroup == null)
            rootCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (rootCanvasGroup == null)
            rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        EnsureChildCanvasesVisibleAndOnTop();

        rootCanvasGroup.alpha = 0f;
        rootCanvasGroup.blocksRaycasts = false;
        rootCanvasGroup.interactable = false;

        if (backToMainButton != null)
        {
            backToMainButton.interactable = false;
            backToMainButton.onClick.AddListener(OnBackToMainClicked);
        }

        if (continueBattleButton != null)
        {
            continueBattleButton.interactable = false;
            continueBattleButton.onClick.AddListener(OnContinueBattleClicked);
        }
    }

    void OnDestroy()
    {
        if (backToMainButton != null)
            backToMainButton.onClick.RemoveListener(OnBackToMainClicked);
        if (continueBattleButton != null)
            continueBattleButton.onClick.RemoveListener(OnContinueBattleClicked);
    }

    public async Task ShowAsync(GameResultController.ResultKind kind, CancellationToken ct = default)
    {
        ApplyHeaderTexts(kind);
        PrepareResultTitleIntroStart();

        await FadeRootAsync(0f, 1f, fadeInDuration, ct);
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.blocksRaycasts = true;
            rootCanvasGroup.interactable = true;
        }

        await AnimateResultTitlesIntroAsync(ct);

        if (backToMainButton != null)
            backToMainButton.interactable = true;
        if (continueBattleButton != null)
            continueBattleButton.interactable = true;

        RecordNpcMatchEnd(kind);
    }

    void EnsureChildCanvasesVisibleAndOnTop()
    {
        foreach (var canvas in GetComponentsInChildren<Canvas>(true))
        {
            if (canvas == null) continue;
            var rt = canvas.transform as RectTransform;
            if (rt != null && rt.localScale.sqrMagnitude < 1e-6f)
                rt.localScale = Vector3.one;

            canvas.overrideSorting = true;
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, resultCanvasSortingOrder);
        }
    }

    void ApplyHeaderTexts(GameResultController.ResultKind kind)
    {
        switch (kind)
        {
            case GameResultController.ResultKind.Victory:
                SetTextIfPresent(resultJpText, "勝利");
                SetTextIfPresent(resultEnText, "VICTORY");
                break;
            case GameResultController.ResultKind.Defeat:
                SetTextIfPresent(resultJpText, "敗北");
                SetTextIfPresent(resultEnText, "DEFEAT");
                break;
            case GameResultController.ResultKind.Stalemate:
            default:
                SetTextIfPresent(resultJpText, "全滅");
                SetTextIfPresent(resultEnText, "STALEMATE");
                break;
        }
    }

    void PrepareResultTitleIntroStart()
    {
        _resultTitleEndCaptured = false;
        if (resultJpText != null)
        {
            var rt = resultJpText.rectTransform;
            _resultTitleJpAnchoredEnd = rt.anchoredPosition;
            rt.anchoredPosition = _resultTitleJpAnchoredEnd + new Vector2(-resultTitleEnterOffsetX, 0f);
            SetTmpAlpha(resultJpText, 0f);
        }

        if (resultEnText != null)
        {
            var rt = resultEnText.rectTransform;
            _resultTitleEnAnchoredEnd = rt.anchoredPosition;
            rt.anchoredPosition = _resultTitleEnAnchoredEnd + new Vector2(resultTitleEnterOffsetX, 0f);
            SetTmpAlpha(resultEnText, 0f);
        }

        _resultTitleEndCaptured = true;
    }

    static void SetTmpAlpha(TMP_Text tmp, float a)
    {
        if (tmp == null) return;
        var c = tmp.color;
        c.a = a;
        tmp.color = c;
    }

    async Task AnimateResultTitlesIntroAsync(CancellationToken ct)
    {
        if (!_resultTitleEndCaptured) return;
        if (resultJpText == null && resultEnText == null) return;

        float dur = Mathf.Max(0.01f, resultTitleEnterDuration);
        var rtJ = resultJpText != null ? resultJpText.rectTransform : null;
        var rtE = resultEnText != null ? resultEnText.rectTransform : null;
        Vector2 sJ = rtJ != null ? rtJ.anchoredPosition : default;
        Vector2 sE = rtE != null ? rtE.anchoredPosition : default;

        float t = 0f;
        while (t < dur)
        {
            ct.ThrowIfCancellationRequested();
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            if (rtJ != null)
            {
                rtJ.anchoredPosition = Vector2.Lerp(sJ, _resultTitleJpAnchoredEnd, u);
                SetTmpAlpha(resultJpText, u);
            }

            if (rtE != null)
            {
                rtE.anchoredPosition = Vector2.Lerp(sE, _resultTitleEnAnchoredEnd, u);
                SetTmpAlpha(resultEnText, u);
            }

            await Task.Yield();
        }

        if (rtJ != null)
        {
            rtJ.anchoredPosition = _resultTitleJpAnchoredEnd;
            SetTmpAlpha(resultJpText, 1f);
        }

        if (rtE != null)
        {
            rtE.anchoredPosition = _resultTitleEnAnchoredEnd;
            SetTmpAlpha(resultEnText, 1f);
        }
    }

    async Task FadeRootAsync(float from, float to, float seconds, CancellationToken ct)
    {
        if (rootCanvasGroup == null) return;
        float dur = Mathf.Max(0.01f, seconds);
        float elapsed = 0f;
        rootCanvasGroup.alpha = from;
        while (elapsed < dur)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            rootCanvasGroup.alpha = Mathf.Lerp(from, to, t);
            await Task.Yield();
        }

        rootCanvasGroup.alpha = to;
    }

    static void RecordNpcMatchEnd(GameResultController.ResultKind kind)
    {
        PlayerProfileService.EnsureLoaded();
        int currentRp = GameProfile.I != null
            ? GameProfile.I.CurrentRP
            : Mathf.Max(0, PlayerProfileService.Data.currentRp);

        string summonId = "unknown";
        if (SummonSelectionManager.I != null)
        {
            var sd = SummonSelectionManager.I.GetSelectedSummonData();
            if (sd != null)
                summonId = sd.StableSummonId;
        }

        PlayerProfileService.RecordMatchEnd(kind, summonId, currentRp);
    }

    void OnBackToMainClicked()
    {
        SoundEffectPlayer.I?.Play(seButtonCursorAddress);
        if (SceneTransitionManager.I != null)
            SceneTransitionManager.I.FadeToScene(mainSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainSceneName);
    }

    void OnContinueBattleClicked()
    {
        SoundEffectPlayer.I?.Play(seButtonCursorAddress);
        if (!SceneFadeNavigation.TryFadeToScene(battleSceneName))
            UnityEngine.SceneManagement.SceneManager.LoadScene(battleSceneName);
    }

    static void SetTextIfPresent(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }
}
