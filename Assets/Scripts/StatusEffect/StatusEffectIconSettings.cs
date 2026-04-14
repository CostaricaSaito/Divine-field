using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 状態異常のアイコン等を Inspector でまとめて設定するアセット。
/// Resources/Debuff 内の画像をプロジェクトビューからドラッグして Sprite として割り当てる。
/// </summary>
[CreateAssetMenu(fileName = "StatusEffectIconSettings", menuName = "Divine/Battle/Status Effect Icon Settings")]
public class StatusEffectIconSettings : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("公式ID 1〜15 に対応")]
        public StatusEffectType type = StatusEffectType.None;

        [Tooltip("UI 用。Debuff フォルダの画像を Sprite にして割り当て")]
        public Sprite icon;

        [Tooltip("デバッグUI等。空のときは type の名前を表示")]
        public string displayName = "";
    }

    [SerializeField]
    private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    /// <summary>
    /// 未設定のときは null。
    /// </summary>
    public Sprite GetIcon(StatusEffectType type)
    {
        if (type == StatusEffectType.None) return null;
        if (entries == null) return null;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].type == type)
                return entries[i].icon;
        }
        return null;
    }

    public string GetDisplayName(StatusEffectType type)
    {
        if (type == StatusEffectType.None) return "";
        if (entries == null) return type.ToString();
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].type == type)
            {
                if (!string.IsNullOrEmpty(entries[i].displayName))
                    return entries[i].displayName;
                break;
            }
        }
        return type.ToString();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (entries == null) return;
        var seen = new HashSet<StatusEffectType>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.type == StatusEffectType.None) continue;
            if (seen.Contains(e.type))
                Debug.LogWarning($"[StatusEffectIconSettings] 重複した type: {e.type} ({name})", this);
            seen.Add(e.type);
        }
    }

    [ContextMenu("エントリを15種（公式順）で初期化")]
    private void EditorInitializeFifteenEntries()
    {
        entries ??= new List<Entry>();
        entries.Clear();
        var types = StatusEffectCatalog.AllAilments;
        var names = StatusEffectCatalog.OfficialDisplayNames;
        for (int i = 0; i < types.Length; i++)
        {
            string dn = (i < names.Length) ? names[i] : "";
            entries.Add(new Entry { type = types[i], displayName = dn });
        }
    }
#endif
}
