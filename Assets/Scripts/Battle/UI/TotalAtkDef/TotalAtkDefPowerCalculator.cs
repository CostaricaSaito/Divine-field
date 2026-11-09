using System.Collections.Generic;
using System.Text;

/// <summary>TotalATKDEF の攻撃・防御力計算とラベル整形。</summary>
public class TotalAtkDefPowerCalculator
{
    public const string IfritBonusColorHex = "#E53935";
    public const string LeviathanSuppressColorHex = "#1E88E5";
    public const string GodRageAtkBaseGreenHex = "#33DD55";
    /// <summary>括弧内訳の TMP リッチテキストサイズ（メイン ATK より一回り小さく）。</summary>
    public const string AttackPowerBreakdownRichTextSize = "80%";

    private readonly TotalAtkDefDisplayState _state;

    public TotalAtkDefPowerCalculator(TotalAtkDefDisplayState state)
    {
        _state = state;
    }

    public int CalculateTotalDefensePower(List<CardData> defenseCards) =>
        CalculateTotalPower(defenseCards, false);

    public int GetEffectiveDefensePowerForDisplay(
        List<CardData> defenseCards,
        PlayerStatus defender,
        ElementType attackElement)
    {
        List<CardData> combat = NormalizeDefenseCardsForCombat(defenseCards);
        return CombatDefensePowerResolver.ResolveEffectiveDefensePower(combat, defender, attackElement);
    }

    public ElementType GetDefenseDisplayCombinedElement(
        List<CardData> defenseCards,
        PlayerStatus defender)
    {
        List<CardData> combat = NormalizeDefenseCardsForCombat(defenseCards);
        return PermanentDefRules.ResolveDisplayCombinedElement(combat, defender);
    }

    private int CalculateTotalPower(List<CardData> cards, bool isAttack)
    {
        int total = 0;
        foreach (var card in cards)
        {
            if (card == null) continue;
            if (FreeMagicCastRules.IsCombatCatalyst(card, cards)) continue;
            total += isAttack ? card.attackPower : card.defensePower;
        }
        return total;
    }

    public int CalculateTotalAttackPower(List<CardData> attackCards, PlayerStatus attackerForMeRule = null)
    {
        if (attackCards == null || attackCards.Count == 0) return 0;
        var postDeathCtx = PostDeathCombatContext.Active;
        if (postDeathCtx != null && postDeathCtx.MatchesIncoming(attackCards))
            return postDeathCtx.FixedAttackPower;
        if (attackerForMeRule != null && MagicalExplosionRules.ContainsMagicalExplosion(attackCards))
        {
            if (_state.SuppressMagicalExplosionPredictionDuringSequenceReveal)
                return MagicalExplosionRules.SumAttackPowerExcludingMagicalExplosion(attackCards);
            return MagicalExplosionRules.SumCardAttackPowerForMagicalExplosionCombo(attackCards, attackerForMeRule);
        }
        if (attackerForMeRule != null && MillionDollarBazookaRules.ContainsMillionDollarBazooka(attackCards))
        {
            if (_state.SuppressMillionDollarBazookaPredictionDuringSequenceReveal)
                return MillionDollarBazookaRules.SumAttackPowerExcludingMillionDollarBazooka(attackCards)
                    + (_state.AttackDisplaySuppressMagicalSwordBonus
                        ? 0
                        : MagicalSwordRules.GetActivePowerBonus(attackCards, attackerForMeRule));
            return MillionDollarBazookaRules.SumCardAttackPowerForMillionDollarBazookaCombo(attackCards, attackerForMeRule);
        }
        if (attackerForMeRule != null && TributeBloodRules.ContainsTributeBlood(attackCards))
        {
            if (_state.SuppressTributeBloodPredictionDuringSequenceReveal)
                return TributeBloodRules.SumAttackPowerExcludingTributeBlood(attackCards)
                    + (_state.AttackDisplaySuppressMagicalSwordBonus
                        ? 0
                        : MagicalSwordRules.GetActivePowerBonus(attackCards, attackerForMeRule));
            return TributeBloodRules.SumCardAttackPowerForTributeBloodCombo(attackCards, attackerForMeRule);
        }
        if (attackerForMeRule != null && HammadnessRules.ContainsHammadness(attackCards))
        {
            if (_state.SuppressHammadnessPredictionDuringSequenceReveal)
                return HammadnessRules.SumAttackPowerExcludingHammadness(attackCards)
                    + (_state.AttackDisplaySuppressMagicalSwordBonus
                        ? 0
                        : MagicalSwordRules.GetActivePowerBonus(attackCards, attackerForMeRule));
            return HammadnessRules.SumCardAttackPowerForHammadnessCombo(attackCards, attackerForMeRule);
        }
        int plain = CalculateTotalPower(attackCards, true);
        if (attackerForMeRule != null && !_state.AttackDisplaySuppressMagicalSwordBonus)
        {
            plain += MagicalSwordRules.GetActivePowerBonus(attackCards, attackerForMeRule);
            plain += GigaFlareRules.GetActivePowerBonus(attackCards, attackerForMeRule);
        }
        return plain;
    }

