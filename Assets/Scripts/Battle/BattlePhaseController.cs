using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// GameState transitions and per-phase runners (StandBy / Attack / Defense / CombatResolve / End).
/// Extracted from <see cref="BattleManager"/> (phase 9).
/// </summary>
public sealed class BattlePhaseController
{
    private readonly IBattlePhaseControllerHost _host;
    private CancellationTokenSource _phaseCts;

    public BattlePhaseController(IBattlePhaseControllerHost host)
    {
        _host = host;
    }

    public CancellationToken GetPhaseToken() => _phaseCts != null ? _phaseCts.Token : default;

    /// <summary>Cancel in-flight phase work (network waits, turn runners) without disposing the controller.</summary>
    public void CancelActivePhase()
    {
        _phaseCts?.Cancel();
    }

    public void Dispose()
    {
        _phaseCts?.Cancel();
        _phaseCts?.Dispose();
        _phaseCts = null;
    }

    public void SetGameState(GameState newState)
    {
        if (_host.CurrentState == newState)
        {
            _host.IsProcessingUseButton = false;
            Debug.Log($"[State] noop {newState}");
            return;
        }

        if (_host.GameEnd != null && _host.GameEnd.IsGameEndTriggered && newState != GameState.BattleEndPhase)
        {
            Debug.Log($"[State] ゲーム終了中のため {newState} への遷移を無視");
            return;
        }

        _phaseCts?.Cancel();
        _phaseCts?.Dispose();
        _phaseCts = new CancellationTokenSource();

        Debug.Log($"[State]{_host.CurrentState} → {newState}(Turn: {_host.CurrentTurnOwner})");
        _host.SetCurrentState(newState);
        _host.IsProcessingUseButton = false;
        HandleStateChange();
        HandReloadController.I?.RefreshReloadEntryButton();
        BattleUIManager.I?.UpdateEconomicActionButtons();
    }

    public void EnterAttackPhase()
    {
        _host.ClearConfusionAttackTargetResolvedForDisplay();
        _host.CardStatsDisplay?.ClearAllAttackSequenceDisplayLocks();
        BattleUIManager.I?.SetHandClickable(true);

        var castOwner = _host.Attacker == PlayerType.Player ? _host.PlayerStatus : _host.EnemyStatus;
        if (castOwner != null && castOwner.IsCastingArchMagic && _host.CardSequenceManager != null
            && !_host.CardSequenceManager.IsArchMagicCastIntroInProgress
            && !_host.CardSequenceManager.IsArchMagicCountdownInProgress)
        {
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.SetIntroModeUI(_host.PlayerHand);
            BattleUIManager.I?.RefreshUseButton();
            Side ownerSide = _host.Attacker == PlayerType.Player ? Side.Player : Side.Enemy;
            _ = _host.CardSequenceManager.RunArchMagicCastingTurnAsync(castOwner, ownerSide, GetPhaseToken());
            return;
        }

        PlayerStatus attackPhaseOwner = _host.CurrentTurnOwner == PlayerType.Player
            ? _host.PlayerStatus
            : _host.EnemyStatus;
        if (FreezeAttackSelectFlow.IsTurnOwnerFrozen(attackPhaseOwner))
        {
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.SetUseButtonInteractable(false);
            BattleUIManager.I?.DisableEconomicActionButtonsTemporarily();
            if (_host.CurrentTurnOwner == PlayerType.Player)
            {
                BattleUIManager.I?.SetHandGrayedOut(_host.PlayerHand, grayedOut: true);
                _ = RunFrozenAttackSelectSkipAsync(attackPhaseOwner);
            }
            return;
        }

        if (_host.Attacker == PlayerType.Player)
        {
            _host.ClearPlayerSelfAttackTargetMode();
            var attackables = CardRules.GetAttackChoices(_host.PlayerHand);
            if (attackables.Count == 0)
            {
                BattleUIManager.I?.SetPrayModeUI(_host.PlayerHand);
            }
            else
            {
                if (_host.ShouldGrayOutCards)
                    BattleUIManager.I?.RefreshAttackInteractivity(_host.PlayerHand, CardRules.GetAttackChoices(_host.PlayerHand));
                else
                    BattleUIManager.I?.SetIntroModeUI(_host.PlayerHand);

                BattleUIManager.I?.UpdateEconomicActionButtons();
            }

            BattleUIManager.I?.RefreshMagicCardInteractivity(_host.PlayerHand);
            BattleUIManager.I?.RefreshUseButton();
            _host.RefreshSummonSkillButtonInteractables();
            HandReloadController.I?.RefreshReloadEntryButton();
        }
        else
        {
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.SetUseButtonInteractable(false);
            BattleUIManager.I?.SetHandGrayedOut(_host.PlayerHand, grayedOut: true);
            BattleUIManager.I?.RefreshMagicCardInteractivity(_host.PlayerHand);
        }

        _host.RefreshSummonSkillButtonInteractables();
    }

