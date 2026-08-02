using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 両替ポップアップのUIロジックを担当するクラス
///
/// 【振替ルール】
/// ① HP ⇔ MP（1:1）
/// ② GP ⇔ MP（GP増は MP 優先、MP0 なら HP。GP減は MP に戻す）
/// ③ 各ステータスは 0〜99。±10 等は上限・下限まで部分適用し、それ以上は動かさない。
///    これ以上動かせないときのみボタンをグレーアウトする。
/// </summary>
public class ExchangePopupUI : MonoBehaviour
{
    private const string SelectorButtonSe = "Assets/SE/普通カード.mp3";

    [Header("ステータス表示")]
    [SerializeField] private TMP_Text hpValueText;
    [SerializeField] private TMP_Text mpValueText;
    [SerializeField] private TMP_Text gpValueText;

    [Header("HP 操作ボタン")]
    [SerializeField] private Button hpPlus10Button;
    [SerializeField] private Button hpPlus1Button;
    [SerializeField] private Button hpMinus1Button;
    [SerializeField] private Button hpMinus10Button;

    [Header("MP 操作ボタン")]
    [SerializeField] private Button mpPlus10Button;
    [SerializeField] private Button mpPlus1Button;
    [SerializeField] private Button mpMinus1Button;
    [SerializeField] private Button mpMinus10Button;

    [Header("GP 操作ボタン")]
    [SerializeField] private Button gpPlus10Button;
    [SerializeField] private Button gpPlus1Button;
    [SerializeField] private Button gpMinus1Button;
    [SerializeField] private Button gpMinus10Button;

    [Header("警告・確定・キャンセル")]
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private int currentHP;
    private int currentMP;
    private int currentGP;
    private int maxHP;
    private int maxMP;
    private int maxGP;

    private TaskCompletionSource<bool> tcs;
    private bool listenersBound;

    public void Setup(PlayerStatus ps)
    {
        currentHP = ps.currentHP;
        currentMP = ps.currentMP;
        currentGP = ps.currentGP;
        maxHP = ps.maxHP;
        maxMP = ps.maxMP;
        maxGP = ps.maxGP;

        BindButtonListenersOnce();

        if (warningText != null)
            warningText.gameObject.SetActive(false);

        Refresh();
    }

