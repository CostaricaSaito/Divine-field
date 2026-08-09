using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// HP0 detection, Ojyou popup, PostDeath queue, BattleEndPhase, and result screen.
/// Extracted from <see cref="BattleManager"/> (PR-5).
/// </summary>
public sealed class GameEndOrchestrator
{
    private readonly IGameEndOrchestratorHost _host;
    private bool _gameEndTriggered;

    private const string OjyouBellSeAddress = "Assets/SE/お寺の鐘.mp3";

    public GameEndOrchestrator(IGameEndOrchestratorHost host)
    {
        _host = host;
    }

    public bool IsGameEndTriggered => _gameEndTriggered;

    public bool IsPostDeathSequenceActive => _host.IsPostDeathSequenceActive;

    public void EnterPostDeathChainNeutralPhase()
    {
        _host.SetCurrentStateDirect(GameState.StandByPhase);
        _host.ResetDefenseInputFlags();
    }

    public void EnterPostDeathChainCombatPhase(PlayerType deadAttackerSide)
    {
        _host.SetCurrentTurnOwner(deadAttackerSide);
        _host.SetCurrentStateDirect(GameState.DefensePhase);
        _host.ResetDefenseInputFlags();
    }

    public void PreparePostDeathChainCombatUi()
    {
        _host.ClearPlayerSelfAttackTargetMode();
        _host.ClearReflectionAttackTotalDisplay();
        _host.ClearPostDeathChainAttackDisplay();
        _host.ClearStatsDisplaySequenceCards();
        _host.SetCurrentAttackCard(null);
        _host.ClearPlayerAttackComboForCombat();
        _host.ClearEnemyAttackComboForCombat();
        _host.ResetPlayerDefenseUseButtonLocks();
        _host.ClearSelectedCards();
        BattleUIManager.I?.ClearAllSelections();
        BattleUIManager.I?.HideAllCardDetails();
        _host.UpdateTotalATKDEFDisplay();
    }

    public async Task<bool> TryHandleDeathIfAnyAsync(CancellationToken ct = default)
    {
        if (_gameEndTriggered) return true;

        bool pDead = _host.PlayerStatus != null && _host.PlayerStatus.IsDead();
        bool eDead = _host.EnemyStatus != null && _host.EnemyStatus.IsDead();
        if (!pDead && !eDead) return false;

        if (NearDeathEffectProcessor.HasPendingRevival(_host.Manager))
        {
            await NearDeathEffectProcessor.TryReviveDeadPlayersAsync(
                _host.Manager, _host.BattleProcessor, _host.HandRefill, ct);

            pDead = _host.PlayerStatus != null && _host.PlayerStatus.IsDead();
            eDead = _host.EnemyStatus != null && _host.EnemyStatus.IsDead();
            if (!pDead && !eDead) return false;
        }

        _gameEndTriggered = true;
        bool gameEndPresentationCompleted = false;

        try
        {
            await Task.Delay(200, ct);
            bool hasPostDeathEffects = PostDeathEffectProcessor.HasPendingEffects(_host.Manager);
            await RunOjyouPopupOnlyAsync(pDead, eDead, startBgmFade: !hasPostDeathEffects, ct);

            _host.IsPostDeathSequenceActive = true;
            try
            {
                await PostDeathEffectProcessor.RunQueueAsync(
                    _host.Manager, _host.BattleProcessor, _host.HandRefill, _host.EnemyAI, ct);
            }
            finally
            {
                _host.IsPostDeathSequenceActive = false;
            }

            pDead = _host.PlayerStatus != null && _host.PlayerStatus.IsDead();
            eDead = _host.EnemyStatus != null && _host.EnemyStatus.IsDead();
            await RunGameEndPresentationAsync(pDead, eDead, startBgmFade: hasPostDeathEffects, ct);
            gameEndPresentationCompleted = true;
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[GameEndOrchestrator] Game end sequence cancelled");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        if (gameEndPresentationCompleted)
            _ = RunGameResultScreenAsync(pDead, eDead);
        return true;
    }

    private async Task RunOjyouPopupOnlyAsync(bool playerDead, bool enemyDead, bool startBgmFade, CancellationToken ct)
    {
        OjyouPopup popupLifetimeRef = null;
        float lifetime = 2.0f;

        if (playerDead)
        {
            if (BattleUIManager.I != null)
                popupLifetimeRef = BattleUIManager.I.ShowOjyouPopup(Side.Player) ?? popupLifetimeRef;
        }
        if (enemyDead)
        {
            if (BattleUIManager.I != null)
                popupLifetimeRef = BattleUIManager.I.ShowOjyouPopup(Side.Enemy) ?? popupLifetimeRef;
        }

        if (popupLifetimeRef != null)
            lifetime = popupLifetimeRef.SequenceLifetimeSeconds;

        SoundEffectPlayer.I?.Play(OjyouBellSeAddress);
        if (startBgmFade)
            _ = BattleBgmController.Instance?.FadeOutBattleBgmAndStopAsync(lifetime);

        await Task.Delay(TimeSpan.FromSeconds(lifetime), ct);
    }

    private async Task RunGameEndPresentationAsync(bool playerDead, bool enemyDead, bool startBgmFade, CancellationToken ct)
    {
        if (startBgmFade)
            _ = BattleBgmController.Instance?.FadeOutBattleBgmAndStopAsync(2.0f);

        if (BattleUIManager.I != null)
        {
            try
            {
                await BattleUIManager.I.ShowPostOjyouFlashAndGameSetAsync(ct);
            }
            catch (OperationCanceledException) { }
        }

        try
        {
            await Task.Delay(500, ct);
        }
        catch (OperationCanceledException) { }

        BattleUIManager.I?.HideBattleUIForGameEnd();
        _host.CardStatsDisplay?.HideAllForGameEnd();
        _host.SetGameState(GameState.BattleEndPhase);
    }

    private async Task RunGameResultScreenAsync(bool playerDead, bool enemyDead)
    {
        GameObject prefab = _host.GameResultPrefab != null
            ? _host.GameResultPrefab
            : Resources.Load<GameObject>("Prefab/GameResult");
        if (prefab == null)
        {
            Debug.LogWarning("[GameEndOrchestrator] GameResult prefab not found");
            return;
        }

        var battleUi = BattleUIManager.I;
        Transform parentForResult = battleUi != null ? battleUi.transform.root : null;
        GameObject resultGo = parentForResult != null
            ? UnityEngine.Object.Instantiate(prefab, parentForResult, false)
            : UnityEngine.Object.Instantiate(prefab);

        var controller = resultGo.GetComponent<GameResultController>();
        if (controller == null)
        {
            Debug.LogWarning("[GameEndOrchestrator] GameResultController missing on prefab");
            return;
        }

        GameResultController.ResultKind kind;
        if (playerDead && enemyDead) kind = GameResultController.ResultKind.Stalemate;
        else if (playerDead) kind = GameResultController.ResultKind.Defeat;
        else kind = GameResultController.ResultKind.Victory;

        try
        {
            await controller.ShowAsync(kind, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
