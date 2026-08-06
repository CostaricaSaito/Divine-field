using System;
using System.Collections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ダメージ／回復／メッセージ用フローティングテキスト。
/// 配色・背景は <see cref="DamagePopupSettings"/>（MessagePopup と同様の ScriptableObject）。
/// レイアウト（パネル内位置・Rect）は BattleUIManager とプレハブ側。このクラスは主に文言・色・浮き／フェード。
/// </summary>
public class DamagePopup : MonoBehaviour
{
    // --- 参照（実行時にルートから取得。プレハブのルートに Image が無い場合はパネル着色なし）---
    private Image _rootPanelImage;
    // valueText（DamageValue）… ダメージ数値（1段目の大きい数字）専用。レイアウトは数字向け。
    [SerializeField] private TMP_Text valueText;
    // labelText … 「ダメージ」の小さい行（数値ダメージ時のみ）。
    [SerializeField] private TMP_Text labelText;
    // messageText（Message）… 「無傷」「衰弱」など語句・状態異常名。DamageValue とは別 Rect で中央寄せしやすくする。
    [SerializeField] private TMP_Text messageText;

    [Header("Message（TextMeshPro Auto Size）")]
    [Tooltip("長い文言はこの範囲で縮小され、Rect 内に収まりやすくなります。プレハブの Message の Rect 幅・高さも確認してください。")]
    [SerializeField] [Range(8f, 80f)] private float messageFontSizeMin = 22f;
    [SerializeField] [Range(40f, 200f)] private float messageFontSizeMax = 160f;

    [Header("Style (optional override)")]
    [Tooltip("未設定時は BattlePopupPresenter または Resources/DamagePopupSettings を参照。")]
    [SerializeField] private DamagePopupSettings settingsOverride;

    private DamagePopupSettings _boundSettings;
    private Sprite _defaultBackgroundSprite;
    private Image.Type _defaultImageType;
    private float _defaultPixelsPerUnitMultiplier;
    private bool _defaultBackgroundCached;

    // --- 演出パラメータ（Inspector からも変更可）---
    // floatSpeed … 上方向に漂う速度。大きいほど速く上に抜ける（ワールド／ローカルは親の向き依存。通常は上へ）。
    public float floatSpeed = 30f;
    // fadeDuration … 何秒かけて透明になるか。大きいほど長く残る。Destroy もこの秒後。
    public float fadeDuration = 0.5f;

    /// <summary>UI 生成に失敗したときなど、闇フォロー前の待ちに使う既定秒数（<see cref="fadeDuration"/> のデフォルトと一致）。</summary>
    public const float DefaultFadeDurationIfUnknown = 1f;

    /// <summary>
    /// ポップアップが画面上に残る時間（<see cref="fadeDuration"/>）のあと、次の処理までの標準インターバル（ms）。
    /// 待機は「表示開始と同時」ではなく、<see cref="WaitAfterPopupLifetimeAsync"/> で <b>寿命終了後</b>に挟む。
    /// </summary>
    public const int PostPopupIntervalMs = 250;

    /// <summary>
    /// ダメージポップ表示完了後、<see cref="StatusEffectApplyTiming.WithDamageThrough"/> の付与処理に入るまでの待ち（旧 1000ms の半分）。
    /// </summary>
    public const int PreStatusEffectAfterDamagePopupDelayMs = 500;

    /// <summary>
    /// ダメージ／回復／状態異常付与など、最後に出したフローティングポップの
    /// 「寿命＋<see cref="PostPopupIntervalMs"/>」まで待った<strong>あと</strong>、CombatResolve（TurnEnd 系）へ進む直前の共通インターバル。
    /// </summary>
    public const int PostLastPresentationBeforeCombatResolveMs = 400;

    /// <summary>即時効果解決の直前など、回復ポップアップより前に置く短い間（カード詳細の読み取り用）。</summary>
    public const int PreImmediateEffectDelayMs = 250;

    /// <summary>戦闘ダメージ数値ポップアップの直前の短い間（命中演出の間）。</summary>
    public const int PreDamagePopupBeatMs = 500;

    /// <summary>ShowDamagePopup / ShowHealPopup 等が返す秒数を正規化（0 以下は <see cref="DefaultFadeDurationIfUnknown"/>）。</summary>
    public static float NormalizedFadeSeconds(float fadeSecondsReturnedFromShow)
    {
        return fadeSecondsReturnedFromShow > 0f ? fadeSecondsReturnedFromShow : DefaultFadeDurationIfUnknown;
    }

