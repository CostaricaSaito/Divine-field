using UnityEngine;

/// <summary>
/// RP に基づくランク（プロファイル表示・リザルト・マッチング用）。
/// 各区間は [最小, 最大)（最大は含まない）。レジェンドのみ 3600 以上。
/// </summary>
public static class PlayerRank
{
    public const int DefaultStartingRp = 1500;

    private static readonly RankTier[] Tiers =
    {
        new RankTier(1500, 1700, "ノービス"),
        new RankTier(1700, 1900, "ブロンズ"),
        new RankTier(1900, 2200, "シルバー"),
        new RankTier(2200, 2400, "ゴールド"),
        new RankTier(2400, 2800, "プラチナ"),
        new RankTier(2800, 3200, "ダイアモンド"),
        new RankTier(3200, 3600, "マスター"),
        new RankTier(3600, int.MaxValue, "レジェンド"),
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

    public static string GetDisplayName(int rp)
    {
        rp = Mathf.Max(0, rp);
        if (rp < Tiers[0].MinInclusive)
            return Tiers[0].DisplayNameJa;

        for (int i = 0; i < Tiers.Length; i++)
        {
            var t = Tiers[i];
            if (rp >= t.MinInclusive && rp < t.MaxExclusive)
                return t.DisplayNameJa;
        }

        return Tiers[Tiers.Length - 1].DisplayNameJa;
    }

    public static bool IsMaxRank(int rp) => rp >= 3600;

    /// <summary>次のランク帯の下限 RP まであと何 RP か。レジェンドは 0。</summary>
    public static int GetRemainingRpToNextTier(int rp)
    {
        rp = Mathf.Max(0, rp);
        if (IsMaxRank(rp))
            return 0;

        if (rp < Tiers[0].MinInclusive)
            return Tiers[0].MaxExclusive - rp;

        for (int i = 0; i < Tiers.Length; i++)
        {
            var t = Tiers[i];
            if (rp >= t.MinInclusive && rp < t.MaxExclusive)
                return t.MaxExclusive - rp;
        }

        return 0;
    }

    /// <summary>現在のランク帯内での進捗 0〜1。レジェンドは 1。ノービス未満は 0。</summary>
    public static float GetProgressInCurrentTier01(int rp)
    {
        rp = Mathf.Max(0, rp);
        if (IsMaxRank(rp))
            return 1f;

        if (rp < Tiers[0].MinInclusive)
            return 0f;

        for (int i = 0; i < Tiers.Length; i++)
        {
            var t = Tiers[i];
            if (rp >= t.MinInclusive && rp < t.MaxExclusive)
            {
                int span = t.MaxExclusive - t.MinInclusive;
                if (span <= 0) return 1f;
                return Mathf.Clamp01((float)(rp - t.MinInclusive) / span);
            }
        }

        return 1f;
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
