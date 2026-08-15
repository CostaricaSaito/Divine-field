using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MagicPanel にプールされる魔法カードの管理クラス
///
/// 【役割】
/// - プレイヤー/敵それぞれ最大3枚の魔法カードエントリをプール
/// - 手札から初使用時: 残り使用回数 = maxUses - 1 でプールに登録
/// - 同種カード使用時: 残り使用回数に +max(0, maxUses-1)（手札からの1回発動分を差し引いた分）
/// - プールから使用時: 使用回数を1消費（0になったら自動削除）
/// - MP消費チェック（実際の消費は呼び出し側で行う）
/// </summary>
public class MagicPoolManager : MonoBehaviour
{
    public static MagicPoolManager I { get; private set; }

    public const int MaxPoolSize = 3;

    private readonly List<MagicCardEntry> playerPool = new List<MagicCardEntry>();
    private readonly List<MagicCardEntry> enemyPool = new List<MagicCardEntry>();

    private System.Action onPlayerPoolChanged;
    private System.Action onEnemyPoolChanged;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    // ===== 初期化 =====

    public void RegisterOnPoolChanged(System.Action callback)
    {
        onPlayerPoolChanged = callback;
    }

    public void RegisterOnEnemyPoolChanged(System.Action callback)
    {
        onEnemyPoolChanged = callback;
    }

    // ===== プール取得 =====

    private List<MagicCardEntry> GetPool(PlayerType owner)
    {
        return owner == PlayerType.Player ? playerPool : enemyPool;
    }

    // ===== 事前チェック =====

    /// <summary>
    /// 指定カードをプールに追加できるか（空きがある or 同種プール済み）
    /// </summary>
    public bool CanAddToPool(CardData card, PlayerType owner = PlayerType.Player)
    {
        if (card == null || card.cardType != CardType.Magic) return false;
        var pool = GetPool(owner);
        return pool.Count < MaxPoolSize || FindEntry(card, pool) != null;
    }

    /// <summary>
    /// MP が足りているか
    /// </summary>
    public bool HasEnoughMP(CardData card, PlayerStatus status)
    {
        if (card == null || status == null) return false;
        if (status.IsMagicUseForbidden()) return false;
        return status.currentMP >= status.GetEffectiveMagicMpCost(card.mpCost);
    }

    // ===== メイン操作 =====

    /// <summary>
    /// 手札の魔法カードを使用する（プールに登録）
    ///
    /// 【フロー】
    /// 1. 同種カードがプール済み → 手札からの1回発動分を差し引いた残りを加算（+max(0, maxUses-1)）。新規登録と同じ考え方。
    ///    → さらにデッキから手札を1枚追加（上限に余裕がある場合）
    /// 2. プールに空きがある → 残り回数 = maxUses - 1 で新規登録
    /// 3. プールが満杯かつ同種なし → false
    /// </summary>
    public bool TryUseMagicCard(CardData card, List<CardData> hand, int handMax,
                                System.Action drawCardCallback, PlayerType owner = PlayerType.Player)
    {
        if (card == null || card.cardType != CardType.Magic) return false;

        var pool = GetPool(owner);

        // 同種カードがプール済みの場合（手札からの使用で1回消費した分は新規登録と同様に差し引く）
        var existing = FindEntry(card, pool);
        if (existing != null)
        {
            int addUses = Mathf.Max(card.maxUses - 1, 0);
            existing.remainingUses += addUses;
            Debug.Log($"[MagicPoolManager] 同種魔法カード使用({owner}): {card.cardName} +{addUses} (maxUses={card.maxUses}) → 残り {existing.remainingUses}");

            if (hand != null && hand.Count < handMax)
            {
                drawCardCallback?.Invoke();
                Debug.Log($"[MagicPoolManager] 手札1枚追加({owner})");
            }

            NotifyPoolChanged(owner);
            return true;
        }

        // プールに空きがある場合 → maxUses - 1 で登録（初回発動で1回消費済み）
        if (pool.Count < MaxPoolSize)
        {
            int initialUses = Mathf.Max(card.maxUses - 1, 0);
            var entry = new MagicCardEntry(card, initialUses);
            pool.Add(entry);
            Debug.Log($"[MagicPoolManager] 新規登録({owner}): {card.cardName} 使用回数={initialUses} (maxUses={card.maxUses})");

            // 残り0回なら即削除（maxUses=1 のカード）
            if (initialUses <= 0)
            {
                pool.Remove(entry);
                Debug.Log($"[MagicPoolManager] 使用回数0のため即削除({owner}): {card.cardName}");
            }

            NotifyPoolChanged(owner);
            return true;
        }

        Debug.Log($"[MagicPoolManager] プールが満杯のため {card.cardName} は使用不可({owner})");
        return false;
    }

