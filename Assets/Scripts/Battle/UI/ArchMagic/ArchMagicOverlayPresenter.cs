using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

/// <summary>
/// 大魔法（ArchMagic）詠唱中の中央オーバーレイを司るサブマネージャ。
/// 見た目は <c>Resources/Prefab/ArchMagicCastOverlay</c>、更新・フェードは本クラス。
/// </summary>
public class ArchMagicOverlayPresenter : MonoBehaviour
{
    private const string PrefabResourcePath = "Prefab/ArchMagicCastOverlay";

    [Header("大魔法 詠唱オーバーレイ")]
    [SerializeField] private GameObject overlayPrefab;
    [Tooltip("大魔法詠唱中カウントダウンに使うフォント（未指定なら Prefab 既定）")]
    [SerializeField] private TMP_FontAsset archMagicCastCountdownFont;
    [Tooltip("カウントダウンの数字のみを %size% で拡大表示する倍率（100=等倍）")]
    [SerializeField] [Range(100, 260)] private int archMagicCountdownNumberSizePercent = 185;
    [Tooltip("カウントダウン背景の不透明度（0=透明, 1=白）")]
    [SerializeField] [Range(0f, 1f)] private float archMagicCountdownBackdropAlpha = 0.42f;
    [Tooltip("カウントダウンオーバーレイのフェードイン時間（ミリ秒）")]
    [SerializeField] [Min(1)] private int fadeInDurationMs = 520;
    [Tooltip("カウントダウンオーバーレイのフェードアウト時間（ミリ秒）")]
    [SerializeField] [Min(1)] private int fadeOutDurationMs = 480;

    public int FadeInDurationMs => fadeInDurationMs;
    public int FadeOutDurationMs => fadeOutDurationMs;

    private GameObject _archMagicCastOverlay;
    private ArchMagicCastOverlayView _overlayView;
    private CanvasGroup _archMagicCastOverlayCanvasGroup;
    private int _displayedBarrierRemaining = -1;

    /// <summary>詠唱中：全画面ディム + 中央に大魔法アイコン + 残りターンをフェードイン表示する。</summary>
    public async Task FadeInAsync(Sprite magicSprite, int remainingTurns, int barrierRemaining, CancellationToken ct)
    {
        EnsureOverlay();
        if (_overlayView == null) return;

        _overlayView.SetIconSprite(magicSprite);
        UpdateRemaining(remainingTurns, barrierRemaining);

        _archMagicCastOverlay.SetActive(true);
        if (_archMagicCastOverlayCanvasGroup != null)
        {
            _archMagicCastOverlayCanvasGroup.alpha = 0f;
            _archMagicCastOverlayCanvasGroup.blocksRaycasts = false;
        }

        int fadeMs = Mathf.Max(1, fadeInDurationMs);
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

    /// <summary>残りターン数と残バリアを差し替える（ダウンカウント表現用）。</summary>
    public void UpdateRemaining(int remainingTurns, int barrierRemaining = -1)
    {
        if (_overlayView == null) return;

        int pct = Mathf.Clamp(archMagicCountdownNumberSizePercent, 100, 260);
        _overlayView.SetRemainingRichText($"残り <size={pct}%>{remainingTurns}</size> ターン");

        if (barrierRemaining >= 0)
            _displayedBarrierRemaining = barrierRemaining;

        bool showBarrier = _displayedBarrierRemaining >= 0;
        _overlayView.SetBarrierText($"残バリア：{_displayedBarrierRemaining}HP", showBarrier);
    }

    /// <summary>詠唱中央オーバーレイを消す（フェード）。</summary>
    public async Task FadeOutAsync(CancellationToken ct)
    {
        if (_archMagicCastOverlay == null || _archMagicCastOverlayCanvasGroup == null)
        {
            HideImmediate();
            return;
        }

        _archMagicCastOverlayCanvasGroup.blocksRaycasts = false;

        int fadeMs = Mathf.Max(1, fadeOutDurationMs);
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
        _displayedBarrierRemaining = -1;
        if (_archMagicCastOverlayCanvasGroup != null)
        {
            _archMagicCastOverlayCanvasGroup.alpha = 0f;
            _archMagicCastOverlayCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>詠唱中の残りターンを常時表示（ターン間も維持、入力はブロックしない）。</summary>
    public void ShowPersistent(Sprite magicSprite, int remainingTurns, int barrierRemaining = -1)
    {
        EnsureOverlay();
        if (_overlayView == null) return;

        _overlayView.SetIconSprite(magicSprite);
        UpdateRemaining(remainingTurns, barrierRemaining);

        _archMagicCastOverlay.SetActive(true);
        if (_archMagicCastOverlayCanvasGroup != null)
        {
            _archMagicCastOverlayCanvasGroup.alpha = 1f;
            _archMagicCastOverlayCanvasGroup.blocksRaycasts = false;
        }
    }

    private void ClearInternalRefs()
    {
        _archMagicCastOverlay = null;
        _overlayView = null;
        _archMagicCastOverlayCanvasGroup = null;
    }

    private void EnsureOverlay()
    {
        if (_overlayView != null && _archMagicCastOverlay != null)
            return;

        if (_archMagicCastOverlay != null)
        {
            Destroy(_archMagicCastOverlay);
            ClearInternalRefs();
        }

        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetPopupCanvas() : null;
        if (canvas == null) return;

        var prefab = overlayPrefab != null
            ? overlayPrefab
            : Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("[ArchMagicOverlayPresenter] ArchMagicCastOverlay prefab not found.");
            return;
        }

        _archMagicCastOverlay = Instantiate(prefab, canvas.transform);
        _archMagicCastOverlay.name = "ArchMagicCastOverlay";

        var rt = _archMagicCastOverlay.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.SetAsLastSibling();
        }

        _overlayView = _archMagicCastOverlay.GetComponent<ArchMagicCastOverlayView>();
        if (_overlayView == null)
            _overlayView = _archMagicCastOverlay.AddComponent<ArchMagicCastOverlayView>();

        _overlayView.CacheRefs();
        _archMagicCastOverlayCanvasGroup = _overlayView.CanvasGroup;

        if (archMagicCastCountdownFont != null)
            _overlayView.ApplyCountdownFont(archMagicCastCountdownFont);
        else if (TMP_Settings.defaultFontAsset != null)
            _overlayView.ApplyCountdownFont(TMP_Settings.defaultFontAsset);

        _overlayView.ApplyDefaultTextStyles();
        _overlayView.SetBackdropAlpha(archMagicCountdownBackdropAlpha);

        _archMagicCastOverlay.SetActive(false);
    }
}
