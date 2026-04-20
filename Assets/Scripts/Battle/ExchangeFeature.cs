using System;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

/// <summary>
/// 「両替」機能を管理するクラス
///
/// 【役割】
/// - 両替ポップアップの表示・制御
/// - HP を対価として MP/GP を変換する処理
/// - 確定後の演出（操作前・操作後のフワッとポップアップ）
/// - クールダウン設定とターン終了への移行
/// </summary>
public class ExchangeFeature : MonoBehaviour
{
    // ===== 参照 =====
    private BattleManager battleManager;
    private PlayerStatus playerStatus;
    private GameObject exchangePopupPrefab;
    private Canvas popupCanvas;

    // ===== 状態 =====
    private bool isExchangeProcessActive = false;
    private GameObject currentPopupInstance;
    private ExchangePopupUI currentPopupUI;

    // ===== 初期化 =====

    /// <summary>
    /// 初期化処理
    /// </summary>
    public void Initialize(
        BattleManager battleManager,
        PlayerStatus playerStatus,
        GameObject exchangePopupPrefab,
        Canvas popupCanvas)
    {
        this.battleManager = battleManager;
        this.playerStatus = playerStatus;
        this.exchangePopupPrefab = exchangePopupPrefab;
        this.popupCanvas = popupCanvas;
    }

    // ===== メインフロー =====

    /// <summary>
    /// 「両替」アクションを実行する
    ///
    /// 【処理フロー】
    /// 1. ポップアップを開く
    /// 2. 確定 or キャンセルを待機
    /// 3. 確定なら演出 → ステータス反映 → クールダウン設定 → TurnEnd
    /// 4. キャンセルなら何もせず終了
    /// </summary>
    public async Task ExecuteExchangeActionAsync()
    {
        if (battleManager == null || battleManager.CurrentState != GameState.AttackPhase)
        {
            Debug.LogWarning("[ExchangeFeature] 両替アクションは AttackSelect フェーズ以外では実行できません");
            return;
        }

        if (EconomicAction.I == null || !EconomicAction.I.CanExchange())
        {
            Debug.LogWarning("[ExchangeFeature] 両替アクションはクールダウン中です");
            return;
        }

        // ポップアップ表示中に再度押された場合はキャンセル
        if (isExchangeProcessActive)
        {
            CancelIfActive();
            return;
        }

        if (exchangePopupPrefab == null)
        {
            Debug.LogError("[ExchangeFeature] exchangePopupPrefab が設定されていません");
            return;
        }

        isExchangeProcessActive = true;
        battleManager?.ClearPlayerSelfAttackTargetMode();
        Debug.Log("[ExchangeFeature] 両替アクション開始");

        // ポップアップを開く
        currentPopupUI = OpenExchangePopup();
        if (currentPopupUI == null)
        {
            isExchangeProcessActive = false;
            return;
        }

        // 操作前のステータスを記録
        int beforeHP = playerStatus.currentHP;
        int beforeMP = playerStatus.currentMP;
        int beforeGP = playerStatus.currentGP;

        // 確定 or キャンセルを待機
        bool confirmed = await currentPopupUI.WaitForDecisionAsync();

        // 操作後のステータスを取得
        int afterHP = currentPopupUI.GetResultHP();
        int afterMP = currentPopupUI.GetResultMP();
        int afterGP = currentPopupUI.GetResultGP();

        // ポップアップを閉じる
        ClosePopup();
        currentPopupUI = null;

        if (!confirmed)
        {
            Debug.Log("[ExchangeFeature] 両替キャンセル");
            SoundEffectPlayer.I?.Play("Assets/SE/キャンセル4.mp3");
            isExchangeProcessActive = false;
            return;
        }

        // 変化がない場合はスキップ
        if (beforeHP == afterHP && beforeMP == afterMP && beforeGP == afterGP)
        {
            Debug.Log("[ExchangeFeature] 両替：変化なし、ターン終了へ");
            isExchangeProcessActive = false;
            FinishExchangeAction();
            return;
        }

        // 演出：操作前の数値をフワッと表示
        ShowExchangeResultPopup(beforeHP, beforeMP, beforeGP, isBefore: true);
        await Task.Delay(TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown));
        await Task.Delay(DamagePopup.PostPopupIntervalMs);

