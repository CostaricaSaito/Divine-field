using UnityEngine;

public class GlobalInitializer : MonoBehaviour
{
    public GameObject fadeCanvasPrefab;

    void Awake()
    {
        if (SceneTransitionManager.I == null)
        {
            var obj = Instantiate(fadeCanvasPrefab);
            DontDestroyOnLoad(obj);
        }
    }
}
