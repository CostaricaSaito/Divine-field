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

        StatusEffectType stageBeforeWorsen = stage;
        bool diseaseNaturalProgressionOccurred = false;

        bool worsenRoll =
            s.debugAlwaysWorsenNaturalProgress
            || BattleRandom.Value < s.worsenChance;

        if (stage != StatusEffectType.ParadiseSickness && worsenRoll)
        {
            StatusEffectType next = DiseaseLineEffect.GetNextStage(stage);
            if (next != StatusEffectType.None)
            {
                ReplaceDiseaseStage(attacker, next);
                stage = next;
                diseaseNaturalProgressionOccurred = true;
            }
        }

        bool skipEcstasyBecausePurgatoryToParadise =
            stageBeforeWorsen == StatusEffectType.PurgatorySickness
            && stage == StatusEffectType.ParadiseSickness;

        if (stage == StatusEffectType.ParadiseSickness)
        {
            await ProcessParadiseAsync(attacker, ui, ct,
                skipEcstasyRoll: skipEcstasyBecausePurgatoryToParadise,
                showPurgatoryToParadiseProgressionIntro: diseaseNaturalProgressionOccurred && skipEcstasyBecausePurgatoryToParadise);
            return;
        }

        await ProcessDamageStagesAsync(attacker, stage, ui, diseaseNaturalProgressionOccurred, ct);
    }

    /// <summary>
    /// 自然進行時：第1「病が体を蝕む」→停止→リール→第2文言→規定インターバルまで（第1ポップアップは破棄される）。
    /// </summary>
    private static async Task RunDiseaseNaturalProgressIntroAsync(
        PlayerStatus attacker,
        BattleUIManager ui,
        MessagePopupKind secondLineKind,
        CancellationToken ct)
    {
        var s = Active;
        var settings = MessagePopupSettings.GetRuntimeFallback();
        var phase1Entry = settings.GetEntryOrDefault(MessagePopupKind.DiseaseErodeBody);
        var secondLineEntry = settings.GetEntryOrDefault(secondLineKind);

        MessagePopup popup = ui.SpawnMessagePopupForTarget(attacker, MessagePopupKind.DiseaseErodeBody);
        if (popup == null)
        {
            ui.ShowStyledMessagePopup(attacker, MessagePopupKind.DiseaseErodeBody);
            SoundEffectPlayer.I?.Play("Assets/SE/メニューを開く2.mp3");
            await Task.Delay(TimeSpan.FromSeconds(Mathf.Max(0.1f, s.diseaseWorsenPhase1FloatSeconds)), ct);
            await Task.Delay(TimeSpan.FromSeconds(s.diseaseWorsenPauseBeforeReelSeconds), ct);
            PlaySecondLineDiseaseIntroSound(secondLineKind);
            ui.ShowStyledMessagePopup(attacker, secondLineKind);
            await Task.Delay(TimeSpan.FromSeconds(PostSecondLineHoldSecondsBeforePopupResolves(secondLineKind)), ct);
            return;
        }

        SoundEffectPlayer.I?.Play("Assets/SE/メニューを開く2.mp3");
        await popup.BeginDiseaseWorsenPhase1AndGetTask(phase1Entry, s.diseaseWorsenPhase1FloatSeconds);

        await Task.Delay(TimeSpan.FromSeconds(s.diseaseWorsenPauseBeforeReelSeconds), ct);

        PlaySecondLineDiseaseIntroSound(secondLineKind);

        await popup.RunDiseaseReelSecondLinePostIntervalAndDestroyAsync(
            secondLineEntry,
            s.diseaseWorsenReelDurationSeconds,
            PostSecondLineHoldSecondsBeforePopupResolves(secondLineKind),
            ct);
    }

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

        float ecstasyMsgFade = ui.ShowMessagePopupForTarget(attacker, "絶頂", new Color(0.9f, 0.1f, 0.1f));
        await DamagePopup.WaitAfterPopupLifetimeAsync(ecstasyMsgFade, ct);

        int lethal = attacker.currentHP;
        ApplyHpLossIgnoringCardModifiers(attacker, lethal);
        float lethalFade = ui.ShowDamagePopup(lethal, attacker);
        RefreshStatuses();
        await DamagePopup.WaitAfterPopupLifetimeAsync(lethalFade, ct);

        if (BattleManager.I != null)
            await BattleManager.I.TryHandleDeathIfAnyAsync(ct);
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

    private static float PostSecondLineHoldSecondsBeforePopupResolves(MessagePopupKind secondLineKind)
    {
        float baseSec = DamagePopup.PostPopupIntervalMs / 1000f;
        return secondLineKind == MessagePopupKind.DiseaseWorsened ? baseSec * 2f : baseSec;
    }

    private static void PlaySecondLineDiseaseIntroSound(MessagePopupKind secondLineKind)
    {
        if (secondLineKind == MessagePopupKind.DiseaseWorsened)
            SoundEffectPlayer.I?.Play("Assets/SE/病ダメージ.mp3");
        else if (secondLineKind == MessagePopupKind.DiseasePoisonFlipped)
            SoundEffectPlayer.I?.Play("Assets/SE/きらーん1.mp3");
    }

    private static void ReplaceDiseaseStage(PlayerStatus status, StatusEffectType newStage)
    {
        status.activeEffects.RemoveAll(e => e != null && DiseaseLineEffect.IsDiseaseFamily(e.EffectType));
        status.activeEffects.Add(new DiseaseLineEffect(newStage));
        Debug.Log($"[DiseaseTurnEndProcessor] 病系が悪化: {newStage}");
    }

    private static async Task ProcessDamageStagesAsync(
        PlayerStatus attacker,
        StatusEffectType stage,
        BattleUIManager ui,
        bool diseaseNaturalProgressionOccurred,
        CancellationToken ct)
    {
        int damage = stage switch
        {
            StatusEffectType.Sickness => 1,
            StatusEffectType.SevereSickness => 3,
            StatusEffectType.PurgatorySickness => 5,
            _ => 0
        };
        if (damage <= 0) return;

        if (diseaseNaturalProgressionOccurred)
        {
            await RunDiseaseNaturalProgressIntroAsync(attacker, ui, MessagePopupKind.DiseaseWorsened, ct);
        }
        else
        {
            float diseaseMsgFade = ui.ShowStyledMessagePopup(attacker, MessagePopupKind.DiseaseErodeBody);
            SoundEffectPlayer.I?.Play("Assets/SE/メニューを開く2.mp3");
            await DamagePopup.WaitAfterPopupLifetimeAsync(diseaseMsgFade, ct);
        }

        ApplyHpLossIgnoringCardModifiers(attacker, damage);
        float diseaseDmgFade = ui.ShowDamagePopup(damage, attacker);
        BattleProcessor.I?.PlayDamagePopupCompanionSound(damage);
        RefreshStatuses();
        await DamagePopup.WaitAfterPopupLifetimeAsync(diseaseDmgFade, ct);

        if (BattleManager.I != null)
            await BattleManager.I.TryHandleDeathIfAnyAsync(ct);
    }

    private static async Task ProcessParadiseAsync(
        PlayerStatus attacker,
        BattleUIManager ui,
        CancellationToken ct,
        bool skipEcstasyRoll = false,
        bool showPurgatoryToParadiseProgressionIntro = false)
    {
        var s = Active;
        if (!skipEcstasyRoll && BattleRandom.Value < s.ecstasyChance)
        {
            await Task.Delay(s.paradiseEcstasyShatterDelayMs, ct);
            await ShatterPlaceholderAsync(s.paradiseEcstasyShatterDurationMs, ct);

            float ecstasyMsgFade = ui.ShowMessagePopupForTarget(attacker, "絶頂", new Color(0.9f, 0.1f, 0.1f));
            await DamagePopup.WaitAfterPopupLifetimeAsync(ecstasyMsgFade, ct);

            int lethal = attacker.currentHP;
            ApplyHpLossIgnoringCardModifiers(attacker, lethal);
            float lethalFade = ui.ShowDamagePopup(lethal, attacker);
            RefreshStatuses();
            await DamagePopup.WaitAfterPopupLifetimeAsync(lethalFade, ct);

            if (BattleManager.I != null)
                await BattleManager.I.TryHandleDeathIfAnyAsync(ct);
            return;
        }

        if (showPurgatoryToParadiseProgressionIntro)
            await RunDiseaseNaturalProgressIntroAsync(attacker, ui, MessagePopupKind.DiseasePoisonFlipped, ct);

        float heavenMsgFade = ui.ShowStyledMessagePopup(attacker, MessagePopupKind.ParadiseHeavenState);
        await DamagePopup.WaitAfterPopupLifetimeAsync(heavenMsgFade, ct);

        int oldHp = attacker.currentHP;
        attacker.currentHP = Mathf.Min(attacker.maxHP, attacker.currentHP + s.paradiseHealAmount);
        int healed = attacker.currentHP - oldHp;
        float healFade = 0f;
        if (healed > 0)
            healFade = ui.ShowHealPopup(healed, "HP", attacker);

        RefreshStatuses();
        if (healed > 0)
            await DamagePopup.WaitAfterPopupLifetimeAsync(healFade, ct);
    }

    private static void RefreshStatuses()
    {
        var bm = BattleManager.I;
        if (bm == null || BattleUIManager.I == null) return;
        BattleUIManager.I.UpdateStatus(bm.GetPlayerStatus(), bm.GetEnemyStatus(), snapHpmgpNumbers: true);
    }

    private static Task ShatterPlaceholderAsync(int durationMs, CancellationToken ct)
    {
        return Task.Delay(Mathf.Max(0, durationMs), ct);
    }

    private static void ApplyHpLossIgnoringCardModifiers(PlayerStatus target, int amount)
    {
        if (target == null || amount <= 0) return;
        target.ApplyRawHpDamage(amount);
    }
}