        // 演出：操作後の数値をフワッと表示
        ShowExchangeResultPopup(afterHP, afterMP, afterGP, isBefore: false);
        await Task.Delay(TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown));
        await Task.Delay(DamagePopup.PostPopupIntervalMs);

        // 実際のステータスに反映
        playerStatus.currentHP = Mathf.Max(afterHP, 0);
        playerStatus.currentMP = Mathf.Clamp(afterMP, 0, playerStatus.maxMP);
        playerStatus.currentGP = Mathf.Clamp(afterGP, 0, playerStatus.maxGP);

        Debug.Log($"[ExchangeFeature] 両替確定: HP {beforeHP}→{playerStatus.currentHP}, MP {beforeMP}→{playerStatus.currentMP}, GP {beforeGP}→{playerStatus.currentGP}");

        // UI 更新
        BattleUIManager.I?.UpdateStatus(playerStatus, BattleManager.I?.GetEnemyStatus());

        isExchangeProcessActive = false;

        // HP が 0 以下なら敗北処理（BattleProcessor に委譲）
        if (playerStatus.currentHP <= 0)
        {
            Debug.Log("[ExchangeFeature] 両替により HP が 0 になりました。敗北処理へ");
            battleManager.SetGameState(GameState.BattleEndPhase);
            return;
        }

        // クールダウン設定 → ターン終了
        FinishExchangeAction();
    }

    // ===== ポップアップ制御 =====

    /// <summary>
    /// 両替ポップアップを生成して初期化する
    /// </summary>
    private ExchangePopupUI OpenExchangePopup()
    {
        Transform parent = (popupCanvas != null) ? popupCanvas.transform : transform;
        currentPopupInstance = Instantiate(exchangePopupPrefab, parent);

        var popupUI = currentPopupInstance.GetComponent<ExchangePopupUI>();
        if (popupUI == null)
        {
            Debug.LogError("[ExchangeFeature] ExchangePopupUI コンポーネントが見つかりません");
            Destroy(currentPopupInstance);
            currentPopupInstance = null;
            return null;
        }

        popupUI.Setup(playerStatus);
        Debug.Log("[ExchangeFeature] 両替ポップアップを表示しました");
        return popupUI;
    }

    /// <summary>
    /// 両替ポップアップを破棄する
    /// </summary>
    private void ClosePopup()
    {
        if (currentPopupInstance != null)
        {
            Destroy(currentPopupInstance);
            currentPopupInstance = null;
        }
    }

    /// <summary>
    /// ポップアップ表示中であればキャンセルして閉じる
    /// </summary>
    public void CancelIfActive()
    {
        if (!isExchangeProcessActive || currentPopupUI == null) return;
        Debug.Log("[ExchangeFeature] 両替ボタン再押下によりキャンセル");
        currentPopupUI.ForceCancel();
    }

    // ===== 演出 =====

    /// <summary>
    /// 操作前・操作後の HP/MP/GP をフワッとポップアップ表示する
    /// </summary>
    private void ShowExchangeResultPopup(int hp, int mp, int gp, bool isBefore)
    {
        string label = isBefore ? "変更前" : "変更後";
        string text = $"{label}  HP:{hp}  MP:{mp}  GP:{gp}";
        Color color = isBefore ? Color.yellow : Color.cyan;

        // BattleUIManager の DamagePopup 機構を流用してプレイヤー側に表示
        BattleUIManager.I?.ShowHealPopup(0, text, playerStatus);

        Debug.Log($"[ExchangeFeature] 演出ポップアップ: {text}");
    }

    // ===== ターン終了 =====

    /// <summary>
    /// クールダウンを設定してターン終了へ移行する
    /// </summary>
    private void FinishExchangeAction()
    {
        EconomicAction.I?.SetExchangeCooldown();
        BattleUIManager.I?.UpdateEconomicActionButtons();
        Debug.Log("[ExchangeFeature] 両替完了。ターン終了へ移行します");
        battleManager.SetGameState(GameState.CombatResolvePhase);
    }

    // ===== 状態確認 =====

    /// <summary>
    /// 両替処理が進行中かどうかを返す
    /// </summary>
    public bool IsExchangeProcessActive() => isExchangeProcessActive;
}
