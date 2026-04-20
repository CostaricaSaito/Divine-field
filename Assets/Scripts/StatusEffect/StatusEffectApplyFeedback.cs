/// <summary>
/// 状態異常付与成功時の UI／SE に関する共通判定。
/// <see cref="PlayerStatus.TryApplyStatusEffect"/> から呼び、付与元（カード・デバッグ等）を問わず同じフィードバックにする。
/// </summary>
public static class StatusEffectApplyFeedback
{
    /// <summary>付与ポップアップ＋共通 SE に使う Addressables キー（<see cref="SoundEffectPlayer"/>）。</summary>
    public const string GrantSoundAddress = "Assets/SE/メニューを開く2.mp3";

    /// <summary>
    /// 付与ポップアップを出してよい結果か（戦闘カード経路と同じ条件）。
    /// </summary>
    public static bool ShouldShowGrantPopup(ProgressiveApplyResult result)
    {
        return result == ProgressiveApplyResult.Applied
            || result == ProgressiveApplyResult.DiseaseProgressed
            || result == ProgressiveApplyResult.ForcedParadiseEcstasy;
    }

    /// <summary>
    /// ポップアップに使う公式タイプ。病系が段階進行したときは付与後の段階を返す。
    /// </summary>
    public static StatusEffectType GetGrantPopupEffectType(
        PlayerStatus target,
        StatusEffectType requested,
        ProgressiveApplyResult result)
    {
        if (result == ProgressiveApplyResult.DiseaseProgressed && target != null)
        {
            foreach (var e in target.activeEffects)
            {
                if (e != null && DiseaseLineEffect.IsDiseaseFamily(e.EffectType))
                    return e.EffectType;
            }
        }

        return requested;
    }
}
