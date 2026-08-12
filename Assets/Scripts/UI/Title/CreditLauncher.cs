using UnityEngine;
using UnityEngine.UI;

/// <summary>Opens the credit prefab from Title scene.</summary>
[DisallowMultipleComponent]
public sealed class CreditLauncher : MonoBehaviour
{
    const string DefaultCreditPrefabResourcePath = "Prefab/CREDIT";

    [SerializeField] private string creditPrefabResourcePath = DefaultCreditPrefabResourcePath;
    [SerializeField] private Transform popupParent;

    Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OpenCredit);
    }

    void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OpenCredit);
    }

    void OpenCredit()
    {
        if (FindOpenCreditPopup() != null)
            return;

        var prefab = Resources.Load<GameObject>(creditPrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"CreditLauncher: prefab not found at Resources/{creditPrefabResourcePath}");
            return;
        }

        var parent = popupParent;
        var go = parent != null ? Instantiate(prefab, parent) : Instantiate(prefab);
        go.transform.SetAsLastSibling();
    }

    static GameObject FindOpenCreditPopup()
    {
        var presenter = Object.FindObjectOfType<CreditPopupPresenter>();
        return presenter != null ? presenter.gameObject : null;
    }
}
