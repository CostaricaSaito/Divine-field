using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ランク帯ごとのアイコン Sprite とアクセントカラーをまとめる ScriptableObject。
/// RP は <see cref="PlayerProfileService"/> に永続化され、表示時に RP → ランク → 見た目を解決します。
/// </summary>
[CreateAssetMenu(fileName = "RankIconSettings", menuName = "Divine/Profile/Rank Icon Settings")]
public sealed class RankIconSettings : ScriptableObject
{
    const string DefaultResourcesPath = "Profile/RankIconSettings";

    [Serializable]
    public sealed class Entry
    {
        public RankTierId tier = RankTierId.Novice;
        public Sprite icon;
        public Color accentColor = Color.white;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    /// <summary>Inspector 未割当時のフォールバック（Resources/Profile/RankIconSettings）。</summary>
    public static RankIconSettings LoadDefault()
    {
        return Resources.Load<RankIconSettings>(DefaultResourcesPath);
    }

    /// <summary><see cref="GameProfile"/> の参照を優先し、無ければ Resources を使う。</summary>
    public static RankIconSettings Resolve()
    {
        if (GameProfile.I != null && GameProfile.I.RankIconSettings != null)
            return GameProfile.I.RankIconSettings;

        return LoadDefault();
    }

    public Sprite GetIcon(RankTierId tier)
    {
        if (entries == null) return null;

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e != null && e.tier == tier)
                return e.icon;
        }

        return null;
    }

    public Color GetAccentColor(RankTierId tier)
    {
        if (entries == null) return Color.white;

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e != null && e.tier == tier)
                return e.accentColor;
        }

        return Color.white;
    }

    public Sprite GetIconForRp(int rp) => GetIcon(PlayerRank.GetTierId(rp));

    public Color GetAccentColorForRp(int rp) => GetAccentColor(PlayerRank.GetTierId(rp));

    public Sprite GetIconForNextTier(int rp)
    {
        if (!PlayerRank.TryGetNextTierId(rp, out var nextTier))
            return null;

        return GetIcon(nextTier);
    }

#if UNITY_EDITOR
    [ContextMenu("エントリを7ランク（公式順）で初期化")]
    void EditorInitializeSevenEntries()
    {
        entries ??= new List<Entry>();
        entries.Clear();

        foreach (RankTierId tier in Enum.GetValues(typeof(RankTierId)))
            entries.Add(new Entry { tier = tier });

        UnityEditor.EditorUtility.SetDirty(this);
    }

    void OnValidate()
    {
        if (entries == null) return;

        var seen = new HashSet<RankTierId>();
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            if (seen.Contains(e.tier))
                Debug.LogWarning($"[RankIconSettings] 重複した tier: {e.tier} ({name})", this);
            seen.Add(e.tier);
        }
    }
#endif
}
