using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Bahamut Maelstrom: dual ImportantPopup, Reality Bending SE, white flash, then Kannaduki grant.
/// </summary>
public static class BahamutMaelstromPresentation
{
    public const string SoundAddress = "Assets/SE/天変地異/現実改変.wav";
    public const float WhiteFlashMs = 50f;

    public static async Task PlayAsync(PlayerStatus summoner, CancellationToken cancellationToken)
    {
        if (summoner == null) return;

        var ui = BattleUIManager.I;
        if (ui == null)
        {
            BahamutRules.GrantMaelstromKannaduki(summoner);
            return;
        }

        float lifePlayer = ui.ShowStyledImportantPopup(
            ImportantPopupKind.BahamutMaelstromAwakening,
            null,
            Side.Player,
            playShowSound: false);
        float lifeEnemy = ui.ShowStyledImportantPopup(
            ImportantPopupKind.BahamutMaelstromAwakening,
            null,
            Side.Enemy,
            playShowSound: false);
        float life = Mathf.Max(lifePlayer, lifeEnemy);
        if (life <= 0f) life = ImportantPopup.DefaultSequenceLifetimeIfUnknown;

        SoundEffectPlayer.I?.Play(SoundAddress);
        ui.PlayFullscreenWhiteFlashMs(WhiteFlashMs);

        await ImportantPopupSettings.WaitAfterLifetimeAsync(life, cancellationToken);
        if (cancellationToken.IsCancellationRequested) return;

        BahamutRules.GrantMaelstromKannaduki(summoner);

        var bm = BattleManager.I;
        if (bm != null)
            ui.UpdateStatus(bm.GetPlayerStatus(), bm.GetEnemyStatus());
    }
}
