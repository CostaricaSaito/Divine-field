using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ランクマッチポップアップの開閉演出。
/// UI は Prefab 上で組み、本スクリプトは演出対象の参照とアニメーションのみ担当します。
/// </summary>
/// <remarks>
/// 推奨階層:
/// RankMatchPopup（本コンポーネント・画面全体 Stretch）
/// └ OverlayRoot（Stretch）
///   ├ DimBlocker（Stretch・半透明）
///   ├ PopupBody（中央アンカー）
///   │  ├ TopShutter（中央アンカー・Pivot 下中央・Image＝ポップアップ上半分）
///   │  └ BottomShutter（中央アンカー・Pivot 上中央・Image＝ポップアップ下半分）
///   └ ContentRoot（中央アンカー・CanvasGroup・最終サイズで配置）
///      └ （ボタン・テキスト等は自由に配置）
/// Top/BottomShutter の Width と ContentRoot の Height は「ポップアップ寸法」と一致させてください。
/// シャッターを全画面にする場合は RankMatchPopupView の Shutter Fill Screen をオンにしてください。
/// </remarks>
[DisallowMultipleComponent]
public sealed class RankMatchPopupView : MonoBehaviour
{
    [Header("演出ターゲット（Prefab で割り当て）")]
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private RectTransform dimBlocker;
    [SerializeField] private RectTransform popupBody;
    [SerializeField] private RectTransform topShutter;
    [SerializeField] private RectTransform bottomShutter;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private CanvasGroup contentCanvasGroup;

    [Header("UI（任意）")]
    [SerializeField] private Button closeButton;

    [Header("ポップアップ寸法（ContentRoot のサイズと揃える）")]
    [SerializeField] [Min(1f)] private float popupWidth = 920f;
    [SerializeField] [Min(1f)] private float popupFullHeight = 1180f;

    [Header("シャッター")]
    [Tooltip("オン: 上下シャッターが OverlayRoot（画面）いっぱいに広がります。オフ: ポップアップ寸法に合わせます。")]
    [SerializeField] private bool shutterFillScreen = true;

    [Header("演出パラメータ")]
    [SerializeField] [Min(0.01f)] private float lineAppearDuration = 0.18f;
    [SerializeField] [Min(0.01f)] private float shutterOpenDuration = 0.45f;
    [SerializeField] [Min(1f)] private float lineThickness = 4f;
    [SerializeField] private LeanTweenType lineEase = LeanTweenType.easeOutQuad;
    [SerializeField] private LeanTweenType shutterEase = LeanTweenType.easeInOutCubic;
    [Tooltip("Time.timeScale=0 でも再生したい場合オン。")]
    [SerializeField] private bool useUnscaledTime;

    [Header("SE（Addressables）")]
    [SerializeField] private string lineAppearSeAddress = "Assets/SE/普通カード.mp3";
    [SerializeField] private string shutterOpenSeAddress = "Assets/SE/メニューを開く4.mp3";

    [Header("プロフィール表示")]
    [SerializeField] private RankMatchPopupProfileBinder profileBinder;

    float _popupHalfHeight;
    float _shutterTargetWidth;
    float _shutterTargetHalfHeight;
    bool _isOpen;
    bool _isAnimating;
    Action _onOpenComplete;

    public bool IsOpen => _isOpen;
    public bool IsAnimating => _isAnimating;
    public event Action Closed;

    void Awake()
    {
        ResolveReferences();
        SyncPopupMetricsFromContent();
        CacheShutterTargets();
        WireCloseButton();
        ApplyInitialHiddenState();
    }

    void OnDisable()
    {
        CancelTweens();
    }

