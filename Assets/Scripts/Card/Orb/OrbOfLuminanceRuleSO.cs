using UnityEngine;

[CreateAssetMenu(fileName = "OrbOfLuminanceRule", menuName = "DivineField/Card/Orb/Orb Of Luminance Rule")]
public class OrbOfLuminanceRuleSO : OrbCardRuleSO
{
    [Tooltip("Primary status is on CardData.statusEffectToApply (Restraint).")]
    public StatusEffectType additionalStatusOnResolve = StatusEffectType.Misfortune;
}
