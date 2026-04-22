using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

/// <summary>
/// バトル終了後のリザルト画面 <c>Assets/Resources/Prefab/GameResult.prefab</c> を制御する。
/// Fade-in → TotalEarnedRP → 個別RP（上から順に） → ResultRPvalue → NextRankValue & Slider の順に演出。
/// BackToMainButton で Main シーンへ戻す。
/// </summary>
public class GameResultController : MonoBehaviour
{
    public enum ResultKind
    {
        Victory,
        Defeat,
        Stalemate,
    }

    [Header("結果テキスト")]
    [SerializeField] private TMP_Text resultJpText;
    [SerializeField] private TMP_Text resultEnText;

    [Header("プレイヤー情報")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerRankText;

    [Header("RP 表示")]
    [SerializeField] private TMP_Text totalEarnedRpText;
    [SerializeField] private TMP_Text basicRpText;
    [SerializeField] private TMP_Text underdogRpText;
    [SerializeField] private TMP_Text stylishRpText;
    [SerializeField] private TMP_Text rpCostText;
    [SerializeField] private TMP_Text resultRpValueText;
    [SerializeField] private TMP_Text nextRankValueText;

    [Header("ランクアイコン / スライダー")]
    [SerializeField] private Image rankIconAsIsImage;
    [SerializeField] private Image rankIconNextImage;
    [SerializeField] private Slider nextRankSlider;

    [Header("操作")]
    [SerializeField] private Button backToMainButton;
    [SerializeField] private string mainSceneName = "Main";

    [Header("ルートフェード")]
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("演出タイミング（秒）")]
    [SerializeField] private float fadeInDuration = 2f;
    [Header("ResultJP/EN タイトル")]
    [Tooltip("最終位置からの X 方向オフセット（px）。JP は +方向へ左から入る＝負、EN は +方向へ右から入る＝正。")]
    [SerializeField] private float resultTitleEnterOffsetX = 100f;
    [SerializeField] private float resultTitleEnterDuration = 1f;
    [Header("リザルト表示 → RP カウント")]
    [Tooltip("ルートフェード完了・タイトル演出後、ポイントカウント開始までの待ち秒数。")]
    [SerializeField] private float waitBeforePointCountSeconds = 2f;
    [SerializeField] private float totalEarnedCountDuration = 0.5f;
    [SerializeField] private float intervalBeforeIndividualRp = 0.5f;
    [SerializeField] private float individualRpCountDuration = 0.5f;
    [SerializeField] private float intervalBeforeResultValue = 0.5f;
    [SerializeField] private float resultValueCountDuration = 0.5f;
    [SerializeField] private float intervalBeforeNextRankValue = 0.2f;
    [SerializeField] private float nextRankCountDuration = 0.5f;

    [Header("プレースホルダー RP（Inspector から調整可）")]
    [Tooltip("Victory 時の basic/underdog/stylish/rpCost。未設定時はスクリプト既定値。")]
    [SerializeField] private RpBundle victoryRp = new RpBundle { basic = 100, underdog = 0, stylish = 0, rpCost = -30 };
    [SerializeField] private RpBundle defeatRp = new RpBundle { basic = 0, underdog = 0, stylish = 0, rpCost = -30 };
    [SerializeField] private RpBundle stalemateRp = new RpBundle { basic = 0, underdog = 0, stylish = 0, rpCost = -30 };

    [Serializable]
    public struct RpBundle
    {
        public int basic;
        public int underdog;
        public int stylish;
        public int rpCost;

        public int Total => basic + underdog + stylish + rpCost;
    }

    [Tooltip("バトル UI より手前に描画するための Canvas.sortingOrder（他 Canvas と被る場合に上げる）。")]
    [SerializeField] private int resultCanvasSortingOrder = 5000;

    [Header("BGM（Address）")]
    [SerializeField] private string bgmVictoryAddress = "Assets/Music/ゲームクリアー！.mp3";
    [SerializeField] private string bgmDefeatAddress = "Assets/Music/アンドロイドの涙.mp3";
    [SerializeField] private string bgmStalemateAddress = "Assets/Music/COLORS.mp3";

    [Header("SE（Address）")]
    [SerializeField] private string seRouletteLoopAddress = "Assets/SE/電子ルーレット回転中.mp3";
    [SerializeField] private string seRouletteStopAddress = "Assets/SE/電子ルーレット停止ボタンを押す.mp3";
    [SerializeField] private string seResultRpGaugeAddress = "Assets/SE/ゲージ回復2.mp3";
    [SerializeField] private string seBackToMainCursorAddress = "Assets/SE/カーソル移動1.mp3";

