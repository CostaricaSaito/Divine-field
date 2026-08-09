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

        if (requested == StatusEffectType.Freeze)
            return ApplyFreezeReplace(target, config.defaultDebugFreezeDurationTurns);

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

    /// <summary>Applies or extends freeze. Shiva passive uses stackExisting=true.</summary>
    public static ProgressiveApplyResult ApplyFreeze(PlayerStatus target, int durationTurns, bool stackExisting)
    {
        if (target == null) return ProgressiveApplyResult.NoChange;

        int dur = Mathf.Max(1, durationTurns);
        var existing = target.GetFreezeEffect();
        if (existing != null && stackExisting)
        {
            existing.AddTurns(dur);
            Debug.Log($"{target.DisplayName} freeze extended (+{dur}, total {existing.TurnsRemaining} turn(s))");
            return ProgressiveApplyResult.Applied;
        }

        return ApplyFreezeReplace(target, dur);
    }

    private static ProgressiveApplyResult ApplyFreezeReplace(PlayerStatus target, int durationTurns)
    {
        target.activeEffects.RemoveAll(e => e != null && e.EffectType == StatusEffectType.Freeze);
        target.activeEffects.Add(new FreezeEffect(Mathf.Max(1, durationTurns)));
        Debug.Log($"{target.DisplayName} freeze applied ({durationTurns} turn(s))");
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

    /// <summary>
    /// 天変地異「感染症」専用：病系段階を問わず指定段階へ上書き（通常の進行・楽園病強制絶頂ルールは適用しない）。
    /// </summary>
    public static bool ForceSetDiseaseStage(PlayerStatus target, StatusEffectType stage)
    {
        if (target == null || !DiseaseLineEffect.IsDiseaseFamily(stage))
            return false;

        SetDiseaseStage(target, stage);
        Debug.Log($"[ProgressiveStatusApplicator] {target.DisplayName} disease forced to {stage} (disaster)");
        return true;
    }

    /// <summary>病系4段階の「強さ」順。列挙値 1〜4 がそのまま順位になる。</summary>
    private static int DiseaseStageRank(StatusEffectType t)
    {
        if (!DiseaseLineEffect.IsDiseaseFamily(t)) return 0;
        return (int)t;
    }

    private static ProgressiveApplyResult ApplyDiseaseFamily(PlayerStatus target, StatusEffectType requested, StatusProgressionConfig config)
    {
        StatusEffectType cur = FindDiseaseStage(target);

        if (cur == StatusEffectType.ParadiseSickness
            && config.paradisePlusSicknessForcesEcstasy)
        {
            // 楽園病中のいかなる病系付与も「進行」扱いで強制絶頂（病・重病・煉獄・楽園・ランダムで病が当たった場合を含む）
            return ProgressiveApplyResult.ForcedParadiseEcstasy;
        }

        if (cur == StatusEffectType.None)
        {
            SetDiseaseStage(target, requested);
            return ProgressiveApplyResult.Applied;
        }

        int rCur = DiseaseStageRank(cur);
        int rReq = DiseaseStageRank(requested);
        if (rCur <= 0 || rReq <= 0)
            return ProgressiveApplyResult.NoChange;

        // 付与が現在より進んだ段階なら、その段階へ置き換え
        if (rReq > rCur)
        {
            SetDiseaseStage(target, requested);
            return ProgressiveApplyResult.Applied;
        }

        // 同じかより低い段階の付与 → 現在段階から1段階だけ進行（病・重病・煉獄を付与するカード・攻撃はいずれも同じルール）
        StatusEffectType next = DiseaseLineEffect.GetNextStage(cur);
        if (next == StatusEffectType.None)
            return ProgressiveApplyResult.NoChange;

        SetDiseaseStage(target, next);
        return ProgressiveApplyResult.DiseaseProgressed;
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
