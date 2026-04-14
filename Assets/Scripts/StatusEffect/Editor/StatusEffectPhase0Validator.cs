#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// フェーズ0（enum / Catalog / IconSettings）の整合性をエディタから確認する。
/// メニュー: Divine > Verify > フェーズ0 状態異常セットアップを検証
/// </summary>
public static class StatusEffectPhase0Validator
{
    private const string MenuPath = "Divine/Verify/フェーズ0 状態異常セットアップを検証";

    [MenuItem(MenuPath)]
    public static void Run()
    {
        int errors = 0;
        int warnings = 0;

        errors += ValidateCatalog();
        errors += ValidateEnumMatchesCatalog();

        var guids = AssetDatabase.FindAssets("t:StatusEffectIconSettings");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[Phase0検証] StatusEffectIconSettings アセットがプロジェクト内に見つかりません。Create > Divine > Battle で作成してください。");
            warnings++;
        }
        else
        {
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<StatusEffectIconSettings>(path);
                if (settings == null) continue;
                int e = ValidateIconSettings(settings, path, ref warnings);
                errors += e;
            }
        }

        if (errors == 0 && warnings == 0)
            Debug.Log("[Phase0検証] <b>問題なし</b>：Catalog・enum・登録済み IconSettings を確認しました。");
        else if (errors == 0)
            Debug.Log($"[Phase0検証] <b>エラーなし</b>（警告 {warnings} 件）。Console を確認してください。");
        else
            Debug.LogError($"[Phase0検証] <b>エラー {errors} 件</b>、警告 {warnings} 件。Console を確認してください。");
    }

    private static int ValidateCatalog()
    {
        int errors = 0;
        var all = StatusEffectCatalog.AllAilments;
        if (all == null || all.Length != 15)
        {
            Debug.LogError($"[Phase0検証] AllAilments は 15 要素である必要があります（現在: {all?.Length ?? 0}）。");
            errors++;
        }

        var names = StatusEffectCatalog.OfficialDisplayNames;
        if (names == null || names.Length != 15)
        {
            Debug.LogError($"[Phase0検証] OfficialDisplayNames は 15 要素である必要があります（現在: {names?.Length ?? 0}）。");
            errors++;
        }

        if (all == null) return errors;

        var seen = new HashSet<StatusEffectType>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == StatusEffectType.None)
            {
                Debug.LogError($"[Phase0検証] AllAilments[{i}] が None です。");
                errors++;
                continue;
            }
            if (!seen.Add(all[i]))
            {
                Debug.LogError($"[Phase0検証] AllAilments 内に重複: {all[i]}");
                errors++;
            }

            int id = StatusEffectCatalog.ToOfficialId(all[i]);
            if (id != i + 1)
            {
                Debug.LogError($"[Phase0検証] AllAilments[{i}]={all[i]} の公式IDが期待と違います（期待 {i + 1}、実際 {id}）。");
                errors++;
            }

            var back = StatusEffectCatalog.FromOfficialId(i + 1);
            if (back != all[i])
            {
                Debug.LogError($"[Phase0検証] FromOfficialId({i + 1}) が AllAilments[{i}] と一致しません。");
                errors++;
            }
        }

        return errors;
    }

    /// <summary>
    /// 列挙の明示値 1〜15 が Catalog の順と一致するか。
    /// </summary>
    private static int ValidateEnumMatchesCatalog()
    {
        int errors = 0;
        var all = StatusEffectCatalog.AllAilments;
        if (all == null || all.Length != 15) return errors;

        for (int i = 0; i < 15; i++)
        {
            int expectedId = i + 1;
            if ((int)all[i] != expectedId)
            {
                Debug.LogError($"[Phase0検証] StatusEffectType の数値が公式IDと一致しません: {all[i]} は {(int)all[i]} ですが {expectedId} である必要があります。");
                errors++;
            }
        }

        return errors;
    }

    private static int ValidateIconSettings(StatusEffectIconSettings settings, string assetPath, ref int warnings)
    {
        int errors = 0;
        var entries = settings.Entries;
        if (entries == null || entries.Count != 15)
        {
            Debug.LogError($"[Phase0検証] <b>{assetPath}</b>: entries は 15 行である必要があります（現在: {entries?.Count ?? 0}）。右クリック → 「エントリを15種（公式順）で初期化」で直せます。");
            errors++;
            return errors;
        }

        var seen = new HashSet<StatusEffectType>();
        int missingIcons = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null)
            {
                Debug.LogError($"[Phase0検証] {assetPath}: entries[{i}] が null です。");
                errors++;
                continue;
            }

            if (e.type == StatusEffectType.None)
            {
                Debug.LogError($"[Phase0検証] {assetPath}: entries[{i}].type が None です。");
                errors++;
            }
            else if (!seen.Add(e.type))
            {
                Debug.LogError($"[Phase0検証] {assetPath}: type 重複 {e.type}");
                errors++;
            }

            var expected = StatusEffectCatalog.FromOfficialId(i + 1);
            if (e.type != expected)
            {
                Debug.LogError($"[Phase0検証] {assetPath}: 行 {i} の type は {expected} である必要があります（現在: {e.type}）。初期化メニューで並びを揃えてください。");
                errors++;
            }

            if (e.icon == null)
                missingIcons++;
        }

        if (missingIcons > 0)
        {
            Debug.LogWarning($"[Phase0検証] <b>{assetPath}</b>: Sprite 未設定の行が {missingIcons} あります（実行時はアイコンが出ません）。Debuff フォルダから割り当ててください。");
            warnings++;
        }

        return errors;
    }
}
#endif
