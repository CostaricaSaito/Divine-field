/// <summary>
/// バトル進行の Phase（主に Layer2）。Turn（Layer1）は <see cref="BattleManager.CurrentTurnOwner"/> で表す。
/// 細かい手順は Layer3 の <see cref="BattleStep"/>（および <see cref="BattleManager.CurrentBattleStep"/>）で扱う。
/// </summary>
public enum GameState
{
    /// <summary>Layer2: 開幕のみ（手札配布・カットイン・先攻決定）。通常ターンでは再入しない。</summary>
    OpeningPhase,

    /// <summary>Layer2: スタンバイ（ターン開始処理・UI 初期化）。</summary>
    StandByPhase,

    /// <summary>Layer2: 攻撃側メイン（攻撃／回復／経済／顕現など、メイン行動は1回）。</summary>
    AttackPhase,

    /// <summary>Layer2: 防御側の選択。</summary>
    DefensePhase,

    /// <summary>Layer2: 防御確定から戦闘演出・解決へ。</summary>
    DefenseConfirmPhase,

    /// <summary>Layer2: ターン終了前の戦闘解決（介入・反射連鎖の再防御など）。終了後に <see cref="EndPhase"/> へ。</summary>
    CombatResolvePhase,

    /// <summary>Layer2: ターン終了（病・補充・表向きなど）。</summary>
    EndPhase,

    /// <summary>結果表示・メインへ戻る前。</summary>
    BattleEndPhase,
}

public enum PlayerType
{
    Player,
    Enemy
}

/// <summary>
/// Layer3 Step（<see cref="GameState"/> より細かい区間）。同一 Phase 内で反射・介入などに応じて切り替わる。
/// </summary>
public enum BattleStep
{
    /// <summary>初期値・想定外。</summary>
    Unknown = 0,

    /// <summary>開幕（配布・カットイン・先攻）。</summary>
    OpeningSequence,

    /// <summary>スタンバイ（ターン開始処理）。</summary>
    StandBy,

    /// <summary>メイン行動選択（攻撃／回復／経済など）。</summary>
    MainActionSelect,

    /// <summary>防御カード選択。</summary>
    DefenseSelect,

    /// <summary>防御確定後の戦闘演出・解決。</summary>
    CombatSequenceResolve,

    /// <summary>反射連鎖中の再防御選択（Phase は <see cref="GameState.AttackPhase"/> のまま）。</summary>
    ReflectionChainDefenseSelect,

    /// <summary>介入による再防御選択（Phase は <see cref="GameState.CombatResolvePhase"/>）。</summary>
    InterventionDefenseSelect,

    /// <summary>天変地異の臨時防御選択（Phase は <see cref="GameState.AttackPhase"/> のまま等）。</summary>
    DisasterDefenseSelect,

    /// <summary>介入抽選・再戦闘解決など（防御入力待ち以外）。</summary>
    CombatResolveProcessing,

    /// <summary>病・補充・表向き・ターン交代。</summary>
    EndPhaseProcessing,

    /// <summary>リザルト。</summary>
    BattleResult,
}

/// <summary><see cref="BattleStep"/> の UI・デバッグ用ラベル。</summary>
public static class BattleStepPresentation
{
    public static string GetDebugLabel(BattleStep step)
    {
        switch (step)
        {
            case BattleStep.OpeningSequence:
                return "開幕（配布・カットイン・先攻）";
            case BattleStep.StandBy:
                return "スタンバイ";
            case BattleStep.MainActionSelect:
                return "メイン行動選択";
            case BattleStep.DefenseSelect:
                return "防御選択";
            case BattleStep.CombatSequenceResolve:
                return "戦闘演出・解決";
            case BattleStep.ReflectionChainDefenseSelect:
                return "反射連鎖: 防御選択";
            case BattleStep.InterventionDefenseSelect:
                return "介入: 防御選択";
            case BattleStep.DisasterDefenseSelect:
                return "天変地異: 防御選択";
            case BattleStep.CombatResolveProcessing:
                return "介入抽選・再戦闘解決";
            case BattleStep.EndPhaseProcessing:
                return "病・補充・表向き・交代";
            case BattleStep.BattleResult:
                return "リザルト";
            default:
                return "—";
        }
    }
}
