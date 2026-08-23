using UnityEngine;
using UnityEngine.UI;

/// <summary>Opens Settings.prefab from Main or Battle scene SettingsButton.</summary>
[DisallowMultipleComponent]
public sealed class SettingsLauncher : MonoBehaviour
{
    const string DefaultSettingsPrefabResourcePath = "Prefab/Settings";

    [SerializeField] private string settingsPrefabResourcePath = DefaultSettingsPrefabResourcePath;
    [SerializeField] private Transform settingsParent;

    Button _button;
    GameObject _openInstance;

    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(ToggleSettings);
    }

    void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(ToggleSettings);
    }

    void ToggleSettings()
    {
        if (_openInstance != null)
        {
            Destroy(_openInstance);
            _openInstance = null;
            return;
        }

        OpenSettings();
    }

    void OpenSettings()
    {
        var prefab = Resources.Load<GameObject>(settingsPrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"SettingsLauncher: prefab not found at Resources/{settingsPrefabResourcePath}");
            return;
        }

        var parent = ResolveSettingsParent();
        _openInstance = parent != null ? Instantiate(prefab, parent) : Instantiate(prefab);
        _openInstance.transform.SetAsLastSibling();

        if (_openInstance.GetComponent<SettingsView>() == null)
            Debug.LogWarning("SettingsLauncher: SettingsView is not attached to the Settings prefab.");
    }

    Transform ResolveSettingsParent()
    {
        if (settingsParent != null)
            return settingsParent;

        if (_button != null)
        {
            var canvas = _button.GetComponentInParent<Canvas>();
            if (canvas != null)
                return canvas.transform;
        }

        return FindCanvasTransform();
    }

    static Transform FindCanvasTransform()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        return canvas != null ? canvas.transform : null;
    }
}
