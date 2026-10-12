using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only: maps a FROZEN hand instance to the template asset name drawn before ice barrier transform.
/// </summary>
public static class IceBarrierTransformRegistry
{
    private static readonly Dictionary<int, string> OriginalAssetByInstanceId = new();

    public const string FrozenTemplateAssetName = "FROZEN";

    public static void Register(CardData frozenInstance, string originalTemplateAssetName)
    {
        if (frozenInstance == null || string.IsNullOrEmpty(originalTemplateAssetName)) return;
        OriginalAssetByInstanceId[frozenInstance.GetInstanceID()] = originalTemplateAssetName;
    }

    public static bool TryGetOriginalTemplateAssetName(CardData card, out string originalAssetName)
    {
        originalAssetName = null;
        if (card == null) return false;
        return OriginalAssetByInstanceId.TryGetValue(card.GetInstanceID(), out originalAssetName)
               && !string.IsNullOrEmpty(originalAssetName);
    }

    public static void Clear(CardData card)
    {
        if (card == null) return;
        OriginalAssetByInstanceId.Remove(card.GetInstanceID());
    }

    public static void ClearAll() => OriginalAssetByInstanceId.Clear();
}
