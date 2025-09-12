using UnityEngine;

/// <summary>
/// 状態異常の種類を定義する列挙体
/// </summary>
public enum StatusEffectType
{
    None,           // 状態異常なし（デフォルト）
    Sickness,         // 病：毎ターンダメージ（軽症）
    SevereSickness,   // 重病：大きな継続ダメージ
    PurgatorySickness,  // 煉獄病：最大級の継続ダメージ
    ParadiseSickness,   // 楽園病：回復+即死のリスク

    Weaken,         // 衰弱：攻撃力低下
    
    EyeStrain,      // 眼精疲労：MP消費2倍
    ClusterHeadache,// 群発頭痛：魔法使用不能

    Smoke,          // 煙幕：命中率低下
    Illusion,       // 幻覚：カードが変化
    Misfortune,     // 不運：敵の攻撃が必中

    Seal,           // 封印：行動不能（防御可）
    Fog,            // 濃霧：ステータス等非表示
    CurseBind,      // 呪縛：パッシブ無効

    Intervention,   // 介入：毎ターンランダムカードが発動
    Confusion,      // 混乱：対象がランダム
    Restraint       // 拘束：防御カード制限
}
