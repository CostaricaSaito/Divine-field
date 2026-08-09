using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Manages ad-hoc player defense sessions (reflection chain, parry rerun, intervention, disaster, post-death).
/// Extracted from <see cref="BattleManager"/> (PR-2).
/// </summary>
public sealed class AdHocDefenseCoordinator
{
    private readonly IAdHocDefenseHost _host;
    private AdHocDefenseSession _session;
    private bool _postDeathPlayerIsDefender;

    public AdHocDefenseCoordinator(IAdHocDefenseHost host)
    {
        _host = host;
    }

    public bool IsPostDeathPlayerDefender => _postDeathPlayerIsDefender;

    public bool IsWaitActive()
        => _session != null && _session.IsPending;

    public bool IsWaitActive(AdHocDefenseKind kind)
        => IsWaitActive() && _session.Kind == kind;

    public bool ShouldKeepOpponentAttackPanelOnSelectionClear()
        => IsWaitActive() && _session.KeepOpponentAttackPanelWhenClearingPlayerSelection;

    public bool IsInterventionWaitActive() => IsWaitActive(AdHocDefenseKind.Intervention);

    public bool IsDisasterPlayerWaitActive() => IsWaitActive(AdHocDefenseKind.Disaster);

    public bool IsPostDeathWaitActive() => IsWaitActive(AdHocDefenseKind.PostDeath);

    public bool IsReflectionChainPending() => IsWaitActive(AdHocDefenseKind.ReflectionChain);

    public bool IsParryRerunPending() => IsWaitActive(AdHocDefenseKind.ParryRerun);

    public bool IsReactiveWaitActive()
        => IsReflectionChainPending() || IsParryRerunPending();

    public BattleStep CurrentBattleStep
        => IsWaitActive() ? _session.ToBattleStep() : BattleStep.Unknown;

    public bool IsAdHocPlayerDefenseInputActive()
    {
        if (!IsWaitActive()) return false;

        if (_session.Kind == AdHocDefenseKind.PostDeath && !_postDeathPlayerIsDefender)
            return false;
        if (_session.RequiresCombatResolvePhase && _host.CurrentState != GameState.CombatResolvePhase)
            return false;
        if (_session.RequiresTurnDefenderIsPlayer && _host.Defender != PlayerType.Player)
            return false;
        return true;
    }

    public void BeginInterventionPlayerDefensePhase(List<CardData> attackCardsForElement)
        => BeginAdHocPlayerDefense(AdHocDefenseKind.Intervention, attackCardsForElement);

    public void BeginDisasterPlayerDefensePhase(List<CardData> attackCardsForElement)
        => BeginAdHocPlayerDefense(AdHocDefenseKind.Disaster, attackCardsForElement);

    public void BeginPostDeathPlayerDefenseWait(List<CardData> attackCardsForElement)
    {
        _postDeathPlayerIsDefender = true;
        BeginAdHocPlayerDefense(AdHocDefenseKind.PostDeath, attackCardsForElement);
    }

    public async Task<List<CardData>> WaitForSubmitAsync(CancellationToken cancellationToken)
    {
        if (_session?.SubmitTcs == null)
            return new List<CardData>();

        var session = _session;
        var tcs = session.SubmitTcs;
        try
        {
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                return await tcs.Task;
        }
        finally
        {
            var kind = session.Kind;
            Clear();
            if (kind == AdHocDefenseKind.ReflectionChain)
                BattleUIManager.I?.HideYurusuButton();
            BattleUIManager.I?.SetHandClickable(false);
        }
    }

    public async Task<List<CardData>> WaitForReflectionChainDefenseAsync(
        List<CardData> attackSnapshot,
        CancellationToken cancellationToken)
    {
        BeginAdHocPlayerDefense(AdHocDefenseKind.ReflectionChain, attackSnapshot);
        return await WaitForSubmitAsync(cancellationToken);
    }

    public async Task<List<CardData>> WaitForParryRerunDefenseSubmitAsync(CancellationToken cancellationToken)
    {
        BeginAdHocPlayerDefense(AdHocDefenseKind.ParryRerun, null);
        return await WaitForSubmitAsync(cancellationToken);
    }

    public void ClearInterventionWait() => ClearIfKind(AdHocDefenseKind.Intervention);

    public void ClearDisasterPlayerWait() => ClearIfKind(AdHocDefenseKind.Disaster);

    public void ClearPostDeathWait() => ClearIfKind(AdHocDefenseKind.PostDeath);

    public void Clear()
    {
        if (_session?.SubmitTcs != null && !_session.SubmitTcs.Task.IsCompleted)
            _session.SubmitTcs.TrySetCanceled();

        if (_session?.Kind == AdHocDefenseKind.PostDeath)
            _postDeathPlayerIsDefender = false;

        _session = null;
    }

