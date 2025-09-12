using UnityEngine;
using UnityEngine.UI;


public enum CardType
{
    Attack = 0,
    Defense = 1,
    Magic = 2,
    Recovery = 3,
    Special = 4,
}

public enum ElementType
{
    None,
    Fire,
    Water,
    Wind,
    Thunder,
    Steel,
    Ice,
    Poison,
    Dark,
    Light,
    
}

public interface ISpecialCardEffect
{
    void Activate(PlayerStatus player, PlayerStatus enemy);
}


[CreateAssetMenu(fileName = "NewCard", menuName = "DivineField/Card")]
public class CardData : ScriptableObject
{

    [Header("基本情報")]
    public string cardName;
    public CardType cardType;
    public Sprite cardImage;
    [TextArea(2, 4)] public string description;

    [Header("数値パラメータ")]
    public int attackPower = 0;
    public int defensePower = 0;
    public ElementType element = ElementType.None;
    [Range(0, 100)] public int hitRate = 100;


    [Header("魔法カード専用")]
    public int mpCost = 0;
    public int maxUses = 1;

    [Header("回復パラメータ")]
    public int recoveryAmount = 0; public bool healsHP = false;
    public bool healsMP = false;
    public bool healsGP = false;

    [Header("使用可能なフェーズ")]
    public bool usableInAttackPhase = false;
    public bool usableInDefensePhase = false;

    [Header("行動分類フラグ")]
    public bool isPrimaryAttack = false;         // 例：剣、炎の拳
    public bool isAdditionalAttack = false;      // 例：連撃、火の粉
    public bool isPrimaryDefense = false;        // 例：盾
    public bool isCounterAttack = false;         // 例：反射剣、カウンター
    public bool isRecovery = false;              // 例：回復草
    public bool isSpecialEffect = false;         // 例：精霊のぬいぐるみ

    [Header("複数枚使用・併用")]
    public bool canBeUsedMultipleTimes = false;  // このカードを複数枚使用可能か？
    public bool canBeUsedWithPrimaryAttack = false; // PrimaryAttackと併用可能か？

    [Header("特殊効果（任意）")]
    public bool canApplyStatusEffect = false;
    [Range(0, 100)] public int statusEffectChance = 0;

    [Header("UI表示用")]
    public Sprite elementIcon; // 属性アイコン（例：火、水など）

    [Header("経済パラメータ")]
    public int cardValue = 0; // GPでの価値（売買価格）

    [Header("UI参照（非表示）")]
    [System.NonSerialized] public CardUI cardUI;

}