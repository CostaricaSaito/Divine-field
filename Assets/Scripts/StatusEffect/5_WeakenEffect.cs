using UnityEngine;

/// <summary>
/// 衰弱状態：相手に与えるダメージが半分になる
/// </summary>
/// 
[CreateAssetMenu(fileName = "05_WeakenEffect", menuName = "DivineField/StatusEffects/Weaken")]
public class WeakenEffect : ScriptableObject, IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.Weaken;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} に『衰弱』が付与されました。");
    }

    public int ModifyDamage(int originalDamage)
    {
        return Mathf.FloorToInt(originalDamage * 0.5f);
    }

    public void OnTurnStart(PlayerStatus target)
    {
        // 継続効果なし
    }

    public void OnRemove(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} の『衰弱』が解除されました。");
    }

    public bool IsExpired()
    {
        // 恒久的ではないならカウント管理なども
        return false;
    }

    public string GetEffectName() => "衰弱";

    public string GetDescription() => "相手に与えるダメージが半減する。";
}