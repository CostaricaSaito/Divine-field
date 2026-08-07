using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PhoenixFeatherNearDeathEffect",
    menuName = "DivineField/Near Death Card Effects/Phoenix Feather")]
public sealed class PhoenixFeatherNearDeathEffectSO : NearDeathCardEffectSO
{
    [Tooltip("HP0 from near-death revival sets current HP to this value (capped by maxHP).")]
    public int reviveHp = 10;

    public override async Task ResolveNearDeathAsync(
        CardData card,
        PlayerStatus owner,
        PlayerStatus opponent,
        PlayerType ownerSide,
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        CancellationToken cancellationToken)
    {
        if (card == null || owner == null || battleManager == null) return;

        var ui = BattleUIManager.I;
        if (ui != null)
        {
            float msgFade = ui.ShowStyledMessagePopup(owner, MessagePopupKind.PhoenixBlessing);
            await MessagePopup.WaitAfterPopupLifetimeAsync(msgFade, cancellationToken);
        }

        int targetHp = Mathf.Clamp(reviveHp, 1, owner.maxHP);
        owner.currentHP = targetHp;

        if (ui != null)
        {
            float healFade = ui.ShowHealPopup(targetHp, "HP", owner);
            SoundEffectPlayer.I?.Play(DamagePopupSfx.HealHp);
            ui.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus(), snapHpmgpNumbers: true);
            await DamagePopup.WaitAfterPopupLifetimeAsync(healFade, cancellationToken);
        }
        else
        {
            BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
        }

        var hand = ownerSide == PlayerType.Player ? battleManager.playerHand : battleManager.cpuHand;
        if (battleProcessor != null && hand != null)
        {
            if (ownerSide == PlayerType.Player)
            {
                int slot = NearDeathCardRules.GetHandSlotIndex(card);
                if (slot >= 0)
                    handRefill?.RecordPlayerUseSlot(slot);
                battleProcessor.UseCard(card, hand);
            }
            else
            {
                handRefill?.RecordEnemyUse(card);
                battleProcessor.UseCard(card, hand);
            }
        }

        battleManager.RecordNearDeathConsumptionForOnlineSync(ownerSide, card.cardName);
        Debug.Log($"[PhoenixFeather] {owner.DisplayName} revived to HP {owner.currentHP} via {card.cardName}");
    }
}
