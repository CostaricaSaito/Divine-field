using System.Collections.Generic;
using UnityEngine;

public readonly struct EnemyCpuDefensePick
{
    public readonly CardData Primary;
    public readonly List<CardData> Selection;

    public EnemyCpuDefensePick(CardData primary, List<CardData> selection)
    {
        Primary = primary;
        Selection = selection;
    }

    public static EnemyCpuDefensePick None => default;

    public static EnemyCpuDefensePick Of(CardData card)
    {
        if (card == null) return None;
        return new EnemyCpuDefensePick(card, new List<CardData> { card });
    }

    public static EnemyCpuDefensePick WithCatalyst(CardData card, CardData catalyst)
    {
        if (card == null) return None;
        if (catalyst == null) return Of(card);
        return new EnemyCpuDefensePick(card, new List<CardData> { card, catalyst });
    }
}

/// <summary>Local CPU defense-phase card choice. Timing and LastDefenseSelection stay on EnemyAI.</summary>
public static class EnemyCpuDefensePlanner
{
    public static EnemyCpuDefensePick Pick(
        List<CardData> cpuHand,
        ElementType attackElement,
        List<CardData> incoming,
        PlayerStatus enemyStatus)
    {
        if (incoming != null && CardRules.IsStatusConduitMagicAttackCombo(incoming))
        {
            if (cpuHand != null)
            {
                var conduitChoices = CardRules.GetStatusConduitMagicDefenseChoices(cpuHand, incoming);
                var conduit = PickConduitReactiveDefense(conduitChoices, incoming, enemyStatus, cpuHand);
                if (conduit.Primary != null)
                    return conduit;
            }

            Debug.Log("[EnemyAI] 状態異常通過魔法：許す");
            return EnemyCpuDefensePick.None;
        }

        if (incoming != null && CardRules.IncomingRequiresFullOnlyReactiveDefense(incoming))
        {
            var fullOnly = FindFullReflection(cpuHand, incoming);
            if (fullOnly != null)
            {
                Debug.Log($"[EnemyAI] 完全反射を優先: {fullOnly.cardName}");
                return EnemyCpuDefensePick.Of(fullOnly);
            }

            Debug.Log("[EnemyAI] 即時回復／即時効果系：許す");
            return EnemyCpuDefensePick.None;
        }

        CardData defenseCard = null;
        EnemyCpuDefensePick blockingPick = EnemyCpuDefensePick.None;

        if (incoming != null && incoming.Count > 0 && cpuHand != null)
        {
            if (ReflectionRules.CanReflectPhysical(incoming))
            {
                defenseCard = FindPhysicalOrFullReflection(cpuHand, incoming);
            }
            else if (ReflectionRules.CanReflectMagic(incoming))
            {
                defenseCard = FindMagicOrFullReflection(cpuHand, incoming);
            }
            else
            {
                defenseCard = FindFullReflection(cpuHand, incoming);
                if (defenseCard != null)
                    Debug.Log($"[EnemyAI] 完全反射を優先: {defenseCard.cardName}");
            }

            if (defenseCard == null)
            {
                foreach (var c in cpuHand)
                {
                    if (!EnemyAI.IsEnemyHandCardSelectable(c)) continue;
                    if (ParryRules.RequiresParryExclusiveLock(c, incoming))
                    {
                        defenseCard = c;
                        Debug.Log($"[EnemyAI] 打ち払いを優先: {defenseCard.cardName}");
                        break;
                    }
                }
            }

            if (defenseCard == null && BlockingRules.CanBlockPhysical(incoming))
            {
                blockingPick = FindPhysicalBlocking(cpuHand, incoming, enemyStatus);
                defenseCard = blockingPick.Primary;
            }

            if (defenseCard == null)
            {
                foreach (var c in cpuHand)
                {
                    if (!EnemyAI.IsEnemyHandCardSelectable(c)) continue;
                    if (!LightningRodRules.CanNullifyIncoming(c, incoming)) continue;
                    defenseCard = c;
                    Debug.Log($"[EnemyAI] 避雷針で雷属性を無効化: {defenseCard.cardName}");
                    break;
                }
            }
        }

        if (defenseCard == null)
            defenseCard = SelectNormalArmor(cpuHand, attackElement, incoming);

        if (defenseCard == null && incoming != null
            && ShiningBarrierRules.CanUseAgainstIncoming(incoming))
        {
            defenseCard = FindShiningBarrierCard(cpuHand);
            if (defenseCard != null)
                Debug.Log($"[EnemyAI] 光のバリアを使用: {defenseCard.cardName}");
        }

        if (defenseCard != null)
        {
            Debug.Log($"[EnemyAI] 防御カード選択完了: {defenseCard.cardName}");
            if (blockingPick.Primary != null
                && ReferenceEquals(blockingPick.Primary, defenseCard)
                && blockingPick.Selection != null
                && blockingPick.Selection.Count > 0)
                return blockingPick;

            return EnemyCpuDefensePick.Of(defenseCard);
        }

        Debug.Log("[EnemyAI] 有効な防御カードがないため、許す");
        return EnemyCpuDefensePick.None;
    }

