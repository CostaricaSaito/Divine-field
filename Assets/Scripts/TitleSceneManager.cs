using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleSceneManager : MonoBehaviour
{
    public void OnStartButtonClicked()
    {
        SceneManager.LoadScene("Main"); // ゲーム本編のシーン名に合わせて！
    }
}