using TMPro;
using UnityEngine;

/// <summary>
/// ゲーム終了時：HP が 0 になったプレイヤー側の <c>CardDisplayPanel</c> に「往生」と表示するポップアップ。
/// 登場後に指定時間かけてパネル上端まで上昇しながら CanvasGroup.alpha を 1→0 にフェードして破棄される。
/// プレハブは <c>Assets/Resources/Prefab/OjyouPopup.prefab</c> を想定。
/// </summary>
public class OjyouPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Color defaultOutlineForMessage = Color.white;

    [Header("上昇＆フェード")]
    [Tooltip("登場から上昇完了＆フェード完了までの秒数。仕様は 2.0 秒。")]
    [SerializeField] private float riseAndFadeDuration = 2.0f;

    [Tooltip("パネル上端とポップアップ上端の間に残す余白 (px)。負値は外側にはみ出す。")]
    [SerializeField] private float topMargin = 8f;

    /// <summary>BattleManager 側から「上昇＋フェード完了まで待つ」用に参照できる秒数。</summary>
    public float SequenceLifetimeSeconds => Mathf.Max(0.02f, riseAndFadeDuration);

    private RectTransform _rt;
    private RectTransform _parentRt;
    private CanvasGroup _canvasGroup;
    private Vector2 _startAnchoredPosition;
    private Vector2 _endAnchoredPosition;
    private float _timer;
    private bool _initialized;

    private void Awake()
    {
        if (messageText == null)
        {
            var t = transform.Find("Message");
            if (t != null) messageText = t.GetComponent<TMP_Text>();
            if (messageText == null) messageText = GetComponentInChildren<TMP_Text>(true);
        }

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// 文言・色・上昇開始位置（＝現在のレイアウト位置）と終着点（親パネル上端）をセットする。
    /// </summary>
    /// <param name="message">通常は「往生」。</param>
    /// <param name="fillColor">文字の塗り色。</param>
    public void Setup(string message, Color fillColor)
    {
        if (messageText != null)
        {
            messageText.gameObject.SetActive(true);
            messageText.text = message ?? string.Empty;
            messageText.color = fillColor;
            messageText.fontStyle = FontStyles.Bold;
            if (messageText.outlineWidth < 0.08f)
                messageText.outlineWidth = 0.22f;
            messageText.outlineColor = defaultOutlineForMessage;
            messageText.enableWordWrapping = false;
            messageText.overflowMode = TextOverflowModes.Overflow;
            messageText.enableAutoSizing = true;
            messageText.alignment = TextAlignmentOptions.Center;
        }

        InitializeRiseTargetsFromLayout();
    }

    private void InitializeRiseTargetsFromLayout()
    {
        if (_initialized) return;
        _initialized = true;

        _rt = transform as RectTransform;
        if (_rt == null) return;

        _parentRt = _rt.parent as RectTransform;
        _startAnchoredPosition = _rt.anchoredPosition;

        float riseUp = 0f;
        if (_parentRt != null)
        {
            // パネル中央（アンカー中央想定）→ パネル上端までの上昇量
            float panelHalfH = _parentRt.rect.height * 0.5f;
            float popupHalfH = _rt.rect.height * 0.5f;
            riseUp = Mathf.Max(0f, panelHalfH - popupHalfH - topMargin);
        }

        _endAnchoredPosition = _startAnchoredPosition + Vector2.up * riseUp;

        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        _timer = 0f;
    }

    private void Update()
    {
        if (!_initialized || _rt == null) return;

        _timer += Time.deltaTime;
        float dur = Mathf.Max(0.02f, riseAndFadeDuration);
        float t = Mathf.Clamp01(_timer / dur);

        float smooth = t * t * (3f - 2f * t);
        _rt.anchoredPosition = Vector2.Lerp(_startAnchoredPosition, _endAnchoredPosition, smooth);

        if (_canvasGroup != null)
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

        if (_timer >= dur)
            Destroy(gameObject);
    }
}