    [Tooltip("未設定時は子に生成。リザルト専用 BGM 用。")]
    [SerializeField] private AudioSource resultBgmSource;

    private Vector2 _resultTitleJpAnchoredEnd;
    private Vector2 _resultTitleEnAnchoredEnd;
    private bool _resultTitleEndCaptured;

    private void Awake()
    {
        if (rootCanvasGroup == null)
            rootCanvasGroup = GetComponent<CanvasGroup>();
        if (rootCanvasGroup == null)
            rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        EnsureChildCanvasesVisibleAndOnTop();

        rootCanvasGroup.alpha = 0f;
        rootCanvasGroup.blocksRaycasts = false;
        rootCanvasGroup.interactable = false;

        ClearAllNumericTexts();

        if (backToMainButton != null)
        {
            backToMainButton.interactable = false;
            backToMainButton.onClick.AddListener(OnBackToMainClicked);
        }

        EnsureResultBgmSource();
    }

    private void OnDestroy()
    {
        if (resultBgmSource != null)
            resultBgmSource.Stop();
        if (SoundEffectPlayer.I != null)
            SoundEffectPlayer.I.StopLooping();
    }

    private void EnsureResultBgmSource()
    {
        if (resultBgmSource == null)
        {
            var child = new GameObject("ResultBGM");
            child.transform.SetParent(transform, false);
            resultBgmSource = child.AddComponent<AudioSource>();
            resultBgmSource.playOnAwake = false;
            resultBgmSource.loop = true;
            resultBgmSource.spatialBlend = 0f;
        }
    }

    /// <summary>
    /// Prefab 誤設定で子 Canvas の localScale が 0 だと一切表示されないため矯正する。
    /// さらに Battle 用 Canvas より手前に出す。
    /// </summary>
    private void EnsureChildCanvasesVisibleAndOnTop()
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

    private void ClearAllNumericTexts()
    {
        SetTextIfPresent(totalEarnedRpText, string.Empty);
        SetTextIfPresent(basicRpText, string.Empty);
        SetTextIfPresent(underdogRpText, string.Empty);
        SetTextIfPresent(stylishRpText, string.Empty);
        SetTextIfPresent(rpCostText, string.Empty);
        SetTextIfPresent(resultRpValueText, string.Empty);
        SetTextIfPresent(nextRankValueText, string.Empty);
        if (nextRankSlider != null)
        {
            // プレイヤーの「現在進捗」を Slider に反映（0..threshold）。
            int threshold = GameProfile.I != null ? Mathf.Max(1, GameProfile.I.NextRankThresholdRP) : 1500;
            int preRp = GameProfile.I != null ? GameProfile.I.PreBattleRP : 0;
            nextRankSlider.minValue = 0f;
            nextRankSlider.maxValue = 1f;
            nextRankSlider.value = Mathf.Clamp01((float)preRp / threshold);
        }
    }

    /// <summary>
    /// 結果演出の本体。kind は勝敗、RP は Inspector のプレースホルダーを使用する。
    /// </summary>
    public async Task ShowAsync(ResultKind kind, CancellationToken ct = default)
    {
        RpBundle rp = kind switch
        {
            ResultKind.Victory => victoryRp,
            ResultKind.Defeat => defeatRp,
            _ => stalemateRp,
        };

        await ShowAsync(kind, rp, ct);
    }

    /// <summary>
    /// 結果演出の本体（RP を外部から渡す版）。
    /// </summary>
    public async Task ShowAsync(ResultKind kind, RpBundle rp, CancellationToken ct)
    {
        ApplyHeaderTexts(kind);
        ApplyProfileTexts();
        ApplyRankIcons();
        EnsureResultBgmSource();

        PrepareResultTitleIntroStart();

        // 1. リザルト BGM ＆ルートフェード同時（フェード既定 2 秒）
        var bgmTask = StartResultBgmForKindAsync(kind, ct);
        var fadeTask = FadeRootAsync(0f, 1f, fadeInDuration, ct);
        await Task.WhenAll(bgmTask, fadeTask);
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.blocksRaycasts = true;
            rootCanvasGroup.interactable = true;
        }

        // 2. ResultJP / ResultEN：左右から位置＋アルファで 1 秒
        await AnimateResultTitlesIntroAsync(ct);

        // 3. リザルト表示後、ポイントカウントまで待つ
        await DelaySeconds(waitBeforePointCountSeconds, ct);

        // 4. TotalEarnedRP 0 → total（ルーレット系 SE）
        int total = rp.Total;
        await CountUpTextWithRouletteSeAsync(totalEarnedRpText, 0, total, totalEarnedCountDuration, true, ct);

        // 5. インターバル
        await DelaySeconds(intervalBeforeIndividualRp, ct);

