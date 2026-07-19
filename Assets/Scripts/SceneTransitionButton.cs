using UnityEngine;
using UnityEngine.UI;

public class SceneTransitionButton : MonoBehaviour
{
    public string targetSceneName;           // Inspector‚ÅÝ’è
    [SerializeField] private string clickSeAddress;
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
        if (button == null) return;
        if (!string.IsNullOrEmpty(clickSeAddress))
            SoundEffectPlayer.I?.Play(clickSeAddress);
        if (SceneFadeNavigation.TryFadeToScene(targetSceneName))
            button.interactable = false;
    }
}