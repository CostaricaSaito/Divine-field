using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PlaceholderDisasterEffect",
    menuName = "DivineField/Disaster Effects/Placeholder")]
public sealed class PlaceholderDisasterEffectSO : DisasterCardEffectSO
{
    public void ConfigureForKind(DisasterKind runtimeKind)
    {
        ConfigureForRuntime(
            runtimeKind,
            DisasterCatalog.GetNotificationMessage(runtimeKind),
            DisasterCatalog.GetDefaultMessagePopupKind(runtimeKind));
    }

    public override Task ResolveAsync(DisasterResolveContext context, CancellationToken cancellationToken)
    {
        Debug.Log($"[Disaster] Placeholder effect for {Kind} (not implemented yet).");
        return Task.CompletedTask;
    }
}