    public void CompleteSubmit(List<CardData> selectedDefenseCards)
    {
        if (!IsWaitActive()) return;
        _session.SubmitTcs.TrySetResult(new List<CardData>(selectedDefenseCards));
    }

    public bool TrySubmitPlayerDefense()
    {
        var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
        if (selectedDefenseCards == null)
            selectedDefenseCards = new List<CardData>();

        if (!TryValidateSelection(selectedDefenseCards))
        {
            _host.IsProcessingUseButton = false;
            BattleUIManager.I?.SetHandClickable(true);
            BattleUIManager.I?.RefreshUseButton();
            return false;
        }

        if (_host.IsOnlineMatch)
            NetworkBattleBridge.SendDefenseSelection(
                selectedDefenseCards.Count > 0 ? selectedDefenseCards : null);

        var submitted = new List<CardData>(selectedDefenseCards);
        CompleteSubmit(submitted);
        BattleUIManager.I?.ClearAllSelections();
        _host.UpdateTotalATKDEFDisplay();
        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.SetUseButtonInteractable(false);
        _host.IsProcessingUseButton = false;
        return true;
    }

    public List<CardData> GetReflectionChainAttackSnapshot()
        => IsWaitActive(AdHocDefenseKind.ReflectionChain) ? _session.AttackSnapshot : null;

    public List<CardData> GetInterventionDefenseAttackSnapshot()
        => IsWaitActive(AdHocDefenseKind.Intervention) ? _session.AttackSnapshot : null;

    public List<CardData> GetDisasterDefenseAttackSnapshot()
        => IsWaitActive(AdHocDefenseKind.Disaster) ? _session.AttackSnapshot : null;

    public List<CardData> GetIncomingAttackSnapshotForDefenseUi()
    {
        if (!IsWaitActive()) return null;

        if (_session.AttackSnapshot != null && _session.AttackSnapshot.Count > 0)
            return new List<CardData>(_session.AttackSnapshot);
        if (_session.Kind == AdHocDefenseKind.ParryRerun)
            return _host.GetAttackCardsForCombat();
        return null;
    }

    public void RefreshReflectionChainInteractivityIfPending()
    {
        if (IsWaitActive(AdHocDefenseKind.ReflectionChain) && _session.AttackSnapshot != null)
            _host.RefreshReflectionChainInteractivity(_session.AttackSnapshot);
    }

    private void ClearIfKind(AdHocDefenseKind kind)
    {
        if (IsWaitActive(kind))
            Clear();
    }

    private void BeginAdHocPlayerDefense(AdHocDefenseKind kind, List<CardData> attackSnapshot)
    {
        _session = AdHocDefenseSession.Create(kind, attackSnapshot);

        _host.ResetPlayerDefenseUseButtonLocks();
        BattleUIManager.I?.ClearAllSelections();

        switch (kind)
        {
            case AdHocDefenseKind.ReflectionChain:
                _host.ClearSelectedCards();
                _host.ClearStatsDisplaySequenceCards();
                BattleUIManager.I?.SetHandClickable(true);
                _host.RefreshReflectionChainInteractivity(attackSnapshot);
                break;
            case AdHocDefenseKind.ParryRerun:
                _host.SetSelectedDefenseCard(null);
                _host.ClearSelectedCards();
                BattleUIManager.I?.SetHandClickable(true);
                SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す13.mp3");
                break;
            default:
                BattleUIManager.I?.HidePlayerCardDetails();
                BattleUIManager.I?.SetHandClickable(true);
                if (kind == AdHocDefenseKind.Disaster || kind == AdHocDefenseKind.PostDeath)
                    _host.UpdateTotalATKDEFDisplay();
                break;
        }

        _host.RefreshPlayerDefensePhaseInteractivity();
        if (_host.PlayerHand is List<CardData> playerHand)
            BattleUIManager.I?.RefreshMagicCardInteractivity(playerHand);
        _host.TryAutoPassPlayerDefenseIfChantingArchMagic();
    }

    private bool TryValidateSelection(IReadOnlyList<CardData> selectedDefenseCards)
    {
        int count = selectedDefenseCards?.Count ?? 0;
        if (_host.PlayerStatus != null && _host.PlayerStatus.HasRestraintEffect() && count > 1)
        {
            BattleUIManager.I?.ShowInfoPopupOnCardPanel("体が重い", new Color(0.22f, 0.24f, 0.38f));
            return false;
        }

        if (_host.IsOnlineMatch && count > 1)
        {
            BattleUIManager.I?.ShowInfoPopupOnCardPanel(
                "オンライン対戦ではカードは1枚ずつ使用できます", new Color(0.95f, 0.25f, 0.2f));
            return false;
        }

        return true;
    }
}