    public int ResolveCardSumForGodRageDisplay(List<CardData> cards, PlayerStatus attacker)
    {
        if (cards == null || cards.Count == 0 || attacker == null) return 0;
        int sum = CalculateTotalAttackPower(cards, attacker);
        if (sum > 0) return sum;
        if (MillionDollarBazookaRules.ContainsMillionDollarBazooka(cards))
            return MillionDollarBazookaRules.SumCardAttackPowerForMillionDollarBazookaCombo(cards, attacker);
        return sum;
    }

    public string FormatDefensePowerLabel(List<CardData> defenseCards)
    {
        return FormatDefensePowerLabel(defenseCards, null, ElementType.None);
    }

    public string FormatDefensePowerLabel(
        List<CardData> defenseCards,
        PlayerStatus defender,
        ElementType attackElement)
    {
        List<CardData> combat = NormalizeDefenseCardsForCombat(defenseCards);
        if (!CombatDefensePowerResolver.TryGetDefenseDisplay(
                combat, defender, attackElement, out int power, out _))
            return "";
        return $"DEF {power}";
    }

    private static List<CardData> NormalizeDefenseCardsForCombat(List<CardData> defenseCards)
    {
        if (defenseCards == null || defenseCards.Count == 0) return null;
        var combatDefense = FreeMagicCastRules.ExcludeCatalystsFromCombatStats(defenseCards);
        return combatDefense.Count > 0 ? combatDefense : null;
    }

    public string FormatAttackPowerDisplayLabel(
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defenderForBlessingsOverride = null,
        bool forMeOnlyPostRampExcludeGodRageDouble = false)
    {
        if (!TryFormatAttackPowerDisplayLabel(
                cards, attacker, defenderForBlessingsOverride, forMeOnlyPostRampExcludeGodRageDouble,
                colorLeadingAtkGreen: false, out string richText))
            return "";
        return richText;
    }

    /// <summary>ゴッドレイジ成立時：先頭 ATK のみ緑。括弧内訳は 2 倍後の基礎＋加護式。</summary>
    public string FormatGodRageDoubledAttackPowerDisplayLabel(List<CardData> cards, PlayerStatus attacker, PlayerStatus defender)
    {
        if (!TryFormatAttackPowerDisplayLabel(
                cards, attacker, defender, forMeOnlyPostRampExcludeGodRageDouble: false,
                colorLeadingAtkGreen: true, out string richText))
            return "";
        return richText;
    }

    /// <summary>ゴッドレイジ カウントアップ中に固定する括弧内訳（先頭 ATK 数字は含めない）。</summary>
    public string GetAttackPowerParenthesesSuffix(
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defender)
    {
        if (!TryBuildAttackPowerParenthesesSuffix(cards, attacker, defender, out string suffix))
            return "";
        return suffix;
    }

