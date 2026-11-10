using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// <see cref="CardType.Special"/> 用：カードごとに ScriptableObject を割り当て、効果解決時の処理を記述する。
/// </summary>
public abstract class SpecialCardEffectSO : ScriptableObject
{
    /// <summary>
    /// Self-target play skips defense and calls this immediately.
    /// Opponent-targeted Specials call this after combat lands (yurusu / established incoming).
    /// </summary>
    public abstract Task ResolveOnImmediatePlayAsync(
        CardData card,
        PlayerStatus user,
        PlayerStatus effectTarget,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken);
}
