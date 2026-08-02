using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大魔法（ArchMagic）の判定・パラメータ参照ユーティリティ。
/// 仕様: 単独使用・他カード併用不可・反射／無効化不可・詠唱中は HP バリアが 0 以下でキャンセル。
/// </summary>
public static class ArchMagicRules
{
    public static bool IsArchMagicCard(CardData c)
    {
        if (c == null) return false;
        if (c.cardType == CardType.ArchMagic) return true;
        return c.specialAttackRule is ArchMagicRuleSO;
    }

    public static bool ContainsArchMagic(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsArchMagicCard(cards[i])) return true;
        }
        return false;
    }

    /// <summary>コンボ中の大魔法カードを返す。無ければ null。</summary>
    public static CardData FindArchMagic(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return null;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsArchMagicCard(cards[i])) return cards[i];
        }
        return null;
    }

    public static ArchMagicRuleSO GetRule(CardData c)
    {
        if (c == null) return null;
        return c.specialAttackRule as ArchMagicRuleSO;
    }

    public static int GetCastTurns(CardData c)
    {
        var rule = GetRule(c);
        return rule != null ? Mathf.Max(1, rule.castTurns) : 2;
    }

    public static int GetBarrierHp(CardData c)
    {
        var rule = GetRule(c);
        return rule != null ? Mathf.Max(1, rule.barrierHp) : 30;
    }

    public static Sprite GetBackgroundSprite(CardData c)
    {
        var rule = GetRule(c);
        return rule != null ? rule.backgroundSprite : null;
    }

    public static string GetReleaseDisplayName(CardData c)
    {
        var rule = GetRule(c);
        if (rule != null && !string.IsNullOrEmpty(rule.displayNameForRelease))
            return rule.displayNameForRelease;
        return c != null ? c.cardName : "";
    }

    /// <summary>Resources/Cards から表示名または asset 名で大魔法テンプレートを探す（オンライン同期用）。</summary>
    public static CardData FindTemplateByDisplayOrAssetName(string displayOrAssetName)
    {
        if (string.IsNullOrEmpty(displayOrAssetName)) return null;
        var loaded = Resources.LoadAll<CardData>("Cards");
        if (loaded == null) return null;
        for (int i = 0; i < loaded.Length; i++)
        {
            var c = loaded[i];
            if (c == null || !IsArchMagicCard(c)) continue;
            if (c.cardName == displayOrAssetName || c.name == displayOrAssetName)
                return c;
        }
        return null;
    }

    public static bool NamesMatch(CardData a, CardData b)
    {
        if (a == null || b == null) return false;
        if (!string.IsNullOrEmpty(a.cardName) && a.cardName == b.cardName) return true;
        return a.name == b.name;
    }
}
