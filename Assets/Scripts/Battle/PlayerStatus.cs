using UnityEngine;
using System.Collections.Generic;


[System.Serializable]
public class PlayerStatus
{
    public string DisplayName { get; private set; } = "プレイヤー";

    public void InitializeAsPlayer()
    {
        DisplayName = (GameProfile.I != null) ? GameProfile.I.PlayerName : "プレイヤー";
    }

    public void InitializeAsEnemy()
    {
        DisplayName = (GameProfile.I != null) ? GameProfile.I.EnemyName : "対敵者";
    }

    public void SetSummonData(SummonData data)
    {
        summonData = data;
    }

    public int maxHP = 99;
    public int maxMP = 99;
    public int maxGP = 99;

    public int currentHP = 50;
    public int currentMP = 50;
    public int currentGP = 50;

    public SummonData summonData;

    public List<IStatusEffect> activeEffects = new List<IStatusEffect>();     // 状態異常一覧


    // ダメージ処理（状態異常による修正あり）
    public void TakeDamage(int amount)
    {
        int modifiedAmount = amount;
        foreach (var effect in activeEffects)
        {
            modifiedAmount = effect.ModifyDamage(modifiedAmount);
        }

        currentHP = Mathf.Max(currentHP - modifiedAmount, 0);
        Debug.Log($"{DisplayName} に {modifiedAmount} ダメージ（元値: {amount}）");
    }

    public void UseMP(int amount)
    {
        currentMP = Mathf.Max(currentMP - amount, 0);
    }

    public void UseGP(int amount)
    {
        currentGP = Mathf.Max(currentGP - amount, 0);
    }

    public bool IsDead()
    {
        return currentHP <= 0;
    }

    // 状態異常の追加
    public void AddStatusEffect(StatusEffectType type)
    {
        foreach (var effect in activeEffects)
        {
            if (effect.EffectType == type)
            {
                Debug.Log($"{DisplayName} はすでに {type} を持っています");
                return;
            }
        }

        var newEffect = StatusEffectFactory.Create(type);
        if (newEffect != null)
        {
            activeEffects.Add(newEffect);
            Debug.Log($"{DisplayName} に状態異常 {newEffect.GetEffectName()} を付与しました");
        }
    }

    // 毎ターンの状態異常評価（BattleManager側で呼ぶ想定）
    public void OnTurnStart()
    {
        foreach (var effect in activeEffects)
        {
            effect.OnTurnStart(this);
        }

        activeEffects.RemoveAll(e =>
        {
            if (e.IsExpired())
            {
                e.OnRemove(this);
                Debug.Log($"{DisplayName} の {e.GetEffectName()} は終了しました");
                return true;
            }
            return false;
        });
    }
}