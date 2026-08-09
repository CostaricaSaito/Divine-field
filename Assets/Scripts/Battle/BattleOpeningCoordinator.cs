using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Battle opening sequence: deal, cut-in, first-turn determination.
/// </summary>
public sealed class BattleOpeningCoordinator
{
    private readonly IBattleOpeningHost _host;

    public BattleOpeningCoordinator(IBattleOpeningHost host)
    {
        _host = host;
    }

    public bool IsBattleOpeningSequenceComplete { get; private set; }

    public PlayerType OpeningTurnOwner { get; private set; } = PlayerType.Player;

    public async Task RunBattleStartSequenceAsync()
    {
        IsBattleOpeningSequenceComplete = false;

        GameProfile.I?.CaptureBattleStartRP();

        SummonGarudaLifecycle.GetOpeningHandTargets(
            _host.PlayerStatus, _host.EnemyStatus, out int openingPlayer, out int openingCpu);
        await RunOpeningDealAsync(openingPlayer, openingCpu);

        _host.UpdateBattleStatusUi();

        await Task.Delay(System.TimeSpan.FromSeconds(_host.CutInDelaySeconds));

        if (_host.CutInController != null)
        {
            var cutInTcs = new TaskCompletionSource<bool>();
            _host.CutInController.OnCutInComplete = () => cutInTcs.TrySetResult(true);
            _host.CutInController.PlayCutIn();
            await cutInTcs.Task;
        }

        _host.SetIntroModeUi();
        DetermineOpeningFirstTurn();
        IsBattleOpeningSequenceComplete = true;
        _host.SetGameState(GameState.StandByPhase);
    }

    private async Task RunOpeningDealAsync(int openingPlayer, int openingCpu)
    {
        var tcs = new TaskCompletionSource<bool>();
        _host.CoroutineRunner.StartCoroutine(
            OpeningDealBridge(openingPlayer, openingCpu, () => tcs.SetResult(true)));
        await tcs.Task;
    }

    private IEnumerator OpeningDealBridge(int openingPlayer, int openingCpu, System.Action onComplete)
    {
        yield return _host.CardDealer.DealOpeningHands(
            _host.PlayerHand, _host.CpuHand, openingPlayer, openingCpu);
        onComplete?.Invoke();
    }

    private void DetermineOpeningFirstTurn()
    {
        if (OnlineMatchContext.IsOnline)
        {
            var turnOwner = OnlineMatchContext.LocalPlayerGoesFirst
                ? PlayerType.Player
                : PlayerType.Enemy;
            _host.SetCurrentTurnOwner(turnOwner);
            OpeningTurnOwner = turnOwner;
            Debug.Log($"[BattleOpeningCoordinator] 先攻(オンライン): {turnOwner}");
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var dbg = _host.BattleDebugTools;
        var resolved = dbg != null ? dbg.ResolveOpeningTurnOwner() : RollRandomOpeningTurnOwner();
#else
        var resolved = RollRandomOpeningTurnOwner();
#endif
        _host.SetCurrentTurnOwner(resolved);
        OpeningTurnOwner = resolved;
        Debug.Log($"[BattleOpeningCoordinator] 先攻: {resolved}");
    }

    private static PlayerType RollRandomOpeningTurnOwner()
        => Random.Range(0, 2) == 0 ? PlayerType.Player : PlayerType.Enemy;
}
