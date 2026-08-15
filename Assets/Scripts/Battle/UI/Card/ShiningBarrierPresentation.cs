using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Shining Barrier (光のバリア): popup after card sheet reveal, before attribute strip.
/// </summary>
public static class ShiningBarrierPresentation
{
    public const string EffectMessage = "光が属性を消し去る";
    public const string MessageSoundEffectPath = "Assets/SE/きらーん1.mp3";
    public static readonly Color MessageColor = new Color(1f, 0.95f, 0.55f);

    public static async Task RunAsync(PlayerStatus messageAnchor, CancellationToken ct)
    {
        var ui = BattleUIManager.I;
        if (ui == null || messageAnchor == null) return;

        SoundEffectPlayer.I?.Play(MessageSoundEffectPath);
        float fadeSec = ui.ShowMessagePopupForTarget(messageAnchor, EffectMessage, MessageColor);
        await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec, ct);
    }
}
