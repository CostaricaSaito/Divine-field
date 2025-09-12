using UnityEngine;

/// <summary>
/// 特殊カード「マジカル・エクスプロージョン」
/// 自分のMPをすべて消費し、その2倍の無属性ダメージを敵に与える
/// </summary>
[CreateAssetMenu(
    fileName = "01_MagicalExplosion",
    menuName = "DivineField/SpecialEffects/01_MagicalExplosion"
)]
public class MagicalExplosionEffect : SpecialCardEffectBase
{
    public override void Activate(PlayerStatus player, PlayerStatus enemy)
    {
        int mp = player.currentMP;
        int damage = mp * 2;

        player.currentMP = 0;

        Debug.Log($"『マジカル・エクスプロージョン』発動！ MP{mp}消費 → {damage}ダメージ（無属性）");

        enemy.TakeDamage(damage);
    }
}