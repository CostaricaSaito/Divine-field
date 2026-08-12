using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Return-to-Main from Battle and online forfeit / opponent-leave victory handling.
/// </summary>
public sealed class BattleExitCoordinator
{
    private const string MainSceneName = "Main";

    private readonly BattleManager _host;
    private bool _exitInProgress;
    private bool _opponentLeftHandled;
    private GameObject _leavingPopupInstance;

    public BattleExitCoordinator(BattleManager host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public bool IsExitInProgress => _exitInProgress;

    public bool IsLeavingCautionOpen => _leavingPopupInstance != null;

    public void Initialize()
    {
        NetworkBattleBridge.RemoteDisconnected += OnRemoteSessionEnded;
        NetworkBattleBridge.RemoteForfeitReceived += OnRemoteSessionEnded;
    }

    public void Shutdown()
    {
        NetworkBattleBridge.RemoteDisconnected -= OnRemoteSessionEnded;
        NetworkBattleBridge.RemoteForfeitReceived -= OnRemoteSessionEnded;
        DestroyLeavingPopup();
    }

    public void RequestReturnToTop()
    {
        if (_host.IsGameEndTriggered || _exitInProgress) return;

        if (!_host.IsOnlineMatch)
        {
            _ = ExitCpuBattleToMainAsync();
            return;
        }

        ShowLeavingCautionPopup();
    }

    private void OnRemoteSessionEnded()
    {
        if (_exitInProgress || _host.IsGameEndTriggered || _opponentLeftHandled) return;

        if (IsImmediateForfeitVictoryPoint())
        {
            Debug.Log("[BattleExit] Opponent left at safe point — immediate forfeit victory");
            _ = CompleteOpponentForfeitVictoryAsync();
        }
        else
        {
            Debug.Log("[BattleExit] Opponent left mid-sequence — defer victory until EndPhase or next remote wait");
            _host.SetOpponentForfeitPending(true);
        }
    }

    /// <summary>
    /// Completes victory when opponent forfeits/disconnects. Safe to call from multiple hooks; guarded.
    /// </summary>
    public async Task CompleteOpponentForfeitVictoryAsync()
    {
        if (_opponentLeftHandled || _host.IsGameEndTriggered || _exitInProgress) return;
        _opponentLeftHandled = true;
        _host.ClearOpponentForfeitPending();

        Debug.Log("[BattleExit] Completing opponent forfeit victory");

        _host.CloseBlockingBattlePopups();
        DestroyLeavingPopup();
        _host.PhaseController.CancelActivePhase();
        if (_host.IsOnlineMatch)
            NetworkBattleBridge.CancelPendingWaits();
        _host.PlayerInput.ResetAllLocks();
        BattleUIManager.I?.SetHandClickable(false);

        await _host.GameEnd.ForceOpponentForfeitVictoryAsync(CancellationToken.None);
    }

    private bool IsImmediateForfeitVictoryPoint()
    {
        if (_host.CurrentState == GameState.EndPhase) return true;
        if (_host.CurrentState == GameState.OpeningPhase) return true;

        if (!_host.IsOnlineMatch) return false;

        return NetworkBattleBridge.IsWaitingForRemoteSelection();
    }

    private void ShowLeavingCautionPopup()
    {
        if (_leavingPopupInstance != null) return;

        var prefab = Resources.Load<GameObject>("Prefab/LeavingCaution");
        if (prefab == null)
        {
            Debug.LogError("[BattleExit] LeavingCaution prefab not found");
            return;
        }

        Canvas canvas = BattleUIManager.I != null ? BattleUIManager.I.GetPopupCanvas() : null;
        if (canvas == null)
            canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
        if (canvas == null)
        {
            Debug.LogError("[BattleExit] Popup canvas not found");
            return;
        }

        _host.CloseBlockingBattlePopups();

        _leavingPopupInstance = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
        var popup = _leavingPopupInstance.GetComponent<LeavingCautionPopup>();
        if (popup == null)
            popup = _leavingPopupInstance.AddComponent<LeavingCautionPopup>();

        popup.Setup(
            onConfirm: () =>
            {
                _leavingPopupInstance = null;
                _ = ConfirmOnlineForfeitAndExitAsync();
            },
            onCancel: () => { _leavingPopupInstance = null; });
    }

    private void DestroyLeavingPopup()
    {
        if (_leavingPopupInstance == null) return;
        UnityEngine.Object.Destroy(_leavingPopupInstance);
        _leavingPopupInstance = null;
    }

    private async Task ExitCpuBattleToMainAsync()
    {
        if (_exitInProgress) return;
        _exitInProgress = true;

        Debug.Log("[BattleExit] CPU battle — returning to Main");
        UnblockBattleLocally();
        await FadeToMainSceneAsync();
    }

    private async Task ConfirmOnlineForfeitAndExitAsync()
    {
        if (_exitInProgress || _host.IsGameEndTriggered) return;
        _exitInProgress = true;
        _host.ClearOpponentForfeitPending();

        Debug.Log("[BattleExit] Online forfeit confirmed — defeat recorded, returning to Main");
        UnblockBattleLocally();
        ApplyForfeitDefeatRecord();

        if (_host.IsOnlineMatch)
        {
            NetworkBattleBridge.SendForfeit();
            try
            {
                await Task.Delay(150);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }

            MatchmakingService.EndOnlineSession();
        }

        await FadeToMainSceneAsync();
    }

    private void UnblockBattleLocally()
    {
        _host.CloseBlockingBattlePopups();
        DestroyLeavingPopup();
        _host.PhaseController.CancelActivePhase();
        if (_host.IsOnlineMatch)
            NetworkBattleBridge.CancelPendingWaits();
        _host.PlayerInput.ResetAllLocks();
        BattleUIManager.I?.SetHandClickable(false);
    }

    private static void ApplyForfeitDefeatRecord()
    {
        PlayerProfileService.EnsureLoaded();
        int preRp = GameProfile.I != null
            ? GameProfile.I.PreBattleRP
            : Mathf.Max(0, PlayerProfileService.Data.currentRp);
        var bundle = BattleResultRpRules.GetBundle(GameResultController.ResultKind.Defeat);
        int afterRp = Mathf.Max(0, preRp + bundle.Total);

        string summonId = "unknown";
        if (BattleManager.I != null)
        {
            var ps = BattleManager.I.GetPlayerStatus();
            if (ps?.summonData != null)
                summonId = ps.summonData.name;
        }

        PlayerProfileService.RecordMatchEnd(GameResultController.ResultKind.Defeat, summonId, afterRp);
        GameProfile.I?.SetCurrentRpAfterBattleResult(afterRp);
    }

    private static async Task FadeToMainSceneAsync()
    {
        try
        {
            await Task.Yield();
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (SceneTransitionManager.I != null)
            SceneTransitionManager.I.FadeToScene(MainSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(MainSceneName);
    }
}
