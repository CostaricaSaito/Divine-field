using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 両替ポップアップのUIロジックを担当するクラス
///
/// 【役割】
/// - HP/MP/GP の現在値表示
/// - MP/GP の増減ボタン操作（HP を対価として消費）
/// - 上限・下限チェックとボタン有効/無効制御
/// - HP が 0 以下になる場合の警告表示
/// - 確定・キャンセルの非同期待機
/// </summary>
public class ExchangePopupUI : MonoBehaviour
{
    [Header("ステータス表示")]
    [SerializeField] private TMP_Text hpValueText;
    [SerializeField] private TMP_Text mpValueText;
    [SerializeField] private TMP_Text gpValueText;

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

    // 操作中の仮ステータス値
    private int currentHP;
    private int currentMP;
    private int currentGP;
    private int maxHP;
    private int maxMP;
    private int maxGP;

    // 非同期待機用
    private TaskCompletionSource<bool> tcs;

    // ===== 初期化 =====

    /// <summary>
    /// ポップアップを初期化してプレイヤーの現在値をセットする
    /// </summary>
    public void Setup(PlayerStatus ps)
    {
        currentHP = ps.currentHP;
        currentMP = ps.currentMP;
        currentGP = ps.currentGP;
        maxHP = ps.maxHP;
        maxMP = ps.maxMP;
        maxGP = ps.maxGP;

        // ボタンのリスナー登録
        mpPlus10Button.onClick.AddListener(() => OnMPChange(+10));
        mpPlus1Button.onClick.AddListener(() => OnMPChange(+1));
        mpMinus1Button.onClick.AddListener(() => OnMPChange(-1));
        mpMinus10Button.onClick.AddListener(() => OnMPChange(-10));

        gpPlus10Button.onClick.AddListener(() => OnGPChange(+10));
        gpPlus1Button.onClick.AddListener(() => OnGPChange(+1));
        gpMinus1Button.onClick.AddListener(() => OnGPChange(-1));
        gpMinus10Button.onClick.AddListener(() => OnGPChange(-10));

        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);

        // 警告テキストは最初は非表示
        if (warningText != null)
        {
            warningText.gameObject.SetActive(false);
        }

        Refresh();
    }

    // ===== 操作ロジック =====

    /// <summary>
    /// MP を delta 分変更する。HP を対価として消費/回収する。
    /// MP の上限は maxMP、下限は 0。HP の下限は 0（警告のみ）。
    /// </summary>
    private void OnMPChange(int delta)
    {
        // 変更後の MP を計算（上限・下限クランプ）
        int newMP = Mathf.Clamp(currentMP + delta, 0, maxMP);
        int actualDelta = newMP - currentMP; // 実際に変化した量

        if (actualDelta == 0) return;

        currentMP = newMP;
        currentHP -= actualDelta; // HP は対価（MP が増えれば HP が減る）

        Refresh();
    }

    /// <summary>
    /// GP を delta 分変更する。HP を対価として消費/回収する。
    /// GP の上限は maxGP、下限は 0。HP の下限は 0（警告のみ）。
    /// </summary>
    private void OnGPChange(int delta)
    {
        int newGP = Mathf.Clamp(currentGP + delta, 0, maxGP);
        int actualDelta = newGP - currentGP;

        if (actualDelta == 0) return;

        currentGP = newGP;
        currentHP -= actualDelta;

        Refresh();
    }

    /// <summary>
    /// 表示を更新し、ボタンの有効/無効・警告テキストを制御する
    /// </summary>
    private void Refresh()
    {
        // テキスト更新
        if (hpValueText != null) hpValueText.text = currentHP.ToString();
        if (mpValueText != null) mpValueText.text = currentMP.ToString();
        if (gpValueText != null) gpValueText.text = currentGP.ToString();

        // HP が 0 以下の場合は警告表示
        bool hpDanger = currentHP <= 0;
        if (warningText != null)
        {
            warningText.gameObject.SetActive(hpDanger);
            warningText.text = "これでは自決します。よいか？";
            warningText.color = Color.red;
        }

        // MP ボタンの有効/無効
        if (mpPlus10Button != null) mpPlus10Button.interactable = (currentMP < maxMP);
        if (mpPlus1Button != null)  mpPlus1Button.interactable  = (currentMP < maxMP);
        if (mpMinus1Button != null) mpMinus1Button.interactable  = (currentMP > 0);
        if (mpMinus10Button != null) mpMinus10Button.interactable = (currentMP > 0);

        // GP ボタンの有効/無効
        if (gpPlus10Button != null) gpPlus10Button.interactable = (currentGP < maxGP);
        if (gpPlus1Button != null)  gpPlus1Button.interactable  = (currentGP < maxGP);
        if (gpMinus1Button != null) gpMinus1Button.interactable  = (currentGP > 0);
        if (gpMinus10Button != null) gpMinus10Button.interactable = (currentGP > 0);
    }

    // ===== 確定・キャンセル =====

    private void OnConfirm()
    {
        tcs?.TrySetResult(true);
    }

    private void OnCancel()
    {
        tcs?.TrySetResult(false);
    }

    /// <summary>
    /// 外部から強制的にキャンセルする（両替ボタン再押下時に使用）
    /// </summary>
    public void ForceCancel()
    {
        tcs?.TrySetResult(false);
    }

    /// <summary>
    /// 確定またはキャンセルが押されるまで非同期で待機する
    /// </summary>
    /// <returns>確定なら true、キャンセルなら false</returns>
    public Task<bool> WaitForDecisionAsync()
    {
        tcs = new TaskCompletionSource<bool>();
        return tcs.Task;
    }

    // ===== 結果取得 =====

    /// <summary>確定後の HP 値を返す</summary>
    public int GetResultHP() => currentHP;

    /// <summary>確定後の MP 値を返す</summary>
    public int GetResultMP() => currentMP;

    /// <summary>確定後の GP 値を返す</summary>
    public int GetResultGP() => currentGP;
}
