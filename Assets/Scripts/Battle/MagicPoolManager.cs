using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MagicPanel にプールされる魔法カードの管理クラス（固定3スロット）。
/// </summary>
public class MagicPoolManager : MonoBehaviour
{
    public static MagicPoolManager I { get; private set; }

    public const int MaxPoolSize = 3;

    private readonly MagicCardEntry[] playerSlots = new MagicCardEntry[MaxPoolSize];
    private readonly MagicCardEntry[] enemySlots = new MagicCardEntry[MaxPoolSize];

    private System.Action onPlayerPoolChanged;
    private System.Action onEnemyPoolChanged;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    public void RegisterOnPoolChanged(System.Action callback) => onPlayerPoolChanged = callback;

    public void RegisterOnEnemyPoolChanged(System.Action callback) => onEnemyPoolChanged = callback;

    private MagicCardEntry[] GetSlots(PlayerType owner)
        => owner == PlayerType.Player ? playerSlots : enemySlots;

    private static int FindFirstEmptySlot(MagicCardEntry[] slots)
    {
        for (int i = 0; i < MaxPoolSize; i++)
            if (slots[i] == null) return i;
        return -1;
    }

    private static int CountOccupied(MagicCardEntry[] slots)
    {
        int n = 0;
        for (int i = 0; i < MaxPoolSize; i++)
            if (slots[i] != null) n++;
        return n;
    }

    public bool CanAddToPool(CardData card, PlayerType owner = PlayerType.Player)
    {
        if (card == null || card.cardType != CardType.Magic) return false;
        var slots = GetSlots(owner);
        return FindFirstEmptySlot(slots) >= 0 || FindEntry(card, slots) != null;
    }

    public bool HasEnoughMP(CardData card, PlayerStatus status)
    {
        if (card == null || status == null) return false;
        if (status.IsMagicUseForbidden()) return false;
        if (status.HasInfiniteManasourceEffect()) return true;
        return status.currentMP >= status.GetEffectiveMagicMpCost(card.mpCost);
    }

    public bool TryUseMagicCard(CardData card, List<CardData> hand, int handMax,
                                System.Action drawCardCallback, PlayerType owner = PlayerType.Player)
    {
        if (card == null || card.cardType != CardType.Magic) return false;

        bool infinite = OwnerHasInfiniteManasource(owner);
        var slots = GetSlots(owner);
        var existing = FindEntry(card, slots);
        if (existing != null)
        {
            if (infinite)
            {
                existing.remainingUses = MagicCardEntry.InfiniteRemainingUses;
                Debug.Log($"[MagicPoolManager] 同種魔法カード使用({owner}): {card.cardName} (infinite uses)");
            }
            else
            {
                int addUses = Mathf.Max(card.maxUses - 1, 0);
                existing.remainingUses += addUses;
                Debug.Log($"[MagicPoolManager] 同種魔法カード使用({owner}): {card.cardName} +{addUses} → 残り {existing.remainingUses}");
            }

            if (hand != null && hand.Count < handMax)
            {
                drawCardCallback?.Invoke();
                Debug.Log($"[MagicPoolManager] 手札1枚追加({owner})");
            }

            NotifyPoolChanged(owner);
            return true;
        }

        int empty = FindFirstEmptySlot(slots);
        if (empty >= 0)
        {
            int initialUses = infinite
                ? MagicCardEntry.InfiniteRemainingUses
                : Mathf.Max(card.maxUses - 1, 0);
            var entry = new MagicCardEntry(card, initialUses);
            slots[empty] = entry;
            Debug.Log($"[MagicPoolManager] 新規登録({owner}) slot={empty}: {card.cardName} 使用回数={initialUses}");

            if (!infinite && initialUses <= 0)
            {
                slots[empty] = null;
                Debug.Log($"[MagicPoolManager] 使用回数0のため即削除({owner}): {card.cardName}");
            }

            NotifyPoolChanged(owner);
            return true;
        }

        Debug.Log($"[MagicPoolManager] プールが満杯のため {card.cardName} は使用不可({owner})");
        return false;
    }

