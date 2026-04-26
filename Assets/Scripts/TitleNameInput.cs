using UnityEngine;
using TMPro;

public class TitleNameInput : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private string defaultName = "ƒvƒŒƒCƒ„[";

    public const string PlayerNameKey = "DF_PlayerName";

    private void Start()
    {
        PlayerProfileService.EnsureLoaded();
        var initial = PlayerProfileService.Data.displayName;
        if (string.IsNullOrWhiteSpace(initial))
            initial = defaultName;
        nameInput.text = initial;

        SaveName(initial);

        nameInput.onEndEdit.AddListener(SaveName);
    }

    private void SaveName(string input)
    {
        var finalName = string.IsNullOrWhiteSpace(input) ? defaultName : input.Trim();

        if (GameProfile.I != null)
            GameProfile.I.SetPlayerName(finalName);
        else
        {
            PlayerProfileService.SetDisplayNameAndSave(finalName);
        }
    }
}