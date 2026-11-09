using System.Collections.Generic;

/// <summary>防御カードが攻撃に対してどの解決ルートになるか。</summary>
public enum ReactiveDefenseKind
{
    /// <summary>通常防御（防御力で受ける）または防御なし。</summary>
    Normal = 0,
    /// <summary>反射（物理／魔法／完全）。攻撃は防御側から攻撃側へ跳ね返る。</summary>
    Reflect = 1,
    /// <summary>打ち払い。50% で跳ね返り、失敗時は防御側が選び直す。</summary>
    Parry = 2,
    /// <summary>無効化（ブロッキング・避雷針）。ダメージなしで終了。</summary>
    Nullify = 3,
}

/// <summary>
/// 反射・打ち払い・無効化の判定を1か所に集約する。
/// いずれも「戦闘用防御カードがちょうど1枚」のときだけ成立する（触媒は除外して数える）。
/// 優先順位: 反射 → 打ち払い → 無効化。
/// </summary>
public static class ReactiveDefenseRules
{
    public static ReactiveDefenseKind Classify(
        IReadOnlyList<CardData> defenseCards,
        IReadOnlyList<CardData> attackCards)
    {
        if (defenseCards == null || attackCards == null || attackCards.Count == 0)
            return ReactiveDefenseKind.Normal;

        var combat = FreeMagicCastRules.ExcludeCatalystsFromCombatStats(new List<CardData>(defenseCards));
        if (combat.Count != 1 || combat[0] == null)
            return ReactiveDefenseKind.Normal;

        var card = combat[0];
        if (ReflectionRules.CanReflectIncoming(card, attackCards)) return ReactiveDefenseKind.Reflect;
        if (ParryRules.CanParryIncoming(card, attackCards)) return ReactiveDefenseKind.Parry;
        if (BlockingRules.ResolvesAsBlockingNullify(card, attackCards)) return ReactiveDefenseKind.Nullify;
        return ReactiveDefenseKind.Normal;
    }

    /// <summary>反射または打ち払い（＝<see cref="ReactiveDefenseChain"/> で解決するルート）か。</summary>
    public static bool StartsChain(ReactiveDefenseKind kind)
        => kind == ReactiveDefenseKind.Reflect || kind == ReactiveDefenseKind.Parry;
}