    /// <summary>ゴッドレイジ発動ランプ：合計 ATK と括弧内の基礎攻撃力を同期カウントアップ。</summary>
    public string FormatGodRageRampStepLabel(
        int atkValue,
        int parensBase,
        int netBlessing,
        bool physical,
        bool showKannadukiMul,
        bool showWeakenHalf)
    {
        if (!TryBuildParenthesesSuffix(
                parensBase, netBlessing, physical, showKannadukiMul, showWeakenHalf,
                colorParensBaseGreen: true, out string parens))
            return $"<color={GodRageAtkBaseGreenHex}>ATK {atkValue}</color>";

        if (string.IsNullOrEmpty(parens))
            return $"<color={GodRageAtkBaseGreenHex}>ATK {atkValue}</color>";

        return $"<color={GodRageAtkBaseGreenHex}>ATK {atkValue}</color>{parens}";
    }

    /// <summary>括弧内訳があるとき、ランプ前後の基礎攻撃力（カード合計）。</summary>
    public bool TryGetGodRageParenBaseRampRange(
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defender,
        out int fromBase,
        out int toBase,
        out int netBlessing,
        out bool physical,
        out bool showKannadukiMul,
        out bool showWeakenHalf)
    {
        fromBase = 0;
        toBase = 0;
        netBlessing = 0;
        physical = false;
        showKannadukiMul = false;
        showWeakenHalf = false;

        if (cards == null || attacker == null || !GodrageRules.IsGodrageDoublingCombo(cards))
            return false;

        if (!TryResolveAttackPowerDisplayValues(
                cards, attacker, defender, forMeOnlyPostRampExcludeGodRageDouble: true,
                out _, out int rawCombo, out netBlessing, out physical,
                out showKannadukiMul, out showWeakenHalf, out _))
            return false;

        bool hasBreakdown = netBlessing != 0 || physical && (showKannadukiMul || showWeakenHalf);
        if (!hasBreakdown)
            return false;

        fromBase = rawCombo;
        toBase = rawCombo * 2;
        return toBase != fromBase;
    }

    private bool TryFormatAttackPowerDisplayLabel(
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defenderForBlessingsOverride,
        bool forMeOnlyPostRampExcludeGodRageDouble,
        bool colorLeadingAtkGreen,
        out string richText)
    {
        richText = "";
        if (!TryResolveAttackPowerDisplayValues(
                cards, attacker, defenderForBlessingsOverride, forMeOnlyPostRampExcludeGodRageDouble,
                out int finalAttack, out _, out int netBlessing, out bool physical,
                out bool showKannadukiMul, out bool showWeakenHalf, out int parensComboBase))
            return false;

        if (!TryBuildParenthesesSuffix(parensComboBase, netBlessing, physical, showKannadukiMul, showWeakenHalf,
                colorParensBaseGreen: false, out string parens))
            return false;

        var sb = new StringBuilder(64);
        if (colorLeadingAtkGreen)
            sb.Append("<color=").Append(GodRageAtkBaseGreenHex).Append(">");
        sb.Append("ATK ").Append(finalAttack);
        if (colorLeadingAtkGreen)
            sb.Append("</color>");
        if (!string.IsNullOrEmpty(parens))
            sb.Append(parens);
        richText = sb.ToString();
        return true;
    }

    private bool TryBuildAttackPowerParenthesesSuffix(
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defender,
        out string suffix)
    {
        suffix = "";
        if (!TryResolveAttackPowerDisplayValues(
                cards, attacker, defender, forMeOnlyPostRampExcludeGodRageDouble: true,
                out _, out _, out int netBlessing, out bool physical,
                out bool showKannadukiMul, out bool showWeakenHalf, out int parensComboBase))
            return false;

        return TryBuildParenthesesSuffix(parensComboBase, netBlessing, physical, showKannadukiMul, showWeakenHalf,
            colorParensBaseGreen: false, out suffix);
    }

