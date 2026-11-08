using System.Collections.Generic;

/// <summary>
/// 魔法無料詠唱：フラグ付きカードを魔法と同時選択すると、その魔法の消費MPを0にする。
/// 触媒は ATK/DEF に加算せず、カードは消費される。大魔法は対象外。同時に1枚まで。
/// </summary>
public static class FreeMagicCastRules
{
    public const string HandLabel = "FREE CAST";

    public static bool IsFreeMagicCastCard(CardData card)
    {
        if (card == null) return false;
        if (!card.enablesFreeMagicCast) return false;
        if (CardRules.IsPassiveHandOnly(card)) return false;
        return true;
    }

    /// <summary>
    /// ATK/DEF がなく単体では戦闘に寄与しない触媒専用（像など）。防具 DEF 付きイヤリングは false。
    /// </summary>
    public static bool IsCatalystOnlyCard(CardData card) =>
        IsFreeMagicCastCard(card) && card.attackPower <= 0 && card.defensePower <= 0;

    public static bool IsEligibleMagic(CardData card)
    {
        if (card == null) return false;
        if (card.cardType != CardType.Magic) return false;
        if (ArchMagicRules.IsArchMagicCard(card)) return false;
        return true;
    }

    public static bool SelectionHasEligibleMagic(IReadOnlyList<CardData> selection) =>
        SelectionHasEligibleMagic(selection, includePlayerMagicPanelPick: true);

    public static bool SelectionHasEligibleMagic(
        IReadOnlyList<CardData> selection,
        bool includePlayerMagicPanelPick)
    {
        var ctx = includePlayerMagicPanelPick
            ? ResolvePlayerHandSelection(selection)
            : ToList(selection);
        for (int i = 0; i < ctx.Count; i++)
        {
            if (IsEligibleMagic(ctx[i])) return true;
        }
        return false;
    }

    public static bool SelectionHasFreeCast(IReadOnlyList<CardData> selection)
    {
        if (selection == null) return false;
        for (int i = 0; i < selection.Count; i++)
        {
            if (IsFreeMagicCastCard(selection[i])) return true;
        }
        return false;
    }

    public static bool AllSelectedAreFreeCast(IReadOnlyList<CardData> selection)
    {
        if (selection == null || selection.Count == 0) return true;
        for (int i = 0; i < selection.Count; i++)
        {
            var c = selection[i];
            if (c == null) continue;
            if (!IsFreeMagicCastCard(c)) return false;
        }
        return true;
    }

    public static bool IsCombatCatalyst(CardData card, IReadOnlyList<CardData> selection)
    {
        var ctx = ResolvePlayerHandSelection(selection);
        return IsFreeMagicCastCard(card) && SelectionHasEligibleMagic(ctx, includePlayerMagicPanelPick: false);
    }

    public static bool ShouldShowFreeCastLabel(CardData card, IReadOnlyList<CardData> selection)
        => IsFreeMagicCastCard(card) && SelectionHasEligibleMagic(selection);

    public static bool WaivesMagicMp(IReadOnlyList<CardData> selection)
    {
        var ctx = ResolvePlayerHandSelection(selection);
        return SelectionHasEligibleMagic(ctx, includePlayerMagicPanelPick: false)
            && SelectionHasFreeCast(ctx);
    }

    public static int GetEffectiveMagicMpCost(IReadOnlyList<CardData> selection, PlayerStatus status)
    {
        if (status == null) return 0;
        var ctx = ResolvePlayerHandSelection(selection);
        if (WaivesMagicMp(ctx)) return 0;
        return status.GetTotalEffectiveMagicMpForCards(ctx);
    }

    public static bool CanAddFreeCastToSelection(CardData card, IReadOnlyList<CardData> selection)
    {
        if (!IsFreeMagicCastCard(card)) return false;
        var ctx = ResolvePlayerHandSelection(selection);
        if (!SelectionHasEligibleMagic(ctx, includePlayerMagicPanelPick: false)) return false;
        if (ContainsInstance(selection, card)) return false;
        if (SelectionHasFreeCast(selection)) return false;
        return true;
    }

