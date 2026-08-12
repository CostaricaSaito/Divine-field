using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 「両替」機能を管理するクラス
///
/// 【役割】
/// - 両替ポップアップの表示・制御
/// - HP を対価として MP/GP を変換する処理
/// - 確定後の演出（ExchangeConfirmPopUP）
/// - クールダウン設定とターン終了への移行
/// </summary>
public class ExchangeFeature : MonoBehaviour
{
    private const string ExchangeConfirmPopupResourcePath = "Prefab/ExchangeConfirmPopUP";

    // ===== 参照 =====
    private BattleManager battleManager;
    private PlayerStatus playerStatus;
    private GameObject exchangePopupPrefab;
    private GameObject exchangeConfirmPopupPrefab;
    private Canvas popupCanvas;

    // ===== 状態 =====
    private bool isExchangeProcessActive = false;
    private GameObject currentPopupInstance;
    private ExchangePopupUI currentPopupUI;

    // ===== 初期化 =====

    public void Initialize(
        BattleManager battleManager,
        PlayerStatus playerStatus,
        GameObject exchangePopupPrefab,
        GameObject exchangeConfirmPopupPrefab,
        Canvas popupCanvas)
    {
        this.battleManager = battleManager;
        this.playerStatus = playerStatus;
        this.exchangePopupPrefab = exchangePopupPrefab;
        this.exchangeConfirmPopupPrefab = exchangeConfirmPopupPrefab;
        this.popupCanvas = popupCanvas;
    }

    // ===== メインフロー =====

    public async Task ExecuteExchangeActionAsync()
    {
        if (battleManager == null
            || battleManager.CurrentState != GameState.AttackPhase
            || !battleManager.PlayerCanUseEconomicActions())
        {
            Debug.LogWarning("[ExchangeFeature] 両替アクションは AttackSelect 中のみ実行できます");
            return;
        }

        if (EconomicAction.I == null || !EconomicAction.I.CanExchange())
        {
            Debug.LogWarning("[ExchangeFeature] 両替アクションはクールダウン中です");
            return;
        }

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

        currentPopupUI = OpenExchangePopup();
        if (currentPopupUI == null)
        {
            isExchangeProcessActive = false;
            return;
        }

        int beforeHP = playerStatus.currentHP;
        int beforeMP = playerStatus.currentMP;
        int beforeGP = playerStatus.currentGP;

        bool confirmed = await currentPopupUI.WaitForDecisionAsync();

        int afterHP = currentPopupUI.GetResultHP();
        int afterMP = currentPopupUI.GetResultMP();
        int afterGP = currentPopupUI.GetResultGP();

        ClosePopup();
        currentPopupUI = null;

        if (!confirmed)
        {
            Debug.Log("[ExchangeFeature] 両替キャンセル");
            SoundEffectPlayer.I?.Play("Assets/SE/キャンセル4.mp3");
            isExchangeProcessActive = false;
            return;
        }

        if (beforeHP == afterHP && beforeMP == afterMP && beforeGP == afterGP)
        {
            Debug.Log("[ExchangeFeature] 両替：変化なし、ターン終了へ");
            if (battleManager.IsOnlineMatch)
                NetworkBattleBridge.SendEconomicExchange(afterHP, afterMP, afterGP);
            isExchangeProcessActive = false;
            FinishExchangeAction();
            return;
        }

        await PlayExchangeConfirmPresentationAsync(
            beforeHP, beforeMP, beforeGP,
            afterHP, afterMP, afterGP,
            CancellationToken.None);

        Debug.Log($"[ExchangeFeature] 両替確定: HP {beforeHP}→{playerStatus.currentHP}, MP {beforeMP}→{playerStatus.currentMP}, GP {beforeGP}→{playerStatus.currentGP}");

        if (battleManager.IsOnlineMatch)
            NetworkBattleBridge.SendEconomicExchange(afterHP, afterMP, afterGP);

        BattleUIManager.I?.UpdateStatus(playerStatus, BattleManager.I?.GetEnemyStatus());

        isExchangeProcessActive = false;

        if (playerStatus.currentHP <= 0)
        {
            Debug.Log("[ExchangeFeature] 両替により HP が 0 になりました。敗北処理へ");
            if (battleManager != null)
            {
                bool ended = await battleManager.TryHandleDeathIfAnyAsync();
                if (ended) return;
            }
            return;
        }

        FinishExchangeAction();
    }

