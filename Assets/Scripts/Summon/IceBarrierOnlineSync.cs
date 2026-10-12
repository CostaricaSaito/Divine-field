using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Online presentation timing for Shiva ice barrier hand transform (host sends, client waits).
/// Draw RNG stays on <see cref="BattleRandom"/>; payload carries template names for presentation.
/// </summary>
public static class IceBarrierOnlineSync
{
    private static int _nextSequenceId;

    public static int AllocateSequenceId() => ++_nextSequenceId;

    public static async Task CoordinateDrawPresentationAsync(
        bool drawerIsHostPlayer,
        bool transformed,
        string originalTemplateAssetName,
        CancellationToken ct)
    {
        var bm = BattleManager.I;
        if (bm == null || !bm.IsOnlineMatch) return;

        int seq = AllocateSequenceId();
        var payload = new NetworkBattleBridge.ShivaIceBarrierDrawEffectSync
        {
            SequenceId = seq,
            DrawerIsHostPlayer = drawerIsHostPlayer,
            Transformed = transformed,
            OriginalTemplateAssetName = originalTemplateAssetName ?? string.Empty,
            FinalTemplateAssetName = transformed ? IceBarrierTransformRegistry.FrozenTemplateAssetName : string.Empty,
        };

        if (OnlineMatchContext.IsHost)
        {
            NetworkBattleBridge.SendShivaIceBarrierDrawEffect(payload);
            return;
        }

        await NetworkBattleBridge.WaitForShivaIceBarrierDrawEffectAsync(seq, ct);
    }

    public static bool ResolveDrawerIsHostPlayer(PlayerType drawerSide)
    {
        if (!OnlineMatchContext.IsOnline) return drawerSide == PlayerType.Player;
        bool localIsHost = OnlineMatchContext.IsHost;
        if (drawerSide == PlayerType.Player)
            return localIsHost;
        return !localIsHost;
    }
}
