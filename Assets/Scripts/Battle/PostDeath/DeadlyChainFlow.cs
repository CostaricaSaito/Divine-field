using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 「道連れの鎖」：死亡者パネル掲出 → 防御 → 固定闇ダメ（反射・連鎖反射対応）。
/// </summary>
public static class DeadlyChainFlow
{
    public static async Task ResolveSingleChainAsync(
        CardData chainCard,
        PlayerStatus deadAttacker,
        PlayerStatus livingDefender,
        DeadlyChainPostDeathEffectSO effect,
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        EnemyAI enemyAI,
        CancellationToken cancellationToken)
    {
        if (chainCard == null || deadAttacker == null || livingDefender == null
            || effect == null || battleManager == null || battleProcessor == null)
            return;

        bool deadIsPlayer = ReferenceEquals(deadAttacker, battleManager.GetPlayerStatus());
        Side deadSide = deadIsPlayer ? Side.Player : Side.Enemy;
        var attackList = new List<CardData> { chainCard };

        using (PostDeathCombatContext.Begin(chainCard, effect.fixedAttackPower, effect.attackElement))
        {
            await PresentChainCardAsync(chainCard, deadSide, effect, battleManager, cancellationToken);

            await ResolveWithDefenseAndReflectionAsync(
                attackList,
                deadAttacker,
                livingDefender,
                effect,
                battleManager,
                battleProcessor,
                handRefill,
                enemyAI,
                cancellationToken);

            ConsumeChainFromHand(chainCard, deadIsPlayer, battleManager, battleProcessor, handRefill);
        }

        BattleUIManager.I?.HideAllCardDetails();
        battleManager.ClearPostDeathChainAttackDisplay();
        battleManager.ClearStatsDisplaySequenceCards();
        battleManager.EnterPostDeathChainNeutralPhase();
        BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
    }

    private static async Task PresentChainCardAsync(
        CardData chainCard,
        Side deadSide,
        DeadlyChainPostDeathEffectSO effect,
        BattleManager battleManager,
        CancellationToken cancellationToken)
    {
        PlayerType deadAttackerSide = deadSide == Side.Player ? PlayerType.Player : PlayerType.Enemy;

        battleManager.EnterPostDeathChainNeutralPhase();
        battleManager.PreparePostDeathChainCombatUi();
        await Task.Delay(300, cancellationToken);

        var attackList = new List<CardData> { chainCard };
        battleManager.SetPostDeathChainAttackDisplay(attackList, deadSide);

        BattleUIManager.I?.ShowCardDetail(chainCard, deadSide);
        battleManager.SetStatsDisplaySequenceCards(attackList, "攻撃", deadSide);
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        await Task.Delay(500, cancellationToken);

        battleManager.EnterPostDeathChainCombatPhase(deadAttackerSide);
    }