    private void BindButtonListenersOnce()
    {
        if (listenersBound) return;
        listenersBound = true;

        BindStatButtons(hpPlus10Button, () => ApplyHPChange(+10));
        BindStatButtons(hpPlus1Button, () => ApplyHPChange(+1));
        BindStatButtons(hpMinus1Button, () => ApplyHPChange(-1));
        BindStatButtons(hpMinus10Button, () => ApplyHPChange(-10));

        BindStatButtons(mpPlus10Button, () => ApplyMPChange(+10));
        BindStatButtons(mpPlus1Button, () => ApplyMPChange(+1));
        BindStatButtons(mpMinus1Button, () => ApplyMPChange(-1));
        BindStatButtons(mpMinus10Button, () => ApplyMPChange(-10));

        BindStatButtons(gpPlus10Button, () => ApplyGPChange(+10));
        BindStatButtons(gpPlus1Button, () => ApplyGPChange(+1));
        BindStatButtons(gpMinus1Button, () => ApplyGPChange(-1));
        BindStatButtons(gpMinus10Button, () => ApplyGPChange(-10));

        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);
    }

    private static void BindStatButtons(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.AddListener(action);
    }

    private void PlaySelectorSound()
    {
        SoundEffectPlayer.I?.Play(SelectorButtonSe);
    }

    /// <summary>HP±n：MP と 1:1 で振替（上限・下限まで部分適用）。</summary>
    private void ApplyHPChange(int requestedHpDelta)
    {
        int applied = ComputeHpMpTransferHpDelta(requestedHpDelta);
        if (applied == 0) return;

        PlaySelectorSound();
        currentHP += applied;
        currentMP -= applied;
        Refresh();
    }

    /// <summary>MP±n：HP と 1:1 で振替（上限・下限まで部分適用）。</summary>
    private void ApplyMPChange(int requestedMpDelta)
    {
        int applied = ComputeHpMpTransferHpDelta(-requestedMpDelta);
        if (applied == 0) return;

        PlaySelectorSound();
        currentHP += applied;
        currentMP -= applied;
        Refresh();
    }

    /// <summary>GP±n：増は MP 優先（MP0 なら HP）、減は MP に戻す。部分適用あり。</summary>
    private void ApplyGPChange(int delta)
    {
        if (delta > 0)
        {
            int gain = ComputeMaxGpGain(delta);
            if (gain <= 0) return;

            PlaySelectorSound();
            currentGP += gain;
            int remaining = gain;
            int fromMp = Mathf.Min(remaining, currentMP);
            currentMP -= fromMp;
            remaining -= fromMp;
            if (remaining > 0)
                currentHP -= remaining;
        }
        else
        {
            int loss = ComputeGpLossToMp(-delta);
            if (loss <= 0) return;

            PlaySelectorSound();
            currentGP -= loss;
            currentMP += loss;
        }

        Refresh();
    }

    /// <summary>
    /// HP↔MP 1:1 振替で実際に動かせる HP 変化量。正＝HP増・MP減、負＝HP減・MP増。
    /// </summary>
    private int ComputeHpMpTransferHpDelta(int requestedHpDelta)
    {
        if (requestedHpDelta == 0) return 0;

        if (requestedHpDelta > 0)
        {
            int hpRoom = maxHP - currentHP;
            int mpAvail = currentMP;
            if (hpRoom <= 0 || mpAvail <= 0) return 0;
            return Mathf.Min(requestedHpDelta, hpRoom, mpAvail);
        }

        int hpAvail = currentHP;
        int mpRoom = maxMP - currentMP;
        if (hpAvail <= 0 || mpRoom <= 0) return 0;
        return -Mathf.Min(-requestedHpDelta, hpAvail, mpRoom);
    }

    private bool CanApplyHPChange(int delta) => ComputeHpMpTransferHpDelta(delta) != 0;

    private bool CanApplyMPChange(int delta) => ComputeHpMpTransferHpDelta(-delta) != 0;

    private bool CanApplyGPChange(int delta)
    {
        if (delta == 0) return false;
        return delta > 0 ? ComputeMaxGpGain(delta) > 0 : ComputeGpLossToMp(-delta) > 0;
    }

    /// <summary>GP を減らして MP に戻すとき、実際に動かせる GP 減少量。</summary>
    private int ComputeGpLossToMp(int requestedLoss)
    {
        if (requestedLoss <= 0) return 0;
        if (currentGP <= 0) return 0;

        int mpRoom = maxMP - currentMP;
        if (mpRoom <= 0) return 0;

        return Mathf.Min(requestedLoss, currentGP, mpRoom);
    }

    /// <summary>GP を増やすとき、MP→HP の順で requested まで何ポイント増やせるか。</summary>
    private int ComputeMaxGpGain(int requested)
    {
        int room = maxGP - currentGP;
        if (room <= 0 || requested <= 0) return 0;

        int want = Mathf.Min(requested, room);
        int mpPool = currentMP;
        int hpPool = currentHP;
        int gain = 0;
        for (int i = 0; i < want; i++)
        {
            if (mpPool > 0)
            {
                mpPool--;
                gain++;
            }
            else if (hpPool > 0)
            {
                hpPool--;
                gain++;
            }
            else
            {
                break;
            }
        }

        return gain;
    }

    private void Refresh()
    {
        if (hpValueText != null) hpValueText.text = currentHP.ToString();
        if (mpValueText != null) mpValueText.text = currentMP.ToString();
        if (gpValueText != null) gpValueText.text = currentGP.ToString();

        bool hpDanger = currentHP <= 0;
        if (warningText != null)
        {
            warningText.gameObject.SetActive(hpDanger);
            warningText.text = "これでは自決します。よいか？";
            warningText.color = Color.red;
        }

        SetButtonInteractable(hpPlus10Button, CanApplyHPChange(+10));
        SetButtonInteractable(hpPlus1Button, CanApplyHPChange(+1));
        SetButtonInteractable(hpMinus1Button, CanApplyHPChange(-1));
        SetButtonInteractable(hpMinus10Button, CanApplyHPChange(-10));

        SetButtonInteractable(mpPlus10Button, CanApplyMPChange(+10));
        SetButtonInteractable(mpPlus1Button, CanApplyMPChange(+1));
        SetButtonInteractable(mpMinus1Button, CanApplyMPChange(-1));
        SetButtonInteractable(mpMinus10Button, CanApplyMPChange(-10));

        SetButtonInteractable(gpPlus10Button, CanApplyGPChange(+10));
        SetButtonInteractable(gpPlus1Button, CanApplyGPChange(+1));
        SetButtonInteractable(gpMinus1Button, CanApplyGPChange(-1));
        SetButtonInteractable(gpMinus10Button, CanApplyGPChange(-10));
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null) button.interactable = interactable;
    }

    private void OnConfirm()
    {
        tcs?.TrySetResult(true);
    }

    private void OnCancel()
    {
        tcs?.TrySetResult(false);
    }

    public void ForceCancel()
    {
        tcs?.TrySetResult(false);
    }

    public Task<bool> WaitForDecisionAsync()
    {
        tcs = new TaskCompletionSource<bool>();
        return tcs.Task;
    }

    public int GetResultHP() => currentHP;
    public int GetResultMP() => currentMP;
    public int GetResultGP() => currentGP;
}
