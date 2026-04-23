using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 大魔法（ArchMagic）詠唱中の中央オーバーレイを司るサブマネージャ。
///
/// 【主な責務】
/// - 半透明ディム＋中央の魔法アイコン＋残りターン数表示の生成
/// - フェードイン／フェードアウト／即時非表示
/// - 残りターン数の動的更新（ダウンカウント表現）
///
/// オーバーレイは <see cref="BattleUIManager.GetPopupCanvas"/>（popupCanvas → uiCanvas）
/// の下に配置される。
/// </summary>
public class ArchMagicOverlayPresenter : MonoBehaviour
{
    [Header("大魔法 詠唱オーバーレイ")]
    [Tooltip("大魔法詠唱中カウントダウンに使うフォント（未指定なら TMP のデフォルト）")]
    [SerializeField] private TMP_FontAsset archMagicCastCountdownFont;
    [Tooltip("カウントダウンの数字のみを %size% で拡大表示する倍率（100=等倍）")]
    [SerializeField] [Range(100, 260)] private int archMagicCountdownNumberSizePercent = 185;
    [Tooltip("カウントダウン背景の不透明度（0=透明, 1=白）")]
    [SerializeField] [Range(0f, 1f)] private float archMagicCountdownBackdropAlpha = 0.42f;

    private GameObject _archMagicCastOverlay;
    private CanvasGroup _archMagicCastOverlayCanvasGroup;
    private Image _archMagicCastDimImage;
    private Image _archMagicCastOverlayImage;
    private TMP_Text _archMagicCastOverlayRemainingText;

