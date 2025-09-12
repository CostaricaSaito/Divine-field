public enum GameState
{
    // ① バトル準備・演出
    Intro,              // 初期手札配布、召喚獣カットイン、先攻後攻決定など

    // ② 先攻プレイヤーの行動選択
    TurnStart,                // ステータス更新などのターン開始処理
    AttackSelect,             // 攻撃/魔法/特殊カード/経済アクション選択
    AttackConfirm,            // 攻撃カード確定、演出付き表示（相手にも見える）

    // ④ 後攻プレイヤーの防御選択
    DefenseSelect,            // 防御カード・反撃カードなど選択
    DefenseConfirm,           // 防御カード確定、演出付き表示

    // ⑤ 戦闘処理・勝敗判定
    DamageResolve,            // ダメージ計算、状態異常付与、HP/MP/GPの変動
    TurnEnd,                   // ターン終了処理、ターン交代

    // ⑥ 敗北直前処理（発動型の効果など）
    DefeatEffect,        // 道づれカードや復活アイテムなど

    // 終了
    BattleEnd,                // 両者に結果通知・報酬画面など
}

public enum PlayerType
{
    Player,     // 自分
    Enemy       // 相手（CPU or ネット対戦相手）
}