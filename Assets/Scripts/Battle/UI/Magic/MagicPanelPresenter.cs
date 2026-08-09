using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 魔法パネル（使用中のプール表示）と、手札→魔法パネルへの飛行アニメを司るサブマネージャ。
///
/// 【主な責務】
/// - <see cref="MagicPoolManager"/> のプール内容を <see cref="MagicPanelUI"/> に同期
/// - プレイヤー魔法 CardUI がプールスロット側にあるか判定
/// - 魔法カード選択時の「手札 → プールスロット」への飛行演出
/// - プレイヤーが魔法パネルから選べる局面（攻撃ターン・防御側の防御／介入防御など）でパネルをインタラクティブ化
/// </summary>
public class MagicPanelPresenter : MonoBehaviour
{
    [Header("魔法パネル")]
    [SerializeField] private MagicPanelUI magicPanelUI;
    [Tooltip("未設定なら相手プールはデータのみ（BattleManager のスナップショット）。設定時は相手のチャージ魔法を表示更新する。")]
    [SerializeField] private MagicPanelUI enemyMagicPanelUI;

    [Header("魔法：手札→MagicPanel 飛行アニメ")]
    [SerializeField] private float magicHandToPanelDuration = 0.2f;

    public void UpdatePanel()
    {
        if (magicPanelUI == null || MagicPoolManager.I == null) return;
        magicPanelUI.Refresh(MagicPoolManager.I.GetPoolEntries());
    }

    public void RefreshHitRateDisplays()
    {
        magicPanelUI?.RefreshHitRateDisplaysOnSlots();
        enemyMagicPanelUI?.RefreshHitRateDisplaysOnSlots();
    }

    /// <summary>相手側 MagicPool を UI に反映（<see cref="enemyMagicPanelUI"/> がある場合のみ）。</summary>
    public void UpdateEnemyPanel()
    {
        if (enemyMagicPanelUI == null || MagicPoolManager.I == null) return;
        enemyMagicPanelUI.Refresh(MagicPoolManager.I.GetPoolEntries(PlayerType.Enemy));
    }

    /// <summary>
    /// プレイヤー魔法の <see cref="CardData.cardUI"/> が MagicPanel スロットの CardUI か。
    /// 手札に同種カードが残っていても、スロットに載っている参照と一致する場合のみ true（プールからの発動）。
    /// </summary>
    public bool IsPlayerMagicCardUiOnMagicPanel(CardData card)
    {
        if (card == null || card.cardType != CardType.Magic || magicPanelUI == null) return false;
        CardUI poolSlotUi = magicPanelUI.GetCardUI(card);
        return poolSlotUi != null && card.cardUI != null && ReferenceEquals(card.cardUI, poolSlotUi);
    }

    /// <summary>
    /// 敵魔法の <see cref="CardData.cardUI"/> が相手用 MagicPanel スロットの CardUI か。
    /// </summary>
    public bool IsEnemyMagicCardUiOnMagicPanel(CardData card)
    {
        if (card == null || card.cardType != CardType.Magic || enemyMagicPanelUI == null) return false;
        CardUI poolSlotUi = enemyMagicPanelUI.GetCardUI(card);
        return poolSlotUi != null && card.cardUI != null && ReferenceEquals(card.cardUI, poolSlotUi);
    }

    /// <summary>
    /// 手札の魔法カードが MagicPanel のスロットへ直線移動する演出（プール登録は呼び出し側）。
    /// 非アクティブの Placeholder では <see cref="RectTransform.rect"/> / TransformPoint が未レイアウトのままになり
    /// 着地点が画面外座標（例: -400,-600 付近）にずれる。まずスロットを有効化し、
    /// ワールド 4 角の中心 + Canvas ローカル補間（<c>moveLocal</c>）で RectTransform との相性を取る。
    /// </summary>
    public async Task PlayFlyHandToPanelAsync(CardData card, RectTransform handCardRt, int slotIndex)
    {
        if (card == null || handCardRt == null || magicPanelUI == null || card.cardImage == null)
        {
            await Task.CompletedTask;
            return;
        }

        Canvas canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
        if (canvas == null) canvas = handCardRt.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            await Task.CompletedTask;
            return;
        }

        RectTransform canvasRt = canvas.transform as RectTransform;
        if (canvasRt == null)
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

        var fly = new GameObject("MagicHandToPanelFly");
        var flyRt = fly.AddComponent<RectTransform>();
        flyRt.SetParent(canvasRt, false);
        flyRt.SetAsLastSibling();
        var img = fly.AddComponent<Image>();
        img.sprite = card.cardImage;
        img.preserveAspect = true;
        img.raycastTarget = false;
        flyRt.sizeDelta = new Vector2(handCardRt.rect.width, handCardRt.rect.height);

        Vector3 startWorld = GetRectTransformWorldCenter(handCardRt);
        Vector3 endWorld = GetRectTransformWorldCenter(slotRt);
        endWorld.z = startWorld.z;

        Vector3 startLocal = canvasRt.InverseTransformPoint(startWorld);
        Vector3 endLocal = canvasRt.InverseTransformPoint(endWorld);
        endLocal.z = startLocal.z;
        flyRt.localPosition = startLocal;

        // world 補間は Canvas 拡大率・RectTransform 駆動と干渉しやすい。親（Canvas）のローカル空間で移動する
        LeanTween.moveLocal(fly, endLocal, magicHandToPanelDuration).setEase(LeanTweenType.easeOutCubic);

        int ms = Mathf.Max(1, Mathf.RoundToInt(magicHandToPanelDuration * 1000f));
        await Task.Delay(ms);

        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す42.mp3");

        if (fly != null) Destroy(fly);
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
        // 攻撃フェーズに限らず、プレイヤーが防御側で魔法（<<アイアンクラッド>> 等）をパネルから選べるフェーズでも
        // インタラクティブにする。Button.interactable=false だと無効カラーで絵が透明に見えるため。
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
