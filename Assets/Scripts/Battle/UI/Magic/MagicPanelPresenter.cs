using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 魔法パネル（使用中のプール表示）と、手札⇔MagicPanel の飛行アニメを司るサブマネージャ。
/// </summary>
public class MagicPanelPresenter : MonoBehaviour
{
    [Header("魔法パネル")]
    [SerializeField] private MagicPanelUI magicPanelUI;
    [Tooltip("未設定なら相手プールはデータのみ（BattleManager のスナップショット）。設定時は相手のチャージ魔法を表示更新する。")]
    [SerializeField] private MagicPanelUI enemyMagicPanelUI;

    [Header("魔法：手札⇔MagicPanel 飛行アニメ")]
    [SerializeField] private float magicHandToPanelDuration = 0.2f;

    public void UpdatePanel()
    {
        if (magicPanelUI == null || MagicPoolManager.I == null) return;
        magicPanelUI.Refresh(MagicPoolManager.I.GetPoolEntries());
    }

    public Task PlaySealedExoDionRightSlotRedTintAsync(float durationSeconds, CancellationToken ct)
        => magicPanelUI != null
            ? magicPanelUI.PlayRightSlotSealedExoRedTintAsync(durationSeconds, ct)
            : Task.CompletedTask;

    public void RefreshHitRateDisplays()
    {
        magicPanelUI?.RefreshHitRateDisplaysOnSlots();
        enemyMagicPanelUI?.RefreshHitRateDisplaysOnSlots();
    }

    public void UpdateEnemyPanel()
    {
        if (enemyMagicPanelUI == null || MagicPoolManager.I == null) return;
        enemyMagicPanelUI.Refresh(MagicPoolManager.I.GetPoolEntries(PlayerType.Enemy));
    }

    public bool IsPlayerMagicCardUiOnMagicPanel(CardData card)
    {
        if (card == null || card.cardType != CardType.Magic || magicPanelUI == null) return false;
        CardUI poolSlotUi = magicPanelUI.GetCardUI(card);
        return poolSlotUi != null && card.cardUI != null && ReferenceEquals(card.cardUI, poolSlotUi);
    }

    public bool IsEnemyMagicCardUiOnMagicPanel(CardData card)
    {
        if (card == null || card.cardType != CardType.Magic || enemyMagicPanelUI == null) return false;
        CardUI poolSlotUi = enemyMagicPanelUI.GetCardUI(card);
        return poolSlotUi != null && card.cardUI != null && ReferenceEquals(card.cardUI, poolSlotUi);
    }

