using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Enemy Dual Blade Dualism: player second-defense streak (0 = first resolve done, 1 = waiting for 2nd input).
/// </summary>
public sealed class DualBladeDefenseCoordinator
{
    private readonly IDualBladeDefenseHost _host;
    private int _streakIndex;

    public DualBladeDefenseCoordinator(IDualBladeDefenseHost host)
    {
        _host = host;
    }

    public bool IsSecondDefenseWaitActive() => _streakIndex == 1;

    public void ResetStreak() => _streakIndex = 0;

    public async Task<bool> TryPrepareSecondDefenseIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (_host.Attacker != PlayerType.Enemy || _host.Defender != PlayerType.Player) return false;
        if (_host.CurrentAttackCard == null) return false;
        if (!DualBladeDualismRules.ContainsDualBladeDualism(
                new List<CardData> { _host.CurrentAttackCard }))
            return false;
        if (_host.PlayerStatus == null || _host.EnemyStatus == null) return false;
        if (_host.PlayerStatus.IsDead() || _host.EnemyStatus.IsDead())
        {
            _streakIndex = 0;
            return false;
        }

        if (_streakIndex == 1)
        {
            _streakIndex = 0;
            return false;
        }

        _streakIndex = 1;
        await BeginSecondDefenseEntryAsync(cancellationToken);
        return true;
    }

    private async Task BeginSecondDefenseEntryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested) return;

            BattleUIManager.I?.HideAllCardDetails();
            _host.ClearCardStatsSequence();
            _host.UpdateCardStatsDisplay();

            await Task.Delay(300, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;

            if (_host.CurrentAttackCard != null)
            {
                var atkList = _host.GetAttackCardsForCombat();
                if (atkList == null || atkList.Count == 0)
                    atkList = new List<CardData> { _host.CurrentAttackCard };

                BattleUIManager.I?.ClearAllCardDisplaysAndSelectionImmediate();
                _host.SetEnemyAttackSequenceDisplay(atkList);
                SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            }

            await Task.Delay(500, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;

            SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す13.mp3");
            Debug.Log("[DualBladeDefenseCoordinator] 双剣デュアリズム: 2回目の防御選択");
            BattleUIManager.I?.SyncRestraintHeavyOverlay();

            _host.SetSelectedDefenseCard(null);
            _host.ResetPlayerDefenseUseButtonLocks();
            BattleUIManager.I?.SetHandClickable(true);
            _host.RefreshPlayerDefensePhaseInteractivity();
            BattleUIManager.I?.RefreshMagicCardInteractivity(_host.PlayerHand);
            _host.TryAutoPassPlayerDefenseIfChantingArchMagic();
        }
        finally
        {
            _host.IsProcessingUseButton = false;
        }
    }
}
