using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tribute Blood popup: choose HP to pay; damage preview = paid HP * rule multiplier (rounded).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TributeBloodPopupUI : MonoBehaviour
{
    private const string SelectorButtonSe = "Assets/SE/普通カード.mp3";
    private const string WarningMessage = "これでは自決します。よいか？";

    [SerializeField] private TMP_Text hpValueText;
    [SerializeField] private TMP_Text damageValueText;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private GameObject warningBox;
    [SerializeField] private Button hpPlus10Button;
    [SerializeField] private Button hpPlus1Button;
    [SerializeField] private Button hpMinus1Button;
    [SerializeField] private Button hpMinus10Button;
    [SerializeField] private Button confirmButton;

    private int _maxPayableHp;
    private int _paymentHp;
    private float _damageMultiplier;
    private bool _listenersBound;
    private TaskCompletionSource<int> _tcs;

    private void Awake()
    {
        WireReferencesByNameIfNeeded();
    }

    public void Setup(PlayerStatus ps, TributeBloodRuleSO rule)
    {
        _maxPayableHp = ps != null ? Mathf.Max(0, ps.currentHP) : 0;
        _paymentHp = 0;
        _damageMultiplier = rule != null ? Mathf.Max(0f, rule.damageMultiplier) : 2f;
        BindListenersOnce();
        Refresh();
    }

    public static async Task<int> ShowAndWaitAsync(
        PlayerStatus ps,
        TributeBloodRuleSO rule,
        CancellationToken cancellationToken)
    {
        if (BattleUIManager.I == null || ps == null)
            return 0;

        var canvas = BattleUIManager.I.GetPopupCanvas();
        if (canvas == null) return 0;

        var prefab = Resources.Load<GameObject>("Prefab/TributePopup");
        if (prefab == null)
        {
            Debug.LogError("[TributeBloodPopupUI] Resources/Prefab/TributePopup not found");
            return 0;
        }

        var go = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
        if (go == null) return 0;

        var comp = go.GetComponent<TributeBloodPopupUI>();
        if (comp == null)
            comp = go.AddComponent<TributeBloodPopupUI>();

        comp.WireReferencesByNameIfNeeded();
        comp.Setup(ps, rule);

        try
        {
            return await comp.RunAndWaitForConfirmAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        finally
        {
            if (go != null)
                UnityEngine.Object.Destroy(go);
        }
    }

    private void WireReferencesByNameIfNeeded()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (hpValueText == null && t.name == "HPvalue")
                hpValueText = t.GetComponent<TMP_Text>();
            if (damageValueText == null && t.name == "DamageValue")
                damageValueText = t.GetComponent<TMP_Text>();
            if (warningText == null && t.name == "WarningText")
                warningText = t.GetComponent<TMP_Text>();
            if (warningBox == null && t.name == "WarningBox")
                warningBox = t.gameObject;
            if (hpPlus10Button == null && t.name == "HP+10Button")
                hpPlus10Button = t.GetComponent<Button>();
            if (hpPlus1Button == null && t.name == "HP+1Button")
                hpPlus1Button = t.GetComponent<Button>();
            if (hpMinus1Button == null && t.name == "HP-1Button")
                hpMinus1Button = t.GetComponent<Button>();
            if (hpMinus10Button == null && t.name == "HP-10Button")
                hpMinus10Button = t.GetComponent<Button>();
            if (confirmButton == null && t.name == "ConfirmButton")
                confirmButton = t.GetComponent<Button>();
        }
    }

    private void BindListenersOnce()
    {
        if (_listenersBound) return;
        _listenersBound = true;

        BindStatButton(hpPlus10Button, () => ApplyPaymentChange(+10));
        BindStatButton(hpPlus1Button, () => ApplyPaymentChange(+1));
        BindStatButton(hpMinus1Button, () => ApplyPaymentChange(-1));
        BindStatButton(hpMinus10Button, () => ApplyPaymentChange(-10));

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);
    }

    private static void BindStatButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.AddListener(action);
    }

    private void ApplyPaymentChange(int delta)
    {
        int next = Mathf.Clamp(_paymentHp + delta, 0, _maxPayableHp);
        if (next == _paymentHp) return;

        SoundEffectPlayer.I?.Play(SelectorButtonSe);
        _paymentHp = next;
        Refresh();
    }

    private void Refresh()
    {
        if (hpValueText != null)
            hpValueText.text = _paymentHp.ToString();

        int damagePreview = Mathf.RoundToInt(_paymentHp * _damageMultiplier);
        if (damageValueText != null)
            damageValueText.text = damagePreview.ToString();

        bool hpZeroWarning = _paymentHp > 0 && _paymentHp >= _maxPayableHp;
        if (warningText != null)
        {
            warningText.gameObject.SetActive(hpZeroWarning);
            if (hpZeroWarning)
            {
                warningText.text = WarningMessage;
                warningText.color = Color.red;
            }
        }

        if (warningBox != null && warningText == null)
            warningBox.SetActive(hpZeroWarning);
        else if (warningBox != null && warningText != null)
            warningBox.SetActive(hpZeroWarning);

        SetButtonInteractable(hpPlus10Button, _paymentHp < _maxPayableHp);
        SetButtonInteractable(hpPlus1Button, _paymentHp < _maxPayableHp);
        SetButtonInteractable(hpMinus1Button, _paymentHp > 0);
        SetButtonInteractable(hpMinus10Button, _paymentHp > 0);
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null) button.interactable = interactable;
    }

    private void OnConfirm()
    {
        _tcs?.TrySetResult(_paymentHp);
    }

    private async Task<int> RunAndWaitForConfirmAsync(CancellationToken cancellationToken)
    {
        _tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (confirmButton == null)
            return _paymentHp;

        using (cancellationToken.Register(() => _tcs.TrySetCanceled()))
        {
            return await _tcs.Task.ConfigureAwait(true);
        }
    }
}
