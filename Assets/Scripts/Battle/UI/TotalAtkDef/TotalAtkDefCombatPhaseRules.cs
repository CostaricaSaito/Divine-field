using System.Collections.Generic;

/// <summary>TotalATKDEF 表示の戦闘フェーズ判定ヘルパー（静的）。</summary>
public static class TotalAtkDefCombatPhaseRules
{
    public static List<CardData> GetIncomingAttackSnapshotForDefenseUi(BattleManager bm)
    {
        if (bm == null) return null;
        var incoming = bm.GetIncomingAttackSnapshotForDefenseUi();
        if (incoming == null || incoming.Count == 0)
            incoming = bm.GetAttackCardsForCombatPublic();
        if (incoming == null || incoming.Count == 0) return null;
        return incoming;
    }

    public static bool IsAttackTotalHeldThroughCombatPhase(BattleManager bm)
    {
        if (bm == null || bm.CurrentState == GameState.EndPhase) return false;
        if (IsPostDeathChainCombatTotalActive(bm)) return false;
        if (bm.CurrentState == GameState.DefensePhase || bm.CurrentState == GameState.DefenseConfirmPhase)
            return true;
        if (bm.IsPlayerDefenseCombatResolving) return true;
        if (IsOutgoingAttackTotalHeld(bm)) return true;
        return false;
    }

    public static bool HasConfirmedOutgoingAttackCards(BattleManager bm)
    {
        if (bm == null) return false;
        var cards = bm.GetAttackCardsForCombatPublic();
        return cards != null && cards.Count > 0;
    }

    public static bool IsOutgoingAttackTotalHeld(BattleManager bm)
    {
        if (bm == null || bm.CurrentState == GameState.EndPhase) return false;
        if (IsPostDeathChainCombatTotalActive(bm)) return false;
        if (!HasConfirmedOutgoingAttackCards(bm)) return false;
        if (bm.IsReflectionAttackTotalDisplayActive()) return true;

        switch (bm.CurrentState)
        {
            case GameState.DefensePhase:
            case GameState.DefenseConfirmPhase:
            case GameState.CombatResolvePhase:
                return true;
            case GameState.AttackPhase:
                var sel = BattleUIManager.I?.GetSelectedAttackCards();
                return sel == null || sel.Count == 0;
            default:
                return false;
        }
    }

    public static bool IsPlayerOutgoingAttackTotalHeld(BattleManager bm) =>
        bm != null
        && bm.AttackerPublic == PlayerType.Player
        && IsOutgoingAttackTotalHeld(bm);

    public static bool IsPostDeathChainCombatTotalActive(BattleManager bm) =>
        bm != null && (bm.IsPostDeathSequenceActive || bm.IsPostDeathChainAttackDisplayActive);

    public static bool IsPlayerDefendingAgainstEnemyAttack(BattleManager bm)
    {
        if (bm == null) return false;
        if (!IsAttackTotalHeldThroughCombatPhase(bm)) return false;
        return bm.DefenderPublic == PlayerType.Player && bm.CurrentTurnOwner == PlayerType.Enemy;
    }

    public static bool IsEnemyDefendingAgainstPlayerAttack(BattleManager bm)
    {
        if (bm == null) return false;
        if (!IsAttackTotalHeldThroughCombatPhase(bm)) return false;
        return bm.DefenderPublic == PlayerType.Enemy && bm.CurrentTurnOwner == PlayerType.Player;
    }

    public static bool IsReflectionOrNullifyDefenseRoute(BattleManager bm, List<CardData> defenseCards)
    {
        if (bm == null || defenseCards == null || defenseCards.Count == 0) return false;

        var incoming = GetIncomingAttackSnapshotForDefenseUi(bm);
        if (incoming == null) return false;

        return BlockingRules.AnyDefenseCardResolvesAsReflectionOrNullify(defenseCards, incoming);
    }

    public static List<CardData> ResolveEnemyDefenseCardsForDisplay(BattleManager bm)
    {
        if (bm == null) return null;
        var combat = bm.GetEnemyDefenseCardsForCombat();
        if (combat != null && combat.Count > 0)
            return combat;
        var ui = BattleUIManager.I?.GetSelectedDefenseCards();
        if (ui != null && ui.Count > 0)
            return ui;
        var single = bm.GetSelectedDefenseCard();
        if (single == null) return null;
        return new List<CardData> { single };
    }

    public static bool TryGetPostDeathChainAttackForPanel(
        bool forPlayerPanel,
        out List<CardData> cards,
        out PlayerStatus attacker)
    {
        cards = null;
        attacker = null;
        var bm = BattleManager.I;
        if (bm == null || !bm.IsPostDeathChainAttackDisplayActive) return false;
        bool onPlayer = bm.GetPostDeathChainAttackDisplaySide() == Side.Player;
        if (onPlayer != forPlayerPanel) return false;
        var src = bm.GetPostDeathChainAttackDisplayCards();
        if (src == null || src.Count == 0) return false;
        cards = new List<CardData>(src);
        attacker = onPlayer ? bm.GetPlayerStatus() : bm.GetEnemyStatus();
        return attacker != null;
    }

    public static string FormatEffectTargetToggleLabel(BattleManager bm, bool recoveryEffectCard)
    {
        if (bm == null) return "対象：相手";
        bool red = bm.IsPlayerSelfAttackTargetMode;
        if (recoveryEffectCard)
            return red ? "対象：相手" : "対象：自分";
        return red ? "対象：自分" : "対象：相手";
    }
}
