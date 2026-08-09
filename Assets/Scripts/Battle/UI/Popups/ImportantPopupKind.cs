/// <summary>
/// Styled popups shown via <see cref="ImportantPopup"/> (rise + hold + fade).
/// </summary>
public enum ImportantPopupKind
{
    RuntimeCustom = 0,

    /// <summary>天変地異共通オープニング「空は裂け、大地が震える」。</summary>
    DisasterIntro = 1,

    DisasterEruption = 10,
    DisasterSolarEclipse = 11,
    DisasterLunarEclipse = 12,
    DisasterKannaduki = 13,
    DisasterBlackMonday = 14,
    DisasterRealityBending = 15,
    DisasterRampageZantetsuken = 16,
    DisasterMiracleArk = 17,
    DisasterManaStream = 18,
    DisasterChaosAttractor = 19,
    DisasterInfection = 20,

    ArchMagicCast = 30,
    ArchMagicFocus = 31,
    ArchMagicRelease = 32,

    /// <summary>Bahamut passive Mega Flare announcement.</summary>
    MegaFlare = 40,
}
