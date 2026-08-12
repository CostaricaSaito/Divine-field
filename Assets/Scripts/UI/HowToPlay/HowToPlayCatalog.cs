using System;
using UnityEngine;

[Serializable]
public struct HowToPlayRuleEntry
{
    public HowToPlayRuleKind kind;
    public string menuLabel;

    [Header("Detail Screen")]
    [Tooltip("Per-topic detail prefab. Highest priority when assigned.")]
    public GameObject detailPrefab;

    [Tooltip("Resources path without 'Resources/' (e.g. HowToPlay/Details/Turn). Used when Detail Prefab is empty.")]
    public string detailPrefabResourcePath;

    [Header("Optional Dynamic Fill (RuleDetailView only)")]
    [TextArea(5, 30)] public string body;
    [Tooltip("Optional illustration(s) for RuleDetailView dynamic layout.")]
    public Sprite[] illustrations;

    public bool isAvailable;
}

/// <summary>Catalog: menu availability + detail prefab mapping for how-to-play.</summary>
[CreateAssetMenu(fileName = "HowToPlayCatalog", menuName = "DivineField/UI/How To Play Catalog")]
public sealed class HowToPlayCatalog : ScriptableObject
{
    [SerializeField] private HowToPlayRuleEntry[] entries;

    public bool TryGetEntry(HowToPlayRuleKind kind, out HowToPlayRuleEntry entry)
    {
        if (entries != null)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].kind == kind)
                {
                    entry = entries[i];
                    return true;
                }
            }
        }

        entry = default;
        return false;
    }
}
