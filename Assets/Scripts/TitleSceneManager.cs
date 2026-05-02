using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleSceneManager : MonoBehaviour
{
    private const string EnterButtonSeAddress = "Assets/SE/バトル開始2.mp3";

    public void OnEnterButtonClicked()
    {
        if (SoundEffectPlayer.I != null && !string.IsNullOrEmpty(EnterButtonSeAddress))
            SoundEffectPlayer.I.Play(EnterButtonSeAddress);

        SceneManager.LoadScene("Main");
    }

    /// <summary>旧ボタン名のまま接続しているシーン向け。</summary>
    public void OnStartButtonClicked() => OnEnterButtonClicked();
}