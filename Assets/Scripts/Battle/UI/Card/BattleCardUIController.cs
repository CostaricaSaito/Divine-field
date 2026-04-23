using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// バトル画面の「カード表示エリア／手札インタラクション／選択管理／反射スライド／介入表示」を担当する Presenter。
///
/// 【責務範囲】
/// - 選択したカードを表示パネルへ出し入れする（<see cref="ShowCardDetail"/>, <see cref="HideAllCardDetails"/> 等）
/// - 手札の入力可否・ハイライト・グレーアウトの同期
/// - 反射時のカードシート横スライド演出
/// - 介入攻撃カードの一時表示
///
/// 【BattleUIManager ファサードとの関係】
/// - <see cref="BattleUIManager"/> は使用ボタン／拘束オーバーレイ等の他 Presenter を保持しているため、
///   本クラスはハブとしての <see cref="BattleUIManager.I"/> 経由でそれらに依頼する（直接参照を避ける）。
/// </summary>
public class BattleCardUIController : MonoBehaviour
{
    [Header("カード詳細表示")]
    [SerializeField] private GameObject cardSheetPrefab;
    [SerializeField] private Transform playerCardDisplayPanel;
    [SerializeField] private Transform enemyCardDisplayPanel;

    [Header("カード管理")]
    [SerializeField] private CardLayoutManager cardLayoutManager;
    [SerializeField] private CardSelectionManager cardSelectionManager;

    private readonly List<GameObject> activeCardSheets = new();
    private bool isHandInputBlocked = false;

    /// <summary>手札入力ブロック中か（ポップアップや反射解決中）。</summary>
    public bool IsHandInputBlocked => isHandInputBlocked;

    public GameObject CardSheetPrefab => cardSheetPrefab;
    public Transform PlayerCardDisplayPanel => playerCardDisplayPanel;
    public Transform EnemyCardDisplayPanel => enemyCardDisplayPanel;
    public CardLayoutManager LayoutManager => cardLayoutManager;

    //==== パブリックAPI：カード詳細表示 =====
    public void ShowCardDetail(CardData card, Side side)
    {
        if (card == null)
        {
            Debug.LogWarning("[BattleCardUIController] ShowCardDetail: card is null");
            return;
        }

        if (cardSelectionManager.IsCardSelected(card))
        {
            Debug.Log($"[BattleCardUIController] カード選択をキャンセル: {card.cardName}");
            CancelCardSelection(card);
            return;
        }

        if (cardSelectionManager.AddCardSelection(card))
        {
            DisplayCard(card, side);

            if (side == Side.Player && !BattleManager.I.IsUseButtonLocked)
            {
                BattleUIManager.I.SetUseButtonInteractable(true);
            }

            BattleManager.I?.ResetPlayerEffectTargetToDefaultForCurrentAttackSelection();
            BattleManager.I?.UpdateTotalATKDEFDisplay();

            if (side == Side.Player
                && BattleManager.I != null
                && BattleManager.I.CurrentState == GameState.AttackPhase
                && BattleManager.I.CurrentTurnOwner == PlayerType.Player
                && !BattleManager.I.IsReflectionChainDefensePending())
            {
                var h = BattleManager.I.playerHand;
                RefreshAttackInteractivity(h, CardRules.GetAttackChoices(h));
            }
            else if (side == Side.Player
                && BattleManager.I != null
                && (BattleManager.I.CurrentState == GameState.DefensePhase
                    || (BattleManager.I.CurrentState == GameState.CombatResolvePhase && BattleManager.I.IsInterventionDefenseWaitActive())))
                BattleManager.I.RefreshPlayerDefensePhaseInteractivity();
            else if (side == Side.Player && BattleManager.I != null && BattleManager.I.IsReflectionChainDefensePending())
                BattleUIManager.I.UpdateDefenseButtonLabel();
        }
    }

    /// <summary>現在表示中の CardSheet から <paramref name="card"/> と同一アセット参照のシートを検索（最後に生成されたもの）。</summary>
    public bool TryGetCardSheetDisplayForCardData(CardData card, out CardSheetDisplay display)
    {
        display = null;
        if (card == null) return false;
        for (int i = activeCardSheets.Count - 1; i >= 0; i--)
        {
            var go = activeCardSheets[i];
            if (go == null) continue;
            var sh = go.GetComponent<CardSheetDisplay>();
            if (sh == null) continue;
            if (ReferenceEquals(sh.GetCurrentCardData(), card))
            {
                display = sh;
                return true;
            }
        }

        return false;
    }

