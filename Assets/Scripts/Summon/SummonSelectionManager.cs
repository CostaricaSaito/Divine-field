using UnityEngine;

public class SummonSelectionManager : MonoBehaviour
{
    public static SummonSelectionManager I;  // シングルトン
    public int SelectedIndex { get; private set; } = 0;

    private SummonData[] summonDataList;

    const string Key = "SelectedSummonIndex";

    void Awake()
    {
        // シングルトン設定
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);

        // Resources/Summons 配下の SummonData アセットをすべて読み込み
        summonDataList = Resources.LoadAll<SummonData>("Summons");

        if (summonDataList == null || summonDataList.Length == 0)
        {
            Debug.LogError("召喚獣データの読み込みに失敗しました。Resources/Summons フォルダに SummonData アセットが存在するか確認してください。");
        }

        SelectedIndex = PlayerPrefs.GetInt(Key, 0);

        // 保存値をロード
        SelectedIndex = PlayerPrefs.GetInt(Key, 0);
    }

    // 選択設定
    public void SetSelectedIndex(int index, bool persist = true)
    {
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