    /// <summary>
    /// プール内の魔法カードを1回使用する（使用回数を消費）
    /// </summary>
    public void ConsumeUse(CardData card, PlayerType owner = PlayerType.Player)
    {
        var pool = GetPool(owner);
        var entry = FindEntry(card, pool);
        if (entry == null)
        {
            Debug.LogWarning($"[MagicPoolManager] ConsumeUse: {card?.cardName} はプールに存在しません({owner})");
            return;
        }

        entry.remainingUses--;
        Debug.Log($"[MagicPoolManager] 使用回数消費({owner}): {card.cardName} 残り={entry.remainingUses}");

        if (entry.remainingUses <= 0)
        {
            pool.Remove(entry);
            Debug.Log($"[MagicPoolManager] 使用回数0({owner}): {card.cardName} をプールから削除");
        }

        NotifyPoolChanged(owner);
    }

    // ===== 状態確認 =====

    public List<MagicCardEntry> GetPoolEntries(PlayerType owner = PlayerType.Player)
        => new List<MagicCardEntry>(GetPool(owner));

    public bool IsInPool(CardData card, PlayerType owner = PlayerType.Player)
        => FindEntry(card, GetPool(owner)) != null;

    public int GetRemainingUses(CardData card, PlayerType owner = PlayerType.Player)
    {
        var entry = FindEntry(card, GetPool(owner));
        return entry?.remainingUses ?? 0;
    }

    public bool IsPoolFull(PlayerType owner = PlayerType.Player)
        => GetPool(owner).Count >= MaxPoolSize;

    /// <summary>
    /// 指定カードを使用できるか（プールに空きがある or 同種プール済み）
    /// </summary>
    public bool CanUseMagicCard(CardData card, PlayerType owner = PlayerType.Player)
    {
        if (card == null || card.cardType != CardType.Magic) return false;
        return !IsPoolFull(owner) || IsInPool(card, owner);
    }

    /// <summary>
    /// 手札から TryUseMagicCard したときに表示されるスロット番号 (0〜2) を、登録前に推定する
    /// </summary>
    public int GetPredictedPlayerSlotIndex(CardData card)
    {
        if (card == null) return 0;
        var pool = playerPool;
        var existing = FindEntry(card, pool);
        if (existing != null)
            return Mathf.Max(0, pool.IndexOf(existing));
        if (pool.Count < MaxPoolSize)
            return pool.Count;
        return MaxPoolSize - 1;
    }

    public List<CardData> GetPooledCardDatas(PlayerType owner = PlayerType.Player)
    {
        var result = new List<CardData>();
        foreach (var e in GetPool(owner)) result.Add(e.cardData);
        return result;
    }

    /// <summary>Remove every entry from the victim's MagicPool and refresh UI.</summary>
    public void ClearPool(PlayerType owner = PlayerType.Player)
    {
        var pool = GetPool(owner);
        if (pool.Count == 0) return;
        pool.Clear();
        Debug.Log($"[MagicPoolManager] ClearPool({owner})");
        NotifyPoolChanged(owner);
    }

    /// <summary>Add remaining uses to every pooled magic card (Magic Fountain).</summary>
    public void AddRemainingUsesToAll(PlayerType owner, int amount)
    {
        if (amount <= 0) return;
        var pool = GetPool(owner);
        if (pool.Count == 0) return;

        for (int i = 0; i < pool.Count; i++)
            pool[i].remainingUses += amount;

        Debug.Log($"[MagicPoolManager] AddRemainingUsesToAll({owner}, +{amount})");
        NotifyPoolChanged(owner);
    }

    // ===== 内部ヘルパー =====

    private MagicCardEntry FindEntry(CardData card, List<MagicCardEntry> pool)
    {
        if (card == null) return null;
        return pool.Find(e => e.cardData != null && e.cardData.cardName == card.cardName);
    }

    private void NotifyPoolChanged(PlayerType owner)
    {
        if (owner == PlayerType.Player)
            onPlayerPoolChanged?.Invoke();
        else
            onEnemyPoolChanged?.Invoke();
    }

    // ===== 後方互換 =====
    // 旧APIをデフォルト引数で維持

    public List<MagicCardEntry> GetPoolEntries() => GetPoolEntries(PlayerType.Player);
    public bool IsInPool(CardData card) => IsInPool(card, PlayerType.Player);
    public int GetRemainingUses(CardData card) => GetRemainingUses(card, PlayerType.Player);
    public bool IsPoolFull() => IsPoolFull(PlayerType.Player);
    public bool CanUseMagicCard(CardData card) => CanUseMagicCard(card, PlayerType.Player);
    public List<CardData> GetPooledCardDatas() => GetPooledCardDatas(PlayerType.Player);
}

/// <summary>
/// MagicPool の1エントリ（カード + 残り使用回数）
/// </summary>
[System.Serializable]
public class MagicCardEntry
{
    public CardData cardData;
    public int remainingUses;

    public MagicCardEntry(CardData data, int uses)
    {
        cardData = data;
        remainingUses = uses;
    }
}
