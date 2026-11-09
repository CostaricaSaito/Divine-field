using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Player hand auto-sort comparison rules (data only, no Unity UI).
/// Order: face-up before face-down; category;
/// then stat ascending (no-stat last); on equal stat → element; on equal stat+element → asset id (name).
/// </summary>
public static class HandSortRules
{
    private const int NoPrimaryStat = 10_000;

    public readonly struct SortableSlot
    {
        public readonly int HandIndex;
        public readonly CardData Card;
        public readonly CardUI Ui;
        public readonly bool FaceDown;

        public SortableSlot(int handIndex, CardData card, CardUI ui, bool faceDown)
        {
            HandIndex = handIndex;
            Card = card;
            Ui = ui;
            FaceDown = faceDown;
        }
    }

    [Serializable]
    public struct SortKeyDebug
    {
        public string AssetName;
        public bool FaceDown;
        public int Category;
        public int Element;
        public int RecoveryBand;
        public int Stat;
        public string KeyLine;
    }

    public static bool ShouldIncludeInSort(CardData card, Transform handPanel)
    {
        if (card == null || handPanel == null) return false;
        if (card.cardUI == null) return false;
        if (card.cardUI.transform.parent != handPanel) return false;
        if (BattleUIManager.I != null
            && BattleUIManager.I.IsPlayerMagicCardUiOnMagicPanel(card))
            return false;
        return true;
    }

    public static List<SortableSlot> CollectSortableSlots(List<CardData> hand, Transform handPanel)
    {
        var list = new List<SortableSlot>();
        if (hand == null || handPanel == null) return list;

        var pending = new List<(int sibling, int handIndex, SortableSlot slot)>();

        for (int i = 0; i < hand.Count; i++)
        {
            var card = hand[i];
            if (!ShouldIncludeInSort(card, handPanel)) continue;

            int sibling = card.cardUI.transform.GetSiblingIndex();
            bool faceDown = card.cardUI.IsFaceDown();
            pending.Add((sibling, i, new SortableSlot(i, card, card.cardUI, faceDown)));
        }

        pending.Sort((a, b) => a.sibling.CompareTo(b.sibling));
        for (int i = 0; i < pending.Count; i++)
            list.Add(pending[i].slot);

        return list;
    }

    public static void SortSlotsInPlace(List<SortableSlot> slots)
    {
        if (slots == null || slots.Count <= 1) return;
        slots.Sort(Compare);
    }

    public static int Compare(SortableSlot a, SortableSlot b)
    {
        if (a.FaceDown != b.FaceDown)
            return a.FaceDown ? 1 : -1;

        var ca = a.Card;
        var cb = b.Card;
        if (ca == null && cb == null) return 0;
        if (ca == null) return 1;
        if (cb == null) return -1;

        int category = GetCategoryOrder(ca).CompareTo(GetCategoryOrder(cb));
        if (category != 0) return category;

        if (ca.cardType == CardType.Recovery && cb.cardType == CardType.Recovery)
        {
            int band = GetRecoveryBand(ca).CompareTo(GetRecoveryBand(cb));
            if (band != 0) return band;
        }

        return CompareStatElementThenAssetId(ca, cb);
    }

    /// <summary>
    /// Rule 2 then 3: primary stat ascending (no-stat after stat-having);
    /// if stat equal → element order; if stat and element equal → asset name (Ordinal).
    /// </summary>
    private static int CompareStatElementThenAssetId(CardData a, CardData b)
    {
        int stat = ComparePrimaryStat(a, b);
        if (stat != 0) return stat;

        int element = ((int)a.element).CompareTo((int)b.element);
        if (element != 0) return element;

        return CompareAssetName(a, b);
    }

    public static SortKeyDebug BuildDebugKey(CardData card, bool faceDown)
    {
        var d = new SortKeyDebug
        {
            AssetName = card != null ? card.name : "(null)",
            FaceDown = faceDown,
            Category = card != null ? GetCategoryOrder(card) : -1,
            Element = card != null ? (int)card.element : -1,
            RecoveryBand = card != null && card.cardType == CardType.Recovery ? GetRecoveryBand(card) : -1,
            Stat = card != null ? GetPrimaryStatForDebug(card) : 0,
        };

        var sb = new StringBuilder(128);
        sb.Append(faceDown ? "BACK" : "FACE");
        sb.Append(" cat=").Append(d.Category);
        if (card != null && card.cardType == CardType.Recovery)
            sb.Append(" recvBand=").Append(d.RecoveryBand);
        if (card != null)
        {
            sb.Append(" stat=").Append(d.Stat);
            sb.Append(" type=").Append(card.cardType);
        }

        sb.Append(" elem=").Append(d.Element);

        sb.Append(" id=").Append(d.AssetName);
        d.KeyLine = sb.ToString();
        return d;
    }

