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

    private CardData currentCardData;
    
    public void Setup(CardData cardData)
    {
        currentCardData = cardData;
        SetupArtwork(cardData);
        if (cardNameText) cardNameText.text = cardData.cardName;
        if (atkDefText) atkDefText.text = $"ATK {cardData.attackPower} / DEF {cardData.defensePower}";
        if (descText) descText.text = cardData.description;
        if (attributeIcon) attributeIcon.gameObject.SetActive(false);
        if (goldIcon) goldIcon.gameObject.SetActive(false);
        if (goldValueText) goldValueText.text = "";
    }
    
    public CardData GetCardData()
    {
        return currentCardData;
    }
    
    /// <summary>
    /// カード画像を設定
    /// </summary>
    private void SetupArtwork(CardData cardData)
    {
        if (artworkSlot == null || cardData?.cardImage == null) return;
        
        // 画像を設定
        artworkSlot.sprite = cardData.cardImage;
        
        // 画像をArtWorkSlotにぴったりフィットさせる設定
        artworkSlot.type = Image.Type.Simple;
        artworkSlot.preserveAspect = false; // アスペクト比を無視して枠にぴったり合わせる
    }
}