    // ===== ポップアップ制御 =====

    private ExchangePopupUI OpenExchangePopup()
    {
        Transform parent = popupCanvas != null ? popupCanvas.transform : transform;
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

    private void ClosePopup()
    {
        if (currentPopupInstance != null)
        {
            Destroy(currentPopupInstance);
            currentPopupInstance = null;
        }
    }

    public void CancelIfActive()
    {
        if (!isExchangeProcessActive || currentPopupUI == null) return;
        Debug.Log("[ExchangeFeature] 両替ボタン再押下によりキャンセル");
        currentPopupUI.ForceCancel();
    }

    // ===== 演出 =====

    private async Task PlayExchangeConfirmPresentationAsync(
        int beforeHp, int beforeMp, int beforeGp,
        int afterHp, int afterMp, int afterGp,
        CancellationToken cancellationToken)
    {
        GameObject prefab = ResolveExchangeConfirmPopupPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[ExchangeFeature] ExchangeConfirmPopUP prefab is missing");
            return;
        }

        Transform parent = ResolveConfirmPopupParent();
        if (parent == null)
        {
            Debug.LogWarning("[ExchangeFeature] CardDisplayPanel parent is missing for confirm popup");
            return;
        }

        var go = Instantiate(prefab, parent, false);
        ApplyPanelCenterLayout(go.transform as RectTransform);

        var confirmUI = go.GetComponent<ExchangeConfirmPopupUI>();
        if (confirmUI == null)
        {
            Debug.LogWarning("[ExchangeFeature] ExchangeConfirmPopupUI is missing on confirm prefab");
            Destroy(go);
            return;
        }

        try
        {
            await confirmUI.PlayConfirmSequenceAsync(
                beforeHp, beforeMp, beforeGp,
                afterHp, afterMp, afterGp,
                cancellationToken);

            playerStatus.currentHP = Mathf.Max(afterHp, 0);
            playerStatus.currentMP = Mathf.Clamp(afterMp, 0, playerStatus.maxMP);
            playerStatus.currentGP = Mathf.Clamp(afterGp, 0, playerStatus.maxGP);
        }
        finally
        {
            if (go != null)
                Destroy(go);
        }
    }

    private GameObject ResolveExchangeConfirmPopupPrefab()
    {
        if (exchangeConfirmPopupPrefab != null)
            return exchangeConfirmPopupPrefab;
        return Resources.Load<GameObject>(ExchangeConfirmPopupResourcePath);
    }

    private Transform ResolveConfirmPopupParent()
    {
        if (BattleUIManager.I != null)
        {
            var panel = BattleUIManager.I.GetPlayerCardDisplayPanel();
            if (panel != null) return panel;
        }

        if (popupCanvas != null)
            return popupCanvas.transform;

        return transform;
    }

    private static void ApplyPanelCenterLayout(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();
    }

    // ===== ターン終了 =====

    private void FinishExchangeAction()
    {
        EconomicAction.I?.SetExchangeCooldown();
        BattleUIManager.I?.UpdateEconomicActionButtons();
        Debug.Log("[ExchangeFeature] 両替完了。ターン終了へ移行します");
        battleManager.SetGameState(GameState.CombatResolvePhase);
    }

    public bool IsExchangeProcessActive() => isExchangeProcessActive;

    /// <summary>オンライン：相手の両替結果を enemyStatus（相手ミラー）へ適用。</summary>
    public void MirrorRemoteExchange(PlayerStatus remoteAttackerStatus, int afterHp, int afterMp, int afterGp)
    {
        if (remoteAttackerStatus == null) return;
        remoteAttackerStatus.currentHP = Mathf.Max(afterHp, 0);
        remoteAttackerStatus.currentMP = Mathf.Clamp(afterMp, 0, remoteAttackerStatus.maxMP);
        remoteAttackerStatus.currentGP = Mathf.Clamp(afterGp, 0, remoteAttackerStatus.maxGP);
        BattleUIManager.I?.UpdateStatus(BattleManager.I?.GetPlayerStatus(), remoteAttackerStatus);
    }
}
