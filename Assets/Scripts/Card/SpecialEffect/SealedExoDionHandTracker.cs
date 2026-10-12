using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cards that entered a hand this battle turn and should move to the magic panel at EndPhase.
/// </summary>
public static class SealedExoDionHandTracker
{
    private static readonly HashSet<int> PendingInstanceIds = new();

    public static void RegisterAddedToHand(CardData card, CardDealer dealer)
    {
        if (!SealedExoDionRules.IsSealedExoDionHandInstance(card, dealer)) return;
        PendingInstanceIds.Add(card.GetInstanceID());
    }

    public static void TransferIceBarrierInstance(CardData fromInstance, CardData frozenInstance)
    {
        if (fromInstance == null || frozenInstance == null) return;
        int id = fromInstance.GetInstanceID();
        if (!PendingInstanceIds.Contains(id)) return;
        PendingInstanceIds.Remove(id);
        PendingInstanceIds.Add(frozenInstance.GetInstanceID());
    }

    public static bool IsPendingEndPhaseMove(CardData handInstance, CardDealer dealer)
    {
        if (handInstance == null) return false;
        if (!SealedExoDionRules.IsSealedExoDionHandInstance(handInstance, dealer)) return false;
        return PendingInstanceIds.Contains(handInstance.GetInstanceID());
    }

    public static void ConsumePending(CardData handInstance)
    {
        if (handInstance == null) return;
        PendingInstanceIds.Remove(handInstance.GetInstanceID());
    }

    public static void ClearBattle() => PendingInstanceIds.Clear();
}
