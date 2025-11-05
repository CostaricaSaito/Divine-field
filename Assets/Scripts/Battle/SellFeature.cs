using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「売る」機能を管理するクラス
/// BattleManagerから売却関連の処理を移設
/// </summary>
public class SellFeature
{
    private BattleManager battleManager;
    private PlayerStatus playerStatus;
    private PlayerStatus enemyStatus;
    private List<CardData> playerHand;
    private List<CardData> cpuHand;
    private CardDealer cardDealer;

    private GameObject sellConfirmPopupPrefab;
    private Canvas popupCanvas;
    private SellConfirmPopup currentPopup; // 現在表示中のポップアップ
    private bool isSellModeActive = false; // 売却モードが有効かどうか
    private bool isProcessingConfirm = false; // 確定処理中かどうか（OnDestroyでキャンセルを呼ばないようにするため）
    private CardData targetSellCard; // 売却対象カード

    /// <summary>
    /// 初期化
    /// </summary>
    public void Initialize(
        BattleManager battleManager,
        PlayerStatus playerStatus,
        PlayerStatus enemyStatus,
        List<CardData> playerHand,
        List<CardData> cpuHand,
        CardDealer cardDealer,
        GameObject sellConfirmPopupPrefab,
        Canvas popupCanvas)
    {
        this.battleManager = battleManager;
        this.playerStatus = playerStatus;
        this.enemyStatus = enemyStatus;
        this.playerHand = playerHand;
        this.cpuHand = cpuHand;
        this.cardDealer = cardDealer;
        this.sellConfirmPopupPrefab = sellConfirmPopupPrefab;
        this.popupCanvas = popupCanvas;
    }

    /// <summary>
    /// 「売る」アクションを実行
    /// </summary>
    public async Task<bool> ExecuteSellActionAsync()
    {
        if (battleManager == null)
        {
            Debug.LogWarning("[SellFeature] BattleManagerがnullです");
            return false;
        }

        if (battleManager.CurrentState != GameState.AttackSelect)
        {
            Debug.LogWarning("[SellFeature] 攻撃フェーズ以外では売るアクションは使用できません");
            return false;
        }

        if (EconomicAction.I == null || !EconomicAction.I.CanSell())
        {
            Debug.LogWarning("[SellFeature] 売るアクションはクールダウン中です");
            return false;
        }

        // 手札が空の場合は実行できない
        if (playerHand == null || playerHand.Count == 0)
        {
            Debug.LogWarning("[SellFeature] 手札が空のため、売るアクションは実行できません");
            return false;
        }

        Debug.Log("[SellFeature] 売るアクション開始");
        Debug.Log($"[SellFeature] sellConfirmPopupPrefab: {(sellConfirmPopupPrefab != null ? sellConfirmPopupPrefab.name : "null")}, popupCanvas: {(popupCanvas != null ? popupCanvas.name : "null")}");

        // ポップアップを表示
        ShowSellConfirmPopup();

        // 売却モードを有効化
        isSellModeActive = true;

        // カード選択をクリア
        CardSelectionManager.I?.ClearAllSelections();

        // クールダウンは承諾時のみ設定する（キャンセル時は設定しない）

        // カード選択を待つ（防御フェーズへの移行は承諾ボタン押下時に行う）
        await Task.CompletedTask;
        return true;
    }

    /// <summary>
    /// 経済アクションの処理（GP奪取、カード移動）
    /// </summary>
    public async Task ProcessEconomicActionAsync()
    {
        if (targetSellCard == null)
        {
            Debug.LogWarning("[SellFeature] 売却対象カードが設定されていません");
            return;
        }

        Debug.Log($"[SellFeature] 経済アクション処理開始 - カード: {targetSellCard.cardName}, 価値: {targetSellCard.cardValue}GP");

        // 処理フラグをリセット
        isProcessingConfirm = false;

        // GP奪取処理
        ProcessGPTheft();

        // カード移動処理
        await ProcessCardTransferAsync();

        // ステータス更新
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);