    /// <summary>
    /// 手札の <see cref="CardUI"/> ルート（ATK/DEF 含む）を MagicPanel スロットへ移動する。
    /// </summary>
  /// <param name="restoreHandSlotAfterFly">false のとき着陸後に手札へ戻さない（EXO-DION 等で UI を破棄する経路）</param>
    public async Task PlayFlyHandToPanelAsync(
        CardData card,
        int slotIndex,
        bool restoreHandSlotAfterFly = true)
    {
        if (card == null || card.cardUI == null || magicPanelUI == null)
        {
            await Task.CompletedTask;
            return;
        }

        CardUI handUi = card.cardUI;
        var handRt = handUi.transform as RectTransform;
        if (handRt == null)
        {
            await Task.CompletedTask;
            return;
        }

        if (!TryGetCanvasRect(out RectTransform canvasRt))
        {
            await Task.CompletedTask;
            return;
        }

        Canvas.ForceUpdateCanvases();
        var panelRt = magicPanelUI.transform as RectTransform;
        if (panelRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);

        if (!magicPanelUI.TryGetSlotTargetRect(slotIndex, out RectTransform slotRt) || slotRt == null)
        {
            await Task.CompletedTask;
            return;
        }

        Transform handPanel = handRt.parent;
        int savedSibling = handRt.GetSiblingIndex();
        var handLayout = handPanel != null ? handPanel.GetComponent<LayoutGroup>() : null;
        bool handLayoutWasOn = handLayout != null && handLayout.enabled;

        Vector3 startWorld = GetRectTransformWorldCenter(handRt);
        Vector3 endWorld = GetRectTransformWorldCenter(slotRt);
        endWorld.z = startWorld.z;
        Vector3 endLocal = canvasRt.InverseTransformPoint(endWorld);
        endLocal.z = canvasRt.InverseTransformPoint(startWorld).z;

        if (handLayout != null)
            handLayout.enabled = false;

        handRt.SetParent(canvasRt, true);
        handRt.SetAsLastSibling();

        LeanTween.cancel(handRt.gameObject);
        LeanTween.moveLocal(handRt.gameObject, endLocal, magicHandToPanelDuration)
            .setEase(LeanTweenType.easeOutCubic);

        int ms = Mathf.Max(1, Mathf.RoundToInt(magicHandToPanelDuration * 1000f));
        await Task.Delay(ms);

        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す42.mp3");

        if (restoreHandSlotAfterFly && handPanel != null)
        {
            handRt.SetParent(handPanel, false);
            handRt.SetSiblingIndex(savedSibling);
            if (handLayout != null)
                handLayout.enabled = handLayoutWasOn;
            if (handPanel is RectTransform handPanelRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(handPanelRt);
        }
    }

    /// <summary>
    /// 魔法パネルスロットから手札の新規カード <see cref="CardUI"/> へ飛行（裏向きのまま移動可）。
    /// </summary>
    public async Task PlayFlyPanelToHandAsync(int slotIndex, CardData drawn, CancellationToken ct = default)
    {
        if (drawn?.cardUI == null || magicPanelUI == null)
        {
            await Task.CompletedTask;
            return;
        }

        var handRt = drawn.cardUI.transform as RectTransform;
        if (handRt == null)
        {
            await Task.CompletedTask;
            return;
        }

        if (!TryGetCanvasRect(out RectTransform canvasRt))
        {
            await Task.CompletedTask;
            return;
        }

        Canvas.ForceUpdateCanvases();
        var panelRt = magicPanelUI.transform as RectTransform;
        if (panelRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);

        if (!magicPanelUI.TryGetSlotTargetRect(slotIndex, out RectTransform slotRt) || slotRt == null)
        {
            await Task.CompletedTask;
            return;
        }

        Transform handPanel = handRt.parent;
        var handLayout = handPanel != null ? handPanel.GetComponent<LayoutGroup>() : null;
        bool handLayoutWasOn = handLayout != null && handLayout.enabled;
        Vector2 savedAnchored = handRt.anchoredPosition;
        int savedSibling = handRt.GetSiblingIndex();

        Vector3 endWorld = GetRectTransformWorldCenter(handRt);
        Vector3 startWorld = GetRectTransformWorldCenter(slotRt);
        startWorld.z = endWorld.z;

        Vector3 startLocal = canvasRt.InverseTransformPoint(startWorld);
        Vector3 endLocal = canvasRt.InverseTransformPoint(endWorld);
        endLocal.z = startLocal.z;

        if (handLayout != null)
            handLayout.enabled = false;

        handRt.SetParent(canvasRt, true);
        handRt.SetAsLastSibling();
        handRt.localPosition = startLocal;

        LeanTween.cancel(handRt.gameObject);
        LeanTween.moveLocal(handRt.gameObject, endLocal, magicHandToPanelDuration)
            .setEase(LeanTweenType.easeOutCubic);

        int ms = Mathf.Max(1, Mathf.RoundToInt(magicHandToPanelDuration * 1000f));
        try
        {
            await Task.Delay(ms, ct);
        }
        catch (OperationCanceledException)
        {
            LeanTween.cancel(handRt.gameObject);
            throw;
        }

        if (handPanel != null)
        {
            handRt.SetParent(handPanel, false);
            handRt.SetSiblingIndex(savedSibling);
            handRt.anchoredPosition = savedAnchored;
            if (handLayout != null)
                handLayout.enabled = handLayoutWasOn;
            if (handPanel is RectTransform handPanelRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(handPanelRt);
        }
    }

    private bool TryGetCanvasRect(out RectTransform canvasRt)
    {
        canvasRt = null;
        Canvas canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
        if (canvas == null) return false;
        canvasRt = canvas.transform as RectTransform;
        return canvasRt != null;
    }

    private static Vector3 GetRectTransformWorldCenter(RectTransform rt)
    {
        if (rt == null) return Vector3.zero;
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return (c[0] + c[1] + c[2] + c[3]) * 0.25f;
    }

    public void RefreshMagicCardInteractivity(List<CardData> hand)
    {
        if (magicPanelUI == null) return;
        bool handBlocked = BattleUIManager.I != null && BattleUIManager.I.IsHandInputBlocked;
        var bm = BattleManager.I;
        bool allowMagicPanel = bm != null && !handBlocked && !bm.IsHandReloadPopupOpen
            && (
            (bm.CurrentState == GameState.AttackPhase && bm.CurrentTurnOwner == PlayerType.Player)
            || (bm.CurrentState == GameState.DefensePhase && bm.DefenderPublic == PlayerType.Player)
            || (bm.CurrentState == GameState.DefenseConfirmPhase && bm.DefenderPublic == PlayerType.Player)
            || bm.IsPlayerDefenseInputActive());

        bool defenseMagicPanel = allowMagicPanel && bm != null
            && (bm.CurrentState == GameState.DefensePhase
                || bm.CurrentState == GameState.DefenseConfirmPhase
                || bm.IsPlayerDefenseInputActive());

        if (defenseMagicPanel)
        {
            var incoming = bm.GetIncomingAttackSnapshotForDefenseUi();
            var ps = bm.GetPlayerStatus();
            magicPanelUI.SetSlotsInteractableForDefense(allowMagicPanel, incoming, ps);
        }
        else
        {
            magicPanelUI.SetAllInteractable(allowMagicPanel);
        }
    }
}
