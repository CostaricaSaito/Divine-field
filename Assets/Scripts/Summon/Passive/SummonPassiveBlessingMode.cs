/// <summary>
/// <see cref="SummonData"/> の加護の決め方。SerializeReference の代わりにインスペクター互換の enum で指定する。
/// </summary>
public enum SummonPassiveBlessingMode
{
    /// <summary>アセット名（Ifrit など）から <see cref="SummonPassiveBlessingFallback"/> で決定</summary>
    AutoByAssetName = 0,
    /// <summary>加護なし（検証用）</summary>
    None = 1,
    /// <summary>イフリートの加護を明示指定</summary>
    Ifrit = 2,
    /// <summary>ガルーダ（開始時・ターン終了ライフサイクル。攻撃加護は別）</summary>
    Garuda = 3,
    /// <summary>リヴァイアサン（被ダメージ軽減。攻撃加護は別）</summary>
    Leviathan = 4,
    /// <summary>ディアボロス（開幕ダークプリパレーション。戦闘数値加護は別・ライフサイクル）</summary>
    Diabolos = 5,
    /// <summary>インドラ（5n ターン終了で相手手札破壊）</summary>
    Indra = 6,
    /// <summary>シヴァ（直接攻撃で5%凍結。数値加護は別・戦闘解決フック）</summary>
    Shiva = 7,
    /// <summary>アルカディアス（攻撃フェーズ Primary 等を必中。命中率は <see cref="HitRateRules"/>）</summary>
    Arcadias = 8,
    /// <summary>オーディン（切り払い：無属性物理攻撃を5%で自動反射）</summary>
    Ordin = 9,
}
