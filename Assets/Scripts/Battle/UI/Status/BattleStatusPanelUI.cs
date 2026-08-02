using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleStatusUI : MonoBehaviour
{
    private const float FogFadeSeconds = 0.5f;
    private static readonly Color FogSummonTint = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Header("UI参照（プレイヤー）")]
    public Image playerSummonIcon;
    public TMP_Text playerNameText;
    public TMP_Text playerStatusText; // HP/MP/GP/HAND表示用

    [Header("UI参照（敵）")]
    public Image enemySummonIcon;
    public TMP_Text enemyNameText;
    public TMP_Text enemyStatusText; // HP/MP/GP/HAND表示用

    [Header("状態異常アイコン")]
    [Tooltip("Create > Divine > Battle > Status Effect Icon Settings で作成し、Debuff フォルダの Sprite を割り当てる")]
    [SerializeField] private StatusEffectIconSettings statusEffectIconSettings;

    [Tooltip("プレイヤー行：HP 等のテキストの下に置く空の RectTransform。子に HorizontalLayoutGroup を付けると横並びになります。")]
    [SerializeField] private RectTransform playerAilmentIconRow;
    [Tooltip("敵行：同上")]
    [SerializeField] private RectTransform enemyAilmentIconRow;
    [Tooltip("任意：1個分のアイコンPrefab（Image＋LayoutElement 推奨）。未設定時は実行時に Image を生成します。")]
    [SerializeField] private GameObject ailmentIconPrefab;
    [SerializeField] private Vector2 ailmentIconSize = new Vector2(36f, 36f);

    [Header("濃霧（視界）")]
    [Tooltip("濃霧が「人間プレイヤー（player）」に付いているときだけ、そのプレイヤー視点の演出としてフェード。相手だけ濃霧のときは使わない。")]
    [SerializeField] private Image fogStatusCloudOverlay;
    [Tooltip("同上。人間プレイヤーが濃霧のときだけ背景用レイヤーをフェード。初期アルファ0。")]
    [SerializeField] private Image fogBattleBackdrop;
    [Tooltip("未設定で fogStatusCloudOverlay に Sprite が無いときに適用（Images/04_状態異常/雲 など）")]
    [SerializeField] private Sprite fogCloudSprite;
    [Tooltip("未設定で fogBattleBackdrop に Sprite が無いときに適用（Images/02_背景/霧状態 など）")]
    [SerializeField] private Sprite fogBattleBackdropSprite;

    /// <summary>フェーズ1以降の UI やデバッグから参照。</summary>
    public StatusEffectIconSettings StatusEffectIconSettings => statusEffectIconSettings;

    private Color _playerSummonColorNatural = Color.white;
    private Color _enemySummonColorNatural = Color.white;
    /// <summary>前フレームで「人間プレイヤー」が濃霧視点だったか（雲・背景フェード用）。敵のみ濃霧のときは常に false。</summary>
    private bool _lastViewerUnderFogForFullScreenVfx;
    private Coroutine _fogFadeCoroutine;

    /// <summary>濃霧付与ポップアップ表示中は true。内部ステータスは濃霧だが、背景・オーバーレイ・「？」表示はまだ行わない。</summary>
    private bool _deferFogVisionVisuals;

    private const float HpmgpChangeStepSec = BattleStatCountRules.ValueStepSec;
    private Coroutine _hpmgpAnimCoroutine;
    private bool _hpmgpDisplayReady;
    private bool _hpmgpLineWasConcealedByFog;
    private float _dispPHp, _dispPMp, _dispPGp, _dispEHp, _dispEMp, _dispEGp;
    private int _lastPlayerHandForStatusLine, _lastEnemyHandForStatusLine;
    private float _hmpgFromPHp, _hmpgFromPMp, _hmpgFromPGp, _hmpgFromEHp, _hmpgFromEMp, _hmpgFromEGp;
    private float _hmpgToPHp, _hmpgToPMp, _hmpgToPGp, _hmpgToEHp, _hmpgToEMp, _hmpgToEGp;

    /// <summary>濃霧の画面演出を遅延するときに BattleUIManager から呼ぶ。</summary>
    public void SetDeferFogVisionVisuals(bool defer)
    {
        _deferFogVisionVisuals = defer;
    }

    public Sprite GetStatusEffectIcon(StatusEffectType type)
    {
        return statusEffectIconSettings != null ? statusEffectIconSettings.GetIcon(type) : null;
    }

    private void Awake()
    {
        if (playerSummonIcon != null)
            _playerSummonColorNatural = playerSummonIcon.color;
        if (enemySummonIcon != null)
            _enemySummonColorNatural = enemySummonIcon.color;

        if (fogStatusCloudOverlay != null)
        {
            if (fogStatusCloudOverlay.sprite == null && fogCloudSprite != null)
                fogStatusCloudOverlay.sprite = fogCloudSprite;
            fogStatusCloudOverlay.raycastTarget = false;
        }

        if (fogBattleBackdrop != null)
        {
            if (fogBattleBackdrop.sprite == null && fogBattleBackdropSprite != null)
                fogBattleBackdrop.sprite = fogBattleBackdropSprite;
            fogBattleBackdrop.raycastTarget = false;
        }
    }

    public void UpdateStatus(PlayerStatus player, PlayerStatus enemy, int playerHandCount = 0, int enemyHandCount = 0, bool snapHpmgpNumbers = false)
    {
        // 濃霧の「視界」は人間プレイヤー（player）に付与されたときだけ。敵だけ濃霧なら見た目は一切変えない。
        // 付与ポップアップ表示〜規定インターバルまでは _deferFogVisionVisuals で演出を遅延。
        bool viewerUnderFog = player != null && player.HasFogEffect() && !_deferFogVisionVisuals;

        if (viewerUnderFog != _lastViewerUnderFogForFullScreenVfx)
        {
            _lastViewerUnderFogForFullScreenVfx = viewerUnderFog;
            if (_fogFadeCoroutine != null)
            {
                StopCoroutine(_fogFadeCoroutine);
                _fogFadeCoroutine = null;
            }
            _fogFadeCoroutine = StartCoroutine(CoFadeFogVfx(viewerUnderFog));
        }

        _lastPlayerHandForStatusLine = playerHandCount;
        _lastEnemyHandForStatusLine = enemyHandCount;

        if (viewerUnderFog)
        {
            StopHpmgpNumberAnimationIfAny();
            if (playerStatusText != null) playerStatusText.text = FormatConcealedStatusLine();
            if (enemyStatusText != null) enemyStatusText.text = FormatConcealedStatusLine();
            _hpmgpLineWasConcealedByFog = true;
        }
        else
        {
            if (_hpmgpLineWasConcealedByFog)
            {
                SyncHpmgpDisplayFromStatus(player, enemy);
                _hpmgpLineWasConcealedByFog = false;
            }
            if (snapHpmgpNumbers)
            {
                StopHpmgpNumberAnimationIfAny();
                SyncHpmgpDisplayFromStatus(player, enemy);
            }
            else
                RequestHpmgpStatusLineTweenOrApply(player, enemy);
        }

        if (player != null)
        {
            playerSummonIcon.sprite = player.summonData != null ? player.summonData.GetBattleStatusIconSprite() : null;
            playerNameText.text = player.DisplayName;

            UpdateRainbowLowHpOverlay(playerSummonIcon, player, viewerUnderFog);

            playerSummonIcon.color = viewerUnderFog ? FogSummonTint : _playerSummonColorNatural;

            RefreshAilmentIconRow(playerAilmentIconRow, player, viewerUnderFog);
        }
        else
        {
            RefreshAilmentIconRow(playerAilmentIconRow, null, false);
        }

        if (enemy != null)
        {
            enemySummonIcon.sprite = enemy.summonData != null ? enemy.summonData.GetBattleStatusIconSprite() : null;
            enemyNameText.text = enemy.DisplayName;

            UpdateRainbowLowHpOverlay(enemySummonIcon, enemy, viewerUnderFog);

            enemySummonIcon.color = viewerUnderFog ? FogSummonTint : _enemySummonColorNatural;

            RefreshAilmentIconRow(enemyAilmentIconRow, enemy, viewerUnderFog);
        }
        else
        {
            RefreshAilmentIconRow(enemyAilmentIconRow, null, false);
        }

        if (player != null)
            BattleBgmController.Instance?.SyncFromPlayer(player);
    }

    /// <summary>劣勢時（HP+MP+GP 合計が閾値以下）の召喚アイコン虹演出。濃霧視点（人間に濃霧）のときは停止。</summary>
    private static void UpdateRainbowLowHpOverlay(Image summonIcon, PlayerStatus ps, bool viewerUnderFog)
    {
        if (summonIcon == null || ps == null) return;
        Transform overlay = summonIcon.transform.Find("RainbowOverlay");
        if (overlay == null) return;

        var overlayRt = overlay.GetComponent<RectTransform>();
        SyncRainbowOverlayRectToSummonIcon(summonIcon, overlayRt);

        if (!viewerUnderFog && DisadvantageRules.IsDisadvantaged(ps) && !ps.hasUsedManifestationSkill)
        {
            if (!overlay.GetComponent<RainbowOutline>())
                overlay.gameObject.AddComponent<RainbowOutline>();
        }
        else
        {
            if (overlay.GetComponent<RainbowOutline>())
                Destroy(overlay.GetComponent<RainbowOutline>());
            var ovImg = overlay.GetComponent<Image>();
            if (ovImg != null)
                ovImg.color = new Color(1, 1, 1, 0);
        }
    }

    /// <summary>RainbowOverlay を召喚アイコン Image の矩形に完全一致させる。</summary>
    private static void SyncRainbowOverlayRectToSummonIcon(Image summonIcon, RectTransform overlayRt)
    {
        if (summonIcon == null || overlayRt == null) return;
        var iconRt = summonIcon.rectTransform;
        overlayRt.SetParent(iconRt, false);
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.pivot = new Vector2(0.5f, 0.5f);
        overlayRt.anchoredPosition = Vector2.zero;
        overlayRt.sizeDelta = Vector2.zero;
        overlayRt.localScale = Vector3.one;
    }

    private IEnumerator CoFadeFogVfx(bool toConcealed)
    {
        float dur = FogFadeSeconds;
        float t = 0f;

        float c0 = fogStatusCloudOverlay != null ? fogStatusCloudOverlay.color.a : 0f;
        float c1 = toConcealed ? 1f : 0f;
        float b0 = fogBattleBackdrop != null ? fogBattleBackdrop.color.a : 0f;
        float b1 = toConcealed ? 1f : 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            SetFogCloudAlpha(Mathf.Lerp(c0, c1, u));
            SetFogBackdropAlpha(Mathf.Lerp(b0, b1, u));
            yield return null;
        }

        SetFogCloudAlpha(c1);
        SetFogBackdropAlpha(b1);
        _fogFadeCoroutine = null;
    }

    private void SetFogCloudAlpha(float a)
    {
        if (fogStatusCloudOverlay == null) return;
        var c = fogStatusCloudOverlay.color;
        c.a = Mathf.Clamp01(a);
        fogStatusCloudOverlay.color = c;
    }

    private void SetFogBackdropAlpha(float a)
    {
        if (fogBattleBackdrop == null) return;
        var c = fogBattleBackdrop.color;
        c.a = Mathf.Clamp01(a);
        fogBattleBackdrop.color = c;
    }

    /// <param name="grayTintIcons">濃霧視点のとき true。アイコンを消さず灰色で塗りつぶす。</param>
    private void RefreshAilmentIconRow(RectTransform row, PlayerStatus status, bool grayTintIcons)
    {
        if (row == null) return;

        for (int i = row.childCount - 1; i >= 0; i--)
            Destroy(row.GetChild(i).gameObject);

        if (status == null) return;

        List<StatusEffectType> types = status.GetActiveAilmentTypesOrdered();
        for (int i = 0; i < types.Count; i++)
        {
            Sprite spr = GetStatusEffectIcon(types[i]);
            if (spr == null) continue;

            GameObject iconGo = ailmentIconPrefab != null
                ? Instantiate(ailmentIconPrefab, row)
                : CreateRuntimeAilmentIcon(row);
            if (iconGo == null) continue;

            Image img = iconGo.GetComponent<Image>();
            if (img == null) img = iconGo.GetComponentInChildren<Image>(true);
            if (img != null)
            {
                img.sprite = spr;
                img.preserveAspect = true;
                img.color = grayTintIcons ? FogSummonTint : Color.white;
            }
        }
    }

    private GameObject CreateRuntimeAilmentIcon(RectTransform parent)
    {
        var go = new GameObject("AilmentIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.sizeDelta = ailmentIconSize;

        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = ailmentIconSize.x;
        le.preferredHeight = ailmentIconSize.y;
        le.minWidth = ailmentIconSize.x;
        le.minHeight = ailmentIconSize.y;

        return go;
    }

    private void StopHpmgpNumberAnimationIfAny()
    {
        if (_hpmgpAnimCoroutine == null) return;
        StopCoroutine(_hpmgpAnimCoroutine);
        _hpmgpAnimCoroutine = null;
    }

    private void SyncHpmgpDisplayFromStatus(PlayerStatus player, PlayerStatus enemy)
    {
        if (player != null)
        {
            _dispPHp = player.currentHP;
            _dispPMp = player.currentMP;
            _dispPGp = player.currentGP;
        }
        if (enemy != null)
        {
            _dispEHp = enemy.currentHP;
            _dispEMp = enemy.currentMP;
            _dispEGp = enemy.currentGP;
        }
        WriteHpmgpStatusLines(player, enemy, _lastPlayerHandForStatusLine, _lastEnemyHandForStatusLine);
    }

    private void RequestHpmgpStatusLineTweenOrApply(PlayerStatus player, PlayerStatus enemy)
    {
        if (player == null && enemy == null) return;

        if (!_hpmgpDisplayReady)
        {
            SyncHpmgpDisplayFromStatus(player, enemy);
            _hpmgpDisplayReady = true;
            return;
        }

        if (!NeedsHpmgpNumberTween(player, enemy))
        {
            WriteHpmgpStatusLines(player, enemy, _lastPlayerHandForStatusLine, _lastEnemyHandForStatusLine);
            return;
        }

        if (player != null)
        {
            _hmpgFromPHp = _dispPHp;
            _hmpgFromPMp = _dispPMp;
            _hmpgFromPGp = _dispPGp;
            _hmpgToPHp = player.currentHP;
            _hmpgToPMp = player.currentMP;
            _hmpgToPGp = player.currentGP;
        }
        if (enemy != null)
        {
            _hmpgFromEHp = _dispEHp;
            _hmpgFromEMp = _dispEMp;
            _hmpgFromEGp = _dispEGp;
            _hmpgToEHp = enemy.currentHP;
            _hmpgToEMp = enemy.currentMP;
            _hmpgToEGp = enemy.currentGP;
        }
        StopHpmgpNumberAnimationIfAny();
        _hpmgpAnimCoroutine = StartCoroutine(CoHpmgpCountTween(player, enemy));
    }

    private bool NeedsHpmgpNumberTween(PlayerStatus player, PlayerStatus enemy)
    {
        if (player != null)
        {
            if (Mathf.RoundToInt(_dispPHp) != player.currentHP
                || Mathf.RoundToInt(_dispPMp) != player.currentMP
                || Mathf.RoundToInt(_dispPGp) != player.currentGP)
                return true;
        }
        if (enemy != null)
        {
            if (Mathf.RoundToInt(_dispEHp) != enemy.currentHP
                || Mathf.RoundToInt(_dispEMp) != enemy.currentMP
                || Mathf.RoundToInt(_dispEGp) != enemy.currentGP)
                return true;
        }
        return false;
    }

    private void WriteHpmgpStatusLines(PlayerStatus player, PlayerStatus enemy, int playerHandCount, int enemyHandCount)
    {
        if (player != null && playerStatusText != null)
        {
            playerStatusText.text = FormatStatusText(
                Mathf.RoundToInt(_dispPHp), player.maxHP,
                Mathf.RoundToInt(_dispPMp), player.maxMP,
                Mathf.RoundToInt(_dispPGp), player.maxGP, playerHandCount);
        }
        if (enemy != null && enemyStatusText != null)
        {
            enemyStatusText.text = FormatStatusText(
                Mathf.RoundToInt(_dispEHp), enemy.maxHP,
                Mathf.RoundToInt(_dispEMp), enemy.maxMP,
                Mathf.RoundToInt(_dispEGp), enemy.maxGP, enemyHandCount);
        }
    }

    private IEnumerator CoHpmgpCountTween(PlayerStatus player, PlayerStatus enemy)
    {
        while (StepHpmgpDisplayTowardTargets(player, enemy))
        {
            WriteHpmgpStatusLines(player, enemy, _lastPlayerHandForStatusLine, _lastEnemyHandForStatusLine);
            yield return new WaitForSeconds(HpmgpChangeStepSec);
        }

        if (player != null)
        {
            _dispPHp = _hmpgToPHp;
            _dispPMp = _hmpgToPMp;
            _dispPGp = _hmpgToPGp;
        }
        if (enemy != null)
        {
            _dispEHp = _hmpgToEHp;
            _dispEMp = _hmpgToEMp;
            _dispEGp = _hmpgToEGp;
        }
        WriteHpmgpStatusLines(player, enemy, _lastPlayerHandForStatusLine, _lastEnemyHandForStatusLine);
        _hpmgpAnimCoroutine = null;
    }

    private bool StepHpmgpDisplayTowardTargets(PlayerStatus player, PlayerStatus enemy)
    {
        bool any = false;
        if (player != null)
        {
            any |= StepHpmgpOne(ref _dispPHp, _hmpgToPHp);
            any |= StepHpmgpOne(ref _dispPMp, _hmpgToPMp);
            any |= StepHpmgpOne(ref _dispPGp, _hmpgToPGp);
        }
        if (enemy != null)
        {
            any |= StepHpmgpOne(ref _dispEHp, _hmpgToEHp);
            any |= StepHpmgpOne(ref _dispEMp, _hmpgToEMp);
            any |= StepHpmgpOne(ref _dispEGp, _hmpgToEGp);
        }
        return any;
    }

    private static bool StepHpmgpOne(ref float displayed, float target)
    {
        int current = Mathf.RoundToInt(displayed);
        int goal = Mathf.RoundToInt(target);
        if (current == goal)
        {
            displayed = target;
            return false;
        }

        displayed = current + (goal > current ? 1 : -1);
        return true;
    }

    /// <summary>濃霧視点（人間プレイヤーに濃霧）時：HP/MP/GP/HAND の表示をすべて「？」にする（内部ステータスはそのまま）。</summary>
    private static string FormatConcealedStatusLine()
    {
        const string q = "？";
        return $"<color=#FF0000><size=80%>HP</size></color> <color=white><size=120%>{q}</size></color> " +
               $"<color=#00FFFF><size=80%>MP</size></color> <color=white><size=120%>{q}</size></color> " +
               $"<color=#FFFF00><size=80%>GP</size></color> <color=white><size=120%>{q}</size></color> " +
               $"<color=#FF00FF><size=80%>HAND</size></color> <color=white><size=120%>{q}</size></color>";
    }

    /// <summary>
    /// ステータステキストをフォーマット（HP MP GP HAND形式）
    /// </summary>
    private string FormatStatusText(int currentHP, int maxHP, int currentMP, int maxMP, int currentGP, int maxGP, int handCount)
    {
        return $"<color=#FF0000><size=80%>HP</size></color> <color=white><size=120%>{currentHP}</size></color> " +
               $"<color=#00FFFF><size=80%>MP</size></color> <color=white><size=120%>{currentMP}</size></color> " +
               $"<color=#FFFF00><size=80%>GP</size></color> <color=white><size=120%>{currentGP}</size></color> " +
               $"<color=#FF00FF><size=80%>HAND</size></color> <color=white><size=120%>{handCount}</size></color>";
    }
}
