using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// <see cref="CardType.Special"/> 用：カードごとに ScriptableObject を割り当て、即時解決時の処理を記述する。
/// </summary>
public abstract class SpecialCardEffectSO : ScriptableObject
{
    /// <summary>攻撃フェーズで使用ボタン確定後、防御フェーズを挟まずに呼ばれる。</summary>
    public abstract Task ResolveOnImmediatePlayAsync(
        CardData card,
        PlayerStatus user,
        PlayerStatus effectTarget,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken);
}
