using System.Collections.Generic;
using System.Text;

/// <summary>TotalATKDEF の攻撃・防御力計算とラベル整形。</summary>
public class TotalAtkDefPowerCalculator
{
    public const string IfritBonusColorHex = "#E53935";
    public const string LeviathanSuppressColorHex = "#1E88E5";
    public const string GodRageAtkBaseGreenHex = "#33DD55";

    private readonly TotalAtkDefDisplayState _state;

    public TotalAtkDefPowerCalculator(TotalAtkDefDisplayState state)
    {
        _state = state;
    }

    public int CalculateTotalDefensePower(List<CardData> defenseCards) =>
        CalculateTotalPower(defenseCards, false);

    private int CalculateTotalPower(List<CardData> cards, bool isAttack)
    {
        int total = 0;
        foreach (var card in cards)
        {
            if (card != null)
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
            plain += MagicalSwordRules.GetActivePowerBonus(attackCards, attackerForMeRule);
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
        if (defenseCards == null || defenseCards.Count == 0) return "";
        if (defenseCards.Count == 1) return $"DEF {defenseCards[0].defensePower}";
        return $"DEF {CalculateTotalDefensePower(defenseCards)}";
    }

    public string FormatReflectionAttackTotalLabel(BattleManager bm, PlayerStatus fallbackAttacker)
    {
        var rc = bm.GetReflectionAttackCardsForTotalDisplay();
        if (rc == null || rc.Count == 0) return "";
        if (bm.GetReflectionAttackDisplayStrengthOverride() is int ovr)
        {
            if (ovr <= 0) return "";
            return $"ATK {ovr}";
        }
        var rAtk = bm.GetReflectionAttackBlessingAttacker();
        var rDef = bm.GetReflectionAttackBlessingDefender();
        if (rAtk != null && rDef != null)
        {
            if (GodrageRules.IsGodrageDoublingCombo(rc))
                return FormatGodRageDoubledAttackPowerDisplayLabel(rc, rAtk, rDef);
            return FormatAttackPowerDisplayLabel(rc, rAtk, rDef);
        }
        return FormatAttackPowerDisplayLabel(rc, fallbackAttacker);
    }

    public string FormatAttackPowerDisplayLabel(
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defenderForBlessingsOverride = null,
        bool forMeOnlyPostRampExcludeGodRageDouble = false)
    {
        if (cards == null || cards.Count == 0 || attacker == null) return "";

        int rawCombo = CalculateTotalAttackPower(cards, attacker);
        if (rawCombo <= 0 && MillionDollarBazookaRules.ContainsMillionDollarBazooka(cards))
            rawCombo = MillionDollarBazookaRules.SumCardAttackPowerForMillionDollarBazookaCombo(cards, attacker);
        if (rawCombo <= 0)
        {
            if (HammadnessRules.ContainsHammadness(cards) || TributeBloodRules.ContainsTributeBlood(cards)
                || MillionDollarBazookaRules.ContainsMillionDollarBazooka(cards))
                return "ATK 0";
            return "";
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
        int baseForBlessings = applyGodDouble ? rawCombo * 2 : rawCombo;

        int afterIfrit = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, baseForBlessings);
        int ifritDelta = afterIfrit - baseForBlessings;

        PlayerStatus defender = defenderForBlessingsOverride ?? GetDefenderForAttackDisplay(attacker);
        int afterLevi = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(attacker, defender, cards, afterIfrit);
        int leviDelta = afterIfrit - afterLevi;

        int final = afterLevi;
        if (!CardRules.IsMagicClassifiedAttackCombo(cards))
            final = attacker.ApplyOutgoingDamageModifiers(afterLevi);

        if (ifritDelta <= 0 && leviDelta <= 0)
            return $"ATK {final}";

        var sb = new StringBuilder(48);
        sb.Append("ATK ").Append(baseForBlessings);
        if (ifritDelta > 0)
            sb.Append(" <color=").Append(IfritBonusColorHex).Append(">+").Append(ifritDelta).Append("</color>");
        if (leviDelta > 0)
            sb.Append(" <color=").Append(LeviathanSuppressColorHex).Append("> -").Append(leviDelta).Append("</color>");
        if (final != afterLevi)
            sb.Append(" → ").Append(final);

        return sb.ToString();
    }

    public string FormatGodRageDoubledAttackPowerDisplayLabel(List<CardData> cards, PlayerStatus attacker, PlayerStatus defender)
    {
        if (cards == null || cards.Count == 0 || attacker == null || defender == null) return "";

        int baseSum = ResolveCardSumForGodRageDisplay(cards, attacker);
        if (baseSum <= 0) return "";

        int doubledBase = baseSum * 2;
        int afterIfrit = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, doubledBase);
        int ifritDelta = afterIfrit - doubledBase;

        int afterLevi = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(attacker, defender, cards, afterIfrit);
        int leviDelta = afterIfrit - afterLevi;

        int final = afterLevi;
        if (!CardRules.IsMagicClassifiedAttackCombo(cards))
            final = attacker.ApplyOutgoingDamageModifiers(afterLevi);

        if (ifritDelta <= 0 && leviDelta <= 0)
            return $"<color={GodRageAtkBaseGreenHex}>ATK {final}</color>";

        var sb = new StringBuilder(96);
        sb.Append("<color=").Append(GodRageAtkBaseGreenHex).Append(">ATK ").Append(doubledBase).Append("</color>");
        if (ifritDelta > 0)
            sb.Append(" <color=").Append(IfritBonusColorHex).Append(">+").Append(ifritDelta).Append("</color>");
        if (leviDelta > 0)
            sb.Append(" <color=").Append(LeviathanSuppressColorHex).Append("> -").Append(leviDelta).Append("</color>");
        if (final != afterLevi)
            sb.Append(" → ").Append(final);

        return sb.ToString();
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
        bool atkIsPlayer = bm != null && ReferenceEquals(atk, bm.GetPlayerStatus());
        bool atkIsEnemy = bm != null && ReferenceEquals(atk, bm.GetEnemyStatus());
        if (bm != null && atkIsPlayer)
        {
            savePlayer = bm.MagicalSwordAttackPowerBonus;
            bm.SetMagicalSwordAttackPowerBonus(0);
        }
        else if (bm != null && atkIsEnemy)
        {
            saveEnemy = bm.MagicalSwordEnemyAttackPowerBonus;
            bm.SetMagicalSwordEnemyAttackPowerBonus(0);
        }
        try
        {
            return GetDisplayedAttackStrengthWithDefender(attackCards, atk, def);
        }
        finally
        {
            if (bm != null && atkIsPlayer)
                bm.SetMagicalSwordAttackPowerBonus(savePlayer);
            if (bm != null && atkIsEnemy)
                bm.SetMagicalSwordEnemyAttackPowerBonus(saveEnemy);
        }
    }

    public int ComputeMagicalSwordDisplayRampTo(
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def) =>
        GetDisplayedAttackStrengthWithDefender(attackCards, atk, def);
}