    private static bool TryBuildParenthesesSuffix(
        int parensBase,
        int netBlessing,
        bool physical,
        bool showKannadukiMul,
        bool showWeakenHalf,
        bool colorParensBaseGreen,
        out string suffix)
    {
        suffix = "";
        bool showBlessing = netBlessing != 0;
        bool showStatus = physical && (showKannadukiMul || showWeakenHalf);
        if (!showBlessing && !showStatus)
            return true;

        var sb = new StringBuilder(48);
        sb.Append(" <size=").Append(AttackPowerBreakdownRichTextSize).Append(">(");
        if (colorParensBaseGreen)
            sb.Append("<color=").Append(GodRageAtkBaseGreenHex).Append(">");
        sb.Append(parensBase);
        if (colorParensBaseGreen)
            sb.Append("</color>");
        if (showBlessing)
            AppendNetBlessingSuffix(sb, netBlessing);
        if (showKannadukiMul)
            sb.Append("×2");
        else if (showWeakenHalf)
            sb.Append("/2");
        sb.Append(")</size>");
        suffix = sb.ToString();
        return true;
    }

    private static void AppendNetBlessingSuffix(StringBuilder sb, int netBlessing)
    {
        if (netBlessing > 0)
        {
            sb.Append("<color=").Append(IfritBonusColorHex).Append(">+").Append(netBlessing).Append("</color>");
            return;
        }
        if (netBlessing < 0)
        {
            int mag = -netBlessing;
            sb.Append("<color=").Append(LeviathanSuppressColorHex).Append(">-").Append(mag).Append("</color>");
        }
    }

    private bool TryResolveAttackPowerDisplayValues(
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defenderForBlessingsOverride,
        bool forMeOnlyPostRampExcludeGodRageDouble,
        out int finalAttack,
        out int rawCombo,
        out int netBlessing,
        out bool physical,
        out bool showKannadukiMul,
        out bool showWeakenHalf,
        out int parensComboBase)
    {
        finalAttack = 0;
        rawCombo = 0;
        netBlessing = 0;
        physical = false;
        showKannadukiMul = false;
        showWeakenHalf = false;
        parensComboBase = 0;

        if (cards == null || cards.Count == 0 || attacker == null)
            return false;

        rawCombo = CalculateTotalAttackPower(cards, attacker);
        if (rawCombo <= 0 && MillionDollarBazookaRules.ContainsMillionDollarBazooka(cards))
            rawCombo = MillionDollarBazookaRules.SumCardAttackPowerForMillionDollarBazookaCombo(cards, attacker);
        if (rawCombo <= 0)
        {
            if (HammadnessRules.ContainsHammadness(cards) || TributeBloodRules.ContainsTributeBlood(cards)
                || MillionDollarBazookaRules.ContainsMillionDollarBazooka(cards)
                || GigaFlareRules.ContainsGigaFlare(cards))
            {
                finalAttack = 0;
                return true;
            }
            return false;
        }

        bool applyGodDouble = GodrageRules.IsGodrageDoublingCombo(cards) && !forMeOnlyPostRampExcludeGodRageDouble
            && !_state.AttackDisplaySuppressGodRageDouble;
        if (_state.SuppressMagicalExplosionPredictionDuringSequenceReveal && MagicalExplosionRules.ContainsMagicalExplosion(cards))
            applyGodDouble = false;
        if (_state.SuppressMillionDollarBazookaPredictionDuringSequenceReveal && MillionDollarBazookaRules.ContainsMillionDollarBazooka(cards))
            applyGodDouble = false;
        if (_state.SuppressTributeBloodPredictionDuringSequenceReveal && TributeBloodRules.ContainsTributeBlood(cards))
            applyGodDouble = false;
        if (_state.SuppressHammadnessPredictionDuringSequenceReveal && HammadnessRules.ContainsHammadness(cards))
            applyGodDouble = false;

        int combatBlessingBase = applyGodDouble ? rawCombo * 2 : rawCombo;
        PlayerStatus defender = defenderForBlessingsOverride ?? GetDefenderForAttackDisplay(attacker);

        int displayAfterIfrit = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, rawCombo);
        int displayAfterLevi = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(
            attacker, defender, cards, displayAfterIfrit);
        netBlessing = displayAfterLevi - rawCombo;

