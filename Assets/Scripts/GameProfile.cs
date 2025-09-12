// GameProfile.cs
using UnityEngine;

public class GameProfile : MonoBehaviour
{
    public static GameProfile I;

    [SerializeField] private string playerName = "プレイヤー";
    [SerializeField] private string enemyName = "対敵者";

    public string PlayerName => playerName;
    public string EnemyName => enemyName;

    private const string PlayerNameKey = TitleNameInput.PlayerNameKey;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        // 起動時に保存値を読み込み
        var saved = PlayerPrefs.GetString(PlayerNameKey, "");
        playerName = string.IsNullOrWhiteSpace(saved) ? playerName : saved;
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
}