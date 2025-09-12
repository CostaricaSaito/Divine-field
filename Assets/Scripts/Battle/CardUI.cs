using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    public Image cardImage;          // ← UIのImage（Inspectorでセット）
    public TMP_Text cardNameText;    // ← カード名表示
    public Button button;            // ← ボタンクリック用

    private CardData cardData;
    private Sprite backSprite;
    private bool isFaceUp = false;

    public void Setup(CardData data, Sprite back)
    {
        cardData = data;
        backSprite = back;
        isFaceUp = false;

        ShowBack();                  // ← 裏面を表示！
        if (button) button.interactable = false;

        // クリック登録を単一入口に
        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);  // ← 修正ポイント！
        }
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

        // ★ 単一入口へ（CardData ではなく CardUI を渡す）
        //    BattleManager 側の新しい API に合わせる
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
}
