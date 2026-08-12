using UnityEngine;

/// <summary>
/// Shows enemy MagicPool via CurrentMagics prefab while EnemyCurrentMagicButton is held.
/// </summary>
public sealed class EnemyCurrentMagicPopupPresenter : MonoBehaviour
{
    [Tooltip("未設定時は Resources.Load(\"Prefab/CurrentMagics\")")]
    [SerializeField] private GameObject currentMagicsPrefab;

    private GameObject _popupRoot;
    private EnemyCurrentMagicPopupView _view;
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    void OnDisable()
    {
        Hide();
    }

    public bool TryShow()
    {
        if (_isOpen) return true;
        if (BattleManager.I != null && BattleManager.I.IsGameEndTriggered) return false;

        BattleManager.I?.CloseBlockingBattlePopups();

        if (!EnsurePopupInstance()) return false;

        RefreshContent();
        _popupRoot.SetActive(true);
        _isOpen = true;
        return true;
    }

    public void Hide()
    {
        if (!_isOpen) return;
        _isOpen = false;
        if (_popupRoot != null)
            _popupRoot.SetActive(false);
    }

    public void RefreshIfOpen()
    {
        if (!_isOpen || _view == null) return;
        RefreshContent();
    }

    private bool EnsurePopupInstance()
    {
        if (_popupRoot != null) return true;

        var prefab = ResolvePrefab();
        if (prefab == null)
        {
            Debug.LogError("[EnemyCurrentMagicPopupPresenter] CurrentMagics prefab not found");
            return false;
        }

        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetPopupCanvas() : null;
        if (canvas == null)
        {
            Debug.LogError("[EnemyCurrentMagicPopupPresenter] Popup canvas not found");
            return false;
        }

        _popupRoot = Instantiate(prefab, canvas.transform, false);
        _view = _popupRoot.GetComponent<EnemyCurrentMagicPopupView>();
        if (_view == null)
            _view = _popupRoot.AddComponent<EnemyCurrentMagicPopupView>();
        _view.EnsureHierarchyBound();
        ApplyNonBlockingOverlay(_popupRoot);
        _popupRoot.SetActive(false);
        return true;
    }

    private GameObject ResolvePrefab()
    {
        if (currentMagicsPrefab != null) return currentMagicsPrefab;
        return Resources.Load<GameObject>("Prefab/CurrentMagics");
    }

    private void RefreshContent()
    {
        if (_view == null || MagicPoolManager.I == null) return;

        Sprite back = BattleManager.I != null ? BattleManager.I.cardBackSprite : null;
        _view.Refresh(MagicPoolManager.I.GetPoolEntries(PlayerType.Enemy), back);
    }

    /// <summary>Hold-to-view overlay must not steal pointer events from the trigger button.</summary>
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
