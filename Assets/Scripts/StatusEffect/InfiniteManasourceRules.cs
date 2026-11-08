using UnityEngine;

/// <summary>
/// Grant Infinite Manasource and sync the magic pool (∞ uses, no MP).
/// </summary>
public static class InfiniteManasourceRules
{
    public static bool TryGrant(PlayerStatus target, bool suppressGrantPopupAndSound = true)
    {
        if (target == null) return false;

        var config = StatusProgressionConfig.GetRuntimeFallback();
        var (result, _) = target.TryApplyStatusEffect(
            StatusEffectType.InfiniteManasource,
            config,
            suppressGrantPopupAndSound: suppressGrantPopupAndSound);

        SyncMagicPoolForOwner(target);

        return result == ProgressiveApplyResult.Applied
               || target.HasInfiniteManasourceEffect();
    }

    public static void SyncMagicPoolForOwner(PlayerStatus target)
    {
        if (target == null) return;
        var bm = BattleManager.I;
        if (bm == null || !target.HasInfiniteManasourceEffect()) return;

        var owner = ReferenceEquals(target, bm.GetPlayerStatus())
            ? PlayerType.Player
            : PlayerType.Enemy;
        MagicPoolManager.I?.ApplyInfiniteManasourceToPool(owner);
    }
}
