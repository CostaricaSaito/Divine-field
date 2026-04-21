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
            || UnityEngine.Random.value < s.worsenChance;

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

        // 煉獄病→楽園病への自然進行直後は絶頂抽選を行わない（即死でバランスが崩れるのを防ぐ）。以降の楽園病ターンは従来どおり ecstasyChance。
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
        string secondLineMessage,
        CancellationToken ct)
    {
        var s = Active;
        DamagePopup dp = ui.SpawnDamagePopupForTarget(attacker);
        if (dp == null)
        {
            ui.ShowMessagePopupForTarget(attacker, "病が\n体を蝕む", Color.black);
            SoundEffectPlayer.I?.Play("Assets/SE/メニューを開く2.mp3");
            await Task.Delay(TimeSpan.FromSeconds(Mathf.Max(0.1f, s.diseaseWorsenPhase1FloatSeconds)), ct);
            await Task.Delay(TimeSpan.FromSeconds(s.diseaseWorsenPauseBeforeReelSeconds), ct);
            PlaySecondLineDiseaseIntroSound(secondLineMessage);
            ui.ShowMessagePopupForTarget(attacker, secondLineMessage, Color.black);
            await Task.Delay(TimeSpan.FromSeconds(PostSecondLineHoldSecondsBeforePopupResolves(secondLineMessage)), ct);
            return;
        }

        SoundEffectPlayer.I?.Play("Assets/SE/メニューを開く2.mp3");
        await dp.BeginDiseaseWorsenPhase1AndGetTask("病が\n体を蝕む", Color.black, s.diseaseWorsenPhase1FloatSeconds);

        await Task.Delay(TimeSpan.FromSeconds(s.diseaseWorsenPauseBeforeReelSeconds), ct);

        PlaySecondLineDiseaseIntroSound(secondLineMessage);

        await dp.RunDiseaseReelSecondLinePostIntervalAndDestroyAsync(
            secondLineMessage,
            Color.black,
            s.diseaseWorsenReelDurationSeconds,
            PostSecondLineHoldSecondsBeforePopupResolves(secondLineMessage),
            ct);
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

        float ecstasyMsgFade = ui.ShowMessagePopupForTarget(attacker, "絶頂", new Color(0.9f, 0.1f, 0.1f));
        await DamagePopup.WaitAfterPopupLifetimeAsync(ecstasyMsgFade, ct);

        int lethal = attacker.currentHP;
        ApplyHpLossIgnoringCardModifiers(attacker, lethal);
        float lethalFade = ui.ShowDamagePopup(lethal, attacker);
        RefreshStatuses();
        await DamagePopup.WaitAfterPopupLifetimeAsync(lethalFade, ct);
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

    /// <summary>
    /// 第2文言（体調が悪くなった／病が裏返った）表示後、ポップアップ破棄までの待ち秒。
    /// 「体調が悪くなった」のあとはダメージポップが続くため、規定 <see cref="DamagePopup.PostPopupIntervalMs"/> の2倍。
    /// </summary>
    private static float PostSecondLineHoldSecondsBeforePopupResolves(string secondLineMessage)
    {
        float baseSec = DamagePopup.PostPopupIntervalMs / 1000f;
        return secondLineMessage == "体調が悪くなった" ? baseSec * 2f : baseSec;
    }

    /// <summary>第2文言表示（リール開始）直前の SE。体調悪化は毒系、煉獄→楽園はきらーん。</summary>
    private static void PlaySecondLineDiseaseIntroSound(string secondLineMessage)
    {
        if (secondLineMessage == "体調が悪くなった")
            SoundEffectPlayer.I?.Play("Assets/SE/病ダメージ.mp3");
        else if (secondLineMessage == "病が裏返った")
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
            await RunDiseaseNaturalProgressIntroAsync(attacker, ui, "体調が悪くなった", ct);
        }
        else
        {
            // 2行表示（「病が」／「体を蝕む」）。ダメージ数値は通常の ShowDamagePopup を流用（病1／重病3／煉獄病5）。
            float diseaseMsgFade = ui.ShowMessagePopupForTarget(attacker, "病が\n体を蝕む", Color.black);
            SoundEffectPlayer.I?.Play("Assets/SE/メニューを開く2.mp3");
            await DamagePopup.WaitAfterPopupLifetimeAsync(diseaseMsgFade, ct);
        }

        ApplyHpLossIgnoringCardModifiers(attacker, damage);
        float diseaseDmgFade = ui.ShowDamagePopup(damage, attacker);
        BattleProcessor.I?.PlayDamagePopupCompanionSound(damage);
        RefreshStatuses();
        await DamagePopup.WaitAfterPopupLifetimeAsync(diseaseDmgFade, ct);
    }

    /// <param name="skipEcstasyRoll">煉獄→楽園に自然進行した当ターンは true。絶頂抽選をせずヘブン＋回復のみ。</param>
    /// <param name="showPurgatoryToParadiseProgressionIntro">煉獄→楽園への自然進行当ターン：蝕む→リール→「病が裏返った」まで。</param>
    private static async Task ProcessParadiseAsync(
        PlayerStatus attacker,
        BattleUIManager ui,
        CancellationToken ct,
        bool skipEcstasyRoll = false,
        bool showPurgatoryToParadiseProgressionIntro = false)
    {
        var s = Active;
        if (!skipEcstasyRoll && UnityEngine.Random.value < s.ecstasyChance)
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
            return;
        }

        if (showPurgatoryToParadiseProgressionIntro)
            await RunDiseaseNaturalProgressIntroAsync(attacker, ui, "病が裏返った", ct);

        float heavenMsgFade = ui.ShowMessagePopupForTarget(attacker, "ヘブン状態", new Color(1f, 0.6f, 0.95f));
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
