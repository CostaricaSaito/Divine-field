using UnityEngine;

[CreateAssetMenu(fileName = "OrbOfBastionRule", menuName = "DivineField/Card/Orb/Orb Of Bastion Rule")]
public class OrbOfBastionRuleSO : OrbCardRuleSO
{
    public override bool IsDamageCounter => true;

    public override bool CounterAttackIsNoneElement => true;

    /// <summary>受けたダメージの 1.5 倍を切り上げた値。</summary>
    public override int ScaleReceivedDamageToAttackBase(int receivedDamage)
    {
        if (receivedDamage <= 0) return 0;
        return (receivedDamage * 3 + 1) / 2;
    }
}