        // 6. 個別 RP
        await CountUpTextWithRouletteSeAsync(basicRpText, 0, rp.basic, individualRpCountDuration, true, ct);
        await CountUpTextWithRouletteSeAsync(underdogRpText, 0, rp.underdog, individualRpCountDuration, true, ct);
        await CountUpTextWithRouletteSeAsync(stylishRpText, 0, rp.stylish, individualRpCountDuration, true, ct);
        await CountUpTextWithRouletteSeAsync(rpCostText, 0, rp.rpCost, individualRpCountDuration, true, ct);

        // 7. インターバル
        await DelaySeconds(intervalBeforeResultValue, ct);

        // 8. ResultRPvalue：SE 秒数＝演出秒数
        int preRp = GameProfile.I != null ? GameProfile.I.PreBattleRP : 0;
        int newRp = preRp + total;
        AudioClip gaugeClip = await LoadAudioClipAddressAsync(seResultRpGaugeAddress, ct);
        float resultRpAnimSeconds = gaugeClip != null
            ? gaugeClip.length
            : resultValueCountDuration;
        if (preRp == newRp)
        {
            if (resultRpValueText != null)
                resultRpValueText.text = newRp.ToString();
        }
        else
        {
            if (gaugeClip != null)
                SoundEffectPlayer.I?.Play(gaugeClip);
            await CountUpTextAsync(resultRpValueText, preRp, newRp, resultRpAnimSeconds, false, ct);
        }

        // 9. インターバル
        await DelaySeconds(intervalBeforeNextRankValue, ct);

        // 10. NextRankValue（残り値）＋ NextRankSlider を同期
        int threshold = GameProfile.I != null ? Mathf.Max(1, GameProfile.I.NextRankThresholdRP) : Mathf.Max(1, newRp);
        int fromRemain = Mathf.Max(0, threshold - preRp);
        int toRemain = Mathf.Max(0, threshold - newRp);
        float fromSlider = Mathf.Clamp01((float)preRp / threshold);
        float toSlider = Mathf.Clamp01((float)newRp / threshold);

        await CountUpWithSliderAsync(
            nextRankValueText,
            nextRankSlider,
            fromRemain,
            toRemain,
            fromSlider,
            toSlider,
            nextRankCountDuration,
            ct);

        // 11. BackToMain を押せるようにする
        if (backToMainButton != null)
            backToMainButton.interactable = true;

