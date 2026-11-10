using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Legacy entry point for Indra hand destroy. Prefer <see cref="CardDestroyPresentation"/>.
/// </summary>
public static class HandDestroyService
{
    public const string DestroyMessage = CardDestroyPresentation.Presets.IndraHandDestroy.DestroyMessage;
    public const string NoTargetMessage = CardDestroyPresentation.Presets.IndraHandDestroy.NoTargetMessage;
    public const string SeAddress = CardDestroyPresentation.Presets.IndraHandDestroy.SoundEffectPath;

    public static Task PlayDestroySequenceAsync(
        BattleManager bm,
        PlayerStatus blessingOwner,
        PlayerStatus victim,
        List<CardData> victimHand,
        bool victimIsPlayerHand,
        CardData targetCard,
        bool noTarget,
        CancellationToken ct)
        => CardDestroyPresentation.PlayIndraHandDestroyAsync(
            bm, blessingOwner, victim, victimHand, victimIsPlayerHand, targetCard, noTarget, ct);

    public static CardData ResolveTargetCard(List<CardData> victimHand, string cardName, int handIndex)
        => CardDestroyPresentation.ResolveTargetCard(victimHand, cardName, handIndex);
}