    public void HideAllCardDetails()
    {
        foreach (var go in activeCardSheets)
        {
            if (go != null) Destroy(go);
        }
        activeCardSheets.Clear();
        cardSelectionManager.ClearAllSelections();
        UpdateHandCardHighlights();
        BattleManager.I?.ClearSelectedCards();
        BattleUIManager.I.HideRestraintHeavyOverlays();
    }

    /// <summary>プレイヤー側のカード表示のみクリア（敵側は残す）。</summary>
    public void HidePlayerCardDetails()
    {
        for (int i = activeCardSheets.Count - 1; i >= 0; i--)
        {
            var go = activeCardSheets[i];
            if (go == null) { activeCardSheets.RemoveAt(i); continue; }
            if (go.transform.parent == playerCardDisplayPanel)
            {
                Destroy(go);
                activeCardSheets.RemoveAt(i);
            }
        }
        cardSelectionManager.ClearAllSelections();
        UpdateHandCardHighlights();
    }

    //==== パブリックAPI：カード選択管理 =====
    public List<CardData> GetSelectedCards() => cardSelectionManager.GetSelectedCards();

    public List<CardData> GetSelectedAttackCards() => cardSelectionManager.GetSelectedAttackCards();

    public List<CardData> GetSelectedDefenseCards() => cardSelectionManager.GetSelectedDefenseCards();

    public void ClearAllSelections()
    {
        cardSelectionManager.ClearAllSelections();
        UpdateHandCardHighlights();
        BattleManager.I?.ClearSelectedCards();
    }

    //==== パブリックAPI：手札管理 =====
    /// <summary>手札カードのクリック受付のみを切り替える（見た目は変更しない）。</summary>
    public void SetHandClickable(bool clickable)
    {
        isHandInputBlocked = !clickable;
        var hand = BattleManager.I?.playerHand;
        if (hand == null) return;
        foreach (var card in hand)
        {
            if (card?.cardUI == null) continue;
            var cg = card.cardUI.GetComponent<CanvasGroup>();
            if (cg == null) cg = card.cardUI.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = clickable;
        }
    }

    public void SetHandInteractivity(List<CardData> hand, bool interactable)
    {
        if (hand == null) return;
        foreach (var c in hand) SetCardInteractable(c, interactable);
    }

    public void SetCardInteractable(CardData card, bool interactable)
    {
        if (card?.cardUI == null) return;

        var btn = card.cardUI.button;
        if (btn != null) btn.interactable = interactable;

        var cg = card.cardUI.GetComponent<CanvasGroup>();
        if (cg == null) cg = card.cardUI.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = interactable ? 1f : 0.5f;
        cg.blocksRaycasts = isHandInputBlocked ? false : interactable;
    }

    public void UpdateHandInteractivity(List<CardData> hand, List<CardData> allowedCards = null)
    {
        if (hand == null) return;

        if (allowedCards == null)
        {
            foreach (var card in hand)
            {
                if (card?.cardUI == null) continue;
                SetCardInteractable(card, true);
            }
            return;
        }

        var allowedCardUIs = new HashSet<CardUI>();
        foreach (var allowedCard in allowedCards)
        {
            if (allowedCard?.cardUI != null)
            {
                allowedCardUIs.Add(allowedCard.cardUI);
            }
        }

        foreach (var card in hand)
        {
            if (card?.cardUI == null) continue;
            bool canUse = allowedCardUIs.Contains(card.cardUI);
            SetCardInteractable(card, canUse);
        }
    }

    public void SetPrayModeUI(List<CardData> hand)
    {
        BattleUIManager.I.SetUseButtonLabel("祈り");
        BattleUIManager.I.SetUseButtonInteractable(true);
        SetHandInteractivity(hand, false);
    }

    public void RefreshAttackInteractivity(List<CardData> hand, List<CardData> attackableCards)
    {
        var currentAttack = GetSelectedAttackCards();
        var filtered = AttackComboSelectionRules.FilterAttackChoicesForCurrentSelection(
            attackableCards, currentAttack);
        UpdateHandInteractivity(hand, filtered);
        BattleUIManager.I.SetUseButtonLabel("使用");
    }

