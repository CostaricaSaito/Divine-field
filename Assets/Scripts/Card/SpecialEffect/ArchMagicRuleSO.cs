using UnityEngine;

/// <summary>
/// 大魔法（ArchMagic）の仕様パラメータ。
/// <see cref="CardData.specialAttackRule"/> に刺して判別する。
/// 背景スプライトは任意（未指定なら背景切替なし）。
/// </summary>
[CreateAssetMenu(fileName = "ArchMagicRule", menuName = "DivineField/Special Attack/Arch Magic Rule")]
public class ArchMagicRuleSO : SpecialAttackRuleSO
{
    [Header("詠唱")]
    [Tooltip("詠唱ターン数。使用ターンの次の自分ターンから数え、0 になった自分ターンに発動する。")]
    [Min(1)] public int castTurns = 2;

    [Header("演出")]
    [Tooltip("詠唱中に両プレイヤー背景を差し替えるスプライト。null のときは背景を変更しない。")]
    public Sprite backgroundSprite;

    [Tooltip("解放ポップアップの表示名。未指定なら CardData.cardName をそのまま使う。")]
    public string displayNameForRelease = "";
}
