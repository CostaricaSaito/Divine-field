using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hold to show enemy MagicPool popup (CurrentMagics).
/// Hides on release anywhere (not on pointer exit — popup/layout would flicker otherwise).
/// </summary>
public sealed class EnemyCurrentMagicButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ICancelHandler
{
    private bool _isHolding;
    private int _pointerId = int.MinValue;

    void Awake()
    {
        // Child TMP/Image blocks pointer events to this handler unless raycast is off.
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

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_isHolding) return;

        _isHolding = true;
        _pointerId = eventData.pointerId;
        BattleUIManager.I?.ShowEnemyCurrentMagicPopup();
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
        BattleUIManager.I?.HideEnemyCurrentMagicPopup();
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
