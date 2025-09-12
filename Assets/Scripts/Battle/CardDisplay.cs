using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardDisplay : MonoBehaviour
{
    public Image cardImage;
    public TMP_Text cardNameText;
    public TMP_Text powerText;
    public TMP_Text descriptionText;
    public void SetCard(CardData card)
    {
        if (card == null)
        {
            Debug.Log("カード表示リセット");
            cardImage.sprite = null;
            cardNameText.text = "";
            powerText.text = "";
            descriptionText.text = "";
            return;
        }

        Debug.Log("カード表示更新：" + card.cardName);

        cardImage.sprite = card.cardImage;
        cardNameText.text = card.cardName;

        switch (card.cardType)
        {
            case CardType.Attack:
                powerText.text = $"ATK: {card.attackPower}";
                break;
            case CardType.Defense:
                powerText.text = $"DEF: {card.defensePower}";
                break;
            case CardType.Magic:
                powerText.text = $"MAG: {card.attackPower}";
                break;
            case CardType.Special:
                powerText.text = $"SPC: {card.attackPower}";
                break;
        }

        descriptionText.text = card.description;
    }
}