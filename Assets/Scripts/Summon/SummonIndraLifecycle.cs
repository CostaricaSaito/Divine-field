using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Indra passive: every 5 own turn ends, destroy one random opponent hand card (see <see cref="SummonTurnEndLifecycle"/>).
/// </summary>
public static class SummonIndraLifecycle
{
    public static bool IsIndraLifecycle(SummonData data)
    {
        if (data == null) return false;
        return data.IsIndraLifecycle();
    }

    public static async Task RunHandDestroySequenceAsync(
        BattleManager bm,
        PlayerStatus blessingOwner,
        PlayerStatus victim,
        System.Collections.Generic.List<CardData> victimHand,
        bool victimIsPlayerHand,
        CardData targetCard,
        bool noTarget,
        CancellationToken ct)
    {
        await CardDestroyPresentation.PlayIndraHandDestroyAsync(
            bm, blessingOwner, victim, victimHand, victimIsPlayerHand, targetCard, noTarget, ct);
    }
}
