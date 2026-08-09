using System.Collections.Generic;

/// <summary>
/// Player defense phase hand gray-out, restraint overlay, and arch-magic auto-pass.
/// Extracted from <see cref="BattleManager"/> (phase 10).
/// </summary>
public sealed class PlayerDefenseInteractivityService
{
    private readonly IPlayerDefenseInteractivityHost _host;

    public PlayerDefenseInteractivityService(IPlayerDefenseInteractivityHost host)
    {
        _host = host;
    }

    public bool IsPlayerChantingArchMagicWhileDefending()
    {
        return _host.PlayerStatus != null && _host.PlayerStatus.IsCastingArchMagic
            && _host.IsPlayerDefenseInputActive();
    }

    public void RefreshPlayerDefensePhaseInteractivity()
    {
        if (BattleUIManager.I == null) return;
        if (!_host.IsPlayerDefenseInputActive()) return;

        if (IsPlayerChantingArchMagicWhileDefending())
        {
            ApplyArchMagicChantingDefenseBlockUi();
            return;
        }

        if (_host.AdHocDefense.IsWaitActive(AdHocDefenseKind.ReflectionChain))
        {
            var snapshot = _host.AdHocDefense.GetReflectionChainAttackSnapshot();
            if (snapshot != null)
            {
                RefreshReflectionChainInteractivity(snapshot);
                return;
            }
        }

        List<CardData> attackSource = _host.GetIncomingAttackSnapshotForDefenseUi();
        if (attackSource != null && attackSource.Count == 1 && attackSource[0] != null
            && EconomicActionNames.IsEconomicAttack(attackSource[0].cardName))
        {
            BattleUIManager.I?.SetHandGrayedOut(_host.PlayerHand, grayedOut: true);
            BattleUIManager.I?.RefreshUseButton();
            RefreshPlayerHandStatusTextForDefenseSnapshot();
            return;
        }

        if (attackSource == null || attackSource.Count == 0)
        {
            BattleUIManager.I?.RefreshUseButton();
            return;
        }

        if (CardRules.IncomingRequiresFullOnlyReactiveDefense(attackSource))
        {
            var defenseChoicesRestricted = CardRules.GetFullOnlyReactiveDefenseChoices(
                _host.PlayerHand, attackSource);
            var selectedDefenseR = BattleUIManager.I.GetSelectedDefenseCards();
            defenseChoicesRestricted = CardRules.ApplyRestraintDefenseFilter(
                defenseChoicesRestricted,
                selectedDefenseR,
                _host.PlayerStatus != null && _host.PlayerStatus.HasRestraintEffect());
            BattleUIManager.I.RefreshDefenseInteractivity(_host.PlayerHand, defenseChoicesRestricted);
            BattleUIManager.I.RefreshUseButton();
            RefreshPlayerHandStatusTextForDefenseSnapshot();
            return;
        }

        var defenseChoices = CardRules.GetDefenseChoicesForIncoming(_host.PlayerHand, attackSource);
        var selectedDefense = BattleUIManager.I.GetSelectedDefenseCards();
        defenseChoices = CardRules.ApplyRestraintDefenseFilter(
            defenseChoices,
            selectedDefense,
            _host.PlayerStatus != null && _host.PlayerStatus.HasRestraintEffect());

        BattleUIManager.I.RefreshDefenseInteractivity(_host.PlayerHand, defenseChoices);
        BattleUIManager.I.RefreshUseButton();
        RefreshPlayerHandStatusTextForDefenseSnapshot();
    }

    public void RefreshPlayerHandStatusTextForDefenseSnapshot()
    {
        if (_host.PlayerHand == null) return;
        foreach (var c in _host.PlayerHand)
        {
            if (c?.cardUI == null) continue;
            c.cardUI.RefreshHandStatusText();
        }
    }

    public void RefreshReflectionChainInteractivity(List<CardData> attackSnapshot)
    {
        if (BattleUIManager.I == null || attackSnapshot == null) return;

        if (IsPlayerChantingArchMagicWhileDefending())
        {
            ApplyArchMagicChantingDefenseBlockUi();
            return;
        }

        if (CardRules.IncomingRequiresFullOnlyReactiveDefense(attackSnapshot))
        {
            var defenseChoicesR = CardRules.GetFullOnlyReactiveDefenseChoices(_host.PlayerHand, attackSnapshot);
            var selectedDefenseR = BattleUIManager.I.GetSelectedDefenseCards();
            defenseChoicesR = CardRules.ApplyRestraintDefenseFilter(
                defenseChoicesR,
                selectedDefenseR,
                _host.PlayerStatus != null && _host.PlayerStatus.HasRestraintEffect());
            BattleUIManager.I.RefreshDefenseInteractivity(_host.PlayerHand, defenseChoicesR);
            BattleUIManager.I.RefreshUseButton();
            RefreshPlayerHandStatusTextForDefenseSnapshot();
            return;
        }

        var defenseChoices = CardRules.GetDefenseChoicesForIncoming(_host.PlayerHand, attackSnapshot);
        var selectedDefense = BattleUIManager.I.GetSelectedDefenseCards();
        defenseChoices = CardRules.ApplyRestraintDefenseFilter(
            defenseChoices,
            selectedDefense,
            _host.PlayerStatus != null && _host.PlayerStatus.HasRestraintEffect());

        BattleUIManager.I.RefreshDefenseInteractivity(_host.PlayerHand, defenseChoices);
        BattleUIManager.I.RefreshUseButton();
        RefreshPlayerHandStatusTextForDefenseSnapshot();
    }

    public bool TryAutoPassPlayerDefenseIfChantingArchMagic()
    {
        if (!IsPlayerChantingArchMagicWhileDefending())
            return false;

        ApplyArchMagicChantingDefenseBlockUi();
        BattleUIManager.I?.ClearAllSelections();
        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.SetUseButtonInteractable(false);
        _host.PlayerInput.IsProcessingUseButton = false;

        if (_host.AdHocDefense.IsWaitActive())
        {
            if (_host.IsOnlineMatch)
                NetworkBattleBridge.SendDefenseSelection(null);
            _host.CompleteAdHocDefenseSubmit(new List<CardData>());
            _host.UpdateTotalATKDEFDisplay();
            return true;
        }

        if (_host.IsOnlineMatch)
            NetworkBattleBridge.SendDefenseSelection(null);
        _host.HandleNoDefenseCard();
        return true;
    }

    private void ApplyArchMagicChantingDefenseBlockUi()
    {
        if (BattleUIManager.I == null) return;
        BattleUIManager.I.RefreshDefenseInteractivity(_host.PlayerHand, new List<CardData>());
        BattleUIManager.I.RefreshUseButton();
        RefreshPlayerHandStatusTextForDefenseSnapshot();
    }
}
