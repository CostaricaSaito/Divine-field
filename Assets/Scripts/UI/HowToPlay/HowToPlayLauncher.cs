using UnityEngine;
using UnityEngine.UI;

/// <summary>Opens the how-to-play menu prefab from Main scene.</summary>
[DisallowMultipleComponent]
public sealed class HowToPlayLauncher : MonoBehaviour
{
    const string DefaultMenuPrefabResourcePath = "Prefab/HowToPlay";

    [SerializeField] private string menuPrefabResourcePath = DefaultMenuPrefabResourcePath;
    [SerializeField] private Transform menuParent;

    Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OpenMenu);
    }

    void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OpenMenu);
    }

    void OpenMenu()
    {
        var prefab = Resources.Load<GameObject>(menuPrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"HowToPlayLauncher: menu prefab not found at Resources/{menuPrefabResourcePath}");
            return;
        }

        var parent = menuParent;
        var go = parent != null ? Instantiate(prefab, parent) : Instantiate(prefab);
        go.transform.SetAsLastSibling();

        if (go.GetComponent<HowToPlayMenuPresenter>() == null)
            go.AddComponent<HowToPlayMenuPresenter>();
    }
}