        int combatAfterIfrit = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, combatBlessingBase);
        int combatAfterLevi = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(
            attacker, defender, cards, combatAfterIfrit);

        physical = !CardRules.IsMagicClassifiedAttackCombo(cards);
        if (physical)
        {
            showKannadukiMul = attacker.HasKannadukiEffect();
            showWeakenHalf = !showKannadukiMul && attacker.HasWeakenEffect();
            finalAttack = attacker.ApplyOutgoingDamageModifiers(combatAfterLevi);
        }
        else
            finalAttack = combatAfterLevi;

        parensComboBase = applyGodDouble ? rawCombo * 2 : rawCombo;
        return true;
    }

    public int GetDisplayedAttackStrength(List<CardData> cards, PlayerStatus attacker) =>
        GetDisplayedAttackStrengthWithDefender(cards, attacker, GetDefenderForAttackDisplay(attacker));

    public int GetDisplayedAttackStrengthWithDefender(
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defenderForBlessings)
    {
        if (cards == null || cards.Count == 0) return 0;
        var postDeathCtx = PostDeathCombatContext.Active;
        if (postDeathCtx != null && postDeathCtx.MatchesIncoming(cards))
            return postDeathCtx.FixedAttackPower;
        int sum = ResolveCardSumForGodRageDisplay(cards, attacker);
        bool godDouble = GodrageRules.IsGodrageDoublingCombo(cards)
            && !(_state.SuppressMagicalExplosionPredictionDuringSequenceReveal && MagicalExplosionRules.ContainsMagicalExplosion(cards))
            && !(_state.SuppressMillionDollarBazookaPredictionDuringSequenceReveal && MillionDollarBazookaRules.ContainsMillionDollarBazooka(cards))
            && !(_state.SuppressTributeBloodPredictionDuringSequenceReveal && TributeBloodRules.ContainsTributeBlood(cards))
            && !(_state.SuppressHammadnessPredictionDuringSequenceReveal && HammadnessRules.ContainsHammadness(cards))
            && !_state.AttackDisplaySuppressGodRageDouble;
        if (godDouble)
            sum *= 2;
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    public int ComputeAttackPowerFromCardSum(
        int cardSum,
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defenderForBlessings)
    {
        if (cards == null || cards.Count == 0) return 0;
        int raw = cardSum;
        if (attacker != null && raw > 0)
            raw = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, raw);
        if (attacker != null && raw > 0 && defenderForBlessings != null)
            raw = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(attacker, defenderForBlessings, cards, raw);
        if (attacker == null || raw <= 0) return raw;
        if (CardRules.IsMagicClassifiedAttackCombo(cards)) return raw;
        return attacker.ApplyOutgoingDamageModifiers(raw);
    }

    public static PlayerStatus GetDefenderForAttackDisplay(PlayerStatus attacker)
    {
        var bm = BattleManager.I;
        if (bm == null || attacker == null) return null;
        var p = bm.GetPlayerStatus();
        var e = bm.GetEnemyStatus();
        if (attacker.HasConfusionEffect())
        {
            if (bm.TryGetConfusionAttackTargetResolved(out bool targetsSelf))
                return targetsSelf ? attacker : (attacker == p ? e : p);
            if ((bm.CurrentState == GameState.AttackPhase || bm.CurrentState == GameState.CombatResolvePhase)
                && bm.CurrentTurnOwner == (attacker == p ? PlayerType.Player : PlayerType.Enemy))
                return attacker == p ? e : p;
        }

        if (bm.IsPlayerSelfAttackTargetMode
            && bm.CurrentState == GameState.AttackPhase
            && bm.CurrentTurnOwner == PlayerType.Player
            && attacker == p
            && PostDeathCombatContext.Active == null
            && !bm.IsPostDeathSequenceActive)
            return p;
        return attacker == p ? e : (attacker == e ? p : null);
    }

    public int ComputeMagicalExplosionRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sumEx = MagicalExplosionRules.SumAttackPowerExcludingMagicalExplosion(cards);
        sumEx += MagicalSwordRules.GetActivePowerBonus(cards, attacker);
        return ComputeAttackPowerFromCardSum(sumEx, cards, attacker, defenderForBlessings);
    }

    public int ComputeMagicalExplosionRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sum = MagicalExplosionRules.SumCardAttackPowerForMagicalExplosionCombo(cards, attacker);
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    public int ComputeMillionDollarBazookaRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sumEx = MillionDollarBazookaRules.SumAttackPowerExcludingMillionDollarBazooka(cards);
        sumEx += MagicalSwordRules.GetActivePowerBonus(cards, attacker);
        return ComputeAttackPowerFromCardSum(sumEx, cards, attacker, defenderForBlessings);
    }

    public int ComputeMillionDollarBazookaRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sum = MillionDollarBazookaRules.SumCardAttackPowerForMillionDollarBazookaCombo(cards, attacker);
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    public int ComputeTributeBloodRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sumEx = TributeBloodRules.SumAttackPowerExcludingTributeBlood(cards);
        sumEx += MagicalSwordRules.GetActivePowerBonus(cards, attacker);
        return ComputeAttackPowerFromCardSum(sumEx, cards, attacker, defenderForBlessings);
    }

    public int ComputeTributeBloodRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sum = TributeBloodRules.SumCardAttackPowerForTributeBloodCombo(cards, attacker);
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    public int ComputeHammadnessRampFrom(List<CardData> cards, PlayerStatus attacker)
    {
        if (cards == null || attacker == null) return 0;
        int sum = HammadnessRules.SumAttackPowerExcludingHammadness(cards);
        sum += MagicalSwordRules.GetActivePowerBonus(cards, attacker);
        return sum;
    }

    public int ComputeHammadnessRampTo(List<CardData> cards, PlayerStatus attacker)
    {
        if (cards == null || attacker == null) return 0;
        return HammadnessRules.SumCardAttackPowerForHammadnessCombo(cards, attacker);
    }

    public int ComputeGodRageRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        int sum = ResolveCardSumForGodRageDisplay(cards, attacker);
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    public int ComputeGodRageRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        int sum = ResolveCardSumForGodRageDisplay(cards, attacker);
        if (GodrageRules.IsGodrageDoublingCombo(cards))
            sum *= 2;
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    public int ComputeMagicalSwordDisplayRampFrom(
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def)
    {
        if (attackCards == null || atk == null || def == null) return 0;
        var bm = BattleManager.I;
        int savePlayer = 0;
        int saveEnemy = 0;
        int saveGigaPlayer = 0;
        int saveGigaEnemy = 0;
        bool atkIsPlayer = bm != null && ReferenceEquals(atk, bm.GetPlayerStatus());
        bool atkIsEnemy = bm != null && ReferenceEquals(atk, bm.GetEnemyStatus());
        if (bm != null && atkIsPlayer)
        {
            savePlayer = bm.MagicalSwordAttackPowerBonus;
            saveGigaPlayer = bm.GigaFlareAttackPowerBonus;
            bm.SetMagicalSwordAttackPowerBonus(0);
            bm.SetGigaFlareAttackPowerBonus(0);
        }
        else if (bm != null && atkIsEnemy)
        {
            saveEnemy = bm.MagicalSwordEnemyAttackPowerBonus;
            saveGigaEnemy = bm.GigaFlareEnemyAttackPowerBonus;
            bm.SetMagicalSwordEnemyAttackPowerBonus(0);
            bm.SetGigaFlareEnemyAttackPowerBonus(0);
        }
        try
        {
            return GetDisplayedAttackStrengthWithDefender(attackCards, atk, def);
        }
        finally
        {
            if (bm != null && atkIsPlayer)
            {
                bm.SetMagicalSwordAttackPowerBonus(savePlayer);
                bm.SetGigaFlareAttackPowerBonus(saveGigaPlayer);
            }
            if (bm != null && atkIsEnemy)
            {
                bm.SetMagicalSwordEnemyAttackPowerBonus(saveEnemy);
                bm.SetGigaFlareEnemyAttackPowerBonus(saveGigaEnemy);
            }
        }
    }

    public int ComputeMagicalSwordDisplayRampTo(
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def) =>
        GetDisplayedAttackStrengthWithDefender(attackCards, atk, def);
}
