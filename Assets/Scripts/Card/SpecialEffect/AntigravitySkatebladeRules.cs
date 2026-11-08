/// <summary>Antigravity Roller Skates: renew defender hand to a fixed count after damage.</summary>
public static class AntigravitySkatebladeRules
{
    public const int TargetHandCount = 10;

    public static bool IsAntigravitySkateblade(CardData card) =>
        card != null && card.specialCardEffect is AntigravitySkatebladeDefenseEffectSO;
}
