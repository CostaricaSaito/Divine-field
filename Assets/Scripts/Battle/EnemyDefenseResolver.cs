using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Enemy defense selection display and DefenseConfirm resolution (reflect / parry / block / normal).
/// </summary>
public sealed class EnemyDefenseResolver
{
    private readonly IEnemyDefenseHost _host;

    public EnemyDefenseResolver(IEnemyDefenseHost host)
    {
        _host = host;
    }

    public List<CardData> GetDefenseCardsForCombat()
    {
        if (_host.EnemyAI is RemotePlayerAgent remote
            && remote.LastDefenseSelection != null && remote.LastDefenseSelection.Count > 0)
            return new List<CardData>(remote.LastDefenseSelection);
        if (_host.SelectedDefenseCard != null)
            return new List<CardData> { _host.SelectedDefenseCard };
        return new List<CardData>();
    }

    public async Task PickAndDisplayAfterPlayerHitAsync(List<CardData> playerAttackCards)
    {
        ElementType attackElement = ElementHelper.GetCombinedElement(playerAttackCards);
        _host.SelectedDefenseCard = await _host.EnemyAI.ExecuteDefenseSelectAsync(
            _host.CpuHand, attackElement, playerAttackCards);
        _host.UpdateCardStatsDisplay();

        var defenseCards = GetDefenseCardsForCombat();
        if (defenseCards == null || defenseCards.Count == 0)
            return;

        await BattleUIManager.I?.ShowEnemyDefenseCardsPresentationSequenceAsync(defenseCards);
    }