    public void ConsumeUse(CardData card, PlayerType owner = PlayerType.Player)
    {
        if (OwnerHasInfiniteManasource(owner)) return;

        var slots = GetSlots(owner);
        int idx = FindEntrySlotIndex(card, slots);
        if (idx < 0)
        {
            Debug.LogWarning($"[MagicPoolManager] ConsumeUse: {card?.cardName} はプールに存在しません({owner})");
            return;
        }

        var entry = slots[idx];
        if (entry.remainingUses == MagicCardEntry.InfiniteRemainingUses) return;

        entry.remainingUses--;
        Debug.Log($"[MagicPoolManager] 使用回数消費({owner}): {card.cardName} 残り={entry.remainingUses}");

        if (entry.remainingUses <= 0)
            slots[idx] = null;

        NotifyPoolChanged(owner);
    }

    public void ApplyInfiniteManasourceToPool(PlayerType owner)
    {
        var slots = GetSlots(owner);
        bool changed = false;
        for (int i = 0; i < MaxPoolSize; i++)
        {
            if (slots[i] == null) continue;
            slots[i].remainingUses = MagicCardEntry.InfiniteRemainingUses;
            changed = true;
        }

        if (changed) NotifyPoolChanged(owner);
    }

    /// <summary>
    /// Sealed EXO-DION: move to right slot, increment stack counter, discard previous right-slot card.
    /// </summary>
    public int ApplySealedExoDionFromHand(CardData card, PlayerType owner, CardDealer dealer)
    {
        var slots = GetSlots(owner);
        var template = SealedExoDionRules.ResolvePoolTemplate(dealer) ?? card;
        int right = SealedExoDionRules.RightSlotIndex;

        int newCount;
        var existing = slots[right];
        if (existing != null && SealedExoDionRules.IsSealedExoDion(existing.cardData))
        {
            existing.remainingUses++;
            existing.usesSealedExoDionCounterStyle = true;
            newCount = existing.remainingUses;
        }
        else
        {
            if (template == null) return 0;
            slots[right] = new MagicCardEntry(template, 1)
            {
                usesSealedExoDionCounterStyle = true,
            };
            newCount = 1;
        }

        NotifyPoolChanged(owner);
        return newCount;
    }

    public int GetSealedExoDionCounter(PlayerType owner)
    {
        var slots = GetSlots(owner);
        int right = SealedExoDionRules.RightSlotIndex;
        var e = slots[right];
        if (e == null || !SealedExoDionRules.IsSealedExoDion(e.cardData)) return 0;
        return e.remainingUses;
    }

    public bool IsSealedExoDionOnPanel(PlayerType owner)
        => GetSealedExoDionCounter(owner) > 0;

    public List<MagicCardEntry> GetPoolEntries(PlayerType owner = PlayerType.Player)
    {
        var slots = GetSlots(owner);
        var result = new List<MagicCardEntry>(MaxPoolSize);
        for (int i = 0; i < MaxPoolSize; i++)
            result.Add(slots[i]);
        return result;
    }

    public bool IsInPool(CardData card, PlayerType owner = PlayerType.Player)
        => FindEntry(card, GetSlots(owner)) != null;

    public int GetRemainingUses(CardData card, PlayerType owner = PlayerType.Player)
    {
        var entry = FindEntry(card, GetSlots(owner));
        return entry?.remainingUses ?? 0;
    }

    public bool IsPoolFull(PlayerType owner = PlayerType.Player)
        => FindFirstEmptySlot(GetSlots(owner)) < 0;

    public bool CanUseMagicCard(CardData card, PlayerType owner = PlayerType.Player)
    {
        if (card == null || card.cardType != CardType.Magic) return false;
        return !IsPoolFull(owner) || IsInPool(card, owner);
    }

    public int GetPredictedPlayerSlotIndex(CardData card)
    {
        if (card == null) return 0;
        var slots = playerSlots;
        var existing = FindEntry(card, slots);
        if (existing != null)
            return FindEntrySlotIndex(card, slots);
        int empty = FindFirstEmptySlot(slots);
        return empty >= 0 ? empty : MaxPoolSize - 1;
    }

    public List<CardData> GetPooledCardDatas(PlayerType owner = PlayerType.Player)
    {
        var result = new List<CardData>();
        var slots = GetSlots(owner);
        for (int i = 0; i < MaxPoolSize; i++)
        {
            if (slots[i]?.cardData != null)
                result.Add(slots[i].cardData);
        }
        return result;
    }

