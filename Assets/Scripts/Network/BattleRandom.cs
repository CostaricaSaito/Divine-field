using UnityEngine;

/// <summary>
/// Deterministic RNG service for battle logic.
/// In online matches both clients are seeded with the same value negotiated
/// during the matchmaking handshake, so combat rolls (hit, status, confusion,
/// intervention, ...) and card draws stay in sync across the two simulations.
/// Offline it falls back to UnityEngine.Random so CPU battles are unchanged.
/// </summary>
public static class BattleRandom
{
    // Shared stream: combat rolls that both simulations execute in the same order.
    static System.Random _shared;
    // Per-side streams for card draws. Keyed by network identity (host / client)
    // so that "my hand" on one machine and "opponent hand" on the other machine
    // consume the same stream and produce identical cards.
    static System.Random _hostDraw;
    static System.Random _clientDraw;
    static bool _localIsHost;

    /// <summary>True while an online (seeded) session is active.</summary>
    public static bool IsDeterministic => _shared != null;

    public static void InitOnline(int seed, bool localIsHost)
    {
        _shared = new System.Random(seed);
        _hostDraw = new System.Random(seed ^ 0x5DEECE6D);
        _clientDraw = new System.Random(seed ^ 0x2545F491);
        _localIsHost = localIsHost;
        Debug.Log($"[BattleRandom] Online deterministic RNG initialized (seed={seed}, localIsHost={localIsHost})");
    }

    public static void ClearOnline()
    {
        _shared = null;
        _hostDraw = null;
        _clientDraw = null;
    }

    /// <summary>Shared combat roll. [minInclusive, maxExclusive)</summary>
    public static int Range(int minInclusive, int maxExclusive)
    {
        if (_shared != null) return _shared.Next(minInclusive, maxExclusive);
        return Random.Range(minInclusive, maxExclusive);
    }

    /// <summary>Shared combat roll. [0, 1)</summary>
    public static float Value
    {
        get
        {
            if (_shared != null) return (float)_shared.NextDouble();
            return Random.value;
        }
    }

    /// <summary>
    /// Card draw roll for the given side (local perspective).
    /// PlayerType.Player = the local player's hand, PlayerType.Enemy = the opponent's hand.
    /// </summary>
    public static int DrawRange(PlayerType localSide, int minInclusive, int maxExclusive)
    {
        if (_shared == null) return Random.Range(minInclusive, maxExclusive);
        bool sideIsHost = (localSide == PlayerType.Player) == _localIsHost;
        var rng = sideIsHost ? _hostDraw : _clientDraw;
        return rng.Next(minInclusive, maxExclusive);
    }
}
