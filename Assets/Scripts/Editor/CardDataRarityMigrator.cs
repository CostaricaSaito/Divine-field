#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>CardData の isRare → rarity 一括移行・古いアセットの rarity 明示保存。</summary>
public static class CardDataRarityMigrator
{
    [MenuItem("DivineField/Migrate Card Rarity From Legacy isRare")]
    public static void MigrateFromLegacyIsRare()
    {
        int changed = 0;
        foreach (var card in LoadAllCardDataAssets())
        {
            if (card == null) continue;

            var so = new SerializedObject(card);
            var legacy = so.FindProperty("_legacyIsRare");
            if (legacy == null || !legacy.boolValue) continue;
            if (card.rarity != CardRarity.Common) continue;

            card.rarity = CardRarity.SuperRare;
            EditorUtility.SetDirty(card);
            changed++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CardDataRarityMigrator] Legacy isRare → SuperRare: {changed} assets.");
    }

    /// <summary>
    /// isRare フィールド自体が無い古い CardData など、YAML に rarity が未保存のアセットを Common として書き込む。
    /// （実行時デフォルトも Common だが、Inspector 上で明示・編集できるようにする）
    /// </summary>
    [MenuItem("DivineField/Ensure Card Rarity Serialized (Default Common)")]
    public static void EnsureRaritySerializedOnAllCards()
    {
        int touched = 0;
        foreach (var card in LoadAllCardDataAssets())
        {
            if (card == null) continue;

            var path = AssetDatabase.GetAssetPath(card);
            var text = System.IO.File.ReadAllText(path);
            if (text.Contains("\n  rarity:") || text.Contains("\r\n  rarity:"))
                continue;

            // Explicit Common so YAML gets the field; edit per-card in Inspector afterward.
            if (card.rarity != CardRarity.Common)
                card.rarity = CardRarity.Common;

            EditorUtility.SetDirty(card);
            touched++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CardDataRarityMigrator] Wrote rarity to YAML for {touched} CardData assets (default Common).");
    }

    private static CardData[] LoadAllCardDataAssets()
    {
        return LoadAllCardDataAssetsInCardsFolder();
    }

    internal static CardData[] LoadAllCardDataAssetsInCardsFolder()
    {
        var guids = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Resources/Cards" });
        var list = new CardData[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            list[i] = AssetDatabase.LoadAssetAtPath<CardData>(path);
        }
        return list;
    }
}
#endif
