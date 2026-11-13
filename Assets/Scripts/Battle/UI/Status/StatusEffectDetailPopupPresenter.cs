using UnityEngine;

/// <summary>
/// Shows StatusEffectDetail prefab while a status ailment icon is held.
/// </summary>
public sealed class StatusEffectDetailPopupPresenter : MonoBehaviour
{
    [Tooltip("Unassigned: Resources.Load(\"Prefab/StatusEffectDetail\").")]
    [SerializeField] private GameObject statusEffectDetailPrefab;

    [Tooltip("Unassigned: Resources/StatusEffectDetailSettings.")]
    [SerializeField] private StatusEffectDetailSettings detailSettings;

    private GameObject _popupRoot;
    private StatusEffectDetailPopupView _view;
    private bool _isOpen;
    private StatusEffectType _shownType;

    public bool IsOpen => _isOpen;

    void OnDisable()
    {
        Hide();
    }

    public bool TryShow(StatusEffectType type)
    {
        if (type == StatusEffectType.None) return false;
        if (BattleManager.I != null && BattleManager.I.IsGameEndTriggered) return false;

        if (!EnsurePopupInstance()) return false;

        _shownType = type;
        RefreshContent(type);
        _popupRoot.SetActive(true);
        _isOpen = true;
        return true;
    }

    public void Hide()
    {
        if (!_isOpen) return;
        _isOpen = false;
        _shownType = StatusEffectType.None;
        if (_popupRoot != null)
            _popupRoot.SetActive(false);
    }

    public void RefreshIfOpen()
    {
        if (!_isOpen || _view == null || _shownType == StatusEffectType.None) return;
        RefreshContent(_shownType);
    }

    private bool EnsurePopupInstance()
    {
        if (_popupRoot != null) return true;

        var prefab = ResolvePrefab();
        if (prefab == null)
        {
            Debug.LogError("[StatusEffectDetailPopupPresenter] StatusEffectDetail prefab not found");
            return false;
        }

        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetPopupCanvas() : null;
        if (canvas == null)
        {
            Debug.LogError("[StatusEffectDetailPopupPresenter] Popup canvas not found");
            return false;
        }

        _popupRoot = Instantiate(prefab, canvas.transform, false);
        _view = _popupRoot.GetComponent<StatusEffectDetailPopupView>();
        if (_view == null)
            _view = _popupRoot.AddComponent<StatusEffectDetailPopupView>();
        _view.EnsureHierarchyBound();
        ApplyNonBlockingOverlay(_popupRoot);
        _popupRoot.SetActive(false);
        return true;
    }

    private void RefreshContent(StatusEffectType type)
    {
        if (_view == null) return;

        Sprite icon = null;
        if (BattleUIManager.I != null && BattleUIManager.I.StatusUI != null)
            icon = BattleUIManager.I.StatusUI.GetStatusEffectIcon(type);

        _view.Refresh(type, icon, ResolveDetailSettings());
    }

    private GameObject ResolvePrefab()
    {
        if (statusEffectDetailPrefab != null) return statusEffectDetailPrefab;
        return Resources.Load<GameObject>("Prefab/StatusEffectDetail");
    }

    private StatusEffectDetailSettings ResolveDetailSettings()
    {
        if (detailSettings != null) return detailSettings;
        var loaded = Resources.Load<StatusEffectDetailSettings>("StatusEffectDetailSettings");
        return loaded != null ? loaded : StatusEffectDetailSettings.GetRuntimeFallback();
    }

    private static void ApplyNonBlockingOverlay(GameObject root)
    {
        if (root == null) return;

        var group = root.GetComponent<CanvasGroup>();
        if (group == null)
            group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
    }
}
