using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 攻撃フェーズ終了時（TurnEnd 突入直後）に、攻撃側の病系状態異常を処理する。
/// 数値は <see cref="DiseaseTurnEndSettings"/>（Inspector）で変更する。
/// </summary>
public static class DiseaseTurnEndProcessor
{
    private static DiseaseTurnEndSettings _settings;

    /// <summary>バトル開始時に BattleManager などから登録。null のときはランタイム既定値。</summary>
    public static void BindSettings(DiseaseTurnEndSettings settings)
    {
        _settings = settings;
    }

    private static DiseaseTurnEndSettings Active
    {
        get
        {
            if (_settings != null) return _settings;
            if (_fallbackInstance == null)
            {
                _fallbackInstance = ScriptableObject.CreateInstance<DiseaseTurnEndSettings>();
                _fallbackInstance.name = "DiseaseTurnEndSettings (Runtime Fallback)";
            }
            return _fallbackInstance;
        }
    }

    private static DiseaseTurnEndSettings _fallbackInstance;

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

        var s = Active;

        if (stage != StatusEffectType.ParadiseSickness && UnityEngine.Random.value < s.worsenChance)
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

    /// <summary>
    /// 楽園病＋「病」付与など、ターン終了10%絶頂とは別ルートの強制絶頂（即死級ダメージ）。
    /// </summary>
    public static async Task ProcessForcedParadiseEcstasyAsync(PlayerStatus attacker, CancellationToken ct)
    {
        if (attacker == null) return;
        var ui = BattleUIManager.I;
        if (ui == null)
        {
            Debug.LogWarning("[DiseaseTurnEndProcessor] BattleUIManager.I が null のため強制絶頂をスキップします");
            return;
        }

        var s = Active;
        await Task.Delay(s.paradiseEcstasyShatterDelayMs, ct);
        await ShatterPlaceholderAsync(s.paradiseEcstasyShatterDurationMs, ct);

        ui.ShowMessagePopupForTarget(attacker, "絶頂", new Color(0.9f, 0.1f, 0.1f));
        await Task.Delay(TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown), ct);
        await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);

        int lethal = attacker.currentHP;
        ApplyHpLossIgnoringCardModifiers(attacker, lethal);
        ui.ShowDamagePopup(lethal, attacker);
        RefreshStatuses();
        await Task.Delay(TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown), ct);
        await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);
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

        var s = Active;
        // 2行表示（「病が」／「体を蝕む」）。ダメージ数値は通常の ShowDamagePopup を流用（病1／重病3／煉獄病5）。
        ui.ShowMessagePopupForTarget(attacker, "病が\n体を蝕む", Color.black);
        SoundEffectPlayer.I?.Play("Assets/SE/病ダメージ.mp3");
        await Task.Delay(TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown), ct);
        await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);

        ApplyHpLossIgnoringCardModifiers(attacker, damage);
        ui.ShowDamagePopup(damage, attacker);
        BattleProcessor.I?.PlayDamagePopupCompanionSound(damage);
        RefreshStatuses();
        await Task.Delay(TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown), ct);
        await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);
    }

    private static async Task ProcessParadiseAsync(PlayerStatus attacker, BattleUIManager ui, CancellationToken ct)
    {
        var s = Active;
        if (UnityEngine.Random.value < s.ecstasyChance)
        {
            await Task.Delay(s.paradiseEcstasyShatterDelayMs, ct);
            await ShatterPlaceholderAsync(s.paradiseEcstasyShatterDurationMs, ct);

            ui.ShowMessagePopupForTarget(attacker, "絶頂", new Color(0.9f, 0.1f, 0.1f));
            await Task.Delay(TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown), ct);
            await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);

            int lethal = attacker.currentHP;
            ApplyHpLossIgnoringCardModifiers(attacker, lethal);
            ui.ShowDamagePopup(lethal, attacker);
            RefreshStatuses();
            await Task.Delay(TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown), ct);
            await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);
            return;
        }

        ui.ShowMessagePopupForTarget(attacker, "ヘブン状態", new Color(1f, 0.6f, 0.95f));
        await Task.Delay(TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown), ct);
        await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);

        int oldHp = attacker.currentHP;
        attacker.currentHP = Mathf.Min(attacker.maxHP, attacker.currentHP + s.paradiseHealAmount);
        int healed = attacker.currentHP - oldHp;
        if (healed > 0)
            ui.ShowHealPopup(healed, "HP", attacker);

        RefreshStatuses();
        await Task.Delay(TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown), ct);
        await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);
    }

    private static void RefreshStatuses()
    {
        var bm = BattleManager.I;
        if (bm == null || BattleUIManager.I == null) return;
        BattleUIManager.I.UpdateStatus(bm.GetPlayerStatus(), bm.GetEnemyStatus());
    }

    private static Task ShatterPlaceholderAsync(int durationMs, CancellationToken ct)
    {
        return Task.Delay(Mathf.Max(0, durationMs), ct);
    }

    private static void ApplyHpLossIgnoringCardModifiers(PlayerStatus target, int amount)
    {
        if (target == null || amount <= 0) return;
        target.currentHP = Mathf.Max(0, target.currentHP - amount);
    }
}
