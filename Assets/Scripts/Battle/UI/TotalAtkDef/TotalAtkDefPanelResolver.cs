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

        if (TryResolveReflectionPanel(battleManager, forPlayerPanel: true, out string reflectionText, out _))
            return string.IsNullOrEmpty(reflectionText);

        if (ShouldShowDisasterStrikeAttackOnPanel(forPlayerPanel: true))
            return false;

        if (ShouldEvaluatePlayerDefenseTotalDuringEnemyAttack(battleManager))
        {
            var defenseCards = ResolvePlayerDefenseCardsForTotalDisplay(battleManager);
            if (defenseCards != null && defenseCards.Count > 0
                && TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, defenseCards))
                return true;
            if (HasDefenderDefenseDisplay(battleManager, defenseCards, battleManager.GetPlayerStatus()))
                return false;
            return true;
        }

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
                if (TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, ToMutableList(_state.CurrentSequenceCards)))
                    return !ShouldShowPlayerPanelHeldOutgoingAttack(battleManager);
                var seqDefPlayer = ToMutableList(_state.CurrentSequenceCards);
                if (!HasDefenderDefenseDisplay(battleManager, seqDefPlayer, battleManager.GetPlayerStatus())) return true;
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

        return true;
    }

    public bool ShouldHideEnemy()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return true;

        if (battleManager.CurrentState == GameState.EndPhase)
            return true;

        if (battleManager.IsEconomicActionInProgress()) return true;

        if (TryResolveReflectionPanel(battleManager, forPlayerPanel: false, out string reflectionText, out _))
            return string.IsNullOrEmpty(reflectionText);

        if (ShouldShowDisasterStrikeAttackOnPanel(forPlayerPanel: false))
            return false;

        if (IsAttackerPanelEmptyDuringAttackSequenceReveal(false))
            return true;

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
                if (TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, ToMutableList(_state.CurrentSequenceCards))) return true;
                var seqDef = ToMutableList(_state.CurrentSequenceCards);
                if (!HasDefenderDefenseDisplay(battleManager, seqDef, battleManager.GetEnemyStatus())) return true;
                return false;
            }
        }

        if (TotalAtkDefCombatPhaseRules.TryGetPostDeathChainAttackForPanel(false, out var pdEnemyAtkCards, out var pdEnemyAttacker)
            && _power.GetDisplayedAttackStrength(pdEnemyAtkCards, pdEnemyAttacker) > 0)
            return false;

        if (TotalAtkDefCombatPhaseRules.IsPlayerDefendingAgainstEnemyAttack(battleManager)
            && !IsAttackerPanelDuringAttackSequenceReveal(false))
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
            && !IsAttackerPanelDuringAttackSequenceReveal(false))
        {
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            if (currentAttackCard != null)
            {
                if (CardRules.SkipsHitAndDefenseWhenSelfTarget(currentAttackCard)) return true;
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
                if (defCards != null && defCards.Count > 0
                    && TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, defCards))
                    return true;
                if (!HasDefenderDefenseDisplay(battleManager, defCards, battleManager.GetEnemyStatus()))
                    return true;
                return false;
            }
            return true;
        }

        return true;
    }

    public string GetPlayerDisplayText()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return "";

        if (TryResolveReflectionPanel(battleManager, forPlayerPanel: true, out string reflectionText, out _))
            return reflectionText;

        if (TryGetPlayerDefenderDefenseDisplayText(battleManager, out string playerDefLabel))
            return playerDefLabel;

        if (battleManager.IsPostDeathDefenseWaitActive() && battleManager.IsPostDeathPlayerDefender)
        {
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0
                && TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, selectedDefenseCards))
                return "";
            return FormatDefenderDefenseLabel(battleManager, selectedDefenseCards, battleManager.GetPlayerStatus());
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
                return FormatDefenderDefenseLabel(battleManager, seqCards, battleManager.GetPlayerStatus());
            }
        }

        if (battleManager.CurrentState == GameState.AttackPhase
            && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
            if (selectedAttackCards != null && selectedAttackCards.Count > 0
                && IsPlayerAttackSelectionNumericAtkZero(selectedAttackCards))
            {
                bool recoveryLikeTargetToggle = selectedAttackCards.Count == 1
                    && (CardRules.IsRecoveryCard(selectedAttackCards[0])
                        || MagicFountainRules.IsMagicFountainCard(selectedAttackCards[0]));
                return TotalAtkDefCombatPhaseRules.FormatEffectTargetToggleLabel(
                    battleManager, recoveryLikeTargetToggle);
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
            var defenseCards = ResolvePlayerDefenseCardsForTotalDisplay(battleManager);
            if (defenseCards != null && defenseCards.Count > 0
                && TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, defenseCards))
                return "";
            return FormatDefenderDefenseLabel(
                battleManager, defenseCards, battleManager.GetPlayerStatus());
        }

        return "";
    }

    public string GetEnemyDisplayText()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return "";

        if (TryResolveReflectionPanel(battleManager, forPlayerPanel: false, out string reflectionText, out _))
            return reflectionText;

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
                return FormatDefenderDefenseLabel(
                    battleManager, ToMutableList(_state.CurrentSequenceCards), battleManager.GetEnemyStatus());
            }
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
            if (defCards != null)
            {
                if (TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(battleManager, defCards)) return "";
                return FormatDefenderDefenseLabel(battleManager, defCards, battleManager.GetEnemyStatus());
            }
        }

        return "";
    }

    public ElementType GetPlayerCombinedElement()
    {
        var battleManager = BattleManager.I;

        if (TryResolveReflectionPanel(battleManager, forPlayerPanel: true, out _, out var reflectionElement))
            return reflectionElement;

        if (TotalAtkDefCombatPhaseRules.TryGetPostDeathChainAttackForPanel(true, out _, out _))
        {
            var ctx = PostDeathCombatContext.Active;
            if (ctx != null) return ctx.AttackElement;
        }

        if (TotalAtkDefCombatPhaseRules.IsEnemyDefendingAgainstPlayerAttack(battleManager))
        {
            var incoming = TotalAtkDefCombatPhaseRules.GetIncomingAttackSnapshotForDefenseUi(battleManager);
            if (incoming != null && incoming.Count > 0)
                return ElementHelper.GetIncomingAttackElement(incoming);
        }

        if (DisasterCombatContext.TryGetAttackerStrikeForPanel(true, out var disasterElCards, out _))
            return ElementHelper.GetCombinedElement(disasterElCards);

        if (_state.CurrentSequenceCards.Count > 0 && _state.SequenceOwnerSide == Side.Player)
        {
            if (_state.CurrentSequenceType == "防御" && battleManager != null)
            {
                return ResolveDefenderDefenseCombinedElement(
                    battleManager,
                    ToMutableList(_state.CurrentSequenceCards),
                    battleManager.GetPlayerStatus());
            }

            var postDeathCtx = PostDeathCombatContext.Active;
            if (postDeathCtx != null && postDeathCtx.MatchesIncoming(ToMutableList(_state.CurrentSequenceCards)))
                return postDeathCtx.AttackElement;
            bool applySpellbookElement = !(_state.SuppressSpellbookElementDuringSequenceReveal && _state.CurrentSequenceType == "攻撃");
            return ElementHelper.GetCombinedElement(ToMutableList(_state.CurrentSequenceCards), applySpellbookElement);
        }

        if (battleManager == null) return ElementType.None;

        if (battleManager.CurrentState == GameState.AttackPhase
            && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            var cards = BattleUIManager.I?.GetSelectedAttackCards();
            if (cards != null && cards.Count > 0) return ElementHelper.GetCombinedElement(cards);
        }
        else if (ShouldEvaluatePlayerDefenseTotalDuringEnemyAttack(battleManager))
        {
            var cards = ResolvePlayerDefenseCardsForTotalDisplay(battleManager);
            return ResolveDefenderDefenseCombinedElement(
                battleManager, cards, battleManager.GetPlayerStatus());
        }
        return ElementType.None;
    }

    public ElementType GetEnemyCombinedElement()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return ElementType.None;

        if (TryResolveReflectionPanel(battleManager, forPlayerPanel: false, out _, out var reflectionElement))
            return reflectionElement;

        if (TotalAtkDefCombatPhaseRules.TryGetPostDeathChainAttackForPanel(false, out _, out _))
        {
            var ctx = PostDeathCombatContext.Active;
            if (ctx != null) return ctx.AttackElement;
        }

        if (TotalAtkDefCombatPhaseRules.IsPlayerDefendingAgainstEnemyAttack(battleManager))
        {
            var incoming = TotalAtkDefCombatPhaseRules.GetIncomingAttackSnapshotForDefenseUi(battleManager);
            if (incoming != null && incoming.Count > 0)
                return ElementHelper.GetIncomingAttackElement(incoming);
        }

        if (DisasterCombatContext.TryGetAttackerStrikeForPanel(false, out var disasterEnemyElCards, out _))
            return ElementHelper.GetCombinedElement(disasterEnemyElCards);

        if (_state.CurrentSequenceCards.Count > 0 && _state.SequenceOwnerSide == Side.Enemy)
        {
            if (_state.CurrentSequenceType == "防御")
            {
                return ResolveDefenderDefenseCombinedElement(
                    battleManager,
                    ToMutableList(_state.CurrentSequenceCards),
                    battleManager.GetEnemyStatus());
            }

            var postDeathCtx = PostDeathCombatContext.Active;
            if (postDeathCtx != null && postDeathCtx.MatchesIncoming(ToMutableList(_state.CurrentSequenceCards)))
                return postDeathCtx.AttackElement;
            bool applySpellbookElement = !(_state.SuppressSpellbookElementDuringSequenceReveal && _state.CurrentSequenceType == "攻撃");
            return ElementHelper.GetCombinedElement(ToMutableList(_state.CurrentSequenceCards), applySpellbookElement);
        }

        if (battleManager.CurrentTurnOwner == PlayerType.Enemy
            && !battleManager.IsSuppressingEnemyStaleAttackerInTotalByOrb())
        {
            var incoming = TotalAtkDefCombatPhaseRules.GetIncomingAttackSnapshotForDefenseUi(battleManager);
            if (incoming != null && incoming.Count > 0)
                return ElementHelper.GetIncomingAttackElement(incoming);
            var card = battleManager.GetCurrentAttackCard();
            if (card != null) return card.element;
        }

        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var defCards = TotalAtkDefCombatPhaseRules.ResolveEnemyDefenseCardsForDisplay(battleManager);
            return ResolveDefenderDefenseCombinedElement(
                battleManager, defCards, battleManager.GetEnemyStatus());
        }
        return ElementType.None;
    }

    /// <summary>
    /// 反射・打ち払い・宝玉反撃中（<see cref="BattleManager.IsReflectionAttackTotalDisplayActive"/>）のパネル表示。
    /// 攻撃カードがあるパネル（Holder）は固定 ATK、反対側パネルは現在掲出中の防御カードの DEF のみ。
    /// 反射・打ち払い・無効化カードは DEF を出さない。それ以外の表示ルールはこの間すべて無視する。
    /// </summary>
    /// <returns>false: 反射表示中ではない（通常ルールで解決する）。true: <paramref name="text"/> が空なら非表示。</returns>
    private bool TryResolveReflectionPanel(
        BattleManager bm,
        bool forPlayerPanel,
        out string text,
        out ElementType element)
    {
        text = "";
        element = ElementType.None;
        if (bm == null || !bm.IsReflectionAttackTotalDisplayActive()) return false;

        var attackCards = bm.GetReflectionAttackCardsForTotalDisplay();
        ElementType attackElement = ResolveReflectionAttackElement(attackCards);

        if (forPlayerPanel == bm.ReflectionAttackTotalOnPlayerSide)
        {
            int power = bm.ReflectionAttackPower;
            if (power > 0)
            {
                text = $"ATK {power}";
                element = attackElement;
            }
            return true;
        }

        var defenseCards = ResolveReflectionDefenderCards(bm, forPlayerPanel);
        if (defenseCards == null || defenseCards.Count == 0) return true;
        if (BlockingRules.AnyDefenseCardResolvesAsReflectionOrNullify(defenseCards, attackCards)) return true;

        var defender = forPlayerPanel ? bm.GetPlayerStatus() : bm.GetEnemyStatus();
        if (defender == null) return true;
        if (_power.GetEffectiveDefensePowerForDisplay(defenseCards, defender, attackElement) <= 0) return true;

        text = _power.FormatDefensePowerLabel(defenseCards, defender, attackElement);
        element = _power.GetDefenseDisplayCombinedElement(defenseCards, defender);
        return true;
    }

    /// <summary>反射中の防御側パネル：掲出済みの防御シーケンス、またはプレイヤー入力中の選択カード。</summary>
    private List<CardData> ResolveReflectionDefenderCards(BattleManager bm, bool forPlayerPanel)
    {
        Side side = forPlayerPanel ? Side.Player : Side.Enemy;
        if (_state.CurrentSequenceCards.Count > 0
            && _state.SequenceOwnerSide == side
            && _state.CurrentSequenceType == "防御")
            return ToMutableList(_state.CurrentSequenceCards);

        if (forPlayerPanel && bm.IsPlayerDefenseInputActive())
            return BattleUIManager.I?.GetSelectedDefenseCards();

        return null;
    }

    private static ElementType ResolveReflectionAttackElement(List<CardData> attackCards)
    {
        var postDeathCtx = PostDeathCombatContext.Active;
        if (postDeathCtx != null && postDeathCtx.MatchesIncoming(attackCards))
            return postDeathCtx.AttackElement;
        return ElementHelper.GetIncomingAttackElement(attackCards);
    }

    private bool TryResolvePlayerHeldOutgoingAttackText(BattleManager bm, out string text)
    {
        text = null;
        if (bm == null) return false;
        if (!TotalAtkDefCombatPhaseRules.IsPlayerOutgoingAttackTotalHeld(bm)) return false;

        text = ResolvePlayerOutgoingAttackDisplayText();
        return !string.IsNullOrEmpty(text);
    }

    private bool ShouldShowPlayerPanelHeldOutgoingAttack(BattleManager bm) =>
        TryResolvePlayerHeldOutgoingAttackText(bm, out _);

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

    private static bool IsPlayerDefenderDefenseTotalContext(BattleManager bm)
    {
        if (bm == null) return false;
        if (!TotalAtkDefCombatPhaseRules.IsPlayerDefendingAgainstEnemyAttack(bm)) return false;
        return bm.CurrentState == GameState.DefensePhase
            || bm.CurrentState == GameState.DefenseConfirmPhase
            || (bm.IsPostDeathDefenseWaitActive() && bm.IsPostDeathPlayerDefender);
    }

    private bool TryGetPlayerDefenderDefenseDisplayText(BattleManager bm, out string text)
    {
        text = "";
        if (!ShouldEvaluatePlayerDefenseTotalDuringEnemyAttack(bm)) return false;

        var defenseCards = ResolvePlayerDefenseCardsForTotalDisplay(bm);
        if (defenseCards != null && defenseCards.Count > 0
            && TotalAtkDefCombatPhaseRules.IsReflectionOrNullifyDefenseRoute(bm, defenseCards))
            return true;

        if (!HasDefenderDefenseDisplay(bm, defenseCards, bm.GetPlayerStatus()))
            return false;

        text = FormatDefenderDefenseLabel(bm, defenseCards, bm.GetPlayerStatus());
        return !string.IsNullOrEmpty(text);
    }

    /// <summary>
    /// 敵攻撃に対するプレイヤー防御 TOTAL（選択中 or 防御掲出シーケンス）。
    /// </summary>
    private static bool ShouldEvaluatePlayerDefenseTotalDuringEnemyAttack(BattleManager bm)
    {
        if (bm == null) return false;
        if (!TotalAtkDefCombatPhaseRules.IsPlayerDefendingAgainstEnemyAttack(bm)) return false;
        if (IsPlayerDefenderDefenseTotalContext(bm)) return true;
        if (bm.IsPlayerDefenseCombatResolving) return true;
        return false;
    }

    private List<CardData> ResolvePlayerDefenseCardsForTotalDisplay(BattleManager bm)
    {
        if (_state.CurrentSequenceCards.Count > 0
            && _state.SequenceOwnerSide == Side.Player
            && _state.CurrentSequenceType == "防御")
            return ToMutableList(_state.CurrentSequenceCards);

        var selected = BattleUIManager.I?.GetSelectedDefenseCards();
        if (selected != null && selected.Count > 0)
            return selected;

        return null;
    }

    private ElementType ResolveIncomingAttackElementForDefenseUi(BattleManager bm)
    {
        if (bm == null) return ElementType.None;
        var incoming = TotalAtkDefCombatPhaseRules.GetIncomingAttackSnapshotForDefenseUi(bm);
        if (incoming == null || incoming.Count == 0) return ElementType.None;
        return ElementHelper.GetIncomingAttackElement(incoming);
    }

    private string FormatDefenderDefenseLabel(
        BattleManager bm,
        List<CardData> defenseCards,
        PlayerStatus defender)
    {
        ElementType attackElement = ResolveIncomingAttackElementForDefenseUi(bm);
        return _power.FormatDefensePowerLabel(defenseCards, defender, attackElement);
    }

    private bool HasDefenderDefenseDisplay(
        BattleManager bm,
        List<CardData> defenseCards,
        PlayerStatus defender)
    {
        if (defender == null) return false;
        ElementType attackElement = ResolveIncomingAttackElementForDefenseUi(bm);
        return _power.GetEffectiveDefensePowerForDisplay(defenseCards, defender, attackElement) > 0;
    }

    private ElementType ResolveDefenderDefenseCombinedElement(
        BattleManager bm,
        List<CardData> defenseCards,
        PlayerStatus defender)
    {
        if (defender == null) return ElementType.None;
        ElementType attackElement = ResolveIncomingAttackElementForDefenseUi(bm);
        if (_power.GetEffectiveDefensePowerForDisplay(defenseCards, defender, attackElement) <= 0)
            return ElementType.None;
        return _power.GetDefenseDisplayCombinedElement(defenseCards, defender);
    }

    private static List<CardData> ToMutableList(IReadOnlyList<CardData> cards)
    {
        if (cards is List<CardData> list) return list;
        return new List<CardData>(cards);
    }
}
