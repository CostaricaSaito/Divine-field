using TMPro;
using UnityEngine;

/// <summary>
/// 大魔法など重要メッセージ用。Canvas の水平中心と、指定側 CardDisplayPanel の縦位置の交点に配置する。
/// 下から定位置へスライドイン → 一定時間表示 → フェードアウト。
/// </summary>
public class ImportantPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
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

    /// <summary>rise → hold → fade の合計秒。シーケンスの <see cref="DamagePopup.WaitAfterPopupLifetimeAsync"/> に渡す。</summary>
    public float SequenceLifetimeSeconds =>
        Mathf.Max(0.02f, riseDuration) + Mathf.Max(0f, holdDuration) + EffectiveFadeDuration;

    private float EffectiveFadeDuration => fadeDuration > 0.001f ? fadeDuration : DamagePopup.DefaultFadeDurationIfUnknown;

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

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>文言と文字色を設定（枠はプレハブのまま）。レイアウト後に呼ぶ想定。</summary>
    public void Setup(string message, Color fillColor)
    {
        if (messageText == null) return;

        messageText.gameObject.SetActive(true);
        messageText.text = message ?? string.Empty;
        messageText.color = fillColor;
        messageText.fontStyle = FontStyles.Bold;
        if (messageText.outlineWidth < 0.08f)
            messageText.outlineWidth = 0.22f;
        messageText.outlineColor = defaultOutlineForMessage;
        messageText.enableWordWrapping = true;
        messageText.overflowMode = TextOverflowModes.Overflow;
        messageText.enableAutoSizing = true;
        messageText.alignment = TextAlignmentOptions.Center;

        InitializeEntranceFromLayout();
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
