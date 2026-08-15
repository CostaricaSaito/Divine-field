using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main の RankMatchPopup と同じオンラインマッチング → Battle 遷移フロー。
/// </summary>
public static class RankMatchmakingFlow
{
    public static async Task<bool> RunAndEnterBattleAsync(
        Transform overlayParent,
        CancellationTokenSource matchmakingCts,
        Action<MatchingOverlayView> onOverlayCreated = null)
    {
        if (overlayParent == null || matchmakingCts == null)
            return false;

        MatchmakingService.EndOnlineSession();

        MatchingOverlayView overlay = null;
        try
        {
            overlay = MatchingOverlayView.Show(overlayParent, () => matchmakingCts.Cancel());
            onOverlayCreated?.Invoke(overlay);
            overlay.SetStatus("サーバに接続しています");

            var progress = new Progress<string>(status => overlay?.SetStatus(status));
            bool matched = await MatchmakingService.FindMatchAsync(matchmakingCts.Token, progress);
            if (!matched)
            {
                overlay?.Close();
                return false;
            }

            overlay.SetCancelInteractable(false);
            overlay.SetStatus($"{OnlineMatchContext.RemotePlayerName} と対戦！");
            await Task.Delay(800, matchmakingCts.Token);

            if (!SceneFadeNavigation.TryFadeToScene("Battle"))
                SceneManager.LoadScene("Battle");

            return true;
        }
        catch (OperationCanceledException)
        {
            overlay?.Close();
            return false;
        }
    }
}
