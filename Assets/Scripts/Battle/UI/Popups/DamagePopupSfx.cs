/// <summary>ダメージポップアップ同時再生 SE（<see cref="SoundEffectPlayer"/> の Addressables キー）。</summary>
public static class DamagePopupSfx
{
    /// <summary>通常ヒット音（1〜29 ダメージ、および病系などの同伴 SE）。</summary>
    public const string Slash = "Assets/SE/剣で斬る1.mp3";

    /// <summary>大ダメージ用（<see cref="HighDamageMin"/> 以上）。</summary>
    public const string Explosion = "Assets/SE/爆発2.mp3";

    /// <summary>この値以上の最終ダメージで <see cref="Explosion"/>、未満で <see cref="Slash"/>。</summary>
    public const int HighDamageMin = 30;

    /// <summary>HP 回復ポップ表示時。</summary>
    public const string HealHp = "Assets/SE/power09(DFHP回復).wav";

    /// <summary>MP 回復ポップ表示時。</summary>
    public const string HealMp = "Assets/SE/StatusChange/MP回復.mp3";

    /// <summary>GP 回復ポップ表示時。</summary>
    public const string HealGp = "Assets/SE/StatusChange/GP回復.mp3";
}
