using UnityEngine;

public class SummonSelectionManager : MonoBehaviour
{
    public static SummonSelectionManager I;
    public int SelectedIndex { get; private set; } = 0;

    private SummonData[] summonDataList;

    const string Key = "SelectedSummonIndex";
    const string CatalogResourcePath = "Summons/SummonCatalog";

    [SerializeField]
    [Tooltip("If empty, loads Resources/Summons/SummonCatalog.")]
    private SummonCatalog catalog;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);

        LoadSummonData();

        SelectedIndex = PlayerPrefs.GetInt(Key, 0);
        if (summonDataList != null && summonDataList.Length > 0)
            SelectedIndex = Mathf.Clamp(SelectedIndex, 0, summonDataList.Length - 1);
        else
            SelectedIndex = 0;
    }

    void LoadSummonData()
    {
        if (catalog == null)
            catalog = Resources.Load<SummonCatalog>(CatalogResourcePath);

        if (catalog != null && catalog.Count > 0)
        {
            summonDataList = catalog.ToArray();
            return;
        }

        summonDataList = Resources.LoadAll<SummonData>("Summons");

        if (summonDataList == null || summonDataList.Length == 0)
        {
            Debug.LogError("Failed to load summon data. Check Resources/Summons for SummonCatalog or SummonData.");
        }
        else
        {
            Debug.LogWarning("[SummonSelectionManager] SummonCatalog missing; fell back to Resources.LoadAll (order may be unstable).");
        }
    }

    public void SetSelectedIndex(int index, bool persist = true)
    {
        if (summonDataList != null && summonDataList.Length > 0)
            SelectedIndex = Mathf.Clamp(index, 0, summonDataList.Length - 1);
        else
            SelectedIndex = index;

        if (persist)
        {
            PlayerPrefs.SetInt(Key, SelectedIndex);
            PlayerPrefs.Save();
        }
    }

    public SummonData GetSelectedSummonData()
    {
        if (summonDataList == null || summonDataList.Length == 0) return null;
        return summonDataList[Mathf.Clamp(SelectedIndex, 0, summonDataList.Length - 1)];
    }

    public SummonData[] GetAllSummonData()
    {
        return summonDataList;
    }
}