    /// <summary>コルーチン用：ポップアップ寿命＋ポストインターバルの合計秒。</summary>
    public static float TotalSecondsAfterPopupShown(float fadeSecondsReturnedFromShow)
    {
        return NormalizedFadeSeconds(fadeSecondsReturnedFromShow) + PostPopupIntervalMs / 1000f;
    }

    /// <summary>
    /// ポップアップ表示<strong>後</strong>、画面上の寿命（フェード）が終わるまで待ち、続けて <see cref="PostPopupIntervalMs"/> 待つ。
    /// </summary>
    public static async Task WaitAfterPopupLifetimeAsync(float fadeSecondsReturnedFromShow, CancellationToken cancellationToken = default)
    {
        float fade = NormalizedFadeSeconds(fadeSecondsReturnedFromShow);
        await Task.Delay(TimeSpan.FromSeconds(fade), cancellationToken);
        await Task.Delay(PostPopupIntervalMs, cancellationToken);
    }

    private float timer;
    // CanvasGroup … ない場合はフェードなし（透明度は変わらず、そのまま消えるまで表示）。
    private CanvasGroup canvasGroup;

    private enum PopupRunMode
    {
        Normal,
        DiseaseWorsenPhase1Float,
        DiseaseWorsenSequenceManual,
    }

    private PopupRunMode _runMode = PopupRunMode.Normal;
    private float _diseasePhase1Duration;
    private TaskCompletionSource<bool> _diseasePhase1Tcs;

    public void BindSettings(DamagePopupSettings settings) => _boundSettings = settings;

    private DamagePopupSettings ResolveSettings() =>
        _boundSettings ?? settingsOverride ?? DamagePopupSettings.GetRuntimeFallback();

    private void Awake()
    {
        // valueText 未割り当て時の保険：子の TMP を1つ拾う（Message 追加後は Inspector 割り当て推奨）。
        if (valueText == null)
            valueText = GetComponentInChildren<TMP_Text>(true);
        _rootPanelImage = GetComponent<Image>();
        CacheDefaultBackgroundState();
    }

    private void CacheDefaultBackgroundState()
    {
        if (_rootPanelImage == null || _defaultBackgroundCached) return;
        _defaultBackgroundSprite = _rootPanelImage.sprite;
        _defaultImageType = _rootPanelImage.type;
        _defaultPixelsPerUnitMultiplier = _rootPanelImage.pixelsPerUnitMultiplier;
        _defaultBackgroundCached = true;
    }

    private void ApplyBackground(DamagePopupStyleEntry entry)
    {
        if (_rootPanelImage == null) return;
        CacheDefaultBackgroundState();

        if (entry.UsesSpriteBackground)
        {
            _rootPanelImage.sprite = entry.backgroundSprite;
            _rootPanelImage.type = Image.Type.Simple;
            _rootPanelImage.preserveAspect = false;
            _rootPanelImage.color = Color.white;
            return;
        }

        if (_defaultBackgroundSprite != null)
            _rootPanelImage.sprite = _defaultBackgroundSprite;
        _rootPanelImage.type = _defaultImageType;
        _rootPanelImage.pixelsPerUnitMultiplier = _defaultPixelsPerUnitMultiplier;
        _rootPanelImage.preserveAspect = false;

        if (entry.backgroundColor.a > 0.001f)
            _rootPanelImage.color = entry.backgroundColor;
    }

    private Color ResolveDefaultSimpleOutline() =>
        ResolveSettings().GetEntryOrDefault(DamagePopupKind.Miss).outlineColor;

    public void SetupFromKind(DamagePopupKind kind, string messageOverride = null)
    {
        var entry = ResolveSettings().GetEntryOrDefault(kind);
        switch (kind)
        {
            case DamagePopupKind.ReflectionBounce:
                SetupReflectionBounce(messageOverride ?? entry.message);
                return;
            case DamagePopupKind.BlockingNullify:
                SetupBlockingNullify(messageOverride ?? entry.message);
                return;
            case DamagePopupKind.ParryIntro:
                SetupParryYellowBanner(messageOverride ?? entry.message);
                return;
            case DamagePopupKind.HandReload:
            case DamagePopupKind.HandDiscardRestart:
                SetupHandReloadFromEntry(entry, messageOverride);
                return;
            default:
                SetupStyledMessage(entry, messageOverride, statusAilmentAutoSize: false);
                return;
        }
    }

