using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    public Image cardImage;          // カード UIのImage（Inspectorでセット）
    public TMP_Text cardNameText;    // カード名表示
    public Button button;            // ボタンクリック用
    public Image highlightBorder;    // ハイライト用の青色枠

    [Header("レア演出（任意・手札裏面のみ）")]
    [Tooltip("未設定時は実行時に CardImage と同レイアウトで生成します。")]
    [SerializeField] private Image rareBackRainbowOverlay;
    [Tooltip("RainbowOutline の不透明度。大きいほど虹がはっきり見えます。")]
    [Range(0.1f, 1f)] [SerializeField] private float rareRainbowIntensity = 0.65f;

    private CardData cardData;
    private Sprite backSprite;
    private bool isFaceUp = false;
    private bool isHighlighted = false;
    private bool playerHandRareBackPresentation;

    public void Setup(CardData data, Sprite back, bool playerHandRareBackPresentation = false)
    {
        cardData = data;
        backSprite = back;
        isFaceUp = false;
        this.playerHandRareBackPresentation = playerHandRareBackPresentation;

        ShowBack();                  // 裏面を表示
        if (button) button.interactable = false;

        // クリック登録をリセット
        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);  // クリックイベント
        }
        
        // ハイライトを初期化
        SetHighlight(false);
    }

    public void Reveal()
    {
        if (isFaceUp) return;
        isFaceUp = true;

        SetRareBackOverlayActive(false);

        if (cardData == null) return;

        if (cardImage) cardImage.sprite = cardData.cardImage;
        if (cardNameText) cardNameText.text = cardData.cardName;
        if (button) button.interactable = true;
    }

    private void ShowBack()
    {
        if (cardImage) cardImage.sprite = backSprite;
        if (cardNameText) cardNameText.text = "";

        UpdateRareBackOverlayForCurrentState();
    }

    private void UpdateRareBackOverlayForCurrentState()
    {
        bool show = !isFaceUp
                    && playerHandRareBackPresentation
                    && cardData != null
                    && cardData.isRare;
        SetRareBackOverlayActive(show);
    }

    private void SetRareBackOverlayActive(bool show)
    {
        var overlay = GetOrCreateRareOverlay();
        if (overlay == null) return;

        if (show)
        {
            if (cardImage != null)
            {
                overlay.sprite = cardImage.sprite;
                overlay.rectTransform.anchorMin = cardImage.rectTransform.anchorMin;
                overlay.rectTransform.anchorMax = cardImage.rectTransform.anchorMax;
                overlay.rectTransform.anchoredPosition = cardImage.rectTransform.anchoredPosition;
                overlay.rectTransform.sizeDelta = cardImage.rectTransform.sizeDelta;
                overlay.rectTransform.pivot = cardImage.rectTransform.pivot;
                overlay.rectTransform.localScale = cardImage.rectTransform.localScale;
            }

            overlay.gameObject.SetActive(true);
            var rainbow = overlay.GetComponent<RainbowOutline>();
            if (rainbow == null)
                rainbow = overlay.gameObject.AddComponent<RainbowOutline>();
            rainbow.intensity = rareRainbowIntensity;
        }
        else
        {
            var ro = overlay.GetComponent<RainbowOutline>();
            if (ro != null)
                Destroy(ro);
            overlay.color = new Color(1f, 1f, 1f, 0f);
            overlay.gameObject.SetActive(false);
        }
    }

    private Image GetOrCreateRareOverlay()
    {
        if (rareBackRainbowOverlay != null)
            return rareBackRainbowOverlay;

        Transform existing = transform.Find("RareBackRainbowOverlay");
        if (existing != null)
        {
            rareBackRainbowOverlay = existing.GetComponent<Image>();
            return rareBackRainbowOverlay;
        }

        if (cardImage == null)
            return null;

        var go = new GameObject("RareBackRainbowOverlay", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        var src = cardImage.rectTransform;
        rt.SetParent(src.parent, false);
        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.anchoredPosition = src.anchoredPosition;
        rt.sizeDelta = src.sizeDelta;
        rt.pivot = src.pivot;
        rt.localScale = src.localScale;
        rt.SetSiblingIndex(src.GetSiblingIndex() + 1);

        var img = go.GetComponent<Image>();
        img.sprite = src.GetComponent<Image>().sprite;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);

        rareBackRainbowOverlay = img;
        return rareBackRainbowOverlay;
    }

    private void OnClick()
    {
        if (!button || !button.interactable || cardData == null) return;

        // カード選択処理（CardData ではなく CardUI を渡す）
        //   BattleManager の新しい API に合わせる
        if (BattleManager.I != null)
        {
            BattleManager.I.SetSelectedCard(this);
        }
        else
        {
            Debug.LogWarning("[CardUI] BattleManager インスタンスが見つかりません");
        }
    }

    public CardData GetCardData() => cardData;
    
    /// <summary>
    /// ハイライト表示を設定
    /// </summary>
    /// <param name="highlight">ハイライトするかどうか</param>
    public void SetHighlight(bool highlight)
    {
        isHighlighted = highlight;
        if (highlightBorder != null)
        {
            highlightBorder.gameObject.SetActive(highlight);
        }
    }
    
    /// <summary>
    /// 現在ハイライトされているかどうか
    /// </summary>
    public bool IsHighlighted => isHighlighted;

    /// <summary>
    /// カードが裏向きかどうか
    /// </summary>
    public bool IsFaceDown() => !isFaceUp;
}
