using UnityEngine;

/// <summary>
/// RP に基づくランク（プロファイル表示・リザルト・マッチング用）。
/// 各区間は [最小, 最大)（最大は含まない）。マスターのみ 3200 以上。
/// </summary>
/// <summary>ランク帯 ID（<see cref="PlayerRank"/> の Tiers 配列順と一致）。</summary>
public enum RankTierId
{
    Novice = 0,
    Bronze,
    Silver,
    Gold,
    Platinum,
    Diamond,
    Master,
}

public static class PlayerRank
{
    public const int DefaultStartingRp = 1500;
    public const int TierCount = 7;

    private static readonly RankTier[] Tiers =
    {
        new RankTier(1500, 1700, "ルーキー"),
        new RankTier(1700, 1900, "ブロンズ"),
        new RankTier(1900, 2200, "シルバー"),
        new RankTier(2200, 2400, "ゴールド"),
        new RankTier(2400, 2800, "プラチナ"),
        new RankTier(2800, 3200, "ダイアモンド"),
        new RankTier(3200, int.MaxValue, "マスター"),
    };

    private readonly struct RankTier
    {
        public readonly int MinInclusive;
        public readonly int MaxExclusive;
        public readonly string DisplayNameJa;

        public RankTier(int minInclusive, int maxExclusive, string displayNameJa)
        {
            MinInclusive = minInclusive;
            MaxExclusive = maxExclusive;
            DisplayNameJa = displayNameJa;
        }
    }

    public static int GetTierIndex(int rp)
    {
        rp = Mathf.Max(0, rp);
        if (rp < Tiers[0].MinInclusive)
            return 0;

        for (var i = 0; i < Tiers.Length; i++)
        {
            var t = Tiers[i];
            if (rp >= t.MinInclusive && rp < t.MaxExclusive)
                return i;
        }

        return Tiers.Length - 1;
    }

    public static RankTierId GetTierId(int rp) => (RankTierId)GetTierIndex(rp);

    public static string GetDisplayName(int rp) => GetDisplayNameForTier(GetTierIndex(rp));

    public static string GetDisplayNameForTier(int tierIndex)
    {
        tierIndex = Mathf.Clamp(tierIndex, 0, Tiers.Length - 1);
        return Tiers[tierIndex].DisplayNameJa;
    }

    public static string GetDisplayName(RankTierId tier) => GetDisplayNameForTier((int)tier);

    public static bool TryGetNextTierId(int rp, out RankTierId nextTier)
    {
        nextTier = default;
        if (IsMaxRank(rp))
            return false;

        var nextIndex = GetTierIndex(rp) + 1;
        if (nextIndex >= Tiers.Length)
            return false;

        nextTier = (RankTierId)nextIndex;
        return true;
    }

    public static bool IsMaxRank(int rp) => rp >= Tiers[Tiers.Length - 1].MinInclusive;

    /// <summary>次のランク帯の下限 RP まであと何 RP か。マスターは 0。</summary>
    public static int GetRemainingRpToNextTier(int rp)
    {
        rp = Mathf.Max(0, rp);
        if (IsMaxRank(rp))
            return 0;

        var tierIndex = GetTierIndex(rp);
        if (tierIndex >= Tiers.Length - 1)
            return 0;

        if (rp < Tiers[0].MinInclusive)
            return Tiers[0].MaxExclusive - rp;

        var t = Tiers[tierIndex];
        return t.MaxExclusive - rp;
    }

    /// <summary>現在のランク帯内での進捗 0〜1。マスターは 1。ルーキー未満は 0。</summary>
    public static float GetProgressInCurrentTier01(int rp)
    {
        rp = Mathf.Max(0, rp);
        if (IsMaxRank(rp))
            return 1f;

        if (rp < Tiers[0].MinInclusive)
            return 0f;

        var tierIndex = GetTierIndex(rp);
        var t = Tiers[tierIndex];
        var span = t.MaxExclusive - t.MinInclusive;
        if (span <= 0) return 1f;
        return Mathf.Clamp01((float)(rp - t.MinInclusive) / span);
    }
}

/// <summary>
/// バトルリザルトでの RP 増減（プロファイル連動の単一ソース。数値は企画で調整可）。
/// </summary>
public static class BattleResultRpRules
{
    public const int VictoryTotal = 70;
    public const int DefeatTotal = -40;
    public const int StalemateTotal = -15;

    public static GameResultController.RpBundle GetBundle(GameResultController.ResultKind kind)
    {
        int basic = kind switch
        {
            GameResultController.ResultKind.Victory => VictoryTotal,
            GameResultController.ResultKind.Defeat => DefeatTotal,
            _ => StalemateTotal,
        };

        return new GameResultController.RpBundle
        {
            basic = basic,
            underdog = 0,
            stylish = 0,
            rpCost = 0,
        };
    }
}