    public async Task ResolveConfirmAsync()
    {
        var defenseCardsToDisplay = GetDefenseCardsForCombat();
        if (defenseCardsToDisplay != null && defenseCardsToDisplay.Count > 0)
        {
            await BattleUIManager.I?.ShowEnemyDefenseCardsPresentationSequenceAsync(defenseCardsToDisplay);
        }

        var atk = (_host.Attacker == PlayerType.Player) ? _host.PlayerStatus : _host.EnemyStatus;
        var def = (_host.Defender == PlayerType.Player) ? _host.PlayerStatus : _host.EnemyStatus;
        var defHand = (_host.Defender == PlayerType.Player) ? _host.PlayerHand : _host.CpuHand;

        List<CardData> attackCards = _host.GetAttackCardsForCombat();
        var defenseCardsForCombat = defenseCardsToDisplay != null && defenseCardsToDisplay.Count > 0
            ? defenseCardsToDisplay
            : new List<CardData>();
        CardData enemyDefenseCard = defenseCardsForCombat.Count > 0 ? defenseCardsForCombat[0] : null;

        bool enemyPhysicalReflect = enemyDefenseCard != null
            && ReflectionRules.CanReflectIncoming(enemyDefenseCard, attackCards)
            && !ReflectionRules.ShouldUseImmediateEffectReflectionFlow(attackCards);
        bool enemyMagicReflect = enemyPhysicalReflect;
        bool enemyImmediateReflect = enemyDefenseCard != null
            && ReflectionRules.CanReflectIncoming(enemyDefenseCard, attackCards)
            && ReflectionRules.ShouldUseImmediateEffectReflectionFlow(attackCards);
        bool enemyPhysicalBlock = enemyDefenseCard != null
            && BlockingRules.CanUsePhysicalBlockingAgainstAttack(enemyDefenseCard, attackCards);
        bool enemyParry = enemyDefenseCard != null
            && ParryRules.RequiresParryExclusiveLock(enemyDefenseCard, attackCards);

        var phaseToken = _host.GetPhaseToken();
        bool showYurusuDuringCombat =
            _host.Defender == PlayerType.Enemy && defenseCardsForCombat.Count == 0 && BattleUIManager.I != null;
        using (YurusuDisplayScope.ShowIf(showYurusuDuringCombat))
        {
            if (attackCards != null && attackCards.Count > 0
                && CardRules.IncomingRequiresFullOnlyReactiveDefense(attackCards))
            {
                if (enemyImmediateReflect)
                {
                    await Task.Delay(DamagePopup.PreImmediateEffectDelayMs, phaseToken);
                    await ImmediateEffectReflectionFlow.RunEnemyDefenderReflectsPlayerImmediateAsync(
                        _host.Manager,
                        _host.BattleProcessor,
                        _host.HandRefill,
                        attackCards,
                        enemyDefenseCard,
                        atk,
                        phaseToken);
                }
                else if (enemyPhysicalReflect || enemyMagicReflect)
                {
                    await PhysicalReflectionFlow.RunEnemyDefenderReflectsPlayerAttackAsync(
                        _host.Manager,
                        _host.BattleProcessor,
                        _host.HandRefill,
                        _host.EnemyAI,
                        attackCards,
                        enemyDefenseCard,
                        phaseToken);
                }
                else if (attackCards.Count == 1 && attackCards[0] != null)
                {
                    await Task.Delay(DamagePopup.PreImmediateEffectDelayMs, phaseToken);
                    await _host.BattleProcessor.ResolveImmediateEffectAsync(attackCards[0], atk, def);
                }
            }
            else if (enemyImmediateReflect)
            {
                await ImmediateEffectReflectionFlow.RunEnemyDefenderReflectsPlayerImmediateAsync(
                    _host.Manager,
                    _host.BattleProcessor,
                    _host.HandRefill,
                    attackCards,
                    enemyDefenseCard,
                    atk,
                    phaseToken);
            }
            else if (enemyPhysicalReflect || enemyMagicReflect)
            {
                await PhysicalReflectionFlow.RunEnemyDefenderReflectsPlayerAttackAsync(
                    _host.Manager,
                    _host.BattleProcessor,
                    _host.HandRefill,
                    _host.EnemyAI,
                    attackCards,
                    enemyDefenseCard,
                    phaseToken);
            }
            else if (enemyParry)
            {
                await ParryFlow.RunEnemyDefenderParriesPlayerAttackAsync(
                    _host.Manager,
                    _host.BattleProcessor,
                    _host.HandRefill,
                    _host.EnemyAI,
                    attackCards,
                    enemyDefenseCard,
                    phaseToken);
            }
            else if (enemyPhysicalBlock)
            {
                if (enemyDefenseCard.cardType == CardType.Magic && _host.CardSequenceManager != null)
                    await _host.CardSequenceManager.ApplyEnemyMagicDefenseFromHandOrPoolAsync(enemyDefenseCard);
                await BlockingNullifyFlow.RunEnemyDefenderNullifiesAsync(
                    _host.Manager,
                    _host.BattleProcessor,
                    _host.HandRefill,
                    attackCards,
                    enemyDefenseCard,
                    phaseToken,
                    defenseCardAlreadyConsumed: enemyDefenseCard.cardType == CardType.Magic);
            }
            else if (defenseCardsForCombat.Count > 1)
            {
                await _host.BattleProcessor.ResolveCombatAsync(attackCards, defenseCardsForCombat, atk, def, defHand);
            }
            else
            {
                CardData singleDef = defenseCardsForCombat.Count == 1 ? defenseCardsForCombat[0] : null;
                await _host.BattleProcessor.ResolveCombatAsync(attackCards, singleDef, atk, def, defHand);
            }
        }

        if (phaseToken.IsCancellationRequested) return;
        if (await _host.TryHandleDeathIfAnyAsync(phaseToken)) return;

        _host.ClearMagicalExplosionComboMpPoolSnapshot();
        _host.ClearMillionDollarBazookaComboGpPoolSnapshot();
        _host.ClearTributeBloodHpPaidSnapshot();
        _host.ClearHammadnessRollSnapshot();
        BattleUIManager.I?.HideAllCardDetails();

        bool skipPostCombatEnemyDefenseUse = enemyPhysicalReflect || enemyMagicReflect || enemyImmediateReflect || enemyParry
            || enemyPhysicalBlock
            || (attackCards != null && CardRules.IncomingRequiresFullOnlyReactiveDefense(attackCards));
        if (defenseCardsForCombat.Count > 0 && !skipPostCombatEnemyDefenseUse)
        {
            foreach (var defenseCardToUse in defenseCardsForCombat)
            {
                if (defenseCardToUse == null) continue;
                if (_host.IsOnlineMatch && defenseCardToUse.cardType == CardType.Magic)
                    continue;
                _host.HandRefill?.RecordEnemyUse(defenseCardToUse);
                _host.BattleProcessor.UseCard(defenseCardToUse, defHand);
            }
        }

        _host.ClearCardStatsSequenceAndAttackLocks();
        _host.CurrentAttackCard = null;
        _host.SetSuppressEnemyStaleAttackerInTotalByOrb(false);
        _host.UpdateCardStatsDisplay();
        _host.SetGameState(GameState.CombatResolvePhase);
    }
}
