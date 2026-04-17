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

    public void UpdateStatus(PlayerStatus player, PlayerStatus enemy, int playerHandCount = 0, int enemyHandCount = 0)
    {
        Debug.Log($"[BattleStatusUI] UpdateStatus呼び出し - プレイヤー手札: {playerHandCount}, 敵手札: {enemyHandCount}");

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

        if (player != null)
        {
            playerSummonIcon.sprite = player.summonData.characterSprite;
            playerNameText.text = player.DisplayName;

            if (viewerUnderFog)
                playerStatusText.text = FormatConcealedStatusLine();
            else
                playerStatusText.text = FormatStatusText(player.currentHP, player.maxHP, player.currentMP, player.maxMP,
                    player.currentGP, player.maxGP, playerHandCount);

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
            enemySummonIcon.sprite = enemy.summonData.characterSprite;
            enemyNameText.text = enemy.DisplayName;

            if (viewerUnderFog)
                enemyStatusText.text = FormatConcealedStatusLine();
            else
                enemyStatusText.text = FormatStatusText(enemy.currentHP, enemy.maxHP, enemy.currentMP, enemy.maxMP,
                    enemy.currentGP, enemy.maxGP, enemyHandCount);

            UpdateRainbowLowHpOverlay(enemySummonIcon, enemy, viewerUnderFog);

            enemySummonIcon.color = viewerUnderFog ? FogSummonTint : _enemySummonColorNatural;

            RefreshAilmentIconRow(enemyAilmentIconRow, enemy, viewerUnderFog);
        }
        else
        {
            RefreshAilmentIconRow(enemyAilmentIconRow, null, false);
        }
    }

    /// <summary>低HP時の召喚アイコン虹演出。濃霧視点（人間に濃霧）のときは停止。</summary>
    private static void UpdateRainbowLowHpOverlay(Image summonIcon, PlayerStatus ps, bool viewerUnderFog)
    {
        if (summonIcon == null || ps == null) return;
        Transform overlay = summonIcon.transform.Find("RainbowOverlay");
        if (overlay == null) return;

        if (!viewerUnderFog && ps.currentHP <= 10)
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
