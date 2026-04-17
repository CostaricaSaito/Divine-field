using UnityEngine;

public static class StatusEffectFactory
{
    public static IStatusEffect Create(StatusEffectType type, StatusProgressionConfig config = null)
    {
        config ??= StatusProgressionConfig.GetRuntimeFallback();

        switch (type)
        {
            case StatusEffectType.Weaken:
                return new WeakenEffect();

            case StatusEffectType.Sickness:
            case StatusEffectType.SevereSickness:
            case StatusEffectType.PurgatorySickness:
            case StatusEffectType.ParadiseSickness:
                return new DiseaseLineEffect(type);

            case StatusEffectType.Seal:
                return new SealEffect(config.defaultSealDurationTurns);

            case StatusEffectType.EyeStrain:
                return new EyeStrainEffect();

            case StatusEffectType.ClusterHeadache:
                return new ClusterHeadacheEffect();

            case StatusEffectType.Smoke:
                return new SmokeEffect();

            case StatusEffectType.Misfortune:
                return new MisfortuneEffect();

            case StatusEffectType.Fog:
                return new FogEffect();

            case StatusEffectType.Restraint:
                return new RestraintEffect();

            case StatusEffectType.Intervention:
                return new InterventionEffect();

            default:
                Debug.LogWarning($"未実装の状態異常: {type}");
                return null;
        }
    }
}
