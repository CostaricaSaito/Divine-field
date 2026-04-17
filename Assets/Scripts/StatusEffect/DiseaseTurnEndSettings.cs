using UnityEngine;

/// <summary>
/// 攻撃フェーズ終了時（TurnEnd 突入直後）の病系ランダム処理のパラメータ。
/// </summary>
[CreateAssetMenu(fileName = "DiseaseTurnEndSettings", menuName = "DivineField/Status/Disease Turn End Settings")]
public sealed class DiseaseTurnEndSettings : ScriptableObject
{
    [Range(0f, 1f)] public float worsenChance = 0.05f;

    [Header("デバッグ")]
    [Tooltip("オン時は自然進行判定を常に成功（悪化100%）。本番ビルド前にオフに戻すこと。")]
    public bool debugAlwaysWorsenNaturalProgress;
    [Tooltip("楽園病の各ターン終了で絶頂（即死級）になる確率。煉獄病→楽園病へ自然進行した当ターンは振らない。")]
    [Range(0f, 1f)] public float ecstasyChance = 0.10f;
    [Min(0)] public int paradiseHealAmount = 5;

    [Header("UI タイミング（ms）")]
    [Min(0)] public int paradiseEcstasyShatterDelayMs = 400;
    [Min(0)] public int paradiseEcstasyShatterDurationMs = 600;

    [Header("病・自然進行演出")]
    [Tooltip("第1文言（病が体を蝕む）が浮上する時間。DamagePopup.fadeDuration と揃えると「アニメ完了＝停止」の体感になりやすい。")]
    [Min(0.05f)] public float diseaseWorsenPhase1FloatSeconds = 1f;
    [Tooltip("第1→第2文言のリール切り替えにかける秒数。")]
    [Min(0.05f)] public float diseaseWorsenReelDurationSeconds = 0.35f;
    [Tooltip("第1文言の移動停止から、リール開始までのインターバル（秒）。")]
    [Min(0f)] public float diseaseWorsenPauseBeforeReelSeconds = 0.3f;
}
