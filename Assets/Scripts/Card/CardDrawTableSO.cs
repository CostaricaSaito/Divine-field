using UnityEngine;

/// <summary>
/// 手札抽選のレア度別デフォルト重み。Resources/CardDrawTable に配置し CardDealer から参照。
/// 数値が大きいほど出やすい。カード個別の customDrawWeight が -1 のときに適用される。
/// </summary>
[CreateAssetMenu(fileName = "CardDrawTable", menuName = "DivineField/Card Draw Table")]
public class CardDrawTableSO : ScriptableObject
{
    [Header("Default draw weight by rarity (higher = more common)")]
    [Min(0)] public int commonDefaultWeight = 30;
    [Min(0)] public int uncommonDefaultWeight = 10;
    [Min(0)] public int rareDefaultWeight = 3;
    [Min(0)] public int superRareDefaultWeight = 1;
    [Min(0)] public int ultraRareDefaultWeight = 0;

    public int GetDefaultWeight(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common: return commonDefaultWeight;
            case CardRarity.Uncommon: return uncommonDefaultWeight;
            case CardRarity.Rare: return rareDefaultWeight;
            case CardRarity.SuperRare: return superRareDefaultWeight;
            case CardRarity.UltraRare: return ultraRareDefaultWeight;
            default: return 0;
        }
    }
}
