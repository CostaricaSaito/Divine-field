using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared ordered summon list for selection screen and SummonSelectionManager.
/// </summary>
[CreateAssetMenu(fileName = "SummonCatalog", menuName = "DivineField/SummonCatalog")]
public class SummonCatalog : ScriptableObject
{
    [SerializeField]
    [Tooltip("Order used for selection index and page navigation.")]
    private List<SummonData> summons = new List<SummonData>();

    public IReadOnlyList<SummonData> Summons => summons;

    public int Count => summons != null ? summons.Count : 0;

    public SummonData GetAt(int index)
    {
        if (summons == null || summons.Count == 0) return null;
        return summons[Mathf.Clamp(index, 0, summons.Count - 1)];
    }

    public SummonData[] ToArray()
    {
        if (summons == null || summons.Count == 0) return new SummonData[0];
        return summons.ToArray();
    }
}
