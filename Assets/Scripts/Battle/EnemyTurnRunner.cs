using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// CPU / remote enemy attack turn: selection, hit roll, and transition to DefensePhase.
/// </summary>
public sealed class EnemyTurnRunner
{
    private readonly IEnemyTurnHost _host;

    public EnemyTurnRunner(IEnemyTurnHost host)
    {
        _host = host;
    }

    public async Task RunAsync()
    {
        if (FreezeAttackSelectFlow.IsTurnOwnerFrozen(_host.EnemyStatus))
        {
            var frozenToken = _host.GetPhaseToken();
            try
            {
                await FreezeAttackSelectFlow.RunSkipFrozenTurnAsync(_host.EnemyStatus, frozenToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_host.CurrentState == GameState.AttackPhase)
                _host.SetGameState(GameState.CombatResolvePhase);
            return;
        }

        if (!_host.IsOnlineMatch
            && BahamutRules.ShouldEnemyUseMegaFlareNow(
                _host.EnemyStatus,
                _host.Manager.SummonTurnCounters,
                _host.CurrentState,
                _host.Manager.CurrentTurnOwner))
        {
            var megaToken = _host.GetPhaseToken();
            try
            {
                await _host.Manager.SummonSkills.TryRunEnemyMegaFlareAsync(megaToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            return;
        }

        var attack = await _host.EnemyAI.ExecuteAttackTurnAsync(
            _host.CpuHand, _host.BattleProcessor, _host.HandRefill, _host.EnemyStatus);

        if (attack == null)
        {
            _host.SetGameState(GameState.CombatResolvePhase);
            return;
        }

        _host.CurrentAttackCard = attack;
        _host.DualBladeDefense.ResetStreak();

        if (EconomicActionNames.IsEconomicAttack(attack.cardName))
        {
            _host.SetGameState(GameState.DefensePhase);
            return;
        }

        var token = _host.GetPhaseToken();
        var atkList = GetAttackCardsForTurn(attack);

        if (atkList.Count == 1 && ArchMagicRules.IsArchMagicCard(attack))
        {
            Debug.Log($"[EnemyTurn] ArchMagic cast start: {attack.cardName}");
            if (_host.CardSequenceManager != null)
            {
                try
                {
                    await _host.CardSequenceManager.StartArchMagicCastIntroAsync(attack, Side.Enemy, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                return;
            }
        }

        if (CardRules.IsImmediateAction(attack) && atkList.Count == 1)
        {
            Debug.Log($"[EnemyTurn] Immediate card: {attack.cardName}");
            try
            {
                await _host.PlayAttackConfirmPresentationAsync(attack, Side.Enemy, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _host.UpdateBattleStatusUi();
            _host.UpdateCardStatsDisplay();

            PlayerStatus immediateTarget = ResolveCpuImmediateEffectTarget(attack);
            bool skipDefenseForImmediate =
                attack.specialCardEffect is DisasterTriggerEffectSO
                || immediateTarget == _host.EnemyStatus;

            if (skipDefenseForImmediate)
            {
                await Task.Delay(DamagePopup.PreImmediateEffectDelayMs, token);
                await _host.BattleProcessor.ResolveImmediateEffectAsync(attack, _host.EnemyStatus, immediateTarget);
                if (_host.CardSequenceManager != null)
                    await _host.RunAfterCombatSharedCleanupAsync(token);
                else
                {
                    BattleUIManager.I?.HideAllCardDetails();
                    _host.CardStatsDisplay?.ClearSequenceCards();
                    _host.CurrentAttackCard = null;
                    _host.ClearMagicalExplosionComboMpPoolSnapshot();
                    _host.ClearTributeBloodHpPaidSnapshot();
                    _host.ClearHammadnessRollSnapshot();
                    _host.UpdateBattleStatusUi();
                    _host.UpdateCardStatsDisplay();
                    _host.SetGameState(GameState.CombatResolvePhase);
                }

                return;
            }

            _host.SetGameState(GameState.DefensePhase);
            return;
        }

        if (ShouldUseAttackPresentationSequence(atkList, attack))
        {
            Debug.Log($"[EnemyTurn] Attack presentation: {atkList.Count} cards");
            if (_host.CardStatsDisplay != null)
            {
                PlayerAttackTotalDisplayFlow.ResetAttackSequenceDisplayLocks(_host.CardStatsDisplay);
                _host.CardStatsDisplay.BeginAttackSequenceReveal(Side.Enemy);
                _host.CardStatsDisplay.SetSequenceCards(new List<CardData>(), "攻撃", Side.Enemy);
                _host.CardStatsDisplay.UpdateDisplay();
            }

            await _host.CardSequenceManager.PresentOnlineEnemyAttackSequenceAsync(atkList, token);
            _host.UpdateBattleStatusUi();
            _host.UpdateCardStatsDisplay();
        }
        else
        {
            BattleUIManager.I?.ShowCardDetail(attack, Side.Enemy);
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            Debug.Log($"[EnemyTurn] Card selected: {attack.cardName}");
            _host.UpdateBattleStatusUi();
            _host.CardStatsDisplay.UpdateDisplay();
            await Task.Delay(1000);
        }

        bool confusedEnemy = _host.EnemyStatus != null && _host.EnemyStatus.HasConfusionEffect();
        bool confusionTargetSelf = confusedEnemy && BattleRandom.Range(0, 2) == 0;
        if (confusedEnemy)
            _host.SetConfusionAttackTargetResolvedForDisplay(confusionTargetSelf);

        if (confusionTargetSelf)
        {
            _host.UpdateCardStatsDisplay();
            await Task.Delay(500);
            if (_host.CardSequenceManager != null)
            {
                bool finished = await _host.ResolveSelfTargetAttackAsync(atkList, token);
                if (!finished)
                    return;
                _host.SetGameState(GameState.CombatResolvePhase);
                return;
            }

            Debug.LogError("[EnemyTurn] CardSequenceManager missing; cannot resolve confusion self-attack");
            _host.SetGameState(GameState.CombatResolvePhase);
            return;
        }

        if (confusedEnemy)
            _host.UpdateCardStatsDisplay();

        if (!confusedEnemy && _host.IsOnlineMatch
            && _host.EnemyAI is RemotePlayerAgent remoteSelfAgent && remoteSelfAgent.LastAttackTargetSelf)
        {
            Debug.Log("[EnemyTurn] Online: resolving opponent self-target attack without defense phase");
            _host.UpdateCardStatsDisplay();
            await Task.Delay(500);
            if (_host.CardSequenceManager != null)
            {
                bool selfAttackFinished = await _host.ResolveSelfTargetAttackAsync(atkList, token);
                if (!selfAttackFinished)
                    return;
                _host.SetGameState(GameState.CombatResolvePhase);
                return;
            }

            Debug.LogError("[EnemyTurn] CardSequenceManager missing; cannot resolve opponent self-target attack");
            _host.SetGameState(GameState.CombatResolvePhase);
            return;
        }

        var primary = HitRateRules.GetPrimaryForHitRate(atkList);
        int finalPct = HitRateRules.ComputeFinalHitPercent(primary, _host.EnemyStatus, _host.PlayerStatus);
        bool rolledHit = HitRateRules.RollHit(finalPct);
        if (!rolledHit)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ニュッ1.mp3");
            BattleUIManager.I?.ShowMissPopup(_host.PlayerStatus);
            await DamagePopup.WaitAfterPopupLifetimeAsync(DamagePopup.DefaultFadeDurationIfUnknown);
            BattleUIManager.I?.HideAllCardDetails();
            _host.ClearCardStatsSequenceAndAttackLocks();
            _host.ClearMagicalExplosionComboMpPoolSnapshot();
            _host.ClearMillionDollarBazookaComboGpPoolSnapshot();
            _host.ClearTributeBloodHpPaidSnapshot();
            _host.ClearHammadnessRollSnapshot();
            _host.ClearMagicalSwordEnemyAttackState();
            _host.CurrentAttackCard = null;
            _host.SetGameState(GameState.CombatResolvePhase);
            return;
        }

        if (finalPct < 100)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/小パンチ.mp3");
            float popupSec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowCombatHitConfirmedPopup(_host.PlayerStatus)
                : DamagePopup.DefaultFadeDurationIfUnknown;
            await DamagePopup.WaitAfterPopupLifetimeAsync(popupSec);
        }

        _host.SetGameState(GameState.DefensePhase);
    }

    private List<CardData> GetAttackCardsForTurn(CardData primary)
    {
        if (_host.CombatSnapshots.TryGetOnlineEnemyAttackCombo(out var onlineCombo))
            return new List<CardData>(onlineCombo);
        if (_host.EnemyAI?.LastAttackSelection != null && _host.EnemyAI.LastAttackSelection.Count > 0)
            return new List<CardData>(_host.EnemyAI.LastAttackSelection);
        return primary != null ? new List<CardData> { primary } : new List<CardData>();
    }

    private bool ShouldUseAttackPresentationSequence(List<CardData> atkList, CardData primary)
    {
        if (_host.CardSequenceManager == null || atkList == null || atkList.Count == 0) return false;
        if (RemotePlayerAgent.ShouldDeferRemoteAttackBookkeeping(atkList)) return true;
        if (atkList.Count > 1) return true;
        if (MagicalExplosionRules.ContainsMagicalExplosion(atkList)) return true;
        if (MillionDollarBazookaRules.ContainsMillionDollarBazooka(atkList)) return true;
        if (TributeBloodRules.ContainsTributeBlood(atkList)) return true;
        if (HammadnessRules.ContainsHammadness(atkList)) return true;
        return false;
    }

    private PlayerStatus ResolveCpuImmediateEffectTarget(CardData attack)
    {
        if (attack == null) return _host.PlayerStatus;
        if (_host.EnemyStatus != null && _host.EnemyStatus.HasConfusionEffect())
            return BattleRandom.Range(0, 2) == 0 ? _host.EnemyStatus : _host.PlayerStatus;
        bool remoteTargetToggled = _host.IsOnlineMatch
            && _host.EnemyAI is RemotePlayerAgent remoteAgent && remoteAgent.LastAttackTargetSelf;
        if (CardRules.IsRecoveryCard(attack))
            return remoteTargetToggled ? _host.PlayerStatus : _host.EnemyStatus;
        return remoteTargetToggled ? _host.EnemyStatus : _host.PlayerStatus;
    }
}
