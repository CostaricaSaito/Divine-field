using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 攻撃フェーズ終了時（TurnEnd 突入直後）に、攻撃側の病系状態異常を処理する。
/// </summary>
public static class DiseaseTurnEndProcessor
{
    private const float WorsenChance = 0.05f;
    private const float EcstasyChance = 0.10f;
    private const int ParadiseHealAmount = 5;

    /// <summary>メッセージと数値ポップの間隔（DamagePopup のフェードに合わせる）</summary>
    private const int MessageToValueDelayMs = 700;

    public static async Task ProcessForAttackerAsync(PlayerStatus attacker, CancellationToken ct)
    {
        if (attacker == null) return;

        StatusEffectType stage = FindDiseaseStage(attacker);
        if (stage == StatusEffectType.None) return;

        var ui = BattleUIManager.I;
        if (ui == null)
        {
            Debug.LogWarning("[DiseaseTurnEndProcessor] BattleUIManager.I が null のため病系処理をスキップします");
            return;
        }

        // 5% で悪化（楽園病では段階アップなし）
        if (stage != StatusEffectType.ParadiseSickness && UnityEngine.Random.value < WorsenChance)
        {
            StatusEffectType next = DiseaseLineEffect.GetNextStage(stage);
            if (next != StatusEffectType.None)
            {
                ReplaceDiseaseStage(attacker, next);
                stage = next;
            }
        }

        if (stage == StatusEffectType.ParadiseSickness)
        {
            await ProcessParadiseAsync(attacker, ui, ct);
            return;
        }

        await ProcessDamageStagesAsync(attacker, stage, ui, ct);
    }

    private static StatusEffectType FindDiseaseStage(PlayerStatus status)
    {
        foreach (var e in status.activeEffects)
        {
            if (e == null) continue;
            if (DiseaseLineEffect.IsDiseaseFamily(e.EffectType))
                return e.EffectType;
        }
        return StatusEffectType.None;
    }

    private static void ReplaceDiseaseStage(PlayerStatus status, StatusEffectType newStage)
    {
        status.activeEffects.RemoveAll(e => e != null && DiseaseLineEffect.IsDiseaseFamily(e.EffectType));
        status.activeEffects.Add(new DiseaseLineEffect(newStage));
        Debug.Log($"[DiseaseTurnEndProcessor] 病系が悪化: {newStage}");
    }

    private static async Task ProcessDamageStagesAsync(PlayerStatus attacker, StatusEffectType stage, BattleUIManager ui, CancellationToken ct)
    {
        int damage = stage switch
        {
            StatusEffectType.Sickness => 1,
            StatusEffectType.SevereSickness => 3,
            StatusEffectType.PurgatorySickness => 5,
            _ => 0
        };
        if (damage <= 0) return;

        ui.ShowMessagePopupForTarget(attacker, "病が体を蝕む！", Color.white);
        await Task.Delay(MessageToValueDelayMs, ct);

        ApplyHpLossIgnoringCardModifiers(attacker, damage);
        ui.ShowDamagePopup(damage, attacker);
        RefreshStatuses();
        await Task.Delay(MessageToValueDelayMs, ct);
    }

    private static async Task ProcessParadiseAsync(PlayerStatus attacker, BattleUIManager ui, CancellationToken ct)
    {
        if (UnityEngine.Random.value < EcstasyChance)
        {
            // 絶頂：ヘブン回復の代わりに即死級ダメージ（砕け散る演出はフェーズ4で差し替え予定のプレースホルダー）
            await Task.Delay(400, ct);
            await ShatterPlaceholderAsync(ct);

            ui.ShowMessagePopupForTarget(attacker, "絶頂", new Color(0.9f, 0.1f, 0.1f));
            await Task.Delay(MessageToValueDelayMs, ct);

            int lethal = attacker.currentHP;
            ApplyHpLossIgnoringCardModifiers(attacker, lethal);
            ui.ShowDamagePopup(lethal, attacker);
            RefreshStatuses();
            await Task.Delay(MessageToValueDelayMs, ct);
            return;
        }

        ui.ShowMessagePopupForTarget(attacker, "ヘブン状態！", new Color(1f, 0.6f, 0.95f));
        await Task.Delay(MessageToValueDelayMs, ct);

        int oldHp = attacker.currentHP;
        attacker.currentHP = Mathf.Min(attacker.maxHP, attacker.currentHP + ParadiseHealAmount);
        int healed = attacker.currentHP - oldHp;
        if (healed > 0)
            ui.ShowHealPopup(healed, "HP", attacker);

        RefreshStatuses();
        await Task.Delay(MessageToValueDelayMs, ct);
    }

    private static void RefreshStatuses()
    {
        var bm = BattleManager.I;
        if (bm == null || BattleUIManager.I == null) return;
        BattleUIManager.I.UpdateStatus(bm.GetPlayerStatus(), bm.GetEnemyStatus());
    }

    private static Task ShatterPlaceholderAsync(CancellationToken ct)
    {
        // TODO: 楽園病専用の砕け散る VFX（フェーズ4）
        return Task.Delay(600, ct);
    }

    /// <summary>衰弱などの ModifyDamage を経由せず、病系ターン終了ダメージのみを適用する。</summary>
    private static void ApplyHpLossIgnoringCardModifiers(PlayerStatus target, int amount)
    {
        if (target == null || amount <= 0) return;
        target.currentHP = Mathf.Max(0, target.currentHP - amount);
    }
}
