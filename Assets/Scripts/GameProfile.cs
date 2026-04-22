// GameProfile.cs
using UnityEngine;

public class GameProfile : MonoBehaviour
{
    public static GameProfile I;

    [SerializeField] private string playerName = "プレイヤー";
    [SerializeField] private string enemyName = "対敵者";

    [Header("ランクポイント（プレースホルダー：将来差し替え予定）")]
    [Tooltip("現在のランクポイント。バトル開始時点の値は CaptureBattleStartRP で PreBattleRP に退避する。")]
    [SerializeField] private int currentRP = 1000;
    [Tooltip("UI に表示するランク名。現時点は固定のプレースホルダー。")]
    [SerializeField] private string rankDisplayName = "Placeholder";
    [Tooltip("次のランクに必要な RP 閾値。NextRankSlider の最大値兼 NextRankValue の基準。")]
    [SerializeField] private int nextRankThresholdRP = 1500;

    [Header("ランクアイコン（プレースホルダー）")]
    [SerializeField] private Sprite currentRankIcon;
    [SerializeField] private Sprite nextRankIcon;

    public string PlayerName => playerName;
    public string EnemyName => enemyName;

    /// <summary>バトル開始時点のランクポイント（リザルト画面のカウント起点）。</summary>
    public int PreBattleRP { get; private set; }

    /// <summary>現在のランクポイント（リザルト画面のカウント終点）。</summary>
    public int CurrentRP => currentRP;

    public string RankDisplayName => rankDisplayName;
    public int NextRankThresholdRP => nextRankThresholdRP;
    public Sprite CurrentRankIcon => currentRankIcon;
    public Sprite NextRankIcon => nextRankIcon;

    private const string PlayerNameKey = TitleNameInput.PlayerNameKey;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        // 起動時に保存値を読み込み
        var saved = PlayerPrefs.GetString(PlayerNameKey, "");
        playerName = string.IsNullOrWhiteSpace(saved) ? playerName : saved;

        PreBattleRP = currentRP;
    }

    // タイトルで変更した直後に即反映したい時に使う
    public void SetPlayerName(string newName)
    {
        playerName = string.IsNullOrWhiteSpace(newName) ? "プレイヤー" : newName.Trim();
    }

    // 敵名を変えたい将来用API（今は固定）
    public void SetEnemyName(string newName)
    {
        enemyName = string.IsNullOrWhiteSpace(newName) ? "対敵者" : newName.Trim();
    }

    /// <summary>
    /// バトル開始直前の RP スナップショットを更新する（リザルト画面のカウント起点に使う）。
    /// </summary>
    public void CaptureBattleStartRP()
    {
        PreBattleRP = currentRP;
    }

    /// <summary>
    /// リザルト画面の演出完了後に、RP の増減を反映する（ダミー実装）。
    /// </summary>
    public void ApplyBattleResult(int deltaRP)
    {
        currentRP = Mathf.Max(0, currentRP + deltaRP);
    }
}