    public static EnemyCpuDefensePick PickParryRerun(
        List<CardData> cpuHand,
        ElementType attackElement,
        List<CardData> incomingAttack,
        CardData usedParryCard)
    {
        CardData normal = SelectNormalArmor(cpuHand, attackElement, incomingAttack, usedParryCard);
        if (normal != null)
            return EnemyCpuDefensePick.Of(normal);

        if (incomingAttack != null && incomingAttack.Count > 0 && cpuHand != null)
        {
            int usedId = usedParryCard != null ? usedParryCard.GetInstanceID() : 0;
            foreach (var c in cpuHand)
            {
                if (!EnemyAI.IsEnemyHandCardSelectable(c)) continue;
                if (usedParryCard != null && c.GetInstanceID() == usedId) continue;
                if (ParryRules.RequiresParryExclusiveLock(c, incomingAttack))
                {
                    Debug.Log($"[EnemyAI] Parry rerun: another parry card {c.cardName}");
                    return EnemyCpuDefensePick.Of(c);
                }
            }
        }

        Debug.Log("[EnemyAI] Parry rerun: no defense, accept damage");
        return EnemyCpuDefensePick.None;
    }

    public static CardData SelectNormalArmor(
        List<CardData> enemyHand,
        ElementType attackElement,
        IReadOnlyList<CardData> attackCards = null,
        CardData excludeInstance = null)
    {
        if (enemyHand == null || enemyHand.Count == 0) return null;

        var choices = attackCards != null && attackCards.Count > 0
            ? CardRules.GetDefenseChoicesForIncoming(enemyHand, attackCards)
            : CardRules.GetDefenseChoicesAgainstAttack(enemyHand, attackElement, attackCards);
        if (choices == null || choices.Count == 0)
            return null;

        choices.RemoveAll(c => !EnemyAI.IsEnemyHandCardSelectable(c));
        if (excludeInstance != null)
        {
            int excludeId = excludeInstance.GetInstanceID();
            choices.RemoveAll(c => c != null && c.GetInstanceID() == excludeId);
        }

        choices.RemoveAll(c =>
            c != null
            && BlockingRules.IsPhysicalBlockingCard(c)
            && !CardRules.CanServeAsNormalArmorDefense(c));
        choices.RemoveAll(c =>
            c != null
            && ParryRules.IsParryCard(c)
            && !CardRules.CanServeAsNormalArmorDefense(c));
        if (choices.Count == 0)
            return null;

        PlayerStatus defender = BattleManager.I?.GetEnemyStatus();
        CardData best = null;
        int bestEffectiveDef = -1;

        void Consider(CardData c)
        {
            if (c == null) return;
            int eff = CombatDefensePowerResolver.ResolveEffectiveDefensePower(
                new List<CardData> { c }, defender, attackElement);
            if (eff > bestEffectiveDef)
            {
                bestEffectiveDef = eff;
                best = c;
            }
        }

        foreach (var c in choices)
        {
            if (c != null && CardRules.IsNormalPhysicalDefenseCard(c))
                Consider(c);
        }

        if (best != null) return best;

        foreach (var c in choices)
        {
            if (c != null && CardRules.IsPrimaryDefenseCard(c))
                Consider(c);
        }

        if (best != null) return best;

        foreach (var c in choices)
            Consider(c);

        return best;
    }

