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
        if (_host.EnemyAI != null
            && _host.EnemyAI.LastDefenseSelection != null
            && _host.EnemyAI.LastDefenseSelection.Count > 0)
            return new List<CardData>(_host.EnemyAI.LastDefenseSelection);
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
        var defenseCardsForCombat = GetDefenseCardsForCombat();
        var atk = (_host.Attacker == PlayerType.Player) ? _host.PlayerStatus : _host.EnemyStatus;
        var def = (_host.Defender == PlayerType.Player) ? _host.PlayerStatus : _host.EnemyStatus;
        var defHand = (_host.Defender == PlayerType.Player) ? _host.PlayerHand : _host.CpuHand;

        List<CardData> attackCards = _host.GetAttackCardsForCombat();
        var combatDefenseCards = FreeMagicCastRules.ExcludeCatalystsFromCombatStats(defenseCardsForCombat);
        CardData enemyDefenseCard = combatDefenseCards.Count > 0 ? combatDefenseCards[0] : null;

        var phaseToken = _host.GetPhaseToken();

        if (ShiningBarrierRules.IsBarrierOnlySelection(defenseCardsForCombat))
        {
            bool resolved = await ShiningBarrierDefenseFlow.RunEnemyInterceptAsync(
                _host.Manager,
                _host.BattleProcessor,
                _host.HandRefill,
                enemyDefenseCard,
                attackCards,
                defHand,
                _host.EnemyAI,
                phaseToken);
            if (resolved)
            {
                if (phaseToken.IsCancellationRequested) return;
                if (await _host.TryHandleDeathIfAnyAsync(phaseToken)) return;

                _host.ClearMagicalExplosionComboMpPoolSnapshot();
                _host.ClearMillionDollarBazookaComboGpPoolSnapshot();
                _host.ClearTributeBloodHpPaidSnapshot();
                _host.ClearHammadnessRollSnapshot();
                BattleUIManager.I?.HideAllCardDetails();
                _host.Manager?.ClearIncomingAttackElementOverrides();
                _host.ClearCardStatsSequenceAndAttackLocks();
                _host.CurrentAttackCard = null;
                _host.SetSuppressEnemyStaleAttackerInTotalByOrb(false);
                _host.UpdateCardStatsDisplay();
                _host.SetGameState(GameState.CombatResolvePhase);
                return;
            }
        }

        if (defenseCardsForCombat.Count > 0)
            await BattleUIManager.I?.ShowEnemyDefenseCardsPresentationSequenceAsync(defenseCardsForCombat);

        var reactiveKind = _host.Defender == PlayerType.Enemy
            ? ReactiveDefenseRules.Classify(combatDefenseCards, attackCards)
            : ReactiveDefenseKind.Normal;
        if (ReactiveDefenseRules.StartsChain(reactiveKind))
        {
            await ReactiveDefenseChain.RunAsync(
                _host.Manager,
                attackCards,
                _host.Attacker,
                ReactiveDefenseResponse.FromPicks(defenseCardsForCombat, alreadyConsumed: false),
                phaseToken);
        }
        else if (reactiveKind == ReactiveDefenseKind.Nullify)
        {
            if (enemyDefenseCard.cardType == CardType.Magic && _host.CardSequenceManager != null)
                await _host.CardSequenceManager.ApplyEnemyMagicDefenseFromHandOrPoolAsync(
                    enemyDefenseCard, defenseCardsForCombat);
            foreach (var extra in defenseCardsForCombat)
            {
                if (extra == null || extra.cardType == CardType.Magic) continue;
                if (_host.IsOnlineMatch) continue;
                _host.HandRefill?.RecordEnemyUse(extra);
                _host.BattleProcessor.UseCard(extra, defHand);
            }
            await BlockingNullifyFlow.RunEnemyDefenderNullifiesAsync(
                _host.Manager,
                _host.BattleProcessor,
                _host.HandRefill,
                attackCards,
                enemyDefenseCard,
                phaseToken,
                defenseCardAlreadyConsumed: enemyDefenseCard.cardType == CardType.Magic);
        }
        else
        {
            bool showYurusuDuringCombat =
                _host.Defender == PlayerType.Enemy && defenseCardsForCombat.Count == 0 && BattleUIManager.I != null;
            using (YurusuDisplayScope.ShowIf(showYurusuDuringCombat))
            {
                if (defenseCardsForCombat.Count > 1)
                {
                    await _host.BattleProcessor.ResolveCombatAsync(attackCards, defenseCardsForCombat, atk, def, defHand);
                }
                else
                {
                    CardData singleDef = defenseCardsForCombat.Count == 1 ? defenseCardsForCombat[0] : null;
                    await _host.BattleProcessor.ResolveCombatAsync(attackCards, singleDef, atk, def, defHand);
                }
            }

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
        }

        if (phaseToken.IsCancellationRequested) return;
        if (await _host.TryHandleDeathIfAnyAsync(phaseToken)) return;

        _host.ClearMagicalExplosionComboMpPoolSnapshot();
        _host.ClearMillionDollarBazookaComboGpPoolSnapshot();
        _host.ClearTributeBloodHpPaidSnapshot();
        _host.ClearHammadnessRollSnapshot();
        BattleUIManager.I?.HideAllCardDetails();

        _host.ClearCardStatsSequenceAndAttackLocks();
        _host.CurrentAttackCard = null;
        _host.SetSuppressEnemyStaleAttackerInTotalByOrb(false);
        _host.UpdateCardStatsDisplay();
        _host.SetGameState(GameState.CombatResolvePhase);
    }
}