        // 12. プレースホルダー RP 反映
        if (GameProfile.I != null)
            GameProfile.I.ApplyBattleResult(total);
    }

    private async Task StartResultBgmForKindAsync(ResultKind kind, CancellationToken ct)
    {
        if (resultBgmSource == null) return;
        string path = kind switch
        {
            ResultKind.Victory => bgmVictoryAddress,
            ResultKind.Defeat => bgmDefeatAddress,
            _ => bgmStalemateAddress,
        };
        var clip = await LoadAudioClipAddressAsync(path, ct);
        if (clip == null) return;
        if (ct.IsCancellationRequested) return;
        resultBgmSource.Stop();
        resultBgmSource.clip = clip;
        resultBgmSource.loop = true;
        resultBgmSource.volume = 0.45f;
        resultBgmSource.Play();
    }

    private static async Task<AudioClip> LoadAudioClipAddressAsync(string address, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(address)) return null;
        var h = Addressables.LoadAssetAsync<AudioClip>(address);
        var tcs = new TaskCompletionSource<AudioClip>();
        h.Completed += op =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
                tcs.TrySetResult(op.Result);
            else
            {
                if (op.Status != AsyncOperationStatus.Succeeded)
                    Debug.LogWarning("[GameResultController] 音声読み込み失敗: " + address);
                tcs.TrySetResult(null);
            }
        };
        return await tcs.Task;
    }

    private async Task CountUpTextWithRouletteSeAsync(
        TMP_Text target,
        int from,
        int to,
        float seconds,
        bool formatSigned,
        CancellationToken ct)
    {
        if (target == null) return;
        if (from == to || seconds < 0.02f)
        {
            target.text = FormatRpValue(to, formatSigned);
            return;
        }
        if (SoundEffectPlayer.I == null)
        {
            await CountUpTextAsync(target, from, to, seconds, formatSigned, ct);
            return;
        }
        try
        {
            await SoundEffectPlayer.I.StartLoopingAsync(seRouletteLoopAddress);
        }
        catch (Exception e)
        {
            Debug.LogWarning(e.Message);
        }
        try
        {
            await CountUpTextAsync(target, from, to, seconds, formatSigned, ct);
        }
        finally
        {
            SoundEffectPlayer.I?.StopLooping();
        }
        SoundEffectPlayer.I?.Play(seRouletteStopAddress);
    }

    private void PrepareResultTitleIntroStart()
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

    private static void SetTmpAlpha(TMP_Text tmp, float a)
    {
        if (tmp == null) return;
        var c = tmp.color;
        c.a = a;
        tmp.color = c;
    }

    private async Task AnimateResultTitlesIntroAsync(CancellationToken ct)
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
            float a = u;
            if (rtJ != null)
            {
                rtJ.anchoredPosition = Vector2.Lerp(sJ, _resultTitleJpAnchoredEnd, u);
                SetTmpAlpha(resultJpText, a);
            }
            if (rtE != null)
            {
                rtE.anchoredPosition = Vector2.Lerp(sE, _resultTitleEnAnchoredEnd, u);
                SetTmpAlpha(resultEnText, a);
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

    private void ApplyHeaderTexts(ResultKind kind)
    {
        switch (kind)
        {
            case ResultKind.Victory:
                SetTextIfPresent(resultJpText, "勝利");
                SetTextIfPresent(resultEnText, "VICTORY");
                break;
            case ResultKind.Defeat:
                SetTextIfPresent(resultJpText, "敗北");
                SetTextIfPresent(resultEnText, "DEFEAT");
                break;
            case ResultKind.Stalemate:
            default:
                SetTextIfPresent(resultJpText, "全滅");
                SetTextIfPresent(resultEnText, "STALEMATE");
                break;
        }
    }

    private void ApplyProfileTexts()
    {
        string name = (GameProfile.I != null) ? GameProfile.I.PlayerName : "プレイヤー";
        string rank = (GameProfile.I != null) ? GameProfile.I.RankDisplayName : "Placeholder";
        SetTextIfPresent(playerNameText, name);
        SetTextIfPresent(playerRankText, rank);
    }

    private void ApplyRankIcons()
    {
        if (GameProfile.I == null) return;
        if (rankIconAsIsImage != null && GameProfile.I.CurrentRankIcon != null)
            rankIconAsIsImage.sprite = GameProfile.I.CurrentRankIcon;
        if (rankIconNextImage != null && GameProfile.I.NextRankIcon != null)
            rankIconNextImage.sprite = GameProfile.I.NextRankIcon;
    }

    private async Task FadeRootAsync(float from, float to, float seconds, CancellationToken ct)
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

    private async Task CountUpTextAsync(TMP_Text target, int from, int to, float seconds, bool formatSigned, CancellationToken ct)
    {
        if (target == null) return;
        float dur = Mathf.Max(0.01f, seconds);
        float elapsed = 0f;
        target.text = FormatRpValue(from, formatSigned);
        while (elapsed < dur)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            int current = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            target.text = FormatRpValue(current, formatSigned);
            await Task.Yield();
        }
        target.text = FormatRpValue(to, formatSigned);
    }

    private async Task CountUpWithSliderAsync(
        TMP_Text target,
        Slider slider,
        int fromRemain,
        int toRemain,
        float fromSlider,
        float toSlider,
        float seconds,
        CancellationToken ct)
    {
        float dur = Mathf.Max(0.01f, seconds);
        float elapsed = 0f;

        if (target != null)
            target.text = FormatNextRankRemain(fromRemain);
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = fromSlider;
        }

        while (elapsed < dur)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            int remain = Mathf.RoundToInt(Mathf.Lerp(fromRemain, toRemain, t));
            if (target != null)
                target.text = FormatNextRankRemain(remain);
            if (slider != null)
                slider.value = Mathf.Lerp(fromSlider, toSlider, t);
            await Task.Yield();
        }

        if (target != null)
            target.text = FormatNextRankRemain(toRemain);
        if (slider != null)
            slider.value = toSlider;
    }

    private static string FormatRpValue(int value, bool formatSigned)
    {
        if (!formatSigned) return value.ToString();
        return value > 0 ? $"+{value}" : value.ToString();
    }

    private static string FormatNextRankRemain(int remain)
    {
        return $"{remain} RP";
    }

    private static Task DelaySeconds(float seconds, CancellationToken ct)
    {
        int ms = Mathf.Max(0, Mathf.RoundToInt(seconds * 1000f));
        return Task.Delay(ms, ct);
    }

    private static void SetTextIfPresent(TMP_Text text, string value)
    {
        if (text != null) text.text = value ?? string.Empty;
    }

    private void OnBackToMainClicked()
    {
        SoundEffectPlayer.I?.Play(seBackToMainCursorAddress);
        if (SceneTransitionManager.I != null)
        {
            SceneTransitionManager.I.FadeToScene(mainSceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainSceneName);
        }
    }
}
