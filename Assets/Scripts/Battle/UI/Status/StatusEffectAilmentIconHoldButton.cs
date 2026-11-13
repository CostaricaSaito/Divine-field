using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hold an ailment icon in BottomStatusPanel to show <see cref="StatusEffectDetailPopupPresenter"/>.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class StatusEffectAilmentIconHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ICancelHandler
{
    private StatusEffectType _effectType = StatusEffectType.None;
    private bool _isHolding;
    private int _pointerId = int.MinValue;

    void Awake()
    {
        EnsureRootRaycastTarget();

        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.gameObject == gameObject) continue;
            graphic.raycastTarget = false;
        }
    }

    void Update()
    {
        if (!_isHolding) return;
        if (!IsPointerStillDown(_pointerId))
            EndHold();
    }

    void OnDisable()
    {
        EndHold();
    }

    public void Configure(StatusEffectType type)
    {
        _effectType = type;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_effectType == StatusEffectType.None) return;
        if (_isHolding) return;

        _isHolding = true;
        _pointerId = eventData.pointerId;
        BattleUIManager.I?.ShowStatusEffectDetail(_effectType);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_isHolding || eventData.pointerId != _pointerId) return;
        EndHold();
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (!_isHolding) return;
        EndHold();
    }

    private void EndHold()
    {
        if (!_isHolding) return;
        _isHolding = false;
        _pointerId = int.MinValue;
        BattleUIManager.I?.HideStatusEffectDetail();
    }

    private void EnsureRootRaycastTarget()
    {
        var image = GetComponent<Image>();
        if (image == null)
            image = gameObject.AddComponent<Image>();

        image.color = new Color(1f, 1f, 1f, 0.004f);
        image.raycastTarget = true;
    }

    private static bool IsPointerStillDown(int pointerId)
    {
        if (Input.touchCount == 0)
            return Input.GetMouseButton(0);

        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.fingerId != pointerId) continue;
            return touch.phase is TouchPhase.Began or TouchPhase.Stationary or TouchPhase.Moved;
        }

        return false;
    }
}