    void OnDestroy()
    {
        CancelTweens();
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            closeButton.onClick.RemoveListener(CloseImmediate);
        }
    }

    /// <summary>開く演出を再生します。既に開いている／再生中は false。</summary>
    public bool TryPlayOpen(Action onComplete = null)
    {
        if (_isOpen || _isAnimating) return false;
        if (!ValidateRequiredReferences()) return false;

        ResolveReferences();
        SyncPopupMetricsFromContent();
        CacheShutterTargets();
        ApplyInitialHiddenState();
        RefreshProfileDisplay();

        gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        CacheShutterTargets();
        ApplyInitialHiddenState();

        _isAnimating = true;
        _onOpenComplete = onComplete;
        SetCloseButtonInteractable(false);
        PlayLineAppear();
        return true;
    }

    /// <summary>閉じる演出を再生します。演出なしで即閉じる場合は <see cref="CloseImmediate"/>。</summary>
    public bool TryPlayClose()
    {
        if (!_isOpen || _isAnimating) return false;

        CancelTweens();
        CacheShutterTargets();
        _isAnimating = true;
        SetCloseButtonInteractable(false);
        PlayShutterClose();
        return true;
    }

    void OnCloseButtonClicked()
    {
        TryPlayClose();
    }

    /// <summary>演出なしで即座に閉じます。</summary>
    public void CloseImmediate()
    {
        CancelTweens();
        _isOpen = false;
        _isAnimating = false;
        _onOpenComplete = null;
        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    void ResolveReferences()
    {
        if (overlayRoot == null) overlayRoot = transform.Find("OverlayRoot") as RectTransform;
        if (dimBlocker == null && overlayRoot != null) dimBlocker = overlayRoot.Find("DimBlocker") as RectTransform;
        if (popupBody == null && overlayRoot != null) popupBody = overlayRoot.Find("PopupBody") as RectTransform;
        if (topShutter == null && popupBody != null) topShutter = popupBody.Find("TopShutter") as RectTransform;
        if (bottomShutter == null && popupBody != null) bottomShutter = popupBody.Find("BottomShutter") as RectTransform;
        if (contentRoot == null && overlayRoot != null) contentRoot = overlayRoot.Find("ContentRoot") as RectTransform;
        if (contentCanvasGroup == null && contentRoot != null)
            contentCanvasGroup = contentRoot.GetComponent<CanvasGroup>();
        if (closeButton == null && contentRoot != null)
            closeButton = contentRoot.Find("CloseButton")?.GetComponent<Button>();
        if (profileBinder == null)
            profileBinder = GetComponent<RankMatchPopupProfileBinder>();
    }

    void RefreshProfileDisplay()
    {
        if (profileBinder == null)
            profileBinder = GetComponent<RankMatchPopupProfileBinder>();
        profileBinder?.Refresh();
    }

    void SyncPopupMetricsFromContent()
    {
        if (contentRoot != null)
        {
            var size = contentRoot.sizeDelta;
            if (size.x > 1f) popupWidth = size.x;
            if (size.y > 1f) popupFullHeight = size.y;
        }

        _popupHalfHeight = popupFullHeight * 0.5f;
    }

    void CacheShutterTargets()
    {
        if (shutterFillScreen)
        {
            var rt = overlayRoot != null ? overlayRoot : GetComponent<RectTransform>();
            var rect = rt != null ? rt.rect : new Rect(0f, 0f, 1080f, 1920f);
            _shutterTargetWidth = rect.width > 1f ? rect.width : 1080f;
            _shutterTargetHalfHeight = rect.height > 1f ? rect.height * 0.5f : 960f;
        }
        else
        {
            _shutterTargetWidth = popupWidth;
            _shutterTargetHalfHeight = _popupHalfHeight;
        }
    }

    void WireCloseButton()
    {
        if (closeButton == null) return;
        closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        closeButton.onClick.RemoveListener(CloseImmediate);
        closeButton.onClick.AddListener(OnCloseButtonClicked);
    }

    bool ValidateRequiredReferences()
    {
        ResolveReferences();

        if (popupBody == null || topShutter == null || bottomShutter == null)
        {
            Debug.LogError(
                "[RankMatchPopupView] PopupBody / TopShutter / BottomShutter が未設定です。Prefab に配置して Inspector で割り当ててください。",
                this);
            return false;
        }

        if (contentCanvasGroup == null)
        {
            Debug.LogError(
                "[RankMatchPopupView] ContentRoot に CanvasGroup が必要です。Prefab の ContentRoot に追加してください。",
                this);
            return false;
        }

        return true;
    }

    void ApplyInitialHiddenState()
    {
        _popupHalfHeight = popupFullHeight * 0.5f;
        ApplyShutterOpen(0f);

        if (popupBody != null)
        {
            popupBody.gameObject.SetActive(true);
            popupBody.localScale = new Vector3(0f, 1f, 1f);
        }

        contentCanvasGroup.alpha = 0f;
    }

    void PlayLineAppear()
    {
        PlaySe(lineAppearSeAddress);
        ApplyShutterOpen(0f);
        popupBody.localScale = new Vector3(0f, 1f, 1f);

        var scaleTween = LeanTween.scale(popupBody, Vector3.one, lineAppearDuration).setEase(lineEase);
        if (useUnscaledTime) scaleTween.setIgnoreTimeScale(true);

        var complete = LeanTween.delayedCall(gameObject, lineAppearDuration, PlayShutterOpen);
        if (useUnscaledTime) complete.setIgnoreTimeScale(true);
    }

    void PlayShutterOpen()
    {
        PlaySe(shutterOpenSeAddress);

        var tween = LeanTween.value(gameObject, 0f, 1f, shutterOpenDuration)
            .setEase(shutterEase)
            .setOnUpdate(t =>
            {
                ApplyShutterOpen(t);
                contentCanvasGroup.alpha = Mathf.Clamp01(t);
            })
            .setOnComplete(FinishOpen);
        if (useUnscaledTime) tween.setIgnoreTimeScale(true);
    }

    void FinishOpen()
    {
        ApplyShutterOpen(1f);
        contentCanvasGroup.alpha = 1f;

        _isAnimating = false;
        _isOpen = true;
        SetCloseButtonInteractable(true);
        _onOpenComplete?.Invoke();
        _onOpenComplete = null;
    }

    void PlayShutterClose()
    {
        var tween = LeanTween.value(gameObject, 1f, 0f, shutterOpenDuration)
            .setEase(shutterEase)
            .setOnUpdate(t =>
            {
                ApplyShutterOpen(t);
                contentCanvasGroup.alpha = Mathf.Clamp01(t);
            })
            .setOnComplete(PlayLineDisappear);
        if (useUnscaledTime) tween.setIgnoreTimeScale(true);
    }

    void PlayLineDisappear()
    {
        ApplyShutterOpen(0f);
        contentCanvasGroup.alpha = 0f;

        var scaleTween = LeanTween.scale(popupBody, new Vector3(0f, 1f, 1f), lineAppearDuration).setEase(lineEase);
        if (useUnscaledTime) scaleTween.setIgnoreTimeScale(true);

        var complete = LeanTween.delayedCall(gameObject, lineAppearDuration, FinishClose);
        if (useUnscaledTime) complete.setIgnoreTimeScale(true);
    }

    void FinishClose()
    {
        CancelTweens();
        _isAnimating = false;
        _isOpen = false;
        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    void SetCloseButtonInteractable(bool interactable)
    {
        if (closeButton != null)
            closeButton.interactable = interactable;
    }

    void ApplyShutterOpen(float t)
    {
        var lineHalf = lineThickness * 0.5f;
        var halfHeight = Mathf.Lerp(lineHalf, _shutterTargetHalfHeight, t);

        topShutter.sizeDelta = new Vector2(_shutterTargetWidth, halfHeight);
        bottomShutter.sizeDelta = new Vector2(_shutterTargetWidth, halfHeight);
    }

    void CancelTweens()
    {
        LeanTween.cancel(gameObject);
        if (popupBody != null) LeanTween.cancel(popupBody.gameObject);
    }

    static void PlaySe(string address)
    {
        if (string.IsNullOrEmpty(address)) return;
        SoundEffectPlayer.I?.Play(address);
    }
}
