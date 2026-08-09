﻿using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 大魔法・天変地異など重要メッセージ用。Canvas の水平中心と、指定側 CardDisplayPanel の縦位置の交点に配置する。
/// 下から定位置へスライドイン → 一定時間表示 → フェードアウト。
/// </summary>
public class ImportantPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color defaultOutlineForMessage = Color.white;

    [Header("登場：下から定位置へ")]
    [Tooltip("最終位置より下にずらす量（px）。この位置から上に補間して定位置へ。")]
    [SerializeField] private float entryOffsetY = 80f;
    [SerializeField] private float riseDuration = 0.45f;

    [Header("定位置で表示してからフェード")]
    [Tooltip("定位置に着いてからフェード開始までの秒（インターバル）。")]
    [SerializeField] private float holdDuration = 0.85f;
    [Tooltip("フェードアウトにかける秒。この間はフェードのみ（移動なし）。")]
    public float fadeDuration = 0.25f;

    /// <summary>UI 生成失敗時など、待機に使う既定秒数（シーケンス全体のおおよその長さ）。</summary>
    public const float DefaultSequenceLifetimeIfUnknown = 2.5f;

    /// <summary>rise → hold → fade の合計秒。</summary>
    public float SequenceLifetimeSeconds =>
        Mathf.Max(0.02f, riseDuration) + Mathf.Max(0f, holdDuration) + EffectiveFadeDuration;

    private float EffectiveFadeDuration =>
        fadeDuration > 0.001f ? fadeDuration : DamagePopup.DefaultFadeDurationIfUnknown;

    private ImportantPopupSettings _boundSettings;
    private float _maxTextWidthPx = 900f;
    private Sprite _defaultBackgroundSprite;
    private Image.Type _defaultImageType;
    private float _defaultPixelsPerUnitMultiplier;
    private bool _defaultBackgroundCached;

    private enum Phase { Rising, Holding, Fading }

    private Phase _phase;
    private float _phaseTimer;
    private RectTransform _rt;
    private Vector2 _restAnchoredPosition;
    private Vector2 _startAnchoredPosition;
    private bool _entranceInitialized;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        if (messageText == null)
        {
            var t = transform.Find("Message");
            if (t != null) messageText = t.GetComponent<TMP_Text>();
        }

        if (backgroundImage == null)
        {
            var frame = transform.Find("GoldFrame");
            if (frame != null) backgroundImage = frame.GetComponent<Image>();
            if (backgroundImage == null)
                backgroundImage = GetComponentInChildren<Image>(true);
        }

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        CacheDefaultBackgroundState();
    }

    public void BindSettings(ImportantPopupSettings settings)
    {
        _boundSettings = settings;
        ApplyEntranceFromSettings(settings);
    }

    private void ApplyEntranceFromSettings(ImportantPopupSettings settings)
    {
        if (settings == null) return;
        var timing = settings.EntranceOrDefault;
        entryOffsetY = timing.entryOffsetY;
        riseDuration = timing.riseDuration;
        holdDuration = timing.holdDuration;
        fadeDuration = timing.fadeDuration;
        _maxTextWidthPx = timing.maxTextWidthPx;
    }

    /// <summary>文言と文字色を設定（後方互換）。</summary>
    public void Setup(string message, Color fillColor)
    {
        var entry = new ImportantPopupStyleEntry
        {
            kind = ImportantPopupKind.RuntimeCustom,
            message = message,
            textColor = fillColor,
            outlineColor = defaultOutlineForMessage,
        };
        Setup(entry);
    }

    public void Setup(ImportantPopupStyleEntry entry, string messageOverride = null)
    {
        ApplyBackground(entry);
        ApplyText(messageOverride ?? entry.message, entry.textColor, entry.outlineColor, entry.fontSize);
        InitializeEntranceFromLayout();
    }

    private void CacheDefaultBackgroundState()
    {
        if (backgroundImage == null || _defaultBackgroundCached) return;
        _defaultBackgroundSprite = backgroundImage.sprite;
        _defaultImageType = backgroundImage.type;
        _defaultPixelsPerUnitMultiplier = backgroundImage.pixelsPerUnitMultiplier;
        _defaultBackgroundCached = true;
    }

    private void ApplyBackground(ImportantPopupStyleEntry entry)
    {
        if (backgroundImage == null) return;
        CacheDefaultBackgroundState();

        if (entry.UsesSpriteBackground)
        {
            backgroundImage.sprite = entry.backgroundSprite;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = false;
            backgroundImage.color = Color.white;
            return;
        }

        if (_defaultBackgroundSprite != null)
            backgroundImage.sprite = _defaultBackgroundSprite;
        backgroundImage.type = _defaultImageType;
        backgroundImage.pixelsPerUnitMultiplier = _defaultPixelsPerUnitMultiplier;
        backgroundImage.preserveAspect = false;
        if (entry.backgroundColor.a > 0.001f)
            backgroundImage.color = entry.backgroundColor;
        else
            backgroundImage.color = Color.white;
    }

    private void ApplyText(string message, Color textColor, Color outlineColor, float fontSize)
    {
        if (messageText == null) return;

        messageText.gameObject.SetActive(true);
        messageText.richText = false;
        messageText.text = MessagePopup.StripLineBreaks(message);
        messageText.color = textColor;
        messageText.fontStyle = FontStyles.Bold;
        messageText.outlineWidth = Mathf.Max(messageText.outlineWidth, 0.22f);
        messageText.outlineColor = outlineColor;
        messageText.enableWordWrapping = false;
        messageText.overflowMode = TextOverflowModes.Overflow;
        messageText.enableAutoSizing = false;
        messageText.alignment = TextAlignmentOptions.Center;
        if (fontSize > 0.01f)
            messageText.fontSize = fontSize;

        MessagePopup.ApplySingleLineHorizontalFit(messageText, _maxTextWidthPx);
    }

    private void InitializeEntranceFromLayout()
    {
        if (_entranceInitialized) return;
        _entranceInitialized = true;

        _rt = transform as RectTransform;
        if (_rt == null) return;

        _restAnchoredPosition = _rt.anchoredPosition;
        _startAnchoredPosition = _restAnchoredPosition + Vector2.down * entryOffsetY;
        _rt.anchoredPosition = _startAnchoredPosition;

        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        _phase = Phase.Rising;
        _phaseTimer = 0f;
    }

    private void Update()
    {
        if (!_entranceInitialized || _rt == null) return;

        switch (_phase)
        {
            case Phase.Rising:
                _phaseTimer += Time.deltaTime;
                float riseDur = Mathf.Max(0.02f, riseDuration);
                float t = Mathf.Clamp01(_phaseTimer / riseDur);
                float smooth = t * t * (3f - 2f * t);
                _rt.anchoredPosition = Vector2.Lerp(_startAnchoredPosition, _restAnchoredPosition, smooth);
                if (_phaseTimer >= riseDur)
                {
                    _rt.anchoredPosition = _restAnchoredPosition;
                    _phase = Phase.Holding;
                    _phaseTimer = 0f;
                }
                break;

            case Phase.Holding:
                _phaseTimer += Time.deltaTime;
                if (_phaseTimer >= Mathf.Max(0f, holdDuration))
                {
                    _phase = Phase.Fading;
                    _phaseTimer = 0f;
                }
                break;

            case Phase.Fading:
                _phaseTimer += Time.deltaTime;
                float fd = EffectiveFadeDuration;
                if (_canvasGroup != null)
                    _canvasGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(_phaseTimer / fd));
                if (_phaseTimer >= fd)
                    Destroy(gameObject);
                break;
        }
    }
}
