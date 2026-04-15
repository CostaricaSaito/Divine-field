using UnityEngine;

/// <summary>
/// 攻撃フェーズ終了時（TurnEnd 突入直後）の病系ランダム処理のパラメータ。
/// </summary>
[CreateAssetMenu(fileName = "DiseaseTurnEndSettings", menuName = "DivineField/Status/Disease Turn End Settings")]
public sealed class DiseaseTurnEndSettings : ScriptableObject
{
    [Range(0f, 1f)] public float worsenChance = 0.05f;
    [Range(0f, 1f)] public float ecstasyChance = 0.10f;
    [Min(0)] public int paradiseHealAmount = 5;

    [Header("UI タイミング（ms）")]
    [Min(0)] public int messageToValueDelayMs = 700;
    [Min(0)] public int paradiseEcstasyShatterDelayMs = 400;
    [Min(0)] public int paradiseEcstasyShatterDurationMs = 600;
}
