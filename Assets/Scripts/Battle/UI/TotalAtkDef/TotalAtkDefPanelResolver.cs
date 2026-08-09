using System.Collections.Generic;

/// <summary>TotalATKDEF パネルの表示可否・テキスト・属性の解決。</summary>
public class TotalAtkDefPanelResolver
{
    private readonly TotalAtkDefDisplayState _state;
    private readonly TotalAtkDefPowerCalculator _power;

    public TotalAtkDefPanelResolver(TotalAtkDefDisplayState state, TotalAtkDefPowerCalculator power)
    {
        _state = state;
        _power = power;
    }

    public bool IsPlayerAttackSelectionNumericAtkZero(IReadOnlyList<CardData> attackCards)
    {
        if (attackCards == null || attackCards.Count == 0 || BattleManager.I == null) return false;
        var list = new List<CardData>(attackCards.Count);
        foreach (var c in attackCards)
        {
            if (c != null) list.Add(c);
        }
        if (list.Count == 0) return false;
        if (list.Count == 1 && HammadnessRules.IsHammadnessCard(list[0]))
            return false;
        return _power.GetDisplayedAttackStrength(list, BattleManager.I.GetPlayerStatus()) <= 0;
    }

    public bool IsShowingLockedIncomingAttackDisplay(bool forEnemyPanel)
    {
        var bm = BattleManager.I;
        if (bm == null || !_state.TryGetSequenceAttackLockedDisplayText(out _))
            return false;
        if (forEnemyPanel)
        {
            if (!TotalAtkDefCombatPhaseRules.IsPlayerDefendingAgainstEnemyAttack(bm)) return false;
            return bm.DefenderPublic == PlayerType.Player && bm.CurrentTurnOwner == PlayerType.Enemy;
        }
        if (!TotalAtkDefCombatPhaseRules.IsEnemyDefendingAgainstPlayerAttack(bm)
            && !IsPlayerOutgoingAttackTotalPending(bm))
            return false;
        return bm.DefenderPublic == PlayerType.Enemy && bm.CurrentTurnOwner == PlayerType.Player;
    }

    public bool ShouldHidePlayer()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return true;

        if (battleManager.CurrentState == GameState.EndPhase)
            return true;

        if (battleManager.IsEconomicActionInProgress()) return true;

        if (battleManager.IsHandReloadPopupOpen) return true;

        if (ShouldShowDisasterStrikeAttackOnPanel(forPlayerPanel: true))
            return false;

        if (ShouldShowDefenderDefenseTotalDuringReflectionOverlay(battleManager, forEnemyPanel: false))
            return false;

        if (ShouldHidePlayerPanelForReflectionOverlay(battleManager))
            return true;

        if (battleManager.IsReflectionAttackTotalDisplayActive()
            && battleManager.ReflectionAttackTotalOnPlayerSide
            && !ShouldHidePlayerPanelForReflectionOverlay(battleManager))
            return false;

        if (IsAttackerPanelEmptyDuringAttackSequenceReveal(true))
            return true;

        if (ShouldShowPlayerPanelHeldOutgoingAttack(battleManager)
            && !IsAttackerPanelDuringAttackSequenceReveal(true))
            return false;

