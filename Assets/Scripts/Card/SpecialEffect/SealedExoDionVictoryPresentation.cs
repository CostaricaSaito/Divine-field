using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Sealed EXO-DION counter-3 victory presentation (jewel flash -> Important -> flash+armor -> GAMESET).
/// </summary>
public static class SealedExoDionVictoryPresentation
{
    public const string ImportantSoundAddress = "Assets/SE/天変地異/現実改変.wav";
    public const string ArmorSpriteAddress = "Assets/Images/06_UIパーツ/騎神装甲.png";
    public const string PanelJewelFlashMenuSeAddress = "Assets/SE/メニューを開く3.mp3";
    public const string ArmorSirenSeAddress = "Assets/SE/天変地異/騎神装甲サイレン.wav";

    private const int MsAfterThirdStack = 500;
    private const float PanelJewelFlashSeconds = 0.4f;
    private const int MsAfterImportant = 500;
    private const float WhiteFlashMs = 100f;
    private const int MsBeforeGameSet = 1000;

    public static async Task PlayAsync(CancellationToken ct)
    {
        var ui = BattleUIManager.I;
        if (ui == null) return;

        await Task.Delay(MsAfterThirdStack, ct);

        SoundEffectPlayer.I?.Play(PanelJewelFlashMenuSeAddress);
        await ui.PlaySealedExoDionMagicPanelRedTintAsync(PanelJewelFlashSeconds, ct);

        float introLife = ui.ShowStyledImportantPopup(
            ImportantPopupKind.SealedExoDionUnsealed,
            null,
            Side.Player);
        if (introLife <= 0f) introLife = ImportantPopup.DefaultSequenceLifetimeIfUnknown;
        await ImportantPopupSettings.WaitAfterLifetimeAsync(introLife, ct);
        if (ct.IsCancellationRequested) return;

        await Task.Delay(MsAfterImportant, ct);

        await ui.PlaySealedExoDionVictoryFlashAndCinematicAsync(WhiteFlashMs, ct);
    }

    public static int PostWhiteoutDelayBeforeGameSetMs => MsBeforeGameSet;
}
