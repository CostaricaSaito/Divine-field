// GameProfile.cs
using UnityEngine;

public class GameProfile : MonoBehaviour
{
    public static GameProfile I;

    [SerializeField] private string playerName = "プレイヤー";
    [SerializeField] private string enemyName = "対敵者";

    [Header("ランクポイント")]
    [Tooltip("現在のランクポイント。既定 1500。バトル開始時点は CaptureBattleStartRP で PreBattleRP に退避。")]
    [SerializeField] private int currentRP = PlayerRank.DefaultStartingRp;

    [Header("ランクアイコン（任意・未設定なら非表示のまま）")]
    [SerializeField] private Sprite currentRankIcon;
    [SerializeField] private Sprite nextRankIcon;

    public string PlayerName => playerName;
    public string EnemyName => enemyName;

    /// <summary>バトル開始時点のランクポイント（リザルト画面のカウント起点）。</summary>
    public int PreBattleRP { get; private set; }

    /// <summary>現在のランクポイント（リザルト画面のカウント終点）。</summary>
    public int CurrentRP => currentRP;

    /// <summary>現在 RP に対応するランク名（<see cref="PlayerRank"/>）。</summary>
    public string RankDisplayName => PlayerRank.GetDisplayName(currentRP);
    public Sprite CurrentRankIcon => currentRankIcon;
    public Sprite NextRankIcon => nextRankIcon;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        PlayerProfileService.EnsureLoaded();
        PlayerProfileService.ApplyPersistedStateToGameProfile(this);
    }

    /// <summary>永続プロファイルからランタイム表示用フィールドへ反映する。</summary>
    public void ApplyPersistedPlayerState(string displayName, int rp)
    {
        playerName = string.IsNullOrWhiteSpace(displayName) ? "プレイヤー" : displayName.Trim();
        currentRP = Mathf.Max(0, rp);
        PreBattleRP = currentRP;
    }

    // タイトルで変更した直後に即反映したい時に使う
    public void SetPlayerName(string newName)
    {
        playerName = string.IsNullOrWhiteSpace(newName) ? "プレイヤー" : newName.Trim();
        PlayerProfileService.SetDisplayNameAndSave(playerName);
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
    /// リザルト適用後の RP 絶対値で確定する（永続化・演出計算と一致させる）。
    /// </summary>
    public void SetCurrentRpAfterBattleResult(int newRpAbsolute)
    {
        currentRP = Mathf.Max(0, newRpAbsolute);
    }

    /// <summary>
    /// RP の増分を加える（単体テスト・デバッグ用。リザルト本番は <see cref="SetCurrentRpAfterBattleResult"/> を推奨）。
    /// </summary>
    public void ApplyBattleResult(int deltaRP)
    {
        currentRP = Mathf.Max(0, currentRP + deltaRP);
    }
}