    private static async Task ResolveWithDefenseAndReflectionAsync(
        List<CardData> attackCards,
        PlayerStatus attacker,
        PlayerStatus defender,
        DeadlyChainPostDeathEffectSO effect,
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        EnemyAI enemyAI,
        CancellationToken cancellationToken)
    {
        bool attackerIsPlayer = ReferenceEquals(attacker, battleManager.GetPlayerStatus());

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<CardData> defenseCards = await SelectDefenseAsync(
                attackCards, defender, attackerIsPlayer, battleManager, enemyAI, cancellationToken);

            CardData primaryDef = defenseCards != null && defenseCards.Count > 0 ? defenseCards[0] : null;

            bool physicalReflect = primaryDef != null
                && ReflectionRules.CanReflectIncoming(primaryDef, attackCards)
                && !ReflectionRules.ShouldUseImmediateEffectReflectionFlow(attackCards);
            bool magicReflect = physicalReflect;
            bool immediateReflect = primaryDef != null
                && ReflectionRules.CanReflectIncoming(primaryDef, attackCards)
                && ReflectionRules.ShouldUseImmediateEffectReflectionFlow(attackCards);
            bool physicalBlock = primaryDef != null
                && BlockingRules.CanUsePhysicalBlockingAgainstAttack(primaryDef, attackCards);
            bool parry = primaryDef != null
                && ParryRules.RequiresParryExclusiveLock(primaryDef, attackCards);

            if (immediateReflect)
            {
                if (attackerIsPlayer)
                {
                    await ImmediateEffectReflectionFlow.RunEnemyDefenderReflectsPlayerImmediateAsync(
                        battleManager, battleProcessor, handRefill,
                        attackCards, primaryDef, attacker, cancellationToken);
                }
                else
                {
                    await ImmediateEffectReflectionFlow.RunPlayerInitiatedAsync(
                        battleManager, battleProcessor, handRefill,
                        attackCards, primaryDef, attacker, defender, cancellationToken);
                }
                attacker = ReferenceEquals(attacker, battleManager.GetPlayerStatus())
                    ? battleManager.GetEnemyStatus()
                    : battleManager.GetPlayerStatus();
                defender = ReferenceEquals(defender, battleManager.GetPlayerStatus())
                    ? battleManager.GetEnemyStatus()
                    : battleManager.GetPlayerStatus();
                attackerIsPlayer = ReferenceEquals(attacker, battleManager.GetPlayerStatus());
                continue;
            }

            if (physicalReflect || magicReflect)
            {
                if (attackerIsPlayer)
                {
                    await PhysicalReflectionFlow.RunEnemyDefenderReflectsPlayerAttackAsync(
                        battleManager, battleProcessor, handRefill, enemyAI,
                        attackCards, primaryDef, cancellationToken);
                }
                else
                {
                    await PhysicalReflectionFlow.RunPlayerInitiatedAsync(
                        battleManager, battleProcessor, handRefill, enemyAI,
                        attackCards, primaryDef, cancellationToken);
                }
                attacker = ReferenceEquals(attacker, battleManager.GetPlayerStatus())
                    ? battleManager.GetEnemyStatus()
                    : battleManager.GetPlayerStatus();
                defender = ReferenceEquals(defender, battleManager.GetPlayerStatus())
                    ? battleManager.GetEnemyStatus()
                    : battleManager.GetPlayerStatus();
                attackerIsPlayer = ReferenceEquals(attacker, battleManager.GetPlayerStatus());
                continue;
            }

            if (parry)
            {
                if (attackerIsPlayer)
                {
                    await ParryFlow.RunEnemyDefenderParriesPlayerAttackAsync(
                        battleManager, battleProcessor, handRefill, enemyAI,
                        attackCards, primaryDef, cancellationToken);
                }
                else
                {
                    bool skipTail = await ParryFlow.RunPlayerInitiatedAsync(
                        battleManager, battleProcessor, handRefill, enemyAI,
                        attackCards, primaryDef, battleManager.Sequences,
                        cancellationToken);
                    if (skipTail) return;
                }
                attacker = ReferenceEquals(attacker, battleManager.GetPlayerStatus())
                    ? battleManager.GetEnemyStatus()
                    : battleManager.GetPlayerStatus();
                defender = ReferenceEquals(defender, battleManager.GetPlayerStatus())
                    ? battleManager.GetEnemyStatus()
                    : battleManager.GetPlayerStatus();
                attackerIsPlayer = ReferenceEquals(attacker, battleManager.GetPlayerStatus());
                continue;
            }

            if (physicalBlock)
            {
                if (attackerIsPlayer)
                {
                    await BlockingNullifyFlow.RunEnemyDefenderNullifiesAsync(
                        battleManager, battleProcessor, handRefill,
                        attackCards, primaryDef, cancellationToken);
                }
                else
                {
                    await BlockingNullifyFlow.RunPlayerInitiatedAsync(
                        battleManager, attackCards, primaryDef, cancellationToken);
                }
                return;
            }

            var defHand = attackerIsPlayer ? battleManager.cpuHand : battleManager.playerHand;
            if (defenseCards != null && defenseCards.Count > 1)
            {
                await battleProcessor.ResolvePostDeathDeadlyChainCombatAsync(
                    attackCards, defenseCards, attacker, defender, defHand, effect, cancellationToken);
            }
            else
            {
                await battleProcessor.ResolvePostDeathDeadlyChainCombatAsync(
                    attackCards, primaryDef, attacker, defender, defHand, effect, cancellationToken);
            }

            foreach (var defCard in defenseCards ?? new List<CardData>())
            {
                if (defCard == null) continue;
                if (battleManager.IsOnlineMatch && defCard.cardType == CardType.Magic) continue;
                if (attackerIsPlayer)
                {
                    handRefill?.RecordEnemyUse(defCard);
                    battleProcessor.UseCard(defCard, battleManager.cpuHand);
                }
                else
                {
                    int slot = defCard.cardUI != null ? defCard.cardUI.transform.GetSiblingIndex() : -1;
                    if (slot >= 0) handRefill?.RecordPlayerUseSlot(slot);
                    battleProcessor.UseCard(defCard, battleManager.playerHand);
                }
            }

            return;
        }
    }

    private static async Task<List<CardData>> SelectDefenseAsync(
        List<CardData> attackCards,
        PlayerStatus defender,
        bool attackerIsPlayer,
        BattleManager battleManager,
        EnemyAI enemyAI,
        CancellationToken cancellationToken)
    {
        bool defenderIsPlayer = ReferenceEquals(defender, battleManager.GetPlayerStatus());
        ElementType attackElement = PostDeathCombatContext.Active?.AttackElement
            ?? ElementHelper.GetCombinedElement(attackCards);

        if (defenderIsPlayer)
        {
            battleManager.BeginPostDeathPlayerDefenseWait(attackCards);
            try
            {
                await battleManager.WaitForPostDeathPlayerDefenseSubmitAsync(cancellationToken);
            }
            finally
            {
                battleManager.ClearPostDeathDefenseWait();
            }

            var selected = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selected != null && selected.Count > 0)
            {
                await BattleUIManager.I?.ShowPlayerDefenseCardsPresentationSequenceAsync(selected);
                return selected;
            }
            return new List<CardData>();
        }

        CardData pick = await enemyAI.ExecuteDefenseSelectAsync(
            battleManager.cpuHand, attackElement, attackCards);
        battleManager.SetSelectedDefenseCard(pick);
        var list = battleManager.GetEnemyDefenseCardsForCombat();
        if (list != null && list.Count > 0)
            await BattleUIManager.I?.ShowEnemyDefenseCardsPresentationSequenceAsync(list);
        return list ?? new List<CardData>();
    }

    private static void ConsumeChainFromHand(
        CardData chainCard,
        bool deadIsPlayer,
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill)
    {
        var hand = deadIsPlayer ? battleManager.playerHand : battleManager.cpuHand;
        if (deadIsPlayer)
        {
            int slot = chainCard?.cardUI != null ? chainCard.cardUI.transform.GetSiblingIndex() : -1;
            if (slot >= 0) handRefill?.RecordPlayerUseSlot(slot);
        }
        else
        {
            handRefill?.RecordEnemyUse(chainCard);
        }
        battleProcessor.UseCard(chainCard, hand);
    }
}