    private static EnemyCpuDefensePick PickConduitReactiveDefense(
        List<CardData> conduitChoices,
        IReadOnlyList<CardData> incoming,
        PlayerStatus enemyStatus,
        List<CardData> cpuHand)
    {
        if (conduitChoices == null || conduitChoices.Count == 0 || incoming == null)
            return EnemyCpuDefensePick.None;

        foreach (var c in conduitChoices)
        {
            if (!EnemyAI.IsEnemyHandCardSelectable(c)) continue;
            if (ReflectionRules.IsFullReflectionCard(c)
                && ReflectionRules.CanReflectIncoming(c, incoming))
            {
                Debug.Log($"[EnemyAI] 状態異常通過：完全反射 {c.cardName}");
                return EnemyCpuDefensePick.Of(c);
            }
        }

        foreach (var c in conduitChoices)
        {
            if (!EnemyAI.IsEnemyHandCardSelectable(c)) continue;
            if (ReflectionRules.IsMagicReflectionCard(c)
                && ReflectionRules.CanReflectIncoming(c, incoming))
            {
                Debug.Log($"[EnemyAI] 状態異常通過：魔法反射 {c.cardName}");
                return EnemyCpuDefensePick.Of(c);
            }
        }

        foreach (var c in conduitChoices)
        {
            if (!EnemyAI.IsEnemyHandCardSelectable(c)) continue;
            if (ParryRules.CanParryIncoming(c, incoming))
            {
                Debug.Log($"[EnemyAI] 状態異常通過：打ち払い {c.cardName}");
                return EnemyCpuDefensePick.Of(c);
            }
        }

        foreach (var c in conduitChoices)
        {
            if (!EnemyAI.IsEnemyHandCardSelectable(c)) continue;
            if (!BlockingRules.ResolvesAsBlockingNullify(c, incoming)) continue;
            bool afford = BlockingRules.CanAffordMagicDefenseMp(c, enemyStatus);
            CardData catalyst = afford ? null : FreeMagicCastRules.FindAvailableCatalyst(cpuHand);
            if (!afford && (catalyst == null || enemyStatus == null || enemyStatus.IsMagicUseForbidden()))
                continue;
            Debug.Log($"[EnemyAI] 状態異常通過：無効化 {c.cardName}");
            return EnemyCpuDefensePick.WithCatalyst(c, catalyst);
        }

        return EnemyCpuDefensePick.None;
    }

    private static CardData FindFullReflection(List<CardData> cpuHand, IReadOnlyList<CardData> incoming)
    {
        if (cpuHand == null) return null;
        foreach (var c in cpuHand)
        {
            if (!EnemyAI.IsEnemyHandCardSelectable(c)) continue;
            if (ReflectionRules.IsFullReflectionCard(c)
                && ReflectionRules.CanReflectIncoming(c, incoming))
                return c;
        }
        return null;
    }

    private static CardData FindPhysicalOrFullReflection(List<CardData> cpuHand, IReadOnlyList<CardData> incoming)
    {
        foreach (var c in cpuHand)
        {
            if (!EnemyAI.IsEnemyHandCardSelectable(c)) continue;
            if (ReflectionRules.IsFullReflectionCard(c)
                && ReflectionRules.CanReflectIncoming(c, incoming))
            {
                Debug.Log($"[EnemyAI] 完全反射を優先: {c.cardName}");
                return c;
            }
            if (ReflectionRules.IsPhysicalReflectionCard(c))
            {
                Debug.Log($"[EnemyAI] 物理反射を優先: {c.cardName}");
                return c;
            }
        }
        return null;
    }

    private static CardData FindMagicOrFullReflection(List<CardData> cpuHand, IReadOnlyList<CardData> incoming)
    {
        foreach (var c in cpuHand)
        {
            if (!EnemyAI.IsEnemyHandCardSelectable(c)) continue;
            if (ReflectionRules.IsFullReflectionCard(c)
                && ReflectionRules.CanReflectIncoming(c, incoming))
            {
                Debug.Log($"[EnemyAI] 完全反射を優先: {c.cardName}");
                return c;
            }
            if (ReflectionRules.IsMagicReflectionCard(c))
            {
                Debug.Log($"[EnemyAI] 魔法反射を優先: {c.cardName}");
                return c;
            }
        }
        return null;
    }

    private static EnemyCpuDefensePick FindPhysicalBlocking(
        List<CardData> cpuHand,
        IReadOnlyList<CardData> incoming,
        PlayerStatus enemyStatus)
    {
        foreach (var c in cpuHand)
        {
            if (!EnemyAI.IsEnemyHandCardSelectable(c)) continue;
            if (!BlockingRules.CanUsePhysicalBlockingAgainstAttack(c, incoming))
                continue;
            bool afford = BlockingRules.CanAffordMagicDefenseMp(c, enemyStatus);
            CardData catalyst = afford ? null : FreeMagicCastRules.FindAvailableCatalyst(cpuHand);
            if (!afford && (catalyst == null || enemyStatus == null || enemyStatus.IsMagicUseForbidden()))
                continue;
            Debug.Log($"[EnemyAI] 物理無効を優先: {c.cardName}");
            return EnemyCpuDefensePick.WithCatalyst(c, catalyst);
        }
        return EnemyCpuDefensePick.None;
    }

    private static CardData FindShiningBarrierCard(List<CardData> hand)
    {
        if (hand == null) return null;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (c != null && ShiningBarrierRules.IsShiningBarrierCard(c))
                return c;
        }
        return null;
    }
}
