using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    public Image cardImage;          // カード UIのImage（Inspectorでセット）
    public TMP_Text cardNameText;    // カード名表示
    public Button button;            // ボタンクリック用
    public Image highlightBorder;    // ハイライト用の青色枠

    private CardData cardData;
    private Sprite backSprite;
    private bool isFaceUp = false;
    private bool isHighlighted = false;

    public void Setup(CardData data, Sprite back)
    {
        cardData = data;
        backSprite = back;
        isFaceUp = false;

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

        if (cardImage) cardImage.sprite = cardData.cardImage;
        if (cardNameText) cardNameText.text = cardData.cardName;
        if (button) button.interactable = true;
    }

    private void ShowBack()
    {
        if (cardImage) cardImage.sprite = backSprite;
        if (cardNameText) cardNameText.text = "";
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