        Debug.Log("[SellFeature] 売却処理完了");
    }

    /// <summary>
    /// GP奪取処理（相手からGP、MP、HPを奪う）
    /// </summary>
    private void ProcessGPTheft()
    {
        if (targetSellCard == null) return;

        int value = targetSellCard.cardValue;
        int remainingTheft = value;

        Debug.Log($"[SellFeature] GP奪取開始 - 価値: {value}GP");

        // GPから奪取
        if (remainingTheft > 0 && enemyStatus.currentGP > 0)
        {
            int gpTheft = Mathf.Min(remainingTheft, enemyStatus.currentGP);
            enemyStatus.currentGP -= gpTheft;
            playerStatus.currentGP += gpTheft;
            remainingTheft -= gpTheft;
            Debug.Log($"[SellFeature] GP奪取: {gpTheft} (プレイヤーGP: {playerStatus.currentGP}, 敵GP: {enemyStatus.currentGP}, 残り: {remainingTheft})");
        }

        // MPから奪取
        if (remainingTheft > 0 && enemyStatus.currentMP > 0)
        {
            int mpTheft = Mathf.Min(remainingTheft, enemyStatus.currentMP);
            enemyStatus.currentMP -= mpTheft;
            playerStatus.currentMP += mpTheft;
            remainingTheft -= mpTheft;
            Debug.Log($"[SellFeature] MP奪取: {mpTheft} (プレイヤーMP: {playerStatus.currentMP}, 敵MP: {enemyStatus.currentMP}, 残り: {remainingTheft})");
        }

        // HPから奪取（HPは0未満にならない）
        if (remainingTheft > 0 && enemyStatus.currentHP > 0)
        {
            int hpTheft = Mathf.Min(remainingTheft, enemyStatus.currentHP);
            enemyStatus.currentHP -= hpTheft;
            playerStatus.currentHP += hpTheft;
            remainingTheft -= hpTheft;
            Debug.Log($"[SellFeature] HP奪取: {hpTheft} (プレイヤーHP: {playerStatus.currentHP}, 敵HP: {enemyStatus.currentHP}, 残り: {remainingTheft})");
        }
    }

    /// <summary>
    /// カード移動処理（プレイヤー手札 → 敵手札）
    /// </summary>
    private async Task ProcessCardTransferAsync()
    {
        if (targetSellCard == null) return;

        // プレイヤーの手札から削除
        if (playerHand != null && playerHand.Contains(targetSellCard))
        {
            playerHand.Remove(targetSellCard);
            Debug.Log($"[SellFeature] プレイヤー手札から削除: {targetSellCard.cardName}");
        }

        // 敵の手札に追加
        if (cpuHand != null)
        {
            cpuHand.Add(targetSellCard);
            Debug.Log($"[SellFeature] 敵手札に追加: {targetSellCard.cardName}");
        }

        // カードUIを削除
        if (targetSellCard.cardUI != null)
        {
            Object.Destroy(targetSellCard.cardUI.gameObject);
            targetSellCard.cardUI = null;
        }

        // 敵側のカードUIを生成（裏向きのまま）
        if (cardDealer != null)
        {
            var ui = cardDealer.CreateCardUIForHand(targetSellCard);
            if (ui != null)
            {
                Debug.Log($"[SellFeature] 敵側カードUI生成: {targetSellCard.cardName}");
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// カードが選択された時の処理
    /// </summary>
    public void OnCardSelected(CardData card)
    {
        if (!isSellModeActive)
        {
            Debug.LogWarning("[SellFeature] 売却モードが有効ではありません");
            return;
        }

        if (card == null)
        {
            Debug.LogWarning("[SellFeature] 選択されたカードがnullです");
            return;
        }

        // プレイヤーの手札のカードのみ選択可能
        if (playerHand == null || !playerHand.Contains(card))
        {
            Debug.LogWarning("[SellFeature] プレイヤーの手札のカードのみ選択可能です");
            return;
        }

        Debug.Log($"[SellFeature] 売却対象カード選択: {card.cardName} (価値: {card.cardValue}GP)");

        targetSellCard = card;

        // ポップアップに選択カードを設定
        if (currentPopup != null)
        {
            currentPopup.SetSelectedCard(card);
        }
    }

    /// <summary>
    /// 売却確認ポップアップを表示
    /// </summary>
    private void ShowSellConfirmPopup()
    {
        Debug.Log("[SellFeature] ShowSellConfirmPopup開始");

        if (sellConfirmPopupPrefab == null)
        {
            Debug.LogError("[SellFeature] sellConfirmPopupPrefabがnullです");
            return;
        }

        if (popupCanvas == null)
        {
            Debug.LogError("[SellFeature] popupCanvasがnullです");
            return;
        }

        Debug.Log($"[SellFeature] Prefab: {sellConfirmPopupPrefab.name}, Canvas: {popupCanvas.name}");

        // 既存のポップアップがあれば破棄
        if (currentPopup != null)
        {
            var existingPopup = currentPopup;
            currentPopup = null;
            if (existingPopup != null)
            {
                var sellPopup = existingPopup.GetComponent<SellConfirmPopup>();
                if (sellPopup != null)
                {
                    Object.Destroy(existingPopup.gameObject);
                }
            }
        }

        // ポップアップを生成
        GameObject popupObj = Object.Instantiate(sellConfirmPopupPrefab, popupCanvas.transform);
        currentPopup = popupObj.GetComponent<SellConfirmPopup>();

        if (currentPopup == null)
        {
            Debug.LogError("[SellFeature] SellConfirmPopupコンポーネントが見つかりません");
            Object.Destroy(popupObj);
            return;
        }

        Debug.Log("[SellFeature] SellConfirmPopup生成成功");

        // ポップアップを確実に表示
        popupObj.SetActive(true);

        // 購入ポップアップと同じように、Prefab側の設定をそのまま使用
        // ただし、必要に応じて位置だけ調整可能
        RectTransform popupRect = popupObj.GetComponent<RectTransform>();
        if (popupRect != null)
        {
            // Anchor/Pivotは変更せず、位置だけ調整する場合は以下を有効化
            // popupRect.anchoredPosition = new Vector2(0f, 100f); // 例: 上に100px移動
            
            popupRect.localScale = Vector3.one;
            Debug.Log($"[SellFeature] ポップアップ設定完了 - anchoredPosition: {popupRect.anchoredPosition}, sizeDelta: {popupRect.sizeDelta}, activeSelf: {popupObj.activeSelf}");
        }

        // ポップアップを設定（確定処理フラグをリセット）
        isProcessingConfirm = false;
        currentPopup.Setup(OnConfirmSell, OnCancelSell);
        
        // ポップアップの状態を確認
        if (popupObj != null && currentPopup != null)
        {
            Debug.Log($"[SellFeature] SellConfirmPopup設定完了 - activeSelf: {popupObj.activeSelf}, activeInHierarchy: {popupObj.activeInHierarchy}");
        }
        else
        {
            Debug.LogWarning("[SellFeature] ポップアップがnullになっています");
        }
    }

    /// <summary>
    /// 売却が確定された時の処理
    /// </summary>
    private void OnConfirmSell()
    {
        if (targetSellCard == null)
        {
            Debug.LogWarning("[SellFeature] 売却対象カードが選択されていません");
            return;
        }

        Debug.Log($"[SellFeature] 売却確定: {targetSellCard.cardName} (価値: {targetSellCard.cardValue}GP)");

        // クールダウンを設定（承諾時のみ）
        EconomicAction.I?.SetSellCooldown();

        // ポップアップを閉じる（破棄前にフラグを設定）
        isProcessingConfirm = true;
        ClosePopup();

        // 売却モードを無効化（カード選択を無効にするため）
        isSellModeActive = false;

        // 経済アクション用のダミー攻撃カードを設定
        var dummyCard = ScriptableObject.CreateInstance<CardData>();
        dummyCard.cardName = "経済アクション（売却）";
        dummyCard.cardType = CardType.Attack;
        battleManager.SetCurrentAttackCard(dummyCard);

        // 防御フェーズに移行（跳ね返し対応）
        battleManager.SetGameState(GameState.DefenseSelect);
    }

    /// <summary>
    /// 売却がキャンセルされた時の処理
    /// </summary>
    private void OnCancelSell()
    {
        // 確定処理中の場合はキャンセル処理をスキップ
        if (isProcessingConfirm)
        {
            Debug.Log("[SellFeature] 確定処理中なのでキャンセル処理をスキップ");
            return;
        }

        Debug.Log("[SellFeature] 売却がキャンセルされました");

        // ポップアップを閉じる
        ClosePopup();

        // 売却モードを無効化
        isSellModeActive = false;
        targetSellCard = null;

        // カード選択をクリア
        CardSelectionManager.I?.ClearAllSelections();

        // 状態を元に戻す
        battleManager.SetCurrentAttackCard(null);
        battleManager.SetGameState(GameState.AttackSelect);

        // ボタンの状態を更新
        BattleUIManager.I?.UpdateEconomicActionButtons();
    }

    /// <summary>
    /// ポップアップを閉じる
    /// </summary>
    private void ClosePopup()
    {
        if (currentPopup != null)
        {
            var popup = currentPopup;
            currentPopup = null;
            
            Debug.Log($"[SellFeature] ClosePopup - isProcessingConfirm: {isProcessingConfirm}, popup: {(popup != null ? popup.name : "null")}");
            
            Object.Destroy(popup.gameObject);
        }
    }

    /// <summary>
    /// 売却対象カードを取得（防御フェーズでの使用）
    /// </summary>
    public CardData GetTargetSellCard()
    {
        return targetSellCard;
    }

    /// <summary>
    /// 売却モードが有効かどうかを取得
    /// </summary>
    public bool IsSellModeActive()
    {
        return isSellModeActive;
    }
}