    public void RefreshDefenseInteractivity(List<CardData> hand, List<CardData> defenseCards)
    {
        UpdateHandInteractivity(hand, defenseCards);
        BattleUIManager.I.SetUseButtonLabel("許す");
        BattleUIManager.I.SetUseButtonInteractable(true);
        BattleUIManager.I.SyncRestraintHeavyOverlay();
    }

    /// <summary>
    /// Intro 時点でのカード表示（グレーアウトなし）
    /// </summary>
    /// <param name="archMagicChantUseButtonLabel">
    /// true のとき「詠唱開始」のまま（大魔法詠唱中の攻撃フェーズ差し替え時など。「使用」に戻さない）。
    /// </param>
    public void SetIntroModeUI(List<CardData> hand, bool archMagicChantUseButtonLabel = false)
    {
        BattleUIManager.I.HideRestraintHeavyOverlays();
        if (archMagicChantUseButtonLabel)
        {
            BattleUIManager.I.SetUseButtonLabel("詠唱開始");
            BattleUIManager.I.SetUseButtonInteractable(false);
        }
        else
            BattleUIManager.I.SetUseButtonLabel("使用");
        SetHandInteractivity(hand, true);
    }

    //==== カードパネル／プレハブ getter =====
    public Vector3 GetPlayerCardDisplayCenter()
        => playerCardDisplayPanel != null ? playerCardDisplayPanel.position : Vector3.zero;

    public Vector3 GetEnemyCardDisplayCenter()
        => enemyCardDisplayPanel != null ? enemyCardDisplayPanel.position : Vector3.zero;

    //==== パネル非表示／カードシート破棄 =====
    /// <summary>ゲーム終了時に、プレイヤー／敵のカード表示パネルを非アクティブ化する。</summary>
    public void DisableCardDisplayPanels()
    {
        if (playerCardDisplayPanel != null)
            playerCardDisplayPanel.gameObject.SetActive(false);
        if (enemyCardDisplayPanel != null)
            enemyCardDisplayPanel.gameObject.SetActive(false);
    }

    /// <summary>
    /// 表示中のカードシート（CardDisplay / EnemyDisplay のいずれか）を CardData で特定して破棄。反射「弾き返す」ポップアップ消滅後など。
    /// </summary>
    public void DestroyCardSheetForCardData(CardData card)
    {
        if (card == null) return;
        RemoveCardFromDisplay(card);
        if (cardSelectionManager != null && cardSelectionManager.IsCardSelected(card))
            cardSelectionManager.CancelCardSelection(card);
        cardLayoutManager?.SetActiveCardSheets(activeCardSheets);
        cardLayoutManager?.SetSelectedCards(cardSelectionManager != null ? cardSelectionManager.GetSelectedCards() : new List<CardData>());
        cardLayoutManager?.HandleCardCancellation();
        UpdateHandCardHighlights();
    }

    /// <summary>
    /// 指定パネル上の該当 CardData のシートだけを破棄。
    /// </summary>
    public void DestroyCardSheetsForCardDataOnPanel(CardData card, Side side)
    {
        if (card == null) return;
        Transform panel = side == Side.Player ? playerCardDisplayPanel : enemyCardDisplayPanel;
        if (panel == null) return;
        int id = card.GetInstanceID();
        bool removed = false;
        for (int i = activeCardSheets.Count - 1; i >= 0; i--)
        {
            var cardObj = activeCardSheets[i];
            if (cardObj == null) continue;
            if (cardObj.transform.parent != panel) continue;
            var cardDisplay = cardObj.GetComponent<CardSheetDisplay>();
            var displayed = cardDisplay?.GetCardData();
            if (displayed != null && displayed.GetInstanceID() == id)
            {
                Destroy(cardObj);
                activeCardSheets.RemoveAt(i);
                removed = true;
            }
        }
        if (!removed) return;
        if (cardSelectionManager != null && cardSelectionManager.IsCardSelected(card))
            cardSelectionManager.CancelCardSelection(card);
        cardLayoutManager?.SetActiveCardSheets(activeCardSheets);
        cardLayoutManager?.SetSelectedCards(cardSelectionManager != null ? cardSelectionManager.GetSelectedCards() : new List<CardData>());
        cardLayoutManager?.HandleCardCancellation();
        UpdateHandCardHighlights();
    }

