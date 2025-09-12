using UnityEngine;
using TMPro;

public class TitleNameInput : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private string defaultName = "プレイヤー";

    public const string PlayerNameKey = "DF_PlayerName";

    private void Start()
    {
        // 入力欄に保存値を表示
        var saved = PlayerPrefs.GetString(PlayerNameKey, "");
        var initial = string.IsNullOrWhiteSpace(saved) ? defaultName : saved;
        nameInput.text = initial;

        // 再保存（フォーカスされず終了した場合にも備えて）
        SaveName(initial);

        // ここでイベントを登録
        nameInput.onEndEdit.AddListener(SaveName);
    }

    private void SaveName(string input)
    {
        var finalName = string.IsNullOrWhiteSpace(input) ? defaultName : input.Trim();

        PlayerPrefs.SetString(PlayerNameKey, finalName);
        PlayerPrefs.Save();

        // シングルトンにも即時反映
        if (GameProfile.I != null)
            GameProfile.I.SetPlayerName(finalName);
    }
}