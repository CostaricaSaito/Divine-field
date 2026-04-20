using System.Collections;
using UnityEngine;

/// <summary>
/// 暗黒の鬼神ディアボロス：加護「ダークプリパレーション」。
/// 開幕手札の先頭1枚を闇属性から抽選（<see cref="CardDealer.DealOpeningHands"/>）。
/// メッセージは配布完了後・表向け直前に表示する。
/// </summary>
public static class SummonDiabolosOpening
{
    public const string DarkPreparationMessage = "闇の力を我が手に";

    /// <summary>ポップアップ表示と同タイミングで鳴らす SE（Addressables）。</summary>
    public const string DarkPreparationSeAddress = "Assets/SE/魔の時計塔の鐘.mp3";

    /// <summary>
    /// 開幕配布が終わった直後、表向けの前に、該当する側へメッセージを順に表示して待機する。
    /// </summary>
    public static IEnumerator RunAfterDealBeforeRevealRoutine(PlayerStatus player, PlayerStatus enemy)
    {
        var ui = BattleUIManager.I;
        if (ui == null) yield break;

        bool showPlayer = player?.summonData != null && player.summonData.IsDiabolosDarkPreparation();
        bool showEnemy = enemy?.summonData != null && enemy.summonData.IsDiabolosDarkPreparation();
        if (!showPlayer && !showEnemy) yield break;

        Color messageColor = new Color(0.42f, 0.32f, 0.58f);

        if (showPlayer)
        {
            SoundEffectPlayer.I?.Play(DarkPreparationSeAddress);
            float fadeSec = ui.ShowMessagePopupForTarget(player, DarkPreparationMessage, messageColor);
            if (fadeSec <= 0f) fadeSec = DamagePopup.DefaultFadeDurationIfUnknown;
            yield return new WaitForSeconds(fadeSec);
            yield return new WaitForSeconds(DamagePopup.PostPopupIntervalMs / 1000f);
        }

        if (showEnemy)
        {
            SoundEffectPlayer.I?.Play(DarkPreparationSeAddress);
            float fadeSec = ui.ShowMessagePopupForTarget(enemy, DarkPreparationMessage, messageColor);
            if (fadeSec <= 0f) fadeSec = DamagePopup.DefaultFadeDurationIfUnknown;
            yield return new WaitForSeconds(fadeSec);
            yield return new WaitForSeconds(DamagePopup.PostPopupIntervalMs / 1000f);
        }
    }
}
