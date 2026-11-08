using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 各 <see cref="StatusEffectType"/> の軽症／重症／特殊分類。Inspector で調整する。
/// </summary>
[CreateAssetMenu(
    fileName = "StatusEffectClassificationConfig",
    menuName = "DivineField/Status/Status Effect Classification")]
public sealed class StatusEffectClassificationConfig : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public StatusEffectType type;
        public StatusEffectAilmentCategory category;
    }

    [Tooltip("状態異常ごとの分類。未登録の型は既定分類または Unspecified→軽症扱い。")]
    public Entry[] entries = Array.Empty<Entry>();

    private Dictionary<StatusEffectType, StatusEffectAilmentCategory> _lookup;
    private bool _lookupDirty = true;

    private static StatusEffectClassificationConfig _runtimeFallback;

    public static StatusEffectClassificationConfig GetRuntimeFallback()
    {
        if (_runtimeFallback == null)
        {
            _runtimeFallback = CreateInstance<StatusEffectClassificationConfig>();
            _runtimeFallback.name = "StatusEffectClassificationConfig (Runtime Fallback)";
            _runtimeFallback.entries = BuildDefaultEntries();
            _runtimeFallback.RebuildLookup();
        }

        return _runtimeFallback;
    }

    public StatusEffectAilmentCategory GetCategory(StatusEffectType type)
    {
        if (type == StatusEffectType.None || type == StatusEffectType.RandomOneAilment)
            return StatusEffectAilmentCategory.Unspecified;

        EnsureLookup();
        if (_lookup.TryGetValue(type, out var cat))
            return cat;

        return GetDefaultCategory(type);
    }

    /// <summary>プロジェクト既定（Inspector 未設定時のフォールバック）。</summary>
    public static StatusEffectAilmentCategory GetDefaultCategory(StatusEffectType type)
    {
        switch (type)
        {
            // 軽症
            case StatusEffectType.Sickness:
            case StatusEffectType.SevereSickness:
            case StatusEffectType.Weaken:
            case StatusEffectType.EyeStrain:
            case StatusEffectType.ClusterHeadache:
            case StatusEffectType.Smoke:
            case StatusEffectType.Freeze:
            case StatusEffectType.Fog:
            case StatusEffectType.Confusion:
            case StatusEffectType.Restraint:
                return StatusEffectAilmentCategory.Mild;

            // 重症
            case StatusEffectType.PurgatorySickness:
            case StatusEffectType.ParadiseSickness:
            case StatusEffectType.Misfortune:
            case StatusEffectType.Intervention:
            case StatusEffectType.CurseBind:
            case StatusEffectType.CyberPsychosis:
                return StatusEffectAilmentCategory.Severe;

            // 特殊
            case StatusEffectType.Kannaduki:
            case StatusEffectType.Zantestuken:
            case StatusEffectType.MilleniumKingdom:
            case StatusEffectType.DiabolicEmission:
            case StatusEffectType.InfiniteManasource:
                return StatusEffectAilmentCategory.Special;

            default:
                return StatusEffectAilmentCategory.Mild;
        }
    }

    public static Entry[] BuildDefaultEntries()
    {
        var types = CollectAllClassifiableTypes();
        var list = new List<Entry>(types.Count);
        foreach (var t in types)
        {
            list.Add(new Entry { type = t, category = GetDefaultCategory(t) });
        }

        return list.ToArray();
    }

    private static List<StatusEffectType> CollectAllClassifiableTypes()
    {
        var list = new List<StatusEffectType>();
        foreach (StatusEffectType t in Enum.GetValues(typeof(StatusEffectType)))
        {
            if (t == StatusEffectType.None || t == StatusEffectType.RandomOneAilment)
                continue;
            list.Add(t);
        }

        list.Sort((a, b) => ((int)a).CompareTo((int)b));
        return list;
    }

    private void OnEnable() => _lookupDirty = true;

    private void OnValidate()
    {
        _lookupDirty = true;
        if (entries == null || entries.Length == 0)
            entries = BuildDefaultEntries();
    }

#if UNITY_EDITOR
    [ContextMenu("Reset Categories To Project Defaults")]
    private void ResetCategoriesToProjectDefaults()
    {
        entries = BuildDefaultEntries();
        _lookupDirty = true;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    internal void RebuildLookup()
    {
        _lookupDirty = true;
        EnsureLookup();
    }

    private void EnsureLookup()
    {
        if (!_lookupDirty && _lookup != null) return;

        _lookup ??= new Dictionary<StatusEffectType, StatusEffectAilmentCategory>();
        _lookup.Clear();

        if (entries != null)
        {
            foreach (var e in entries)
            {
                if (e.type == StatusEffectType.None || e.type == StatusEffectType.RandomOneAilment)
                    continue;
                _lookup[e.type] = e.category;
            }
        }

        _lookupDirty = false;
    }
}
