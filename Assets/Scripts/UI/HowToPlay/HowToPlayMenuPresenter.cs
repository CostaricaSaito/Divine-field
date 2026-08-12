using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>How-to-play menu: wires LIST buttons and opens per-topic detail prefabs.</summary>
[DisallowMultipleComponent]
public sealed class HowToPlayMenuPresenter : MonoBehaviour
{
    const string DefaultCatalogResourcePath = "HowToPlay/HowToPlayCatalog";
    const string DefaultDetailPrefabResourcePath = "Prefab/RuleDetail Variant";
    const string DetailPrefabConventionRoot = "HowToPlay/Details/";

    [SerializeField] private HowToPlayCatalog catalog;
    [SerializeField] private string catalogResourcePath = DefaultCatalogResourcePath;
    [SerializeField] private string defaultDetailPrefabResourcePath = DefaultDetailPrefabResourcePath;
    [SerializeField] private Transform listRoot;
    [SerializeField] private Button backToMainButton;
    [SerializeField] private GameObject menuCanvasRoot;

    GameObject _detailInstance;

    void Awake()
    {
        if (catalog == null && !string.IsNullOrEmpty(catalogResourcePath))
            catalog = Resources.Load<HowToPlayCatalog>(catalogResourcePath);

        if (menuCanvasRoot == null)
        {
            var canvas = transform.Find("Canvas");
            if (canvas != null)
                menuCanvasRoot = canvas.gameObject;
        }

        if (listRoot == null)
        {
            var list = transform.Find("Canvas/LIST");
            if (list != null)
                listRoot = list;
        }

        if (backToMainButton == null)
        {
            var back = transform.Find("Canvas/BacktoMainButton");
            if (back != null)
                backToMainButton = back.GetComponent<Button>();
        }

        WireMenuButtons();

        if (backToMainButton != null)
        {
            backToMainButton.onClick.RemoveAllListeners();
            backToMainButton.onClick.AddListener(CloseMenu);
        }
    }

    void WireMenuButtons()
    {
        if (listRoot == null || catalog == null) return;

        for (var i = 0; i < listRoot.childCount; i++)
        {
            var child = listRoot.GetChild(i);
            if (!System.Enum.TryParse(child.name, out HowToPlayRuleKind kind))
                continue;

            var button = child.GetComponent<Button>();
            if (button == null) continue;

            if (!catalog.TryGetEntry(kind, out var entry))
            {
                button.interactable = false;
                continue;
            }

            button.interactable = entry.isAvailable;
            if (!entry.isAvailable)
                continue;

            button.onClick.RemoveAllListeners();
            var captured = kind;
            button.onClick.AddListener(() => OpenDetail(captured));
        }
    }

    void OpenDetail(HowToPlayRuleKind kind)
    {
        if (catalog == null || !catalog.TryGetEntry(kind, out var entry))
            return;

        CloseDetail();
        HideMenuList();

        var prefab = ResolveDetailPrefab(kind, entry);
        if (prefab == null)
        {
            Debug.LogWarning(
                $"HowToPlayMenuPresenter: detail prefab not found for '{kind}'. " +
                $"Assign Detail Prefab in catalog, set Detail Prefab Resource Path, " +
                $"or create Resources/{DetailPrefabConventionRoot}{kind}.prefab");
            ShowMenuList();
            return;
        }

        _detailInstance = Instantiate(prefab, transform);
        _detailInstance.transform.SetAsLastSibling();
        _detailInstance.name = $"{kind}Detail";

        var view = _detailInstance.GetComponent<RuleDetailView>();
        if (view != null)
            view.Show(entry);

        WireDetailNavigation(_detailInstance);
    }

    GameObject ResolveDetailPrefab(HowToPlayRuleKind kind, HowToPlayRuleEntry entry)
    {
        if (entry.detailPrefab != null)
            return entry.detailPrefab;

        if (!string.IsNullOrEmpty(entry.detailPrefabResourcePath))
        {
            var fromEntryPath = Resources.Load<GameObject>(entry.detailPrefabResourcePath);
            if (fromEntryPath != null)
                return fromEntryPath;
        }

        var conventionPath = DetailPrefabConventionRoot + kind;
        var fromConvention = Resources.Load<GameObject>(conventionPath);
        if (fromConvention != null)
            return fromConvention;

        if (!string.IsNullOrEmpty(defaultDetailPrefabResourcePath))
            return Resources.Load<GameObject>(defaultDetailPrefabResourcePath);

        return null;
    }

    void WireDetailNavigation(GameObject detailRoot)
    {
        WireNamedButton(detailRoot, "BackButton", HandleDetailBackToList);
        WireNamedButton(detailRoot, "SelectedRule", HandleDetailBackToList);
    }

    static void WireNamedButton(GameObject root, string buttonName, UnityAction handler)
    {
        var transforms = root.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name != buttonName)
                continue;

            var button = transforms[i].GetComponent<Button>();
            if (button == null)
                continue;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(handler);
            return;
        }

        Debug.LogWarning($"HowToPlayMenuPresenter: detail button '{buttonName}' was not found on '{root.name}'.");
    }

    void HandleDetailBackToList()
    {
        CloseDetail();
    }

    void CloseDetail()
    {
        if (_detailInstance != null)
        {
            Destroy(_detailInstance);
            _detailInstance = null;
        }

        ShowMenuList();
    }

    void CloseMenu()
    {
        CloseDetail();
        Destroy(gameObject);
    }

    void HideMenuList()
    {
        if (menuCanvasRoot != null)
            menuCanvasRoot.SetActive(false);
    }

    void ShowMenuList()
    {
        if (menuCanvasRoot != null)
            menuCanvasRoot.SetActive(true);
    }
}