    /// <summary>
    /// 同一パネルに同じ CardData のシートが複数あるとき、最後に追加された1枚だけ破棄（反射バウンスの重複除去）。
    /// </summary>
    public void DestroyMostRecentCardSheetOnPanelForCardData(CardData card, Side side)
    {
        if (card == null) return;
        Transform panel = side == Side.Player ? playerCardDisplayPanel : enemyCardDisplayPanel;
        if (panel == null) return;
        int id = card.GetInstanceID();
        for (int i = activeCardSheets.Count - 1; i >= 0; i--)
        {
            var cardObj = activeCardSheets[i];
            if (cardObj == null) continue;
            if (cardObj.transform.parent != panel) continue;
            var cardDisplay = cardObj.GetComponent<CardSheetDisplay>();
            var displayed = cardDisplay?.GetCardData();
            if (displayed != null && displayed.GetInstanceID() == id)
            {
                Destroy(cardObj);
                activeCardSheets.RemoveAt(i);
                if (cardSelectionManager != null && cardSelectionManager.IsCardSelected(card))
                    cardSelectionManager.CancelCardSelection(card);
                cardLayoutManager?.SetActiveCardSheets(activeCardSheets);
                cardLayoutManager?.SetSelectedCards(cardSelectionManager != null ? cardSelectionManager.GetSelectedCards() : new List<CardData>());
                cardLayoutManager?.HandleCardCancellation();
                UpdateHandCardHighlights();
                return;
            }
        }
    }

    //==== 反射スライド演出 =====
    /// <summary>
    /// 反射で表示中の攻撃カードシートを、パネル間で横スライド（線形・既定500ms）する。
    /// </summary>
    public Task SlideReflectionAttackSheetsAsync(
        List<CardData> attackCards,
        bool slideTowardPlayer,
        float durationSec,
        CancellationToken cancellationToken = default)
    {
        if (attackCards == null || attackCards.Count == 0 || cardLayoutManager == null)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        StartCoroutine(CoSlideReflectionAttackSheets(attackCards, slideTowardPlayer, durationSec, cancellationToken, () =>
        {
            if (!tcs.Task.IsCompleted)
                tcs.TrySetResult(true);
        }));
        return tcs.Task;
    }

