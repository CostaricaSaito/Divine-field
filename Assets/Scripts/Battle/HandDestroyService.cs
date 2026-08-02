using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Hand card destruction presentation and removal from hand list / UI.
/// </summary>
public static class HandDestroyService
{
    public const string DestroyMessage = "雷が手札を破壊する";
    public const string NoTargetMessage = "破壊する手札がない！";
    public const string SeAddress = "Assets/SE/Thunder-Synthetic01-1.mp3";
    private const int PreDissolveHoldMs = 500;

    private static readonly Color MessageColor = new Color(1f, 0.92f, 0.35f);

    /// <summary>
    /// Full sequence: message popup, card reveal, dissolve, remove from hand.
    /// </summary>
    public static async Task PlayDestroySequenceAsync(
        BattleManager bm,
        PlayerStatus blessingOwner,
        PlayerStatus victim,
        List<CardData> victimHand,
        bool victimIsPlayerHand,
        CardData targetCard,
        bool noTarget,
        CancellationToken ct)
    {
        var ui = BattleUIManager.I;
        if (ui == null || blessingOwner == null) return;

        string message = noTarget ? NoTargetMessage : DestroyMessage;
        SoundEffectPlayer.I?.Play(SeAddress);
        float fadeSec = ui.ShowMessagePopupForTarget(blessingOwner, message, MessageColor);
        await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec, ct);
        if (ct.IsCancellationRequested || noTarget || targetCard == null) return;

        Side victimSide = victimIsPlayerHand ? Side.Player : Side.Enemy;
        Transform panel = victimSide == Side.Player
            ? ui.GetPlayerCardDisplayPanel()
            : ui.GetEnemyCardDisplayPanel();
        var prefab = ui.GetCardSheetPrefab();
        if (panel == null || prefab == null)
        {
            RemoveFromHand(bm, victimHand, targetCard, victimIsPlayerHand);
            return;
        }

        if (!panel.gameObject.activeSelf)
            panel.gameObject.SetActive(true);

        var sheet = Object.Instantiate(prefab, panel);
        sheet.name = $"HandDestroy_{targetCard.cardName}";
        var display = sheet.GetComponent<CardSheetDisplay>();
        display?.Setup(targetCard);

        var rect = sheet.GetComponent<RectTransform>();
        if (rect != null)
            rect.anchoredPosition = Vector2.zero;

        await Task.Delay(PreDissolveHoldMs, ct);
        if (ct.IsCancellationRequested) return;

        await CardDissolvePlayer.PlayAsync(sheet, ct);
        if (sheet != null)
            Object.Destroy(sheet);

        RemoveFromHand(bm, victimHand, targetCard, victimIsPlayerHand);
    }

    /// <summary>
    /// Resolve destroy target on victim hand (by index from host sync, else by name).
    /// </summary>
    public static CardData ResolveTargetCard(List<CardData> victimHand, string cardName, int handIndex)
    {
        if (victimHand == null || victimHand.Count == 0) return null;

        if (handIndex >= 0 && handIndex < victimHand.Count)
        {
            var atIndex = victimHand[handIndex];
            if (atIndex != null && (string.IsNullOrEmpty(cardName) || atIndex.cardName == cardName))
                return atIndex;
        }

        if (string.IsNullOrEmpty(cardName)) return null;

        for (int i = 0; i < victimHand.Count; i++)
        {
            var c = victimHand[i];
            if (c != null && c.cardName == cardName)
                return c;
        }

        return null;
    }

    private static void RemoveFromHand(
        BattleManager bm,
        List<CardData> hand,
        CardData card,
        bool isPlayerHand)
    {
        if (hand == null || card == null) return;

        if (isPlayerHand && card.cardUI != null)
        {
            var uiObj = card.cardUI.gameObject;
            card.cardUI = null;
            Object.Destroy(uiObj);
        }

        hand.Remove(card);

        if (isPlayerHand && bm != null && bm.handPanel is RectTransform rt)
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        if (isPlayerHand)
            BattleUIManager.I?.SetIntroModeUI(hand);
    }
}
