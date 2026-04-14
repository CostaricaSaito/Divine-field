using UnityEngine;

/// <summary>
/// 病・重病・煉獄病・楽園病のいずれか1段階を表す状態異常（相互排他）。
/// ターン開始ではなく、攻撃フェーズ終了時に <see cref="DiseaseTurnEndProcessor"/> で処理する。
/// </summary>
public sealed class DiseaseLineEffect : IStatusEffect
{
    public StatusEffectType EffectType { get; }

    public DiseaseLineEffect(StatusEffectType stage)
    {
        if (!IsDiseaseFamily(stage))
            Debug.LogWarning($"[DiseaseLineEffect] 病系以外が指定されました: {stage}");
        EffectType = stage;
    }

    public static bool IsDiseaseFamily(StatusEffectType t)
    {
        return t == StatusEffectType.Sickness
            || t == StatusEffectType.SevereSickness
            || t == StatusEffectType.PurgatorySickness
            || t == StatusEffectType.ParadiseSickness;
    }

    /// <summary>煉獄病の次は楽園病。楽園病の次は None。</summary>
    public static StatusEffectType GetNextStage(StatusEffectType current)
    {
        switch (current)
        {
            case StatusEffectType.Sickness: return StatusEffectType.SevereSickness;
            case StatusEffectType.SevereSickness: return StatusEffectType.PurgatorySickness;
            case StatusEffectType.PurgatorySickness: return StatusEffectType.ParadiseSickness;
            default: return StatusEffectType.None;
        }
    }

    public void ApplyEffect(PlayerStatus target) { }

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target) { }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public bool IsExpired() => false;

    public string GetEffectName()
    {
        switch (EffectType)
        {
            case StatusEffectType.Sickness: return "病";
            case StatusEffectType.SevereSickness: return "重病";
            case StatusEffectType.PurgatorySickness: return "煉獄病";
            case StatusEffectType.ParadiseSickness: return "楽園病";
            default: return EffectType.ToString();
        }
    }

    public string GetDescription() => "";
}