    /// <summary>詠唱中：全画面ディム + 中央に大魔法アイコン + 残りターンをフェードイン表示する。</summary>
    public async Task FadeInAsync(Sprite magicSprite, int remainingTurns, int fadeMs, CancellationToken ct)
    {
        EnsureOverlay();
        if (_archMagicCastOverlay == null) return;

        if (_archMagicCastOverlayImage != null) _archMagicCastOverlayImage.sprite = magicSprite;
        UpdateRemaining(remainingTurns);

        _archMagicCastOverlay.SetActive(true);
        if (_archMagicCastOverlayCanvasGroup != null)
        {
            _archMagicCastOverlayCanvasGroup.alpha = 0f;
            _archMagicCastOverlayCanvasGroup.blocksRaycasts = false;
        }

        int steps = Mathf.Max(1, fadeMs / 16);
        float stepDelta = 1f / steps;
        int stepMs = Mathf.Max(1, fadeMs / steps);
        for (int i = 1; i <= steps; i++)
        {
            if (ct.IsCancellationRequested) break;
            if (_archMagicCastOverlayCanvasGroup != null)
                _archMagicCastOverlayCanvasGroup.alpha = Mathf.Clamp01(stepDelta * i);
            await Task.Delay(stepMs, ct);
        }
        if (_archMagicCastOverlayCanvasGroup != null)
        {
            _archMagicCastOverlayCanvasGroup.alpha = 1f;
            _archMagicCastOverlayCanvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>残りターン数のみ差し替える（ダウンカウント表現用）。</summary>
    public void UpdateRemaining(int remainingTurns)
    {
        if (_archMagicCastOverlayRemainingText == null) return;
        _archMagicCastOverlayRemainingText.richText = true;
        int pct = Mathf.Clamp(archMagicCountdownNumberSizePercent, 100, 260);
        _archMagicCastOverlayRemainingText.text =
            $"残り <size={pct}%>{remainingTurns}</size> ターン";
    }

    /// <summary>詠唱中央オーバーレイを消す（フェード）。</summary>
    public async Task FadeOutAsync(int fadeMs, CancellationToken ct)
    {
        if (_archMagicCastOverlay == null || _archMagicCastOverlayCanvasGroup == null)
        {
            HideImmediate();
            return;
        }

        _archMagicCastOverlayCanvasGroup.blocksRaycasts = false;

        int steps = Mathf.Max(1, fadeMs / 16);
        float stepDelta = 1f / steps;
        int stepMs = Mathf.Max(1, fadeMs / steps);
        float a = _archMagicCastOverlayCanvasGroup.alpha;
        for (int i = 1; i <= steps; i++)
        {
            if (ct.IsCancellationRequested) break;
            _archMagicCastOverlayCanvasGroup.alpha = Mathf.Clamp01(a - stepDelta * i);
            await Task.Delay(stepMs, ct);
        }
        HideImmediate();
    }

    public void HideImmediate()
    {
        if (_archMagicCastOverlay == null) return;
        _archMagicCastOverlay.SetActive(false);
        if (_archMagicCastOverlayCanvasGroup != null)
        {
            _archMagicCastOverlayCanvasGroup.alpha = 0f;
            _archMagicCastOverlayCanvasGroup.blocksRaycasts = false;
        }
    }

    private void ClearInternalRefs()
    {
        _archMagicCastOverlay = null;
        _archMagicCastOverlayCanvasGroup = null;
        _archMagicCastDimImage = null;
        _archMagicCastOverlayImage = null;
        _archMagicCastOverlayRemainingText = null;
    }

    private void EnsureOverlay()
    {
        if (_archMagicCastOverlay != null)
        {
            if (_archMagicCastDimImage != null)
                return;
            Destroy(_archMagicCastOverlay);
            ClearInternalRefs();
        }

        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetPopupCanvas() : null;
        if (canvas == null) return;

        var root = new GameObject("ArchMagicCastOverlay", typeof(RectTransform), typeof(CanvasGroup));
        var rt = root.GetComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();

        var cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var dimGo = new GameObject("ArchMagicDim", typeof(RectTransform));
        var dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.SetParent(rt, false);
        dimRt.SetAsFirstSibling();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        var dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.5f);
        dimImg.raycastTarget = true;

        var contentGo = new GameObject("ArchMagicCastContent", typeof(RectTransform));
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.SetParent(rt, false);
        contentRt.anchorMin = new Vector2(0.5f, 0.5f);
        contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.pivot = new Vector2(0.5f, 0.5f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(420f, 560f);

        var imgGo = new GameObject("Icon", typeof(RectTransform));
        var imgRt = imgGo.GetComponent<RectTransform>();
        imgRt.SetParent(contentRt, false);
        imgRt.anchorMin = new Vector2(0.5f, 0.5f);
        imgRt.anchorMax = new Vector2(0.5f, 0.5f);
        imgRt.pivot = new Vector2(0.5f, 0.5f);
        imgRt.anchoredPosition = new Vector2(0f, 50f);
        imgRt.sizeDelta = new Vector2(360f, 360f);
        var iconImg = imgGo.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        var panelGo = new GameObject("RemainingBackdrop", typeof(RectTransform));
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.SetParent(contentRt, false);
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = new Vector2(0f, -210f);
        panelRt.sizeDelta = new Vector2(720f, 108f);
        var backdrop = panelGo.AddComponent<Image>();
        {
            var w = Texture2D.whiteTexture;
            backdrop.sprite = Sprite.Create(w, new Rect(0, 0, w.width, w.height), new Vector2(0.5f, 0.5f), 100f);
        }
        backdrop.type = Image.Type.Simple;
        backdrop.color = new Color(1f, 1f, 1f, Mathf.Clamp01(archMagicCountdownBackdropAlpha));
        backdrop.raycastTarget = false;

        var textGo = new GameObject("Remaining", typeof(RectTransform));
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(panelRt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(14f, 10f);
        textRt.offsetMax = new Vector2(-14f, -10f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 58f;
        tmp.richText = true;
        if (archMagicCastCountdownFont != null)
            tmp.font = archMagicCastCountdownFont;
        else if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontStyle = FontStyles.Normal;
        tmp.color = Color.white;
        tmp.outlineColor = new Color(0.85f, 0.12f, 0.12f, 1f);
        tmp.outlineWidth = 0.22f;
        tmp.text = "";
        tmp.raycastTarget = false;

        _archMagicCastOverlay = root;
        _archMagicCastOverlayCanvasGroup = cg;
        _archMagicCastDimImage = dimImg;
        _archMagicCastOverlayImage = iconImg;
        _archMagicCastOverlayRemainingText = tmp;
        root.SetActive(false);
    }
}
