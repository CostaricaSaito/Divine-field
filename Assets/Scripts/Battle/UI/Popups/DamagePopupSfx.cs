/// <summary>ダメージポップ同伴 SE の閾値ほか（クリップは <see cref="SoundEffectCatalogSO"/>）。</summary>
public static class DamagePopupSfx
{
    /// <summary>この値以下で軽ダメージ SE（<see cref="SoundEffectCatalogSO"/> の <c>battleDamageLow</c>）。</summary>
    public const int LowDamageMax = 5;

    /// <summary>この値以上で大ダメージ SE（<c>battleDamageHigh</c>）。その間は <c>battleDamageMid</c>。</summary>
    public const int HighDamageMin = 30;

    /// <summary>HP 回復ポップ表示時。</summary>
    public const string HealHp = "Assets/SE/power09(DFHP回復).wav";

    /// <summary>MP 回復ポップ表示時。</summary>
    public const string HealMp = "Assets/SE/StatusChange/MP回復.mp3";

    /// <summary>GP 回復ポップ表示時。</summary>
    public const string HealGp = "Assets/SE/StatusChange/GP回復.mp3";
}
