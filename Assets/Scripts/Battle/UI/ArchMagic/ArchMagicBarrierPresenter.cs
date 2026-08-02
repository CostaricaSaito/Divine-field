using UnityEngine;
using TMPro;

/// <summary>
/// 大魔法詠唱中の HP バリア表示（Barriar.prefab）。
/// 詠唱者の PlayerName / EnemyName と同じ RectTransform 位置に重ねる。
/// </summary>
public class ArchMagicBarrierPresenter : MonoBehaviour
{
    private const string PrefabResourcePath = "Prefab/Barriar";

    [SerializeField] private TMP_Text playerNameAnchor;
    [SerializeField] private TMP_Text enemyNameAnchor;
    [SerializeField] private GameObject barrierPrefab;
    [Tooltip("PlayerName / EnemyName からの追加オフセット（通常は 0）")]
    [SerializeField] private Vector2 nameOverlayOffset = Vector2.zero;

    private GameObject _playerBarrierRoot;
    private GameObject _enemyBarrierRoot;
    private TMP_Text _playerBarrierValueText;
    private TMP_Text _enemyBarrierValueText;

    public void BindNameAnchors(TMP_Text playerName, TMP_Text enemyName)
    {
        if (playerName != null) playerNameAnchor = playerName;
        if (enemyName != null) enemyNameAnchor = enemyName;
    }

    public void Show(Side side, int remaining)
    {
        EnsureInstance(side, out var root, out var valueText);
        if (root == null || valueText == null) return;
        AlignInstanceToNameAnchor(side, root);
        SetValueText(valueText, remaining);
        root.SetActive(true);
    }

    public void UpdateRemaining(Side side, int remaining)
    {
        var valueText = side == Side.Player ? _playerBarrierValueText : _enemyBarrierValueText;
        if (valueText == null) return;
        SetValueText(valueText, remaining);
    }

    public void Hide(Side side)
    {
        var root = side == Side.Player ? _playerBarrierRoot : _enemyBarrierRoot;
        if (root != null)
            root.SetActive(false);
    }

    public void HideAll()
    {
        Hide(Side.Player);
        Hide(Side.Enemy);
    }

    public void SyncFromStatus(PlayerStatus player, PlayerStatus enemy, PlayerStatus playerRef, PlayerStatus enemyRef)
    {
        SyncOne(player, playerRef, Side.Player);
        SyncOne(enemy, enemyRef, Side.Enemy);
    }

    private void SyncOne(PlayerStatus status, PlayerStatus reference, Side side)
    {
        if (status == null || !ReferenceEquals(status, reference)) return;
        if (status.IsCastingArchMagic)
            Show(side, status.archMagicBarrierRemaining);
        else
            Hide(side);
    }

    private void EnsureInstance(Side side, out GameObject root, out TMP_Text valueText)
    {
        if (side == Side.Player)
        {
            root = _playerBarrierRoot;
            valueText = _playerBarrierValueText;
            if (root == null)
                CreateInstance(playerNameAnchor, ref _playerBarrierRoot, ref _playerBarrierValueText);
            root = _playerBarrierRoot;
            valueText = _playerBarrierValueText;
            return;
        }

        root = _enemyBarrierRoot;
        valueText = _enemyBarrierValueText;
        if (root == null)
            CreateInstance(enemyNameAnchor, ref _enemyBarrierRoot, ref _enemyBarrierValueText);
        root = _enemyBarrierRoot;
        valueText = _enemyBarrierValueText;
    }

    private void CreateInstance(TMP_Text nameAnchor, ref GameObject rootRef, ref TMP_Text valueTextRef)
    {
        if (nameAnchor == null) return;

        var prefab = barrierPrefab != null
            ? barrierPrefab
            : Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("[ArchMagicBarrierPresenter] Barriar prefab not found.");
            return;
        }

        var parent = nameAnchor.transform.parent;
        rootRef = Instantiate(prefab, parent);
        rootRef.name = prefab.name + (ReferenceEquals(nameAnchor, playerNameAnchor) ? "_Player" : "_Enemy");

        AlignRectToNameAnchor(nameAnchor.rectTransform, rootRef.GetComponent<RectTransform>());

        valueTextRef = FindBarrierValueText(rootRef.transform);
        rootRef.SetActive(false);
    }

    private void AlignInstanceToNameAnchor(Side side, GameObject root)
    {
        if (root == null) return;
        var nameAnchor = side == Side.Player ? playerNameAnchor : enemyNameAnchor;
        if (nameAnchor == null) return;
        AlignRectToNameAnchor(nameAnchor.rectTransform, root.GetComponent<RectTransform>());
    }

    private void AlignRectToNameAnchor(RectTransform nameRt, RectTransform barrierRt)
    {
        if (nameRt == null || barrierRt == null) return;

        barrierRt.anchorMin = nameRt.anchorMin;
        barrierRt.anchorMax = nameRt.anchorMax;
        barrierRt.pivot = nameRt.pivot;
        barrierRt.sizeDelta = nameRt.sizeDelta;
        barrierRt.anchoredPosition = nameRt.anchoredPosition + nameOverlayOffset;
        barrierRt.localScale = Vector3.one;
        barrierRt.localRotation = Quaternion.identity;
        barrierRt.SetAsLastSibling();
    }

    private static TMP_Text FindBarrierValueText(Transform root)
    {
        if (root == null) return null;
        var direct = root.Find("BarrierRestValue");
        if (direct != null)
            return direct.GetComponent<TMP_Text>();

        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == "BarrierRestValue")
                return texts[i];
        }
        return null;
    }

    private static void SetValueText(TMP_Text text, int remaining)
    {
        if (text != null)
            text.text = Mathf.Max(0, remaining).ToString();
    }
}
