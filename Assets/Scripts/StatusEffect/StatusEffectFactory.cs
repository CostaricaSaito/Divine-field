using UnityEngine;

public static class StatusEffectFactory
{
    public static IStatusEffect Create(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Weaken:
                return new WeakenEffect();

            case StatusEffectType.Sickness:
            case StatusEffectType.SevereSickness:
            case StatusEffectType.PurgatorySickness:
            case StatusEffectType.ParadiseSickness:
                return new DiseaseLineEffect(type);

            default:
                Debug.LogWarning($"未実装の状態異常: {type}");
                return null;
        }
    }
}