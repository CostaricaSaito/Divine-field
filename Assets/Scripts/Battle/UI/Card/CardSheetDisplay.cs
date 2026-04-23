using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CardSheetDisplay : MonoBehaviour
{
    [Header("UI参照")]
    [Tooltip("未設定時は Panel/BG を自動検索。顕現カードの枠差し替えに使用。")]
    [SerializeField] private Image cardSheetBackground;

    public Image artworkSlot;
    public TMP_Text cardNameText;
    public TMP_Text atkDefText;
    public TMP_Text descText;
    public Image attributeIcon;
    public Image goldIcon;
    public TMP_Text goldValueText;

    [Header("魔法カード：Gold の代わりに消費MP（Prefab 上の CardSheet_MPcost / CardSheet_MPcostText を割り当て）")]
    public GameObject cardSheetMPcost;
    public TMP_Text cardSheetMPcostText;
    [SerializeField] private Color mpCostBoxDefaultColor = new Color(0.55f, 0.88f, 0.62f, 1f);
    [SerializeField] private Color mpCostTextDefaultColor = Color.black;
    [SerializeField] private Color mpCostEyeStrainBoxColor = new Color(1f, 0.92f, 0.35f, 1f);
    [SerializeField] private Color mpCostEyeStrainTextColor = Color.black;
    [SerializeField] private Color mpCostClusterBoxColor = new Color(0.88f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color mpCostClusterTextColor = Color.white;

    private CardData currentCardData;

    private void Awake()
    {
        if (cardSheetBackground == null)
        {
            var t = transform.Find("Panel/BG");
            if (t != null) cardSheetBackground = t.GetComponent<Image>();
        }
    }
    
    /// <param name="ownerForMpDisplay">魔法の消費MP表示・眼精疲労／群発頭痛の見た目に使う。null ならカード素の mpCost。</param>
    public void Setup(CardData cardData, PlayerStatus ownerForMpDisplay = null)
    {
        currentCardData = cardData;
        if (cardSheetBackground != null && cardData != null && cardData.cardDisplayFrameSprite != null)
            cardSheetBackground.sprite = cardData.cardDisplayFrameSprite;

        SetupArtwork(cardData);
        if (cardNameText) cardNameText.text = cardData.cardName;
        bool atkLineLikeAttack = cardData.cardType == CardType.Attack || cardData.cardType == CardType.Ultimate;
        if (atkDefText) atkDefText.text = FormatAtkDefLine(
            cardData.attackPower,
            cardData.defensePower,
            atkLineLikeAttack);
        if (descText) descText.text = cardData.description;
        SetupElementDisplay(cardData);
        SetupGoldOrMpCostDisplay(cardData, ownerForMpDisplay);
    }

    public CardData GetCurrentCardData() => currentCardData;

    /// <summary>マジカルエクスプロージョン演出など：ATK 行のみ差し替え。</summary>
    public void SetAtkDefenseNumbers(int attack, int defense)
    {
        if (atkDefText == null) return;
        bool attackCard = currentCardData != null
            && (currentCardData.cardType == CardType.Attack || currentCardData.cardType == CardType.Ultimate);
        atkDefText.text = FormatAtkDefLine(attack, defense, attackCard);
    }

    /// <summary>
    /// 非 <see cref="CardType.Attack"/>: ATK0→DEFのみ、DEF0→ATKのみ、両方0→空欄。
    /// Attack: 基礎ATK0でも行を確保（マジカルエクスプロージョン等の数値差し替え用）。ATK0かつDEF1+のAttackは想定外だが、出た場合は ATK 0 も併記。
    /// </summary>
    private static string FormatAtkDefLine(int attack, int defense, bool isAttackTypeCard)
    {
        if (isAttackTypeCard)
        {
            if (attack == 0 && defense == 0)
                return "ATK 0";
            if (attack == 0)
                return $"ATK 0 / DEF {defense}";
            if (defense == 0)
                return $"ATK {attack}";
            return $"ATK {attack} / DEF {defense}";
        }

        if (attack == 0 && defense == 0)
            return "";
        if (attack == 0)
            return $"DEF {defense}";
        if (defense == 0)
            return $"ATK {attack}";
        return $"ATK {attack} / DEF {defense}";
    }

    /// <summary>
    /// 魔法以外：右下に GoldIcon + 価値（cardValue）。魔法：Gold を隠し消費MP（眼精疲労で2倍・群発で使用不可表示）。
    /// </summary>
    private void SetupGoldOrMpCostDisplay(CardData cardData, PlayerStatus owner)
    {
        bool isMagic = cardData != null && cardData.cardType == CardType.Magic;

        if (cardSheetMPcost != null)
            cardSheetMPcost.SetActive(isMagic);
        var mpBg = cardSheetMPcost != null ? cardSheetMPcost.GetComponent<Image>() : null;

        if (cardSheetMPcostText != null)
        {
            if (isMagic)
            {
                if (owner != null && owner.IsMagicUseForbidden())
                {
                    cardSheetMPcostText.text = "使用不可";
                    if (mpBg != null) mpBg.color = mpCostClusterBoxColor;
                    cardSheetMPcostText.color = mpCostClusterTextColor;
                }
                else if (owner != null && owner.HasEyeStrainEffect())
                {
                    int eff = owner.GetEffectiveMagicMpCost(cardData.mpCost);
                    cardSheetMPcostText.text = $"消費MP {eff}";
                    if (mpBg != null) mpBg.color = mpCostEyeStrainBoxColor;
                    cardSheetMPcostText.color = mpCostEyeStrainTextColor;
                }
                else
                {
                    cardSheetMPcostText.text = $"消費MP {cardData.mpCost}";
                    if (mpBg != null) mpBg.color = mpCostBoxDefaultColor;
                    cardSheetMPcostText.color = mpCostTextDefaultColor;
                }
            }
            else
                cardSheetMPcostText.text = "";
        }

        if (goldIcon != null)
            goldIcon.gameObject.SetActive(!isMagic);
        if (goldValueText != null)
        {
            goldValueText.gameObject.SetActive(!isMagic);
            if (!isMagic && cardData != null)
                goldValueText.text = $"¥ {cardData.cardValue}";
            else
                goldValueText.text = "";
        }
    }

    private void SetupElementDisplay(CardData cardData)
    {
        ElementType elem = cardData != null ? cardData.element : ElementType.None;

        if (attributeIcon)
        {
            Sprite icon = ElementHelper.LoadIcon(elem);
            if (icon != null)
            {
                attributeIcon.sprite = icon;
                attributeIcon.gameObject.SetActive(true);
            }
            else
            {
                attributeIcon.gameObject.SetActive(false);
            }
        }

        if (elem != ElementType.None)
        {
            Color elemColor = ElementHelper.GetElementColor(elem);
            if (cardNameText) cardNameText.color = elemColor;
            if (atkDefText) atkDefText.color = elemColor;
            if (descText) descText.color = elemColor;
        }
        else
        {
            if (cardNameText) cardNameText.color = Color.black;
            if (atkDefText) atkDefText.color = Color.black;
            if (descText) descText.color = Color.black;
        }
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