    public void SetupStyledMessage(
        DamagePopupStyleEntry entry,
        string messageOverride = null,
        bool statusAilmentAutoSize = false)
    {
        ApplyBackground(entry);
        string text = string.IsNullOrEmpty(messageOverride) ? entry.message : messageOverride;
        if (entry.useRainbowText)
        {
            SetupRainbowMessage(text, entry.outlineColor);
            return;
        }

        ShowMessageLayout(text, entry.textColor, entry.outlineColor, statusAilmentAutoSize);
    }

    private void SetupHandReloadFromEntry(DamagePopupStyleEntry entry, string messageOverride)
    {
        ApplyBackground(entry);
        string text = string.IsNullOrEmpty(messageOverride) ? entry.message : messageOverride;
        var t = messageText != null ? messageText : valueText;
        if (t != null)
            t.richText = false;
        ShowMessageLayout(text, entry.textColor, entry.outlineColor, statusAilmentAutoSize: false);
        if (t != null)
        {
            if (t.fontSharedMaterial == null && t.font != null)
                t.fontSharedMaterial = t.font.material;
            float ow = t.outlineWidth >= 0.08f ? t.outlineWidth : 0.22f;
            ApplyOutlinedMaterialInstance(t, entry.outlineColor, ow);
        }
    }

    /// <summary>数値ダメージ用：DamageValue を表示し Message は隠す（ラベルは呼び出し側で「ダメージ」時に表示）。</summary>
    private void PrepareDamageNumberLayout()
    {
        if (messageText != null)
            messageText.gameObject.SetActive(false);
        if (valueText != null)
            valueText.gameObject.SetActive(true);
    }

    /// <summary>語句・状態異常・無傷など Message のみ。DamageValue／ラベルは隠す。</summary>
    private void ShowMessageLayout(string text, Color fillColor, Color outlineColor, bool statusAilmentAutoSize)
    {
        if (labelText != null)
            labelText.gameObject.SetActive(false);
        if (valueText != null)
            valueText.gameObject.SetActive(false);

        var target = messageText != null ? messageText : valueText;
        if (target == null) return;

        target.gameObject.SetActive(true);
        target.text = text ?? string.Empty;
        ApplyFillAndOutline(target, fillColor, outlineColor);

        if (messageText != null)
        {
            if (statusAilmentAutoSize)
            {
                // 状態異常名：1行を大きく（従来どおり折り返しなし）
                target.enableWordWrapping = false;
                target.overflowMode = TextOverflowModes.Overflow;
                target.enableAutoSizing = true;
                target.fontSizeMin = messageFontSizeMin;
                target.fontSizeMax = messageFontSizeMax;
                target.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                // 病系・ミス・無傷など：Rect 内に収まるよう Auto Size
                ApplyMessageAutoSizeForPopup(target);
            }
        }
        else
        {
            // Message 未設定の旧プレハブ：valueText のみ
            ApplyMessageAutoSizeForPopup(target);
        }
    }

    /// <summary>
    /// <see cref="Setup(string, Color)"/> 等：プレハブの Message Rect 内に収まるよう TMP の Auto Size を使用。
    /// </summary>
    private void ApplyMessageAutoSizeForPopup(TMP_Text target)
    {
        if (target == null) return;

        target.enableWordWrapping = true;
        target.overflowMode = TextOverflowModes.Overflow;
        target.enableAutoSizing = true;
        target.fontSizeMin = messageFontSizeMin;
        target.fontSizeMax = messageFontSizeMax;
        target.alignment = TextAlignmentOptions.Center;
    }

