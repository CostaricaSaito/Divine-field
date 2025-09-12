using UnityEngine;
using UnityEngine.UI;

public class SceneTransitionButton : MonoBehaviour
{
    public string targetSceneName;           // Inspectorで設定
    private Button button;

    void Awake()
    {
        Debug.Log("Awake called on SceneTransitionButton!");

        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClickTransition);
    }

    void OnClickTransition()
    {
        if (!string.IsNullOrEmpty(targetSceneName)
            && SceneTransitionManager.I != null
            && SceneTransitionManager.I.gameObject.activeInHierarchy)
        {
            button.interactable = false; // グレーアウト
            SceneTransitionManager.I.FadeToScene(targetSceneName);
        }
        else
        {
            Debug.LogWarning("SceneTransitionManager が null または非アクティブです");
        }
    }
}