    public static bool HandHasAvailableCatalyst(IReadOnlyList<CardData> hand)
    {
        return FindAvailableCatalyst(hand) != null;
    }

    public static CardData FindAvailableCatalyst(IReadOnlyList<CardData> hand)
    {
        if (hand == null) return null;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!IsFreeMagicCastCard(c)) continue;
            if (!EnemyAI.IsEnemyHandCardAvailable(c)) continue;
            return c;
        }
        return null;
    }

    public static void AppendSelectableFreeCastCards(
        List<CardData> allowed,
        IReadOnlyList<CardData> hand,
        IReadOnlyList<CardData> selection)
    {
        if (allowed == null || hand == null) return;
        var ctx = ResolvePlayerHandSelection(selection);
        if (!SelectionHasEligibleMagic(ctx, includePlayerMagicPanelPick: false)) return;
        if (SelectionHasFreeCast(selection)) return;

        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!IsFreeMagicCastCard(c)) continue;
            if (ContainsInstance(allowed, c)) continue;
            allowed.Add(c);
        }
    }

    /// <summary>
    /// 攻撃確定・MP 判定用。手札選択＋魔法パネルハイライト＋触媒を含む。
    /// </summary>
    public static List<CardData> BuildPlayerAttackConfirmSelection(IReadOnlyList<CardData> handPicks)
    {
        var resolved = ResolvePlayerHandSelection(handPicks);
        var confirm = new List<CardData>();
        for (int i = 0; i < resolved.Count; i++)
        {
            var c = resolved[i];
            if (c == null) continue;
            if (IsFreeMagicCastCard(c))
            {
                confirm.Add(c);
                continue;
            }
            if (CardRules.ContributesToAttackComboCombat(c, resolved))
                confirm.Add(c);
        }
        return confirm;
    }

    /// <summary>
    /// 防御確定・MP 判定用。
    /// </summary>
    /// <summary>CardDisplayPanel 順次表示: 戦闘カード → 魔法 → 無料詠唱触媒。</summary>
    public static List<CardData> OrderCardsForRevealSequence(IReadOnlyList<CardData> cards)
    {
        if (cards == null || cards.Count == 0) return new List<CardData>();

        var combat = new List<CardData>();
        var magic = new List<CardData>();
        var catalyst = new List<CardData>();

        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            if (IsFreeMagicCastCard(c))
                catalyst.Add(c);
            else if (IsEligibleMagic(c))
                magic.Add(c);
            else
                combat.Add(c);
        }

        var ordered = new List<CardData>(cards.Count);
        ordered.AddRange(combat);
        ordered.AddRange(magic);
        ordered.AddRange(catalyst);
        return ordered;
    }

    public static List<CardData> BuildPlayerDefenseConfirmSelection(IReadOnlyList<CardData> handPicks)
    {
        var resolved = ResolvePlayerHandSelection(handPicks);
        var confirm = new List<CardData>();
        for (int i = 0; i < resolved.Count; i++)
        {
            var c = resolved[i];
            if (c == null) continue;
            if (IsFreeMagicCastCard(c))
            {
                confirm.Add(c);
                continue;
            }
            if (IsEligibleMagic(c))
            {
                confirm.Add(c);
                continue;
            }
            if (CardRules.IsUsableInDefensePhase(c) && !IsCatalystOnlyCard(c))
                confirm.Add(c);
        }
        return confirm;
    }

    /// <summary>
    /// 手札 <see cref="CardSelectionManager"/> 選択に、プレイヤーの魔法パネル選択を足した文脈。
    /// </summary>
    public static List<CardData> ResolvePlayerHandSelection(IReadOnlyList<CardData> handSelection)
    {
        var list = ToList(handSelection);
        if (!IsPlayerFreeCastContext()) return list;

        var bm = BattleManager.I;
        var ui = BattleUIManager.I;
        if (bm == null || ui == null) return list;

        var highlighted = bm.GetSelectedCard();
        if (highlighted == null || !IsEligibleMagic(highlighted)) return list;
        if (ContainsInstance(list, highlighted)) return list;

        if (ui.IsPlayerMagicCardUiOnMagicPanel(highlighted))
            list.Add(highlighted);

        return list;
    }

    private static bool IsPlayerFreeCastContext()
    {
        var bm = BattleManager.I;
        if (bm == null) return false;
        if (bm.CurrentTurnOwner == PlayerType.Player && bm.CurrentState == GameState.AttackPhase)
            return true;
        if (bm.IsPlayerDefenseInputActive()) return true;
        return false;
    }

    private static List<CardData> ToList(IReadOnlyList<CardData> handSelection)
    {
        var list = new List<CardData>();
        if (handSelection == null) return list;
        for (int i = 0; i < handSelection.Count; i++)
        {
            if (handSelection[i] != null) list.Add(handSelection[i]);
        }
        return list;
    }

    public static bool ShouldUseImmediateAttackSingleCardFlow(IReadOnlyList<CardData> fullSelection)
    {
        var ctx = ResolvePlayerHandSelection(fullSelection);
        var combat = ExcludeCatalystsFromCombatStats(ctx);
        if (combat.Count != 1 || !CardRules.IsImmediateAction(combat[0]))
            return false;
        if (CardRules.IsRecoveryCard(combat[0]))
            return false;
        return !SelectionHasFreeCast(ctx);
    }

    public static List<CardData> ExcludeCatalystsFromCombatStats(IReadOnlyList<CardData> cards)
    {
        var result = new List<CardData>();
        if (cards == null) return result;
        var ctx = ResolvePlayerHandSelection(cards);
        bool exclude = SelectionHasEligibleMagic(ctx, includePlayerMagicPanelPick: false);
        for (int i = 0; i < ctx.Count; i++)
        {
            var c = ctx[i];
            if (c == null) continue;
            if (exclude && IsFreeMagicCastCard(c)) continue;
            result.Add(c);
        }
        return result;
    }

    public static List<CardData> PruneInvalidFreeCastSelections(
        List<CardData> selectedCards,
        bool attackContext,
        bool defenseContext)
    {
        var removed = new List<CardData>();
        if (selectedCards == null) return removed;
        var resolved = ResolvePlayerHandSelection(selectedCards);
        if (SelectionHasEligibleMagic(resolved, includePlayerMagicPanelPick: false)) return removed;

        for (int i = selectedCards.Count - 1; i >= 0; i--)
        {
            var c = selectedCards[i];
            if (!IsFreeMagicCastCard(c)) continue;

            if (IsCatalystOnlyCard(c))
            {
                removed.Add(c);
                selectedCards.RemoveAt(i);
                continue;
            }

            bool independentlyLegal = attackContext
                ? CardRules.IsUsableInAttackPhaseStandalone(c)
                : defenseContext && CardRules.IsUsableInDefensePhaseStandalone(c);
            if (independentlyLegal) continue;

            removed.Add(c);
            selectedCards.RemoveAt(i);
        }

        return removed;
    }

    public static IReadOnlyList<CardData> GetCurrentSelectionOrEmpty()
    {
        if (CardSelectionManager.I == null) return System.Array.Empty<CardData>();
        var sel = CardSelectionManager.I.GetSelectedCards();
        return sel ?? (IReadOnlyList<CardData>)System.Array.Empty<CardData>();
    }

    public static bool ContainsInstance(IReadOnlyList<CardData> list, CardData card)
    {
        if (list == null || card == null) return false;
        int id = card.GetInstanceID();
        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];
            if (c != null && c.GetInstanceID() == id) return true;
        }
        return false;
    }

    /// <summary>
    /// 反射／パリィで攻撃カードをパネル間スライドするときの一覧（触媒含む）。
    /// 戦闘計算用の <paramref name="combatIncomingFallback"/> は ATK/DEF 用に触媒除外済みであること。
    /// </summary>
    public static List<CardData> ResolveIncomingAttackCardsForReflectionSlide(
        BattleManager battleManager,
        IReadOnlyList<CardData> combatIncomingFallback)
    {
        if (battleManager != null)
        {
            var full = battleManager.GetAttackCardsForReflectionSlide();
            if (full != null && full.Count > 0)
                return full;
        }

        if (combatIncomingFallback == null || combatIncomingFallback.Count == 0)
            return new List<CardData>();

        return OrderCardsForRevealSequence(combatIncomingFallback);
    }
}
