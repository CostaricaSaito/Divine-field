using UnityEngine;

/// <summary>
/// 状態異常の「付与時」ルール（病系段階・眼精／群発・封印の既定ターン数など）。
/// BattleManager / BattleProcessor にアサインして Inspector で調整する。
/// </summary>
[CreateAssetMenu(fileName = "StatusProgressionConfig", menuName = "DivineField/Status/Status Progression Config")]
public sealed class StatusProgressionConfig : ScriptableObject
{
    [Header("封印（期限付き）")]
    [Min(1)] public int defaultSealDurationTurns = 2;

    [Header("病系（段階付与）")]
    [Tooltip("楽園病中に病系（病・重病・煉獄・楽園のいずれか）が付与されたとき、強制絶頂（残りHP相当・状態異常の被ダメ軽減なし）を発生させる")]
    public bool paradisePlusSicknessForcesEcstasy = true;

    [Header("眼精疲労 / 群発頭痛")]
    [Tooltip("眼精疲労が既にある状態で、再度眼精疲労が付与されたとき群発頭痛に進行させる")]
    public bool eyeStrainDuplicateEscalatesToCluster = true;

    [Tooltip("片方のみ所持。もう一方を付与すると入れ替わる（排他）")]
    public bool eyeClusterMutuallyExclusive = true;

    private static StatusProgressionConfig _runtimeFallback;

    /// <summary>アセット未設定時に使うランタイム用インスタンス（フィールド初期値がそのまま使われる）。</summary>
    public static StatusProgressionConfig GetRuntimeFallback()
    {
        if (_runtimeFallback == null)
        {
            _runtimeFallback = CreateInstance<StatusProgressionConfig>();
            _runtimeFallback.name = "StatusProgressionConfig (Runtime Fallback)";
        }
        return _runtimeFallback;
    }
}