    public static void LogOrderedKeys(IReadOnlyList<SortableSlot> ordered, string context)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var sb = new StringBuilder();
        sb.Append("[HandSort] ").Append(context).Append(" (").Append(ordered.Count).Append(")\n");
        for (int i = 0; i < ordered.Count; i++)
        {
            var s = ordered[i];
            var key = BuildDebugKey(s.Card, s.FaceDown);
            sb.Append("  ").Append(i).Append(": ").Append(key.KeyLine).Append('\n');
        }

        Debug.Log(sb.ToString());
#endif
    }

    private static int GetCategoryOrder(CardData card)
    {
        switch (card.cardType)
        {
            case CardType.Attack: return 0;
            case CardType.Defense: return 1;
            case CardType.Recovery: return 2;
            case CardType.Magic: return 3;
            case CardType.ArchMagic: return 4;
            case CardType.Special:
            case CardType.Disaster:
                return 5;
            default:
                return 6;
        }
    }

    private static int GetRecoveryBand(CardData card)
    {
        if (card.healsHP) return 0;
        if (card.healsMP) return 1;
        if (card.healsGP) return 2;
        if (card.statusEffectCureScope == StatusEffectCureScope.PartialCure) return 3;
        if (card.statusEffectCureScope == StatusEffectCureScope.AllCure) return 4;
        return 5;
    }

    private static bool IsAttackMagic(CardData card)
    {
        return card.cardType == CardType.Magic
            && card.attackPower > 0
            && card.usableInAttackPhase;
    }

    /// <summary>Lower values sort first. <see cref="NoPrimaryStat"/> = no ATK/DEF-style key (sorts after others).</summary>
    private static int GetPrimaryStatSortValue(CardData card)
    {
        if (card == null) return NoPrimaryStat;

        switch (card.cardType)
        {
            case CardType.Attack:
                return card.attackPower;
            case CardType.Defense:
                return card.defensePower;
            case CardType.Recovery:
                return card.recoveryAmount;
            case CardType.Magic:
                return IsAttackMagic(card) ? card.attackPower : NoPrimaryStat;
            case CardType.ArchMagic:
                return card.attackPower > 0 ? card.attackPower : NoPrimaryStat;
            default:
                return NoPrimaryStat;
        }
    }

    private static int ComparePrimaryStat(CardData a, CardData b)
    {
        int va = GetPrimaryStatSortValue(a);
        int vb = GetPrimaryStatSortValue(b);
        bool aHas = va < NoPrimaryStat;
        bool bHas = vb < NoPrimaryStat;

        if (aHas && bHas)
            return va.CompareTo(vb);
        if (aHas != bHas)
            return aHas ? -1 : 1;
        return 0;
    }

    private static int CompareAssetName(CardData a, CardData b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        return string.Compare(a.name, b.name, StringComparison.Ordinal);
    }

    private static int GetPrimaryStatForDebug(CardData card)
    {
        int v = GetPrimaryStatSortValue(card);
        return v >= NoPrimaryStat ? 0 : v;
    }

    public static void ApplySortToHandAndUi(
        List<CardData> hand,
        Transform handPanel,
        IReadOnlyList<SortableSlot> slotsInVisualOrder,
        IReadOnlyList<SortableSlot> sortedLeftToRight)
    {
        if (hand == null || handPanel == null || slotsInVisualOrder == null || sortedLeftToRight == null)
            return;
        if (slotsInVisualOrder.Count != sortedLeftToRight.Count) return;

        for (int j = 0; j < sortedLeftToRight.Count; j++)
        {
            int handIdx = slotsInVisualOrder[j].HandIndex;
            if (handIdx >= 0 && handIdx < hand.Count)
                hand[handIdx] = sortedLeftToRight[j].Card;
        }

        ApplySiblingOrder(sortedLeftToRight);
    }

    /// <summary>Restores HandPanel sibling order after layout-free tweening.</summary>
    public static void ApplySiblingOrder(IReadOnlyList<SortableSlot> sortedLeftToRight)
    {
        if (sortedLeftToRight == null) return;
        for (int sibling = 0; sibling < sortedLeftToRight.Count; sibling++)
        {
            var ui = sortedLeftToRight[sibling].Ui;
            if (ui != null)
                ui.transform.SetSiblingIndex(sibling);
        }
    }

    public static Dictionary<CardUI, Vector2> CaptureAnchoredByUi(IReadOnlyList<SortableSlot> slots)
    {
        var map = new Dictionary<CardUI, Vector2>();
        if (slots == null) return map;

        for (int i = 0; i < slots.Count; i++)
        {
            var ui = slots[i].Ui;
            if (ui == null) continue;
            var rt = ui.transform as RectTransform;
            if (rt == null) continue;
            map[ui] = rt.anchoredPosition;
        }

        return map;
    }

    public static void SnapAnchoredPositions(IReadOnlyDictionary<CardUI, Vector2> anchoredByUi)
    {
        if (anchoredByUi == null) return;
        foreach (var pair in anchoredByUi)
        {
            var ui = pair.Key;
            if (ui == null) continue;
            var rt = ui.transform as RectTransform;
            if (rt == null) continue;
            rt.anchoredPosition = pair.Value;
        }
    }
}
