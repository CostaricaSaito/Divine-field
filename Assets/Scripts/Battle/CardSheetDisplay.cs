using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CardSheetDisplay : MonoBehaviour
{
    [Header("UI参照")]
    public Image artworkSlot;
    public TMP_Text cardNameText;
    public TMP_Text atkDefText;
    public TMP_Text descText;
    public Image attributeIcon;
    public Image goldIcon;
    public TMP_Text goldValueText;

    public void Setup(CardData cardData)
    {
        if (artworkSlot) artworkSlot.sprite = cardData.cardImage;
        if (cardNameText) cardNameText.text = cardData.cardName;
        if (atkDefText) atkDefText.text = $"ATK {cardData.attackPower} / DEF {cardData.defensePower}";
        if (descText) descText.text = cardData.description;
        if (attributeIcon) attributeIcon.sprite = cardData.elementIcon;
        if (goldValueText) goldValueText.text = $"¥{cardData.cardValue}";
    }
}