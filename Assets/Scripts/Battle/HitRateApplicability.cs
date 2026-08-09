using UnityEngine;

/// <summary>
/// 命中率（煙幕等）の表示・補正が及ぶカードかどうか。
/// 能動的に相手を攻撃するカードのみ対象。
/// </summary>
public static class HitRateApplicability
{
    /// <summary>カードシートの表示文脈。</summary>
    public enum SheetContext
    {
        Normal = 0,
        ReflectedAttack = 1,
        InterventionAttack = 2,
    }

    /// <summary>手札 CardUI の表示文脈。</summary>
    public enum HandContext
    {
        PlayerHand = 0,
        MagicPanel = 1,
    }

    /// <summary>
    /// カード種別として命中率の対象になりうるか（フェーズ・文脈を除く）。
    /// </summary>
    public static bool IsHitRateEligibleCard(CardData card)
    {
        if (card == null) return false;
        if (DeadlyChainRules.IsDeadlyChainCard(card)) return true;
        if (CardRules.IsPassiveHandOnly(card)) return false;
        if (card.cardType == CardType.ArchMagic || card.cardType == CardType.Ultimate) return false;
        if (CardRules.IsImmediateAction(card)) return false;
        if (card.cardType == CardType.Defense) return false;
        if (CardRules.IsPrimaryDefenseCard(card)) return false;
        if (CardRules.IsRecoveryCard(card)) return false;
        if (CardRules.IsAttackMagic(card)) return true;
        if (card.cardType == CardType.Attack) return true;
        if (card.cardType == CardType.Disaster && card.attackPower > 0) return true;
        return false;
    }

    /// <summary>攻撃側の煙幕等ペナルティを命中率に適用するか（戦闘解決用）。</summary>
    public static bool IsSubjectToAttackerSmokePenalty(CardData card)
    {
        return IsHitRateEligibleCard(card);
    }

    public static bool ShouldApplyHitRateDisplayOnPlayerHand(CardData card)
    {
        if (!IsHitRateEligibleCard(card)) return false;

        var bm = BattleManager.I;
        if (bm != null && bm.IsPlayerDefenseInputActive())
        {
            return DeadlyChainRules.IsDeadlyChainCard(card);
        }

        return true;
    }

    public static bool ShouldApplyHitRateDisplayOnMagicPanel(CardData card, PlayerStatus owner)
    {
        if (!IsHitRateEligibleCard(card)) return false;
        if (!CardRules.IsAttackMagic(card)) return false;

        var bm = BattleManager.I;
        if (bm != null && owner != null
            && ReferenceEquals(owner, bm.GetPlayerStatus())
            && bm.IsPlayerDefenseInputActive())
        {
            return false;
        }

        return true;
    }

    public static bool ShouldApplyHitRateDisplayOnCardSheet(
        CardData card,
        PlayerStatus owner,
        SheetContext sheetContext = SheetContext.Normal)
    {
        if (sheetContext == SheetContext.ReflectedAttack || sheetContext == SheetContext.InterventionAttack)
            return false;
        if (!IsHitRateEligibleCard(card)) return false;

        var bm = BattleManager.I;
        if (bm == null || owner == null) return true;

        bool ownerIsPlayer = ReferenceEquals(owner, bm.GetPlayerStatus());
        if (!ownerIsPlayer || !bm.IsPlayerDefenseInputActive()) return true;

        if (DeadlyChainRules.IsDeadlyChainCard(card)) return true;
        if (ReflectionRules.IsReflectionCard(card)) return false;
        if (ParryRules.IsParryCard(card)) return false;
        if (BlockingRules.IsPhysicalBlockingCard(card)) return false;
        if (CardRules.IsPrimaryDefenseCard(card)) return false;
        if (CardRules.IsUsableInDefensePhase(card) && CardRules.IsUsableInAttackPhase(card))
            return false;

        return true;
    }
}