    public void ClearPool(PlayerType owner = PlayerType.Player)
    {
        var slots = GetSlots(owner);
        bool any = false;
        for (int i = 0; i < MaxPoolSize; i++)
        {
            if (slots[i] != null) any = true;
            slots[i] = null;
        }
        if (!any) return;
        Debug.Log($"[MagicPoolManager] ClearPool({owner})");
        NotifyPoolChanged(owner);
    }

    /// <summary>Magic Sealer: destroy pooled magic except Sealed EXO-DION on the right slot.</summary>
    public void ClearPoolForMagicSealer(PlayerType owner = PlayerType.Player)
    {
        var slots = GetSlots(owner);
        bool changed = false;
        for (int i = 0; i < MaxPoolSize; i++)
        {
            if (i == SealedExoDionRules.RightSlotIndex
                && slots[i] != null
                && SealedExoDionRules.IsSealedExoDion(slots[i].cardData))
                continue;

            if (slots[i] != null)
            {
                slots[i] = null;
                changed = true;
            }
        }
        if (!changed) return;
        Debug.Log($"[MagicPoolManager] ClearPoolForMagicSealer({owner})");
        NotifyPoolChanged(owner);
    }

    public void AddRemainingUsesToAll(PlayerType owner, int amount)
    {
        if (amount <= 0) return;
        var slots = GetSlots(owner);
        bool changed = false;
        for (int i = 0; i < MaxPoolSize; i++)
        {
            var e = slots[i];
            if (e == null || e.cardData == null) continue;
            if (SealedExoDionRules.IsSealedExoDion(e.cardData)) continue;
            e.remainingUses += amount;
            changed = true;
        }
        if (!changed) return;
        Debug.Log($"[MagicPoolManager] AddRemainingUsesToAll({owner}, +{amount})");
        NotifyPoolChanged(owner);
    }

    private static MagicCardEntry FindEntry(CardData card, MagicCardEntry[] slots)
    {
        if (card == null) return null;
        for (int i = 0; i < MaxPoolSize; i++)
        {
            var e = slots[i];
            if (e?.cardData != null && e.cardData.cardName == card.cardName)
                return e;
        }
        return null;
    }

    private static int FindEntrySlotIndex(CardData card, MagicCardEntry[] slots)
    {
        if (card == null) return -1;
        for (int i = 0; i < MaxPoolSize; i++)
        {
            var e = slots[i];
            if (e?.cardData != null && e.cardData.cardName == card.cardName)
                return i;
        }
        return -1;
    }

    private void NotifyPoolChanged(PlayerType owner)
    {
        if (owner == PlayerType.Player)
            onPlayerPoolChanged?.Invoke();
        else
            onEnemyPoolChanged?.Invoke();
    }

    public List<MagicCardEntry> GetPoolEntries() => GetPoolEntries(PlayerType.Player);
    public bool IsInPool(CardData card) => IsInPool(card, PlayerType.Player);
    public int GetRemainingUses(CardData card) => GetRemainingUses(card, PlayerType.Player);
    public bool IsPoolFull() => IsPoolFull(PlayerType.Player);
    public bool CanUseMagicCard(CardData card) => CanUseMagicCard(card, PlayerType.Player);
    public List<CardData> GetPooledCardDatas() => GetPooledCardDatas(PlayerType.Player);

    private static bool OwnerHasInfiniteManasource(PlayerType owner)
    {
        var bm = BattleManager.I;
        if (bm == null) return false;
        var ps = owner == PlayerType.Player ? bm.GetPlayerStatus() : bm.GetEnemyStatus();
        return ps != null && ps.HasInfiniteManasourceEffect();
    }
}

[System.Serializable]
public class MagicCardEntry
{
    public const int InfiniteRemainingUses = -1;
    public const string InfiniteUsesDisplay = "\u221E";

    public CardData cardData;
    public int remainingUses;
    public bool usesSealedExoDionCounterStyle;

    public MagicCardEntry(CardData data, int uses)
    {
        cardData = data;
        remainingUses = uses;
    }

    public static string FormatRemainingUsesForDisplay(int remainingUses)
        => remainingUses == InfiniteRemainingUses ? InfiniteUsesDisplay : remainingUses.ToString();
}