    private void Start()
    {
        // フェード用：ルートに無ければ子（内側 Canvas など）を探す。
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);
    }

    /// <summary>
    /// 回復・ミス・病系メッセージ・両替表示など「1ブロックの文字列」向け。
    /// </summary>
    /// <param name="message">そのまま表示する全文。</param>
    /// <param name="fillColor">文字の塗りつぶし色（縁は ApplyFillAndOutline 参照）。</param>
    public void Setup(string message, Color fillColor)
    {
        ShowMessageLayout(message, fillColor, ResolveDefaultSimpleOutline(), statusAilmentAutoSize: false);
    }

    /// <summary>
    /// Message popup with explicit fill and outline colors.
    /// </summary>
    public void Setup(string message, Color fillColor, Color outlineColor)
    {
        ShowMessageLayout(message, fillColor, outlineColor, statusAilmentAutoSize: false);
    }

    /// <summary>
    /// 戦闘ダメージ表示：ダメージありは数字＋「ダメージ」、0 のときは1行（無傷）など。
    /// </summary>
    /// <param name="amount">与ダメ。0 以下は else 側の見た目。</param>
    /// <param name="damageHitsPlayer">true＝プレイヤーが食らう、false＝敵が食らう。色は <see cref="DamagePopupSettings"/> を参照。</param>
    public void SetupDamage(int amount, bool damageHitsPlayer)
    {
        if (amount > 0)
        {
            if (valueText == null) return;

            var kind = damageHitsPlayer ? DamagePopupKind.DamageToPlayer : DamagePopupKind.DamageToEnemy;
            var style = ResolveSettings().GetEntryOrDefault(kind);
            ApplyBackground(style);

            PrepareDamageNumberLayout();
            valueText.enableAutoSizing = false;
            valueText.fontSize = 160f;
            valueText.text = amount.ToString();
            if (labelText != null)
            {
                labelText.gameObject.SetActive(true);
                labelText.text = "ダメージ";
            }
            ApplyFillAndOutline(valueText, style.textColor, style.outlineColor);
            if (labelText != null)
                ApplyFillAndOutline(labelText, style.textColor, style.outlineColor);
        }
        else
        {
            var nd = ResolveSettings().GetEntryOrDefault(DamagePopupKind.NoDamage);
            ApplyBackground(nd);
            string noDmgText = string.IsNullOrEmpty(nd.message) ? "無傷" : nd.message;
            if (messageText != null)
            {
                ShowMessageLayout(noDmgText, nd.textColor, nd.outlineColor, statusAilmentAutoSize: false);
            }
            else if (valueText != null)
            {
                if (labelText != null)
                    labelText.gameObject.SetActive(false);
                valueText.gameObject.SetActive(true);
                valueText.text = noDmgText;
                ApplyMessageAutoSizeForPopup(valueText);
                ApplyFillAndOutline(valueText, nd.textColor, nd.outlineColor);
            }
        }
    }

    /// <summary>
    /// 闇属性の第2段（超過ダメージ適用後の残HP分）。背景を紫系にし、数字は通常の SetupDamage と同じ配色。
    /// </summary>
    public void SetupDarkFollowupDamage(int amount, bool damageHitsPlayer)
    {
        var panel = ResolveSettings().GetEntryOrDefault(DamagePopupKind.DarkFollowupDamage);
        ApplyBackground(panel);
        SetupDamage(amount, damageHitsPlayer);
    }

    /// <summary>状態異常付与表示：公式名をオートサイズで大きく表示。</summary>
    public void SetupStatusAilmentGrant(string ailmentDisplayName, StatusEffectPopupStyleEntry style)
    {
        ApplyBackground(new DamagePopupStyleEntry
        {
            backgroundMode = style.backgroundMode,
            backgroundColor = style.backgroundColor,
            backgroundSprite = style.backgroundSprite,
        });

        Color outline = style.outlineColor.a > 0.001f
            ? style.outlineColor
            : ResolveDefaultSimpleOutline();

        if (messageText != null)
        {
            ShowMessageLayout(
                ailmentDisplayName ?? string.Empty,
                style.textColor,
                outline,
                statusAilmentAutoSize: true);
        }
        else if (valueText != null)
        {
            if (labelText != null)
                labelText.gameObject.SetActive(false);
            valueText.gameObject.SetActive(true);
            valueText.text = ailmentDisplayName ?? string.Empty;
            valueText.enableWordWrapping = false;
            valueText.overflowMode = TextOverflowModes.Overflow;
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = messageFontSizeMin;
            valueText.fontSizeMax = messageFontSizeMax;
            ApplyFillAndOutline(valueText, style.textColor, outline);
        }
    }

    /// <summary>互換：呼び出し側で色を直接渡す場合。</summary>
    public void SetupStatusAilmentGrant(string ailmentDisplayName, Color panelBackgroundColor, Color textFillColor)
    {
        SetupStatusAilmentGrant(ailmentDisplayName, new StatusEffectPopupStyleEntry
        {
            backgroundMode = MessagePopupBackgroundMode.SolidColor,
            backgroundColor = panelBackgroundColor,
            textColor = textFillColor,
            outlineColor = ResolveDefaultSimpleOutline(),
        });
    }

    /// <summary>手札リロード演出。色は <see cref="DamagePopupSettings"/> の HandReload 種別。</summary>
    public void SetupHandReload(string displayName)
    {
        SetupFromKind(DamagePopupKind.HandReload, displayName);
    }

    /// <summary>互換：呼び出し側で色を渡す場合（Settings より引数を優先）。</summary>
    public void SetupHandReload(string displayName, Color panelBackgroundColor, Color textFillColor, Color textOutlineColor)
    {
        SetupHandReloadFromEntry(new DamagePopupStyleEntry
        {
            backgroundMode = MessagePopupBackgroundMode.SolidColor,
            backgroundColor = panelBackgroundColor,
            textColor = textFillColor,
            outlineColor = textOutlineColor,
            message = displayName,
        }, null);
    }

    /// <summary>物理反射：赤背景・虹色文字の「弾き返す」。</summary>
    public void SetupReflectionBounce(string message = null)
    {
        var entry = ResolveSettings().GetEntryOrDefault(DamagePopupKind.ReflectionBounce);
        ApplyBackground(entry);
        SetupRainbowMessage(
            string.IsNullOrEmpty(message) ? entry.message : message,
            entry.outlineColor);
    }

    private void SetupRainbowMessage(string message, Color outlineColor)
    {
        var target = messageText != null ? messageText : valueText;
        if (target == null) return;

        if (labelText != null)
            labelText.gameObject.SetActive(false);
        if (valueText != null && messageText != null)
            valueText.gameObject.SetActive(false);

        target.gameObject.SetActive(true);
        target.richText = true;
        target.text = BuildRainbowRichText(string.IsNullOrEmpty(message) ? "弾き返す" : message);
        target.enableWordWrapping = false;
        target.overflowMode = TextOverflowModes.Overflow;
        target.enableAutoSizing = true;
        target.fontSizeMin = messageFontSizeMin;
        target.fontSizeMax = messageFontSizeMax;
        target.alignment = TextAlignmentOptions.Center;
        target.fontStyle = FontStyles.Bold;
        float ow = target.outlineWidth >= 0.08f ? target.outlineWidth : 0.22f;
        ApplyOutlinedMaterialInstance(target, outlineColor, ow);
    }

    /// <summary>物理無効など：黒背景・灰色字・白縁の「護身」。</summary>
    public void SetupBlockingNullify(string message = null)
    {
        var entry = ResolveSettings().GetEntryOrDefault(DamagePopupKind.BlockingNullify);
        ApplyBackground(entry);
        string text = string.IsNullOrEmpty(message) ? entry.message : message;
        ShowMessageLayout(text, entry.textColor, entry.outlineColor, statusAilmentAutoSize: false);
    }

    /// <summary>打ち払い：黄背景・白字・黒縁。</summary>
    public void SetupParryYellowBanner(string message = null)
    {
        var entry = ResolveSettings().GetEntryOrDefault(DamagePopupKind.ParryIntro);
        ApplyBackground(entry);
        string text = string.IsNullOrEmpty(message) ? entry.message : message;
        var t = messageText != null ? messageText : valueText;
        if (t != null)
            t.richText = false;

        ShowMessageLayout(text, entry.textColor, entry.outlineColor, statusAilmentAutoSize: false);
        if (t != null)
        {
            if (t.fontSharedMaterial == null && t.font != null)
                t.fontSharedMaterial = t.font.material;
            float ow = t.outlineWidth >= 0.08f ? t.outlineWidth : 0.22f;
            ApplyOutlinedMaterialInstance(t, entry.outlineColor, ow);
        }
    }

    /// <summary>
    /// SDF で <see cref="TMP_Text.outlineColor"/> だけでは縁が白のままになることがあるため、
    /// マテリアルに <see cref="ShaderUtilities"/> でアウトラインを書く（文字の塗りは <see cref="TMP_Text.color"/>／リッチテキストに任せる）。
    /// </summary>
    private static void ApplyOutlinedMaterialInstance(TMP_Text t, Color outlineColor, float outlineWidth)
    {
        if (t == null || t.fontSharedMaterial == null) return;

        var mat = Instantiate(t.fontSharedMaterial);
        t.fontMaterial = mat;
        if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
            mat.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
        if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);

        t.outlineWidth = outlineWidth;
        t.outlineColor = outlineColor;
    }

    private static string BuildRainbowRichText(string s)
    {
        var palette = new[]
        {
            new Color32(255, 90, 90, 255),
            new Color32(255, 200, 80, 255),
            new Color32(255, 255, 120, 255),
            new Color32(120, 255, 160, 255),
            new Color32(100, 220, 255, 255),
            new Color32(220, 140, 255, 255),
        };
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            var c = palette[i % palette.Length];
            sb.Append("<color=#");
            sb.Append(c.r.ToString("X2"));
            sb.Append(c.g.ToString("X2"));
            sb.Append(c.b.ToString("X2"));
            sb.Append(">");
            sb.Append(s[i]);
            sb.Append("</color>");
        }
        return sb.ToString();
    }

    /// <summary>
    /// 病系・自然進行：第1文言「病が体を蝕む」。<paramref name="phase1FloatSeconds"/> 経過で移動が止まり、戻り値の Task が完了する。
    /// </summary>
    public Task BeginDiseaseWorsenPhase1AndGetTask(string message, Color color, float phase1FloatSeconds)
    {
        EnsureDiseaseReelClippingMask();
        Setup(message, color);
        _runMode = PopupRunMode.DiseaseWorsenPhase1Float;
        timer = 0f;
        _diseasePhase1Duration = Mathf.Max(0.02f, phase1FloatSeconds);
        _diseasePhase1Tcs = new TaskCompletionSource<bool>();
        return _diseasePhase1Tcs.Task;
    }

    /// <summary>第1文言停止後、リールで第2文言へ差し替え、規定インターバル後にこのインスタンスを破棄する。</summary>
    public Task RunDiseaseReelSecondLinePostIntervalAndDestroyAsync(
        string secondMessage,
        Color secondColor,
        float reelDurationSeconds,
        float postIntervalSeconds,
        CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(CoDiseaseReelSecondThenIntervalDestroy(secondMessage, secondColor, reelDurationSeconds, postIntervalSeconds, ct, tcs));
        return tcs.Task;
    }

    /// <summary>
    /// リールで文言がルート矩形からはみ出しても、イラストのクリッピングのように枠外を描画しない（<see cref="RectMask2D"/>）。
    /// </summary>
    private void EnsureDiseaseReelClippingMask()
    {
        if (GetComponent<RectMask2D>() != null)
            return;
        gameObject.AddComponent<RectMask2D>();
    }

    private IEnumerator CoDiseaseReelSecondThenIntervalDestroy(
        string secondMessage,
        Color secondColor,
        float reelDurationSeconds,
        float postIntervalSeconds,
        CancellationToken ct,
        TaskCompletionSource<bool> tcs)
    {
        EnsureDiseaseReelClippingMask();
        _runMode = PopupRunMode.DiseaseWorsenSequenceManual;

        TMP_Text t = messageText != null ? messageText : valueText;
        if (t == null)
        {
            Setup(secondMessage, secondColor);
            yield return WaitForSecondsOrCancel(postIntervalSeconds, ct);
            tcs?.TrySetResult(true);
            Destroy(gameObject);
            yield break;
        }

        var rt = t.rectTransform;
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

        Setup(secondMessage, secondColor);
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

    /// <summary>
    /// 文字の「塗り」と TMP のアウトライン（縁）をまとめて適用。
    /// </summary>
    private void ApplyFillAndOutline(TMP_Text t, Color fill, Color outlineColor)
    {
        if (t == null) return;
        t.color = fill;
        t.fontStyle = FontStyles.Bold;
        if (t.outlineWidth < 0.08f)
            t.outlineWidth = 0.22f;
        t.outlineColor = outlineColor;
    }

    private void Update()
    {
        if (_runMode == PopupRunMode.DiseaseWorsenPhase1Float)
        {
            timer += Time.deltaTime;
            transform.Translate(Vector3.up * (floatSpeed * Time.deltaTime));
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;

            if (timer >= _diseasePhase1Duration)
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

        timer += Time.deltaTime;

        // 親の座標系で上方向へ移動。斜めにしたいなら Vector3.up を変えるか、RectTransform.anchoredPosition をいじる方式に変更。
        transform.Translate(Vector3.up * (floatSpeed * Time.deltaTime));

        // フェード：canvasGroup が無いと何もしない（常に不透明のまま消滅まで）。
        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);

        // 表示時間＝fadeDuration 秒後に破棄。長く見せたいなら fadeDuration を伸ばす。
        if (timer >= fadeDuration)
            Destroy(gameObject);
    }
}
