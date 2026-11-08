using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Defense card included in a resolved defense combo: runs after combat damage (same gate as Cyber Exoskeleton).
/// Assign via <see cref="CardData.specialCardEffect"/>.
/// </summary>
public abstract class DefenseIncludedPostDamageEffectSO : SpecialCardEffectSO
{
    public override Task ResolveOnImmediatePlayAsync(
        CardData card,
        PlayerStatus user,
        PlayerStatus effectTarget,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public abstract Task RunAfterCombatDamageIncludedAsync(
        DefenseIncludedPostDamageContext context,
        CardData triggeringCard,
        CancellationToken cancellationToken);
}

/// <summary>Payload for defense-included post-damage effects.</summary>
public sealed class DefenseIncludedPostDamageContext
{
    public BattleProcessor BattleProcessor;
    public PlayerStatus Attacker;
    public PlayerStatus Defender;
    public IReadOnlyList<CardData> DefenseCards;
}