        if (_state.CurrentSequenceCards.Count > 0 && _state.SequenceOwnerSide == Side.Player)
        {
            if (_state.CurrentSequenceType == "攻撃")
            {
                int totalAttack = _power.GetDisplayedAttackStrength(ToMutableList(_state.CurrentSequenceCards), battleManager.GetPlayerStatus());
                if (totalAttack <= 0) return true;
                return false;
            }
            else if (_state.CurrentSequenceType == "防御")
            {
                if (IsHellfireOrbSequenceWithActiveReflectionForPanel(battleManager, true))
                    return false;
                if (TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, ToMutableList(_state.CurrentSequenceCards)))
                    return !ShouldShowPlayerPanelHeldOutgoingAttack(battleManager);
                int totalDefense = _power.CalculateTotalDefensePower(ToMutableList(_state.CurrentSequenceCards));
                if (totalDefense <= 0) return true;
                return false;
            }
        }

        if (TotalAtkDefCombatPhaseRules.TryGetPostDeathChainAttackForPanel(true, out var pdAtkCards, out var pdAttacker)
            && _power.GetDisplayedAttackStrength(pdAtkCards, pdAttacker) > 0
            && !(battleManager.IsPostDeathDefenseWaitActive() && battleManager.IsPostDeathPlayerDefender))
            return false;

        if (battleManager.CurrentState == GameState.AttackPhase
            && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            if (IsAttackerPanelDuringAttackSequenceReveal(true))
                return true;

            var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
            if (selectedAttackCards != null && selectedAttackCards.Count > 0)
                return false;

            if (battleManager.IsPlayerSelfAttackTargetMode)
                return false;
            return true;
        }

        if (IsPlayerOutgoingAttackTotalPending(battleManager))
        {
            var outgoing = ResolvePlayerOutgoingAttackDisplayText();
            if (!string.IsNullOrEmpty(outgoing)) return false;
        }

        if (TotalAtkDefCombatPhaseRules.IsEnemyDefendingAgainstPlayerAttack(battleManager))
        {
            var incoming = TotalAtkDefCombatPhaseRules.GetIncomingAttackSnapshotForDefenseUi(battleManager);
            if (incoming != null && incoming.Count > 0)
            {
                if (_state.TryGetSequenceAttackLockedDisplayText(out _))
                    return false;
                if (_power.GetDisplayedAttackStrength(incoming, battleManager.GetPlayerStatus()) > 0)
                    return false;
            }
        }

        if (battleManager.CurrentState == GameState.DefensePhase
            || battleManager.CurrentState == GameState.DefenseConfirmPhase
            || (battleManager.IsPostDeathDefenseWaitActive() && battleManager.IsPostDeathPlayerDefender))
        {
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
            {
                if (TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, selectedDefenseCards)) return true;
                if (selectedDefenseCards.Count > 1)
                {
                    int totalDefense = _power.CalculateTotalDefensePower(selectedDefenseCards);
                    if (totalDefense <= 0) return true;
                    return false;
                }

                var card = selectedDefenseCards[0];
                if (card.defensePower <= 0) return true;
                return false;
            }

            return true;
        }

        return true;
    }

    public bool ShouldHideEnemy()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return true;

        if (battleManager.CurrentState == GameState.EndPhase)
            return true;

        if (battleManager.IsEconomicActionInProgress()) return true;

        if (ShouldShowDisasterStrikeAttackOnPanel(forPlayerPanel: false))
            return false;

        if (IsAttackerPanelEmptyDuringAttackSequenceReveal(false))
            return true;

        if (ShouldShowDefenderDefenseTotalDuringReflectionOverlay(battleManager, forEnemyPanel: true))
            return false;

        if (TryGetReflectionTotalHideForPanel(false, out bool refHideEnemyEarly))
            return refHideEnemyEarly;

        if (_state.CurrentSequenceCards.Count > 0 && _state.SequenceOwnerSide == Side.Enemy)
        {
            if (_state.CurrentSequenceType == "攻撃")
            {
                int totalAttack = _power.GetDisplayedAttackStrength(ToMutableList(_state.CurrentSequenceCards), battleManager.GetEnemyStatus());
                if (totalAttack <= 0) return true;
                return false;
            }
            else if (_state.CurrentSequenceType == "防御")
            {
                if (IsHellfireOrbSequenceWithActiveReflectionForPanel(battleManager, false))
                    return false;
                if (TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, ToMutableList(_state.CurrentSequenceCards))) return true;
                int totalDefense = _power.CalculateTotalDefensePower(ToMutableList(_state.CurrentSequenceCards));
                if (totalDefense <= 0) return true;
                return false;
            }
        }

        if (TotalAtkDefCombatPhaseRules.TryGetPostDeathChainAttackForPanel(false, out var pdEnemyAtkCards, out var pdEnemyAttacker)
            && _power.GetDisplayedAttackStrength(pdEnemyAtkCards, pdEnemyAttacker) > 0)
            return false;

        if (TotalAtkDefCombatPhaseRules.IsPlayerDefendingAgainstEnemyAttack(battleManager)
            && !IsAttackerPanelDuringAttackSequenceReveal(false)
            && !battleManager.IsReflectionAttackTotalDisplayActive())
        {
            var incoming = TotalAtkDefCombatPhaseRules.GetIncomingAttackSnapshotForDefenseUi(battleManager);
            if (incoming != null && incoming.Count > 0)
            {
                if (_state.TryGetSequenceAttackLockedDisplayText(out _))
                    return false;
                if (_power.GetDisplayedAttackStrength(incoming, battleManager.GetEnemyStatus()) > 0)
                    return false;
            }
        }

        if (!TotalAtkDefCombatPhaseRules.IsPostDeathChainCombatTotalActive(battleManager)
            && battleManager.CurrentTurnOwner == PlayerType.Enemy
            && !battleManager.IsSuppressingEnemyStaleAttackerInTotalByOrb()
            && !IsAttackerPanelDuringAttackSequenceReveal(false)
            && !battleManager.IsReflectionAttackTotalDisplayActive())
        {
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            if (currentAttackCard != null)
            {
                if (CardRules.IsImmediateAction(currentAttackCard)) return true;
                var one = new List<CardData> { currentAttackCard };
                if (_power.GetDisplayedAttackStrength(one, battleManager.GetEnemyStatus()) <= 0) return true;
                return false;
            }
            return true;
        }

        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var state = battleManager.CurrentState;
            if (state == GameState.DefensePhase || state == GameState.DefenseConfirmPhase)
            {
                var defCards = TotalAtkDefCombatPhaseRules.ResolveEnemyDefenseCardsForDisplay(battleManager);
                if (defCards != null && defCards.Count > 0)
                {
                    if (TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, defCards)) return true;
                    if (defCards.Count > 1)
                        return _power.CalculateTotalDefensePower(defCards) <= 0;
                    return defCards[0].defensePower <= 0;
                }
            }
            return true;
        }

        return true;
    }

    public string GetPlayerDisplayText()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return "";

        if (ShouldShowDefenderDefenseTotalDuringReflectionOverlay(battleManager, forEnemyPanel: false))
        {
            var defCards = ResolveDefenderDefenseCardsForReflectionOverlay(battleManager, forEnemyPanel: false);
            if (defCards != null && TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, defCards))
                return "";
            if (defCards != null && defCards.Count > 0)
                return _power.FormatDefensePowerLabel(defCards);
        }

        if (IsHellfireOrbSequenceWithActiveReflectionForPanel(battleManager, true))
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return _power.FormatReflectionAttackTotalLabel(battleManager, battleManager.GetPlayerStatus());
        }

        if (battleManager.IsPostDeathDefenseWaitActive() && battleManager.IsPostDeathPlayerDefender)
        {
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
            {
                if (TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, selectedDefenseCards)) return "";
                return _power.FormatDefensePowerLabel(selectedDefenseCards);
            }
        }

        if (TotalAtkDefCombatPhaseRules.TryGetPostDeathChainAttackForPanel(true, out var pdChainCards, out var pdChainAttacker)
            && !(battleManager.IsPostDeathDefenseWaitActive() && battleManager.IsPostDeathPlayerDefender))
            return _power.FormatAttackPowerDisplayLabel(pdChainCards, pdChainAttacker);

        if (TryResolvePlayerHeldOutgoingAttackText(battleManager, out var heldOutgoing))
            return heldOutgoing;

        if (DisasterCombatContext.TryGetAttackerStrikeForPanel(true, out var disasterAtkCards, out var disasterAttacker))
            return _power.FormatAttackPowerDisplayLabel(disasterAtkCards, disasterAttacker);

        if (IsPlayerOutgoingAttackTotalPending(battleManager))
        {
            var outgoingText = ResolvePlayerOutgoingAttackDisplayText();
            if (!string.IsNullOrEmpty(outgoingText))
                return outgoingText;
        }

        if (TotalAtkDefCombatPhaseRules.IsEnemyDefendingAgainstPlayerAttack(battleManager))
        {
            var incomingText = ResolveIncomingAttackDisplayText(battleManager.GetPlayerStatus());
            if (!string.IsNullOrEmpty(incomingText))
                return incomingText;
        }

        if (_state.CurrentSequenceCards.Count > 0 && _state.SequenceOwnerSide == Side.Player)
        {
            if (_state.CurrentSequenceType == "攻撃")
            {
                if (_state.TryGetSequenceAttackLockedDisplayText(out var lockedAtk))
                    return lockedAtk;
                return _power.FormatAttackPowerDisplayLabel(ToMutableList(_state.CurrentSequenceCards), battleManager.GetPlayerStatus());
            }
            else if (_state.CurrentSequenceType == "防御")
            {
                var seqCards = ToMutableList(_state.CurrentSequenceCards);
                if (TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, seqCards))
                {
                    if (TryResolvePlayerHeldOutgoingAttackText(battleManager, out var heldAtk))
                        return heldAtk;
                    return "";
                }
                return _power.FormatDefensePowerLabel(seqCards);
            }
        }

        if (battleManager.IsReflectionAttackTotalDisplayActive() && battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return _power.FormatReflectionAttackTotalLabel(battleManager, battleManager.GetPlayerStatus());
        }

        if (battleManager.CurrentState == GameState.AttackPhase
            && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
            if (selectedAttackCards != null && selectedAttackCards.Count > 0
                && IsPlayerAttackSelectionNumericAtkZero(selectedAttackCards))
            {
                bool recovery = selectedAttackCards.Count == 1
                    && CardRules.IsRecoveryCard(selectedAttackCards[0]);
                return TotalAtkDefCombatPhaseRules.FormatEffectTargetToggleLabel(battleManager, recovery);
            }

            if (selectedAttackCards != null && selectedAttackCards.Count > 1)
                return _power.FormatAttackPowerDisplayLabel(selectedAttackCards, battleManager.GetPlayerStatus());

            if (selectedAttackCards != null && selectedAttackCards.Count == 1)
            {
                var one = new List<CardData> { selectedAttackCards[0] };
                return _power.FormatAttackPowerDisplayLabel(one, battleManager.GetPlayerStatus());
            }
        }
        else if (battleManager.CurrentState == GameState.DefensePhase
            || battleManager.CurrentState == GameState.DefenseConfirmPhase)
        {
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0
                && TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, selectedDefenseCards))
                return "";
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
                return _power.FormatDefensePowerLabel(selectedDefenseCards);
        }

        return "";
    }

    public string GetEnemyDisplayText()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return "";

        if (IsHellfireOrbSequenceWithActiveReflectionForPanel(battleManager, false))
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return _power.FormatReflectionAttackTotalLabel(battleManager, battleManager.GetEnemyStatus());
        }

        if (ShouldShowDefenderDefenseTotalDuringReflectionOverlay(battleManager, forEnemyPanel: true))
        {
            var defCards = ResolveDefenderDefenseCardsForReflectionOverlay(battleManager, forEnemyPanel: true);
            if (defCards != null && TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, defCards))
                return "";
            if (defCards != null && defCards.Count > 0)
                return _power.FormatDefensePowerLabel(defCards);
        }

        if (TotalAtkDefCombatPhaseRules.IsPlayerDefendingAgainstEnemyAttack(battleManager))
        {
            var incomingText = ResolveIncomingAttackDisplayText(battleManager.GetEnemyStatus());
            if (!string.IsNullOrEmpty(incomingText))
                return incomingText;
        }

        if (TotalAtkDefCombatPhaseRules.TryGetPostDeathChainAttackForPanel(false, out var pdEnemyChainCards, out var pdEnemyChainAttacker))
            return _power.FormatAttackPowerDisplayLabel(pdEnemyChainCards, pdEnemyChainAttacker);

        if (DisasterCombatContext.TryGetAttackerStrikeForPanel(false, out var disasterEnemyAtkCards, out var disasterEnemyAttacker))
            return _power.FormatAttackPowerDisplayLabel(disasterEnemyAtkCards, disasterEnemyAttacker);

        if (_state.CurrentSequenceCards.Count > 0 && _state.SequenceOwnerSide == Side.Enemy)
        {
            if (_state.CurrentSequenceType == "攻撃")
            {
                if (_state.TryGetSequenceAttackLockedDisplayText(out var lockedAtk))
                    return lockedAtk;
                return _power.FormatAttackPowerDisplayLabel(ToMutableList(_state.CurrentSequenceCards), battleManager.GetEnemyStatus());
            }
            else if (_state.CurrentSequenceType == "防御")
            {
                if (TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, ToMutableList(_state.CurrentSequenceCards))) return "";
                return _power.FormatDefensePowerLabel(ToMutableList(_state.CurrentSequenceCards));
            }
        }

        if (battleManager.IsReflectionAttackTotalDisplayActive() && !battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return _power.FormatReflectionAttackTotalLabel(battleManager, battleManager.GetEnemyStatus());
        }

        if (!TotalAtkDefCombatPhaseRules.IsPostDeathChainCombatTotalActive(battleManager)
            && battleManager.CurrentTurnOwner == PlayerType.Enemy
            && !battleManager.IsSuppressingEnemyStaleAttackerInTotalByOrb())
        {
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            if (currentAttackCard != null)
            {
                var one = new List<CardData> { currentAttackCard };
                return _power.FormatAttackPowerDisplayLabel(one, battleManager.GetEnemyStatus());
            }
        }

        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var defCards = TotalAtkDefCombatPhaseRules.ResolveEnemyDefenseCardsForDisplay(battleManager);
            if (defCards != null && defCards.Count > 0)
            {
                if (TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, defCards)) return "";
                return _power.FormatDefensePowerLabel(defCards);
            }
        }

        return "";
    }

    public ElementType GetPlayerCombinedElement()
    {
        var battleManager = BattleManager.I;

        if (TotalAtkDefCombatPhaseRules.TryGetPostDeathChainAttackForPanel(true, out _, out _))
        {
            var ctx = PostDeathCombatContext.Active;
            if (ctx != null) return ctx.AttackElement;
        }

        if (battleManager != null
            && ShouldShowDefenderDefenseTotalDuringReflectionOverlay(battleManager, forEnemyPanel: false))
        {
            var defCards = ResolveDefenderDefenseCardsForReflectionOverlay(battleManager, forEnemyPanel: false);
            if (defCards != null && defCards.Count > 0)
                return ElementHelper.GetCombinedElement(defCards);
        }

        if (TotalAtkDefCombatPhaseRules.IsEnemyDefendingAgainstPlayerAttack(battleManager))
        {
            var incoming = TotalAtkDefCombatPhaseRules.GetIncomingAttackSnapshotForDefenseUi(battleManager);
            if (incoming != null && incoming.Count > 0)
                return ElementHelper.GetCombinedElement(incoming);
        }

        if (DisasterCombatContext.TryGetAttackerStrikeForPanel(true, out var disasterElCards, out _))
            return ElementHelper.GetCombinedElement(disasterElCards);

        if (_state.CurrentSequenceCards.Count > 0 && _state.SequenceOwnerSide == Side.Player)
        {
            var postDeathCtx = PostDeathCombatContext.Active;
            if (postDeathCtx != null && postDeathCtx.MatchesIncoming(ToMutableList(_state.CurrentSequenceCards)))
                return postDeathCtx.AttackElement;
            bool applySpellbookElement = !(_state.SuppressSpellbookElementDuringSequenceReveal && _state.CurrentSequenceType == "攻撃");
            return ElementHelper.GetCombinedElement(ToMutableList(_state.CurrentSequenceCards), applySpellbookElement);
        }

        if (battleManager != null && battleManager.IsReflectionAttackTotalDisplayActive()
            && battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return ElementHelper.GetCombinedElement(rc);
        }

        if (battleManager == null) return ElementType.None;

        if (battleManager.CurrentState == GameState.AttackPhase
            && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            var cards = BattleUIManager.I?.GetSelectedAttackCards();
            if (cards != null && cards.Count > 0) return ElementHelper.GetCombinedElement(cards);
        }
        else if (battleManager.CurrentState == GameState.DefensePhase
            || battleManager.CurrentState == GameState.DefenseConfirmPhase)
        {
            var cards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (cards != null && cards.Count > 0) return ElementHelper.GetCombinedElement(cards);
        }
        return ElementType.None;
    }

    public ElementType GetEnemyCombinedElement()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return ElementType.None;

        if (TotalAtkDefCombatPhaseRules.TryGetPostDeathChainAttackForPanel(false, out _, out _))
        {
            var ctx = PostDeathCombatContext.Active;
            if (ctx != null) return ctx.AttackElement;
        }

        if (ShouldShowDefenderDefenseTotalDuringReflectionOverlay(battleManager, forEnemyPanel: true))
        {
            var defCards = ResolveDefenderDefenseCardsForReflectionOverlay(battleManager, forEnemyPanel: true);
            if (defCards != null && defCards.Count > 0)
                return ElementHelper.GetCombinedElement(defCards);
        }

        if (TotalAtkDefCombatPhaseRules.IsPlayerDefendingAgainstEnemyAttack(battleManager))
        {
            var incoming = TotalAtkDefCombatPhaseRules.GetIncomingAttackSnapshotForDefenseUi(battleManager);
            if (incoming != null && incoming.Count > 0)
                return ElementHelper.GetCombinedElement(incoming);
        }

        if (DisasterCombatContext.TryGetAttackerStrikeForPanel(false, out var disasterEnemyElCards, out _))
            return ElementHelper.GetCombinedElement(disasterEnemyElCards);

        if (_state.CurrentSequenceCards.Count > 0 && _state.SequenceOwnerSide == Side.Enemy)
        {
            var postDeathCtx = PostDeathCombatContext.Active;
            if (postDeathCtx != null && postDeathCtx.MatchesIncoming(ToMutableList(_state.CurrentSequenceCards)))
                return postDeathCtx.AttackElement;
            bool applySpellbookElement = !(_state.SuppressSpellbookElementDuringSequenceReveal && _state.CurrentSequenceType == "攻撃");
            return ElementHelper.GetCombinedElement(ToMutableList(_state.CurrentSequenceCards), applySpellbookElement);
        }

        if (battleManager.IsReflectionAttackTotalDisplayActive() && !battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return ElementHelper.GetCombinedElement(rc);
        }

        if (battleManager.CurrentTurnOwner == PlayerType.Enemy
            && !battleManager.IsSuppressingEnemyStaleAttackerInTotalByOrb())
        {
            var card = battleManager.GetCurrentAttackCard();
            if (card != null) return card.element;
        }

        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var defCards = TotalAtkDefCombatPhaseRules.ResolveEnemyDefenseCardsForDisplay(battleManager);
            if (defCards != null && defCards.Count > 0)
                return ElementHelper.GetCombinedElement(defCards);
        }
        return ElementType.None;
    }

    private bool TryResolvePlayerHeldOutgoingAttackText(BattleManager bm, out string text)
    {
        text = null;
        if (bm == null) return false;

        if (bm.IsReflectionAttackTotalDisplayActive())
        {
            if (bm.ReflectionAttackTotalOnPlayerSide)
            {
                var rc = bm.GetReflectionAttackCardsForTotalDisplay();
                if (rc != null && rc.Count > 0)
                {
                    text = _power.FormatReflectionAttackTotalLabel(bm, bm.GetPlayerStatus());
                    return !string.IsNullOrEmpty(text);
                }
            }

            return false;
        }

        if (!TotalAtkDefCombatPhaseRules.IsPlayerOutgoingAttackTotalHeld(bm)) return false;

        text = ResolvePlayerOutgoingAttackDisplayText();
        return !string.IsNullOrEmpty(text);
    }

    private bool ShouldShowPlayerPanelHeldOutgoingAttack(BattleManager bm) =>
        TryResolvePlayerHeldOutgoingAttackText(bm, out _);

    private bool ShouldHidePlayerPanelForReflectionOverlay(BattleManager bm)
    {
        if (!TryGetReflectionTotalHideForPanel(true, out bool hide)) return false;
        return hide;
    }

    private static bool IsPlayerOutgoingAttackTotalPending(BattleManager bm) =>
        TotalAtkDefCombatPhaseRules.IsPlayerOutgoingAttackTotalHeld(bm);

    private string ResolvePlayerOutgoingAttackDisplayText()
    {
        var bm = BattleManager.I;
        if (bm == null) return null;
        var cards = bm.GetAttackCardsForCombatPublic();
        if (cards == null || cards.Count == 0) return null;
        if (_state.TryGetSequenceAttackLockedDisplayText(out var locked))
            return locked;
        return _power.FormatAttackPowerDisplayLabel(cards, bm.GetPlayerStatus());
    }

    private string ResolveIncomingAttackDisplayText(PlayerStatus attacker)
    {
        var bm = BattleManager.I;
        if (bm == null || attacker == null) return null;
        var incoming = TotalAtkDefCombatPhaseRules.GetIncomingAttackSnapshotForDefenseUi(bm);
        if (incoming == null || incoming.Count == 0) return null;
        if (_state.TryGetSequenceAttackLockedDisplayText(out var locked))
            return locked;
        return _power.FormatAttackPowerDisplayLabel(incoming, attacker);
    }

    private bool TryGetReflectionTotalHideForPanel(bool evaluatingPlayerPanel, out bool hide)
    {
        hide = true;
        var bm = BattleManager.I;
        if (bm == null || !bm.IsReflectionAttackTotalDisplayActive()) return false;

        bool totalOnPlayer = bm.ReflectionAttackTotalOnPlayerSide;

        if (evaluatingPlayerPanel)
        {
            if (!totalOnPlayer)
            {
                if (ShouldShowDefenderDefenseTotalDuringReflectionOverlay(bm, forEnemyPanel: false))
                    return false;

                hide = true;
                return true;
            }
        }
        else
        {
            if (totalOnPlayer)
            {
                if (ShouldShowDefenderDefenseTotalDuringReflectionOverlay(bm, forEnemyPanel: true))
                    return false;

                hide = true;
                return true;
            }
        }

        var cards = bm.GetReflectionAttackCardsForTotalDisplay();
        var atkB = bm.GetReflectionAttackBlessingAttacker();
        var defB = bm.GetReflectionAttackBlessingDefender();
        int s = (atkB != null && defB != null)
            ? GetReflectionAttackNumericStrength(cards, atkB, defB)
            : _power.GetDisplayedAttackStrength(
                cards,
                evaluatingPlayerPanel ? bm.GetPlayerStatus() : bm.GetEnemyStatus());
        hide = s <= 0;
        return true;
    }

    /// <summary>
    /// Reflection overlay: opposite panel shows reflected ATK; defender panel may show DEF instead of hiding.
    /// </summary>
    private bool ShouldShowDefenderDefenseTotalDuringReflectionOverlay(BattleManager bm, bool forEnemyPanel)
    {
        if (bm == null || !bm.IsReflectionAttackTotalDisplayActive()) return false;

        bool totalOnPlayer = bm.ReflectionAttackTotalOnPlayerSide;
        // ATK が載っているのは反対側パネル。プレイヤー再防御待ちは ATK と同じ側に DEF を出す。
        bool showDefOnThisPanel = forEnemyPanel == totalOnPlayer
            || (!forEnemyPanel && (bm.IsReflectionChainDefensePending() || bm.IsParryRerunDefensePending()));
        if (!showDefOnThisPanel) return false;

        var defCards = ResolveDefenderDefenseCardsForReflectionOverlay(bm, forEnemyPanel);
        if (defCards == null || defCards.Count == 0) return false;

        var incoming = TotalAtkDefCombatPhaseRules.GetIncomingAttackSnapshotForDefenseUi(bm);
        if (incoming == null || incoming.Count == 0)
            incoming = bm.GetReflectionAttackCardsForTotalDisplay();
        if (incoming == null || incoming.Count == 0) return false;

        if (BlockingRules.AnyDefenseCardResolvesAsReflectionOrNullify(defCards, incoming))
            return false;

        return _power.CalculateTotalDefensePower(defCards) > 0;
    }

    private List<CardData> ResolveDefenderDefenseCardsForReflectionOverlay(BattleManager bm, bool forEnemyPanel)
    {
        Side wantSide = forEnemyPanel ? Side.Enemy : Side.Player;
        if (_state.CurrentSequenceCards.Count > 0
            && _state.SequenceOwnerSide == wantSide
            && _state.CurrentSequenceType == "防御")
        {
            return ToMutableList(_state.CurrentSequenceCards);
        }

        if (forEnemyPanel)
            return TotalAtkDefCombatPhaseRules.ResolveEnemyDefenseCardsForDisplay(bm);

        if (bm.IsReflectionChainDefensePending() || bm.IsParryRerunDefensePending())
        {
            var pending = BattleUIManager.I?.GetSelectedDefenseCards();
            if (pending != null && pending.Count > 0) return pending;
        }

        var selected = BattleUIManager.I?.GetSelectedDefenseCards();
        if (selected != null && selected.Count > 0) return selected;

        return null;
    }

    private int GetReflectionAttackNumericStrength(
        List<CardData> cards,
        PlayerStatus blessingAttacker,
        PlayerStatus blessingDefender)
    {
        if (cards == null || cards.Count == 0) return 0;
        var bm = BattleManager.I;
        if (bm != null && bm.GetReflectionAttackDisplayStrengthOverride() is int ovr)
            return ovr;
        return _power.GetDisplayedAttackStrengthWithDefender(cards, blessingAttacker, blessingDefender);
    }

    private bool IsHellfireOrbSequenceWithActiveReflectionForPanel(BattleManager bm, bool forPlayerPanel)
    {
        if (bm == null || !bm.IsReflectionAttackTotalDisplayActive()) return false;
        if (forPlayerPanel != bm.ReflectionAttackTotalOnPlayerSide) return false;
        if (_state.CurrentSequenceCards == null || _state.CurrentSequenceCards.Count != 1) return false;
        if (_state.CurrentSequenceType != "防御") return false;
        var wantSide = forPlayerPanel ? Side.Player : Side.Enemy;
        if (_state.SequenceOwnerSide != wantSide) return false;
        var c = _state.CurrentSequenceCards[0];
        if (c == null || c.orbReactionRule is not OrbOfHellfireRuleSO) return false;
        return true;
    }

    private bool ShouldShowDisasterStrikeAttackOnPanel(bool forPlayerPanel)
    {
        if (!DisasterCombatContext.TryGetAttackerStrikeForPanel(forPlayerPanel, out var cards, out var attacker))
            return false;
        return _power.GetDisplayedAttackStrength(cards, attacker) > 0;
    }

    private bool IsAttackerPanelDuringAttackSequenceReveal(bool forPlayerPanel)
    {
        if (!_state.AttackSequenceRevealInProgress) return false;
        return forPlayerPanel
            ? _state.AttackSequenceRevealAttackerSide == Side.Player
            : _state.AttackSequenceRevealAttackerSide == Side.Enemy;
    }

    private bool IsAttackerPanelEmptyDuringAttackSequenceReveal(bool forPlayerPanel)
    {
        if (!IsAttackerPanelDuringAttackSequenceReveal(forPlayerPanel)) return false;
        return _state.CurrentSequenceType == "攻撃" && _state.CurrentSequenceCards.Count == 0;
    }

    private static List<CardData> ToMutableList(IReadOnlyList<CardData> cards)
    {
        if (cards is List<CardData> list) return list;
        return new List<CardData>(cards);
    }
}
