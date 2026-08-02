using System.Collections.Generic;

/// <summary>
/// Summon turn-end passive effect kinds (Garuda draw / Indra hand destroy).
/// </summary>
public enum SummonTurnEndEffectKind : byte
{
    GarudaDraw = 1,
    IndraHandDestroy = 2,
    IndraNoTarget = 3,
}

/// <summary>
/// One summon turn-end effect for presentation and online sync.
/// </summary>
public struct SummonTurnEndEffectEntry
{
    public SummonTurnEndEffectKind Kind;
    /// <summary>Summon owner is the network host's local player side.</summary>
    public bool OwnerIsHostPlayer;
    public string CardName;
    public int VictimHandIndex;
    public List<string> DrawnCardNames;
}
