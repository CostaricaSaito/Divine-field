using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// メイン画面：RankMatchButton 押下でランクマッチポップアップを生成し、開く演出を再生します。
/// </summary>
[DisallowMultipleComponent]
public sealed class MainRankMatchPopupController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private RectTransform rankMatchButton;
    [Tooltip("未割当なら Resources/Prefab/RankMatchPopup を使用。")]
    [SerializeField] private RankMatchPopupView popupPrefab;
    [SerializeField] private Transform popupParent;

    [Header("ボタン名（未割当時の探索）")]
    [SerializeField] private string rankMatchButtonObjectName = "RankMatchButton";

    RankMatchPopupView _popupInstance;
    Button _sourceButton;

    void Awake()
    {
        ResolveRankMatchButton();
    }

    void OnDestroy()
    {
        if (_popupInstance != null)
        {
            _popupInstance.Closed -= OnPopupClosed;
            Destroy(_popupInstance.gameObject);
        }
    }

    /// <summary>ランクマッチポップアップの開く演出を開始します。</summary>
    public bool TryOpen(Button sourceButton = null)
    {
        if (_popupInstance != null && (_popupInstance.IsOpen || _popupInstance.IsAnimating))
            return false;

        _sourceButton = sourceButton ?? rankMatchButton?.GetComponent<Button>();
        if (_sourceButton != null)
            _sourceButton.interactable = false;

        var popup = EnsurePopupInstance();
        if (popup == null)
        {
            RestoreSourceButton();
            return false;
        }

        if (!popup.TryPlayOpen(RestoreSourceButton))
        {
            RestoreSourceButton();
            return false;
        }

        return true;
    }

    RankMatchPopupView EnsurePopupInstance()
    {
        if (_popupInstance != null)
            return _popupInstance;

        var prefab = popupPrefab;
        if (prefab == null)
        {
            var loaded = Resources.Load<RankMatchPopupView>("Prefab/RankMatchPopup");
            if (loaded != null)
                prefab = loaded;
        }

        if (prefab == null)
        {
            Debug.LogError("[MainRankMatchPopupController] RankMatchPopup プレハブが見つかりません。", this);
            return null;
        }

        var parent = popupParent != null ? popupParent : transform;
        _popupInstance = Instantiate(prefab, parent, false);
        _popupInstance.transform.SetAsLastSibling();
        _popupInstance.Closed += OnPopupClosed;
        _popupInstance.gameObject.SetActive(false);
        return _popupInstance;
    }

    void OnPopupClosed()
    {
        if (_sourceButton != null)
            _sourceButton.interactable = true;
    }

    void ResolveRankMatchButton()
    {
        if (rankMatchButton != null) return;

        var byName = GameObject.Find(rankMatchButtonObjectName);
        if (byName != null)
            rankMatchButton = byName.GetComponent<RectTransform>();

        if (rankMatchButton == null)
        {
            var legacy = GameObject.Find("BattleButton");
            if (legacy != null)
                rankMatchButton = legacy.GetComponent<RectTransform>();
        }
    }

    void RestoreSourceButton()
    {
        if (_sourceButton == null) return;
        if (_popupInstance != null && (_popupInstance.IsOpen || _popupInstance.IsAnimating))
            return;
        _sourceButton.interactable = true;
    }
}