    private IEnumerator CoSlideReflectionAttackSheets(
        List<CardData> attackCards,
        bool slideTowardPlayer,
        float durationSec,
        CancellationToken cancellationToken,
        System.Action onComplete)
    {
        Transform sourcePanel = slideTowardPlayer ? enemyCardDisplayPanel : playerCardDisplayPanel;
        Transform targetPanel = slideTowardPlayer ? playerCardDisplayPanel : enemyCardDisplayPanel;
        if (sourcePanel == null || targetPanel == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        var sheetsOrdered = new List<GameObject>();
        foreach (var ac in attackCards)
        {
            if (ac == null) continue;
            GameObject found = null;
            foreach (var go in activeCardSheets)
            {
                if (go == null) continue;
                var disp = go.GetComponent<CardSheetDisplay>();
                if (disp != null && disp.GetCardData() == ac)
                {
                    found = go;
                    break;
                }
            }
            if (found != null)
                sheetsOrdered.Add(found);
        }

        if (sheetsOrdered.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        var dstRt = targetPanel as RectTransform;
        if (cardLayoutManager != null && dstRt != null)
            cardLayoutManager.SetLayoutPanelRect(dstRt);

        var srcRt = sourcePanel as RectTransform;
        Vector3 delta = dstRt.position - srcRt.position;
        delta.y = 0f;
        delta.z = 0f;

        var starts = new Vector3[sheetsOrdered.Count];
        var ends = new Vector3[sheetsOrdered.Count];
        for (int i = 0; i < sheetsOrdered.Count; i++)
        {
            var rt = sheetsOrdered[i].transform as RectTransform;
            if (rt == null) continue;
            starts[i] = rt.position;
            ends[i] = starts[i] + delta;
        }

        float dur = Mathf.Max(0.02f, durationSec);
        float elapsed = 0f;
        while (elapsed < dur)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            for (int i = 0; i < sheetsOrdered.Count; i++)
            {
                var go = sheetsOrdered[i];
                if (go == null) continue;
                var rt = go.transform as RectTransform;
                if (rt == null) continue;
                rt.position = Vector3.Lerp(starts[i], ends[i], t);
            }
            yield return null;
        }

        cardLayoutManager.SetSelectedCards(attackCards);
        cardLayoutManager.SetActiveCardSheets(activeCardSheets);
        foreach (var go in sheetsOrdered)
        {
            if (go == null) continue;
            go.transform.SetParent(targetPanel, false);
            cardLayoutManager.SetupCardPosition(go, targetPanel);
        }

        onComplete?.Invoke();
    }

    //==== 介入攻撃カードシート =====
    /// <summary>介入攻撃カードを表示パネル先頭に出す（選択マネージャには載せない）。</summary>
    public void ShowInterventionAttackSheet(CardData card, Side side)
    {
        if (card == null) return;
        Transform parent = side == Side.Player ? playerCardDisplayPanel : enemyCardDisplayPanel;
        if (cardSheetPrefab == null || parent == null) return;

        var go = Instantiate(cardSheetPrefab, parent);
        if (!parent.gameObject.activeSelf) parent.gameObject.SetActive(true);
        if (!go.activeSelf) go.SetActive(true);

        var display = go.GetComponent<CardSheetDisplay>();
        if (display != null)
        {
            PlayerStatus mpOwner = side == Side.Player
                ? BattleManager.I?.GetPlayerStatus()
                : BattleManager.I?.GetEnemyStatus();
            display.Setup(card, mpOwner);
        }

        activeCardSheets.Add(go);
        var single = new List<CardData> { card };
        cardLayoutManager.SetActiveCardSheets(activeCardSheets);
        cardLayoutManager.SetSelectedCards(single);
        cardLayoutManager.SetupCardPosition(go, parent);
        UpdateHandCardHighlights();
    }

    //==== プライベート =====
    private void CancelCardSelection(CardData card)
    {
        bool removed = cardSelectionManager.CancelCardSelection(card);
        Debug.Log($"[BattleCardUIController] カード選択をキャンセル: {card.cardName} (削除成功: {removed}, selectedCards数: {cardSelectionManager.SelectedCardCount})");

        RemoveCardFromDisplay(card);

        UpdateHandCardHighlights();

        cardLayoutManager.SetActiveCardSheets(activeCardSheets);
        cardLayoutManager.SetSelectedCards(cardSelectionManager.GetSelectedCards());
        cardLayoutManager.HandleCardCancellation();

        UpdateBattleManagerAfterCancel();

        BattleManager.I?.ResetPlayerEffectTargetToDefaultForCurrentAttackSelection();
        BattleManager.I?.UpdateTotalATKDEFDisplay();

        if (BattleManager.I?.CurrentState == GameState.AttackPhase
            && cardSelectionManager.SelectedCardCount == 0)
        {
            BattleUIManager.I.SetUseButtonInteractable(false);
        }
    }

    private void UpdateHandCardHighlights()
    {
        var handCards = FindObjectsOfType<CardUI>();

        foreach (var cardUI in handCards)
        {
            if (cardUI == null) continue;

            var cardData = cardUI.GetCardData();
            if (cardData == null) continue;

            bool isSelected = cardSelectionManager.IsCardSelected(cardData);
            cardUI.SetHighlight(isSelected);
        }
    }

    private void DisplayCard(CardData card, Side side)
    {
        Transform parent = (side == Side.Player) ? playerCardDisplayPanel : enemyCardDisplayPanel;

        if (cardSheetPrefab != null && parent != null)
        {
            var go = Instantiate(cardSheetPrefab, parent);
            if (!parent.gameObject.activeSelf) parent.gameObject.SetActive(true);
            if (!go.activeSelf) go.SetActive(true);

            var display = go.GetComponent<CardSheetDisplay>();
            if (display != null)
            {
                PlayerStatus mpOwner = side == Side.Player
                    ? BattleManager.I?.GetPlayerStatus()
                    : BattleManager.I?.GetEnemyStatus();
                display.Setup(card, mpOwner);
            }

            activeCardSheets.Add(go);

            cardLayoutManager.SetActiveCardSheets(activeCardSheets);
            cardLayoutManager.SetSelectedCards(cardSelectionManager.GetSelectedCards());

            cardLayoutManager.SetupCardPosition(go, parent);
            UpdateHandCardHighlights();
            return;
        }

        HandleCardDisplayFallback(card, side);
    }

    private void RemoveCardFromDisplay(CardData card)
    {
        if (card == null) return;
        int id = card.GetInstanceID();
        for (int i = activeCardSheets.Count - 1; i >= 0; i--)
        {
            var cardObj = activeCardSheets[i];
            if (cardObj == null) continue;

            var cardDisplay = cardObj.GetComponent<CardSheetDisplay>();
            var displayed = cardDisplay?.GetCardData();
            if (displayed != null && displayed.GetInstanceID() == id)
            {
                Destroy(cardObj);
                activeCardSheets.RemoveAt(i);
            }
        }
    }

    private void UpdateBattleManagerAfterCancel()
    {
        if (cardSelectionManager.HasNoSelectedCards())
        {
            BattleManager.I?.ClearSelectedCards();
            if (BattleManager.I != null)
            {
                if (BattleManager.I.IsReflectionChainDefensePending())
                    BattleManager.I.RefreshReflectionChainInteractivityIfPending();
                else if (BattleManager.I.CurrentState == GameState.DefensePhase
                    || (BattleManager.I.CurrentState == GameState.CombatResolvePhase && BattleManager.I.IsInterventionDefenseWaitActive()))
                    BattleManager.I.RefreshPlayerDefensePhaseInteractivity();
                else if (BattleManager.I.CurrentState == GameState.AttackPhase
                         && BattleManager.I.CurrentTurnOwner == PlayerType.Player)
                {
                    var hand = BattleManager.I.playerHand;
                    RefreshAttackInteractivity(hand, CardRules.GetAttackChoices(hand));
                }
            }
        }
        else if (BattleManager.I != null)
        {
            if (BattleManager.I.CurrentState == GameState.AttackPhase
                && !BattleManager.I.IsReflectionChainDefensePending())
            {
                if (BattleManager.I.CurrentTurnOwner == PlayerType.Player)
                {
                    var hand = BattleManager.I.playerHand;
                    RefreshAttackInteractivity(hand, CardRules.GetAttackChoices(hand));
                }
                var selectedAttackCards = GetSelectedAttackCards();
                if (selectedAttackCards.Count == 0)
                {
                    BattleManager.I.ClearSelectedCards();
                }
                else
                {
                    BattleManager.I.UpdateTotalATKDEFDisplay();
                }
            }
            else if (BattleManager.I.CurrentState == GameState.DefensePhase
                     || (BattleManager.I.CurrentState == GameState.CombatResolvePhase && BattleManager.I.IsInterventionDefenseWaitActive())
                     || BattleManager.I.IsReflectionChainDefensePending()
                     || BattleManager.I.IsParryRerunDefensePending())
            {
                BattleManager.I.UpdateTotalATKDEFDisplay();
                BattleUIManager.I.UpdateDefenseButtonLabel();
                if (BattleManager.I.IsReflectionChainDefensePending())
                    BattleManager.I.RefreshReflectionChainInteractivityIfPending();
                else
                    BattleManager.I.RefreshPlayerDefensePhaseInteractivity();
            }
        }
    }

    private void HandleCardDisplayFallback(CardData card, Side side)
    {
        if (cardSheetPrefab == null)
        {
            Debug.LogWarning("[BattleCardUIController] cardSheetPrefab が設定されていません。CardDisplayController へのフォールバック処理を実行します。");
        }
        if ((side == Side.Player ? playerCardDisplayPanel : enemyCardDisplayPanel) == null)
        {
            Debug.LogWarning("[BattleCardUIController] CardDisplayPanel が設定されていません。CardDisplayController へのフォールバック処理を実行します。side=" + side);
        }

        var controller = FindObjectOfType<CardDisplayController>(true);
        if (controller != null)
        {
            controller.ShowCard(card);
        }
        else
        {
            Debug.LogError("[BattleCardUIController] すべての表示方法が利用できません。cardSheetPrefab / panel が設定されていない、CardDisplayController も見つかりません。");
        }
    }
}