    private void HandleStateChange()
    {
        switch (_host.CurrentState)
        {
            case GameState.OpeningPhase:
                break;

            case GameState.StandByPhase:
                _ = RunStandByPhaseEnteredAsync();
                break;

            case GameState.AttackPhase:
                EnterAttackPhase();
                break;

            case GameState.DefensePhase:
                _ = RunDefenseSelectAsync();
                break;

            case GameState.DefenseConfirmPhase:
                _ = RunDefenseConfirmAsync();
                break;

            case GameState.CombatResolvePhase:
                _ = RunCombatResolvePhaseAsync();
                break;

            case GameState.EndPhase:
                _host.CardStatsDisplay?.ClearSequenceCardsAndAttackDisplayLocks();
                _host.ClearReflectionAttackTotalDisplay();
                _ = RunEndPhaseAsync();
                break;

            case GameState.BattleEndPhase:
                break;
        }
    }

    private async Task RunStandByPhaseEnteredAsync()
    {
        CancellationToken phaseToken = GetPhaseToken();

        BattleUIManager.I?.HideYurusuButton();
        BattleUIManager.I?.RefreshTurnCountDisplay(_host.SummonTurnCounters, _host.CurrentTurnOwner);

        if (_host.CurrentTurnOwner == PlayerType.Player)
            SoundEffectPlayer.I.Play("Assets/SE/決定ボタンを押す13.mp3");

        if (_host.CurrentTurnOwner == PlayerType.Player)
            _host.PlayerStatus.OnTurnStart();
        else
            _host.EnemyStatus.OnTurnStart();

        if (_host.CurrentTurnOwner == PlayerType.Player)
        {
            EconomicAction.I?.OnTurnStart();
            BattleUIManager.I?.UpdateEconomicActionButtons();
        }

        BattleUIManager.I?.HideAllCardDetails();
        _host.CurrentAttackCard = null;
        _host.ClearOnlineEnemyAttackCombo();
        _host.ClearEnemyAttackComboForCombat();
        _host.ClearMagicalSwordEnemyAttackState();
        _host.SetSuppressEnemyStaleAttackerInTotalByOrb(false);
        _host.CardStatsDisplay?.UpdateDisplay();

        BattleUIManager.I?.SetIntroModeUI(_host.PlayerHand);
        _host.ShouldGrayOutCards = true;

        bool ownerIsCasting = _host.CurrentTurnOwner == PlayerType.Player
            ? _host.PlayerStatus != null && _host.PlayerStatus.IsCastingArchMagic
            : _host.EnemyStatus != null && _host.EnemyStatus.IsCastingArchMagic;

        if (_host.CurrentTurnOwner == PlayerType.Player && _host.TryConsumeUltimateReadyPresentation())
        {
            try
            {
                await UltimateReadyPresentation.PlayAsync(phaseToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        SetGameState(GameState.AttackPhase);
        if (_host.CurrentTurnOwner == PlayerType.Enemy && !ownerIsCasting)
            _ = _host.EnemyTurn.RunAsync();
    }

    private async Task RunDefenseSelectAsync()
    {
        bool sellFlowFromPendingConfirm = _host.CurrentAttackCard != null
            && _host.CurrentAttackCard.cardName == EconomicActionNames.Sell;
        bool reachedDefenseConfirm = false;
        CancellationToken phaseToken = GetPhaseToken();

        try
        {
            var attackCards = _host.GetAttackCardsForCombat();
            if (attackCards != null && attackCards.Count > 0)
            {
                if (_host.Defender == PlayerType.Enemy)
                {
                    if (await OrdinSlashReflectFlow.TryInterceptEnemyDefenseAsync(
                            _host.Manager, attackCards, OrdinInterceptContext.NormalDefensePhase, phaseToken))
                        return;
                }
                else if (await OrdinSlashReflectFlow.TryInterceptPlayerDefenseAsync(
                             _host.Manager, attackCards, OrdinInterceptContext.NormalDefensePhase, phaseToken))
                    return;
            }

            await Task.Delay(1000);
            SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す13.mp3");
            Debug.Log("[BattlePhaseController] 攻撃カード確定、防御カード選択開始");

            BattleUIManager.I?.SyncRestraintHeavyOverlay();

            if (_host.Defender == PlayerType.Enemy)
            {
                ElementType attackElement = ElementHelper.GetIncomingAttackElement(_host.GetAttackCardsForCombat());
                _host.SelectedDefenseCard = await _host.EnemyAI.ExecuteDefenseSelectAsync(
                    _host.CpuHand, attackElement, _host.GetAttackCardsForCombat());

                _host.CardStatsDisplay?.UpdateDisplay();
                SetGameState(GameState.DefenseConfirmPhase);
                reachedDefenseConfirm = true;
            }
            else
            {
                BattleUIManager.I?.HidePlayerCardDetails();
                BattleUIManager.I?.SetHandClickable(true);
                _host.RefreshPlayerDefensePhaseInteractivity();
                BattleUIManager.I?.RefreshMagicCardInteractivity(_host.PlayerHand);
                _host.TryAutoPassPlayerDefenseIfChantingArchMagic();
            }
        }
        finally
        {
            if (sellFlowFromPendingConfirm && !reachedDefenseConfirm && _host.SellFeature != null)
                _host.SellFeature.ForceEndSellProcessingState();
        }
    }

    private async Task RunDefenseConfirmAsync()
    {
        bool sellFlow = _host.CurrentAttackCard != null
            && _host.CurrentAttackCard.cardName == EconomicActionNames.Sell;

        try
        {
            if (_host.CurrentAttackCard == null)
            {
                Debug.LogWarning("攻撃カードが設定されていません");
                SetGameState(GameState.AttackPhase);
                return;
            }

            if (_host.CurrentAttackCard.cardName == EconomicActionNames.Buy)
            {
                Debug.Log("[BattlePhaseController] 経済アクション（購入）の防御フェーズ処理");
                await _host.BuyFeature.ProcessEconomicActionAsync();
                _host.CurrentAttackCard = null;
                _host.SelectedDefenseCard = null;
                _host.UpdateTotalATKDEFDisplay();
                if (await _host.TryHandleDeathIfAnyAsync(GetPhaseToken()))
                    return;
                SetGameState(GameState.CombatResolvePhase);
                return;
            }

            if (_host.CurrentAttackCard.cardName == EconomicActionNames.Sell)
            {
                Debug.Log("[BattlePhaseController] 経済アクション（売却）の防御フェーズ処理");
                await _host.SellFeature.ProcessEconomicActionAsync();
                _host.CurrentAttackCard = null;
                _host.SelectedDefenseCard = null;
                _host.UpdateTotalATKDEFDisplay();
                if (await _host.TryHandleDeathIfAnyAsync(GetPhaseToken()))
                    return;
                SetGameState(GameState.CombatResolvePhase);
                return;
            }

            if (_host.Defender == PlayerType.Player)
                return;

            await _host.EnemyDefense.ResolveConfirmAsync();
        }
        finally
        {
            if (sellFlow && _host.SellFeature != null)
                _host.SellFeature.ForceEndSellProcessingState();
        }
    }

    private async Task RunCombatResolvePhaseAsync()
    {
        CancellationToken phaseToken = GetPhaseToken();

        try
        {
            if (_host.CurrentState != GameState.CombatResolvePhase) return;

            _host.ClearMagicalSwordEnemyAttackState();
            await _host.OnlineSync.RunResolveStateSyncAsync(phaseToken);

            if (_host.CurrentState != GameState.CombatResolvePhase) return;

            await ProcessArchMagicCancelIfPendingAsync(phaseToken);

            if (_host.CurrentState != GameState.CombatResolvePhase) return;

            try
            {
                await InterventionTurnEndProcessor.ProcessIfNeededAsync(_host.Manager, phaseToken);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BattlePhaseController] InterventionTurnEnd: キャンセル");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (_host.CurrentState != GameState.CombatResolvePhase) return;

            await ProcessArchMagicCancelIfPendingAsync(phaseToken);

            if (_host.CurrentState != GameState.CombatResolvePhase) return;

            SetGameState(GameState.EndPhase);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            TryRecoverLateTurnPhaseToStandByPhase();
        }
    }

    private async Task RunEndPhaseAsync()
    {
        CancellationToken phaseToken = GetPhaseToken();

        try
        {
            if (_host.CurrentState != GameState.EndPhase) return;

            if (_host.Manager != null && _host.Manager.IsOpponentForfeitPending)
            {
                if (_host.Manager.BattleExit != null)
                    await _host.Manager.BattleExit.CompleteOpponentForfeitVictoryAsync();
                return;
            }

            PlayerStatus attackerStatus = _host.CurrentTurnOwner == PlayerType.Player
                ? _host.PlayerStatus
                : _host.EnemyStatus;
            try
            {
                await DiseaseTurnEndProcessor.ProcessForAttackerAsync(attackerStatus, phaseToken);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BattlePhaseController] DiseaseTurnEndProcessor: キャンセル（EndPhase 続行を試みます）");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            PlayerStatus turnOwnerStatus = _host.CurrentTurnOwner == PlayerType.Player
                ? _host.PlayerStatus
                : _host.EnemyStatus;
            try
            {
                await FreezeTurnEndProcessor.ProcessTurnOwnerDecayAsync(turnOwnerStatus, phaseToken);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BattlePhaseController] FreezeTurnEndProcessor: cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            await ProcessArchMagicCancelIfPendingAsync(phaseToken);

            if (_host.CurrentState != GameState.EndPhase) return;

            try
            {
                await SummonTurnEndLifecycle.ProcessTurnEndAsync(_host.Manager, _host.SummonTurnCounters, phaseToken);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BattlePhaseController] SummonTurnEndLifecycle: cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            await ProcessArchMagicCancelIfPendingAsync(phaseToken);

            if (_host.CurrentState != GameState.EndPhase) return;

            if (_host.HandRefill != null)
            {
                try
                {
                    await _host.HandRefill.RefillAtTurnEndAsync(_host.PlayerHand, _host.CpuHand, phaseToken);
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("[BattlePhaseController] RefillAtTurnEnd: キャンセル");
                }
            }

            if (_host.CurrentState != GameState.EndPhase) return;

            await ProcessEconomicActionDrawAsync();

            if (_host.CurrentState != GameState.EndPhase) return;

            await RevealFaceDownCardsAsync();

            if (_host.CurrentState != GameState.EndPhase) return;

            BattleUIManager.I?.UpdateStatus(_host.PlayerStatus, _host.EnemyStatus);
            BattleUIManager.I?.SetIntroModeUI(_host.PlayerHand);

            await Task.Delay(500);

            if (_host.CurrentState != GameState.EndPhase) return;

            _host.ShouldGrayOutCards = true;

            bool turnOwnerAppliedBySync = false;
            if (_host.IsOnlineMatch && !_host.GameEnd.IsGameEndTriggered)
                turnOwnerAppliedBySync = await _host.OnlineSync.RunTurnBoundarySyncAsync(phaseToken);

            if (_host.CurrentState != GameState.EndPhase) return;

            if (!turnOwnerAppliedBySync)
                _host.ToggleTurnOwner();
            SetGameState(GameState.StandByPhase);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            TryRecoverLateTurnPhaseToStandByPhase();
        }
    }

    private async Task ProcessArchMagicCancelIfPendingAsync(CancellationToken phaseToken)
    {
        if (_host.CardSequenceManager == null) return;

        try
        {
            if (_host.PlayerStatus != null && _host.PlayerStatus.archMagicCancelPending)
                await _host.CardSequenceManager.RunArchMagicCastCancelAsync(_host.PlayerStatus, phaseToken);

            if (_host.EnemyStatus != null && _host.EnemyStatus.archMagicCancelPending)
                await _host.CardSequenceManager.RunArchMagicCastCancelAsync(_host.EnemyStatus, phaseToken);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[BattlePhaseController] ArchMagicCastCancel: キャンセル");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private void TryRecoverLateTurnPhaseToStandByPhase()
    {
        if (_host.CurrentState != GameState.EndPhase && _host.CurrentState != GameState.CombatResolvePhase)
            return;

        Debug.LogWarning("[BattlePhaseController] CombatResolve/End から復帰できなかったため StandByPhase に移行します");
        _host.ShouldGrayOutCards = true;
        _host.ToggleTurnOwner();
        SetGameState(GameState.StandByPhase);
    }

    private async Task RunFrozenAttackSelectSkipAsync(PlayerStatus frozenOwner)
    {
        CancellationToken token = GetPhaseToken();
        try
        {
            await FreezeAttackSelectFlow.RunSkipFrozenTurnAsync(frozenOwner, token);
            if (_host.CurrentState != GameState.AttackPhase) return;

            if (_host.IsOnlineMatch && ReferenceEquals(frozenOwner, _host.PlayerStatus))
                NetworkBattleBridge.SendAttackSelection(null);

            SetGameState(GameState.CombatResolvePhase);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[BattlePhaseController] Frozen attack select skip: cancelled");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private async Task ProcessEconomicActionDrawAsync()
    {
        if (_host.CurrentAttackCard != null && _host.CurrentAttackCard.cardName == EconomicActionNames.Buy)
        {
            await Task.Delay(500);
            await ProcessCardDrawAsync();
            BattleUIManager.I?.UpdateStatus(_host.PlayerStatus, _host.EnemyStatus);
        }
    }

    private async Task ProcessCardDrawAsync()
    {
        Debug.Log("[BattlePhaseController] ドロー処理開始");
        if (_host.HandRefill != null)
        {
            await _host.HandRefill.DrawCardAsync(_host.PlayerHand);
            Debug.Log($"[BattlePhaseController] ドロー完了 - 手札枚数: {_host.PlayerHand.Count}");
        }
        else
        {
            Debug.LogWarning("[BattlePhaseController] HandRefillServiceが設定されていません");
        }
    }

    private Task RevealFaceDownCardsAsync()
        => HandRevealPresentation.RevealFaceDownCardsLeftToRightAsync(
            _host.HandPanel,
            GetPhaseToken());
}
