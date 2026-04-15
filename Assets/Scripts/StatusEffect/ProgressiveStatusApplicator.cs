using UnityEngine;

/// <summary>
/// 段階型・排他型の状態異常を <see cref="PlayerStatus"/> に付与する（単純な重複チェックはここに含めない）。
/// </summary>
public enum ProgressiveApplyResult
{
    /// <summary>付与・置換が行われた</summary>
    Applied,
    /// <summary>病系が「病」付与で段階が進んだ</summary>
    DiseaseProgressed,
    /// <summary>楽園病＋病：呼び出し側で強制絶頂演出を実行すること</summary>
    ForcedParadiseEcstasy,
    /// <summary>既に同じ段階・付与不要など</summary>
    NoChange,
}

public static class ProgressiveStatusApplicator
{
    public static ProgressiveApplyResult Apply(PlayerStatus target, StatusEffectType requested, StatusProgressionConfig config)
    {
        if (target == null || requested == StatusEffectType.None)
            return ProgressiveApplyResult.NoChange;

        config ??= StatusProgressionConfig.GetRuntimeFallback();

        if (requested == StatusEffectType.Seal)
            return ApplySeal(target, config);

        if (DiseaseLineEffect.IsDiseaseFamily(requested))
            return ApplyDiseaseFamily(target, requested, config);

        if (requested == StatusEffectType.EyeStrain || requested == StatusEffectType.ClusterHeadache)
            return ApplyEyeCluster(target, requested, config);

        return ProgressiveApplyResult.NoChange;
    }

    /// <summary>単純付与（衰弱など）用。既存の重複チェック付き。</summary>
    public static bool TryAddSimpleEffect(PlayerStatus target, StatusEffectType type, StatusProgressionConfig config)
    {
        if (target == null || type == StatusEffectType.None) return false;
        config ??= StatusProgressionConfig.GetRuntimeFallback();

        foreach (var e in target.activeEffects)
        {
            if (e != null && e.EffectType == type)
                return false;
        }

        var created = StatusEffectFactory.Create(type, config);
        if (created == null) return false;
        target.activeEffects.Add(created);
        Debug.Log($"{target.DisplayName} に状態異常 {created.GetEffectName()} を付与しました");
        return true;
    }

    private static ProgressiveApplyResult ApplySeal(PlayerStatus target, StatusProgressionConfig config)
    {
        target.activeEffects.RemoveAll(e => e != null && e.EffectType == StatusEffectType.Seal);
        var seal = new SealEffect(config.defaultSealDurationTurns);
        target.activeEffects.Add(seal);
        Debug.Log($"{target.DisplayName} に封印（{config.defaultSealDurationTurns}ターン）を付与しました");
        return ProgressiveApplyResult.Applied;
    }

    private static StatusEffectType FindDiseaseStage(PlayerStatus status)
    {
        foreach (var e in status.activeEffects)
        {
            if (e == null) continue;
            if (DiseaseLineEffect.IsDiseaseFamily(e.EffectType))
                return e.EffectType;
        }
        return StatusEffectType.None;
    }

    private static void SetDiseaseStage(PlayerStatus status, StatusEffectType stage)
    {
        status.activeEffects.RemoveAll(e => e != null && DiseaseLineEffect.IsDiseaseFamily(e.EffectType));
        status.activeEffects.Add(new DiseaseLineEffect(stage));
    }

    private static ProgressiveApplyResult ApplyDiseaseFamily(PlayerStatus target, StatusEffectType requested, StatusProgressionConfig config)
    {
        StatusEffectType cur = FindDiseaseStage(target);

        if (cur == StatusEffectType.ParadiseSickness
            && requested == StatusEffectType.Sickness
            && config.paradisePlusSicknessForcesEcstasy)
        {
            return ProgressiveApplyResult.ForcedParadiseEcstasy;
        }

        if (cur == StatusEffectType.None)
        {
            SetDiseaseStage(target, requested);
            return ProgressiveApplyResult.Applied;
        }

        if (requested == StatusEffectType.Sickness)
        {
            StatusEffectType next = DiseaseLineEffect.GetNextStage(cur);
            if (next == StatusEffectType.None)
                return ProgressiveApplyResult.NoChange;

            if (next == cur)
                return ProgressiveApplyResult.NoChange;

            SetDiseaseStage(target, next);
            return ProgressiveApplyResult.DiseaseProgressed;
        }

        if (requested == cur)
            return ProgressiveApplyResult.NoChange;

        SetDiseaseStage(target, requested);
        return ProgressiveApplyResult.Applied;
    }

    private static bool HasEffect(PlayerStatus status, StatusEffectType t)
    {
        foreach (var e in status.activeEffects)
            if (e != null && e.EffectType == t) return true;
        return false;
    }

    private static void RemoveEffectType(PlayerStatus status, StatusEffectType t)
    {
        status.activeEffects.RemoveAll(e => e != null && e.EffectType == t);
    }

    private static ProgressiveApplyResult ApplyEyeCluster(PlayerStatus target, StatusEffectType requested, StatusProgressionConfig config)
    {
        bool hadEye = HasEffect(target, StatusEffectType.EyeStrain);
        bool hadCluster = HasEffect(target, StatusEffectType.ClusterHeadache);

        if (requested == StatusEffectType.EyeStrain)
        {
            if (hadCluster && config.eyeClusterMutuallyExclusive)
            {
                RemoveEffectType(target, StatusEffectType.ClusterHeadache);
                target.activeEffects.Add(new EyeStrainEffect());
                return ProgressiveApplyResult.Applied;
            }
            if (hadEye && config.eyeStrainDuplicateEscalatesToCluster)
            {
                RemoveEffectType(target, StatusEffectType.EyeStrain);
                target.activeEffects.Add(new ClusterHeadacheEffect());
                return ProgressiveApplyResult.Applied;
            }
            if (hadEye)
                return ProgressiveApplyResult.NoChange;

            target.activeEffects.Add(new EyeStrainEffect());
            return ProgressiveApplyResult.Applied;
        }

        if (hadEye && config.eyeClusterMutuallyExclusive)
        {
            RemoveEffectType(target, StatusEffectType.EyeStrain);
            target.activeEffects.Add(new ClusterHeadacheEffect());
            return ProgressiveApplyResult.Applied;
        }
        if (hadCluster)
            return ProgressiveApplyResult.NoChange;

        target.activeEffects.Add(new ClusterHeadacheEffect());
        return ProgressiveApplyResult.Applied;
    }
}
