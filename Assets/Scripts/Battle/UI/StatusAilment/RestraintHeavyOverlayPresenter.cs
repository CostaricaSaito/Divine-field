using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 防御フェーズで拘束（HasRestraintEffect）のプレイヤー／敵側カード表示パネルに
/// 「体が重い」オーバーレイを被せて、カード選択が事実上できないことを視覚化する。
///
/// 配置は 2 枚目スロット相当（<see cref="CardLayoutManager"/> の計算結果）または
/// Inspector の override Rect をアンカーに使用する。
/// </summary>
public class RestraintHeavyOverlayPresenter : MonoBehaviour
{
    [Header("拘束：防御フェーズ「体が重い」")]
    [Tooltip("未指定なら CardLayoutManager と同じ計算で2枚目スロット相当に配置。環境でずれる場合のみ指定。")]
    [SerializeField] private RectTransform restraintHeavySlotPlayerOverride;
    [SerializeField] private RectTransform restraintHeavySlotEnemyOverride;

    private GameObject restraintHeavyGoPlayer;
    private GameObject restraintHeavyGoEnemy;

    /// <summary>
    /// 防御側が拘束中のとき、カード表示パネル上の「体が重い」枠を表示（2枚目スロット相当またはオーバーライドRect）。
    /// </summary>
    public void Sync()
    {
        HideAll();
        if (BattleManager.I == null) return;
        if (BattleManager.I.CurrentState != GameState.DefensePhase) return;

        var bm = BattleManager.I;
        if (bm.DefenderPublic == PlayerType.Player && bm.GetPlayerStatus().HasRestraintEffect())
            Show(Side.Player);
        else if (bm.DefenderPublic == PlayerType.Enemy && bm.GetEnemyStatus().HasRestraintEffect())
            Show(Side.Enemy);
    }

    public void HideAll()
    {
        if (restraintHeavyGoPlayer != null) restraintHeavyGoPlayer.SetActive(false);
        if (restraintHeavyGoEnemy != null) restraintHeavyGoEnemy.SetActive(false);
    }

    private void Show(Side side)
    {
        var go = GetOrCreate(side);
        if (go == null) return;
        Layout(go, side);
        go.SetActive(true);
    }

    private GameObject GetOrCreate(Side side)
    {
        if (side == Side.Player)
        {
            if (restraintHeavyGoPlayer == null)
                restraintHeavyGoPlayer = Build(Side.Player);
            return restraintHeavyGoPlayer;
        }
        if (restraintHeavyGoEnemy == null)
            restraintHeavyGoEnemy = Build(Side.Enemy);
        return restraintHeavyGoEnemy;
    }

    private GameObject Build(Side side)
    {
        var go = new GameObject("RestraintHeavyOverlay");
        var img = go.AddComponent<Image>();
        img.color = new Color(0.07f, 0.1f, 0.18f, 0.9f);
        img.raycastTarget = false;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "体が重い";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.9f, 0.91f, 0.96f);
        var labelFont = BattleUIManager.I != null ? BattleUIManager.I.GetUseButtonLabelFont() : null;
        if (labelFont != null)
            tmp.font = labelFont;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 96f;
        tmp.fontSize = 72f;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;

        return go;
    }

    private void Layout(GameObject go, Side side)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();

        RectTransform anchor = side == Side.Player ? restraintHeavySlotPlayerOverride : restraintHeavySlotEnemyOverride;
        Transform panel = side == Side.Player
            ? (BattleUIManager.I != null ? BattleUIManager.I.GetPlayerCardDisplayPanel() : null)
            : (BattleUIManager.I != null ? BattleUIManager.I.GetEnemyCardDisplayPanel() : null);
        var panelRt = panel as RectTransform;

        if (anchor != null)
        {
            go.transform.SetParent(anchor, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            go.transform.SetAsLastSibling();
            return;
        }

        if (panelRt == null) return;

        var cardLayoutManager = BattleUIManager.I != null ? BattleUIManager.I.GetCardLayoutManager() : null;

        float panelHeight = panelRt.rect.height;
        float cardH = cardLayoutManager != null ? cardLayoutManager.LayoutCardHeight : 120f;
        float topY = cardLayoutManager != null
            ? cardLayoutManager.GetSecondSlotTopYForPanelHeight(panelHeight)
            : -cardH - 10f;

        float bottomY = -panelHeight;

        go.transform.SetParent(panelRt, false);
        rt.anchorMin = new Vector2(0, 1f);
        rt.anchorMax = new Vector2(1, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0, bottomY);
        rt.offsetMax = new Vector2(0, topY);
        rt.localScale = Vector3.one;
        go.transform.SetAsLastSibling();
    }
}
