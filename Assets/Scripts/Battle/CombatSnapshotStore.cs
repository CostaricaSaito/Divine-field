using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Combat-time snapshots and display overrides extracted from <see cref="BattleManager"/>.
/// Holds attack combos, card-effect calculation snapshots, and TOTAL ATK/DEF display state.
/// </summary>
public sealed class CombatSnapshotStore
{
    private CardData _currentAttackCard;

    private List<CardData> _onlineEnemyAttackCombo;
    private List<CardData> _enemyAttackComboForCombat;
    private List<CardData> _playerAttackComboForCombat;

    private bool _confusionAttackTargetResolved;
    private bool _confusionAttackTargetsSelf;

    private bool _magicalExplosionMpSnapActive;
    private int _magicalExplosionMpPoolAfterOtherCosts;

    private bool _millionDollarBazookaGpSnapActive;
    private int _millionDollarBazookaGpPoolBeforeDrain;

    private bool _hammadnessRollSnapActive;
    private int _hammadnessRolledAttackPower;

    private int _magicalSwordAttackPowerBonus;
    private bool _magicalSwordPlayerPreMeRampVisualDone;

    private int _magicalSwordEnemyAttackPowerBonus;
    private bool _magicalSwordEnemyPreMeRampVisualDone;

    private bool _tributeBloodHpPaidSnapActive;
    private int _tributeBloodPlayerHpPaid;
    private int _tributeBloodEnemyHpPaid;

    private bool _reflectionAtkTotalActive;
    private bool _reflectionAtkTotalOnPlayerSide;
    private PlayerStatus _reflectionAtkBlessAttacker;
    private PlayerStatus _reflectionAtkBlessDefender;
    private readonly List<CardData> _reflectionAtkCardsForTotalDisplay = new();
    private int? _reflectionAtkDisplayStrengthOverride;
    private bool _suppressEnemyStaleAttackerInTotalByOrb;

    private List<CardData> _postDeathChainAttackDisplay;
    private Side _postDeathChainAttackDisplaySide = Side.Player;

    public CardData CurrentAttackCard
    {
        get => _currentAttackCard;
        set => _currentAttackCard = value;
    }

    public CardData GetCurrentAttackCard() => _currentAttackCard;

    public void SetCurrentAttackCard(CardData card)
    {
        _currentAttackCard = card;
        if (card == null)
        {
            ClearPlayerAttackComboForCombat();
            ClearEnemyAttackComboForCombat();
        }
    }

    public void SetEnemyAttackComboForCombat(List<CardData> cards)
        => _enemyAttackComboForCombat = cards != null && cards.Count > 0
            ? new List<CardData>(cards)
            : null;

    public void ClearEnemyAttackComboForCombat() => _enemyAttackComboForCombat = null;

    public void SetOnlineEnemyAttackCombo(List<CardData> cards)
        => _onlineEnemyAttackCombo = cards != null ? new List<CardData>(cards) : null;

    public void ClearOnlineEnemyAttackCombo() => _onlineEnemyAttackCombo = null;

    public bool TryGetOnlineEnemyAttackCombo(out List<CardData> combo)
    {
        if (_onlineEnemyAttackCombo != null && _onlineEnemyAttackCombo.Count > 0)
        {
            combo = _onlineEnemyAttackCombo;
            return true;
        }

        combo = null;
        return false;
    }

    public void SetPlayerAttackComboForCombat(List<CardData> cards)
        => _playerAttackComboForCombat = cards != null && cards.Count > 0
            ? new List<CardData>(cards)
            : null;

    public void ClearPlayerAttackComboForCombat() => _playerAttackComboForCombat = null;

    /// <summary>
    /// Resolves attack cards for combat from UI selection, stored combos, and current attack card.
    /// </summary>
    public List<CardData> ResolveAttackCardsForCombat(
        PlayerType attacker,
        IReadOnlyList<CardData> uiSelectedAttackCards,
        IReadOnlyList<CardData> attackerLastAttackSelection)
    {
        if (attacker == PlayerType.Player)
        {
            if (uiSelectedAttackCards != null && uiSelectedAttackCards.Count > 0)
                return new List<CardData>(uiSelectedAttackCards);
            if (_playerAttackComboForCombat != null && _playerAttackComboForCombat.Count > 0)
                return new List<CardData>(_playerAttackComboForCombat);
            if (_currentAttackCard != null)
                return new List<CardData> { _currentAttackCard };
            return uiSelectedAttackCards != null
                ? new List<CardData>(uiSelectedAttackCards)
                : new List<CardData>();
        }

        if (_enemyAttackComboForCombat != null && _enemyAttackComboForCombat.Count > 0)
            return new List<CardData>(_enemyAttackComboForCombat);
        if (_onlineEnemyAttackCombo != null && _onlineEnemyAttackCombo.Count > 0
            && MatchesCurrentAttackContext(_onlineEnemyAttackCombo))
            return new List<CardData>(_onlineEnemyAttackCombo);
        if (attackerLastAttackSelection != null && attackerLastAttackSelection.Count > 0
            && MatchesCurrentAttackContext(attackerLastAttackSelection))
            return new List<CardData>(attackerLastAttackSelection);
        return _currentAttackCard != null
            ? new List<CardData> { _currentAttackCard }
            : new List<CardData>();
    }

    /// <summary>
    /// 単一 <see cref="_currentAttackCard"/> フォールバックを避け、多枚コンボを優先するための一致判定。
    /// </summary>
    private bool MatchesCurrentAttackContext(IReadOnlyList<CardData> candidate)
    {
        if (candidate == null || candidate.Count == 0) return false;
        if (_currentAttackCard == null) return candidate.Count > 1;
        for (int i = 0; i < candidate.Count; i++)
        {
            if (candidate[i] == _currentAttackCard)
                return true;
        }
        return false;
    }

    public void SetConfusionAttackTargetResolvedForDisplay(bool targetsSelf)
    {
        _confusionAttackTargetResolved = true;
        _confusionAttackTargetsSelf = targetsSelf;
    }

    public void ClearConfusionAttackTargetResolvedForDisplay()
    {
        _confusionAttackTargetResolved = false;
    }

    public bool TryGetConfusionAttackTargetResolved(out bool targetsSelf)
    {
        if (_confusionAttackTargetResolved)
        {
            targetsSelf = _confusionAttackTargetsSelf;
            return true;
        }

        targetsSelf = false;
        return false;
    }

    public void SetMagicalExplosionComboMpPoolSnapshot(int mpRemainingBeforeMeDrain)
    {
        _magicalExplosionMpSnapActive = true;
        _magicalExplosionMpPoolAfterOtherCosts = Mathf.Max(0, mpRemainingBeforeMeDrain);
    }

    public bool TryGetMagicalExplosionComboMpPoolSnapshot(out int mp)
    {
        if (_magicalExplosionMpSnapActive)
        {
            mp = _magicalExplosionMpPoolAfterOtherCosts;
            return true;
        }

        mp = 0;
        return false;
    }

    public void ClearMagicalExplosionComboMpPoolSnapshot()
    {
        _magicalExplosionMpSnapActive = false;
    }

    public void SetMillionDollarBazookaComboGpPoolSnapshot(int gpRemainingBeforeDrain)
    {
        _millionDollarBazookaGpSnapActive = true;
        _millionDollarBazookaGpPoolBeforeDrain = Mathf.Max(0, gpRemainingBeforeDrain);
    }

    public bool TryGetMillionDollarBazookaComboGpPoolSnapshot(out int gp)
    {
        if (_millionDollarBazookaGpSnapActive)
        {
            gp = _millionDollarBazookaGpPoolBeforeDrain;
            return true;
        }

        gp = 0;
        return false;
    }

    public void ClearMillionDollarBazookaComboGpPoolSnapshot()
    {
        _millionDollarBazookaGpSnapActive = false;
    }

    public void SetHammadnessRollSnapshot(int rolledAttackPower)
    {
        _hammadnessRollSnapActive = true;
        _hammadnessRolledAttackPower = Mathf.Clamp(
            rolledAttackPower,
            HammadnessRules.MinRollInclusive,
            HammadnessRules.MaxRollInclusive);
    }

    public bool TryGetHammadnessRollSnapshot(out int rolledAttackPower)
    {
        if (_hammadnessRollSnapActive)
        {
            rolledAttackPower = _hammadnessRolledAttackPower;
            return true;
        }

        rolledAttackPower = 0;
        return false;
    }

    public void ClearHammadnessRollSnapshot()
    {
        _hammadnessRollSnapActive = false;
        _hammadnessRolledAttackPower = 0;
    }

    public int MagicalSwordAttackPowerBonus => _magicalSwordAttackPowerBonus;

    public void SetMagicalSwordAttackPowerBonus(int value)
        => _magicalSwordAttackPowerBonus = Mathf.Max(0, value);

    public void ClearMagicalSwordAttackPowerBonus() => _magicalSwordAttackPowerBonus = 0;

    public bool MagicalSwordPlayerPreMeRampVisualDone => _magicalSwordPlayerPreMeRampVisualDone;

    public void SetMagicalSwordPlayerPreMeRampVisualDone(bool value)
        => _magicalSwordPlayerPreMeRampVisualDone = value;

    public void ClearMagicalSwordPlayerAttackState()
    {
        _magicalSwordAttackPowerBonus = 0;
        _magicalSwordPlayerPreMeRampVisualDone = false;
    }

    public int MagicalSwordEnemyAttackPowerBonus => _magicalSwordEnemyAttackPowerBonus;

    public void SetMagicalSwordEnemyAttackPowerBonus(int value)
        => _magicalSwordEnemyAttackPowerBonus = Mathf.Max(0, value);

    public void ClearMagicalSwordEnemyAttackPowerBonus() => _magicalSwordEnemyAttackPowerBonus = 0;

    public bool MagicalSwordEnemyPreMeRampVisualDone => _magicalSwordEnemyPreMeRampVisualDone;

    public void SetMagicalSwordEnemyPreMeRampVisualDone(bool value)
        => _magicalSwordEnemyPreMeRampVisualDone = value;

    public void ClearMagicalSwordEnemyAttackState()
    {
        _magicalSwordEnemyAttackPowerBonus = 0;
        _magicalSwordEnemyPreMeRampVisualDone = false;
    }

    public void SetTributeBloodPlayerHpPaidSnapshot(int hpPaid)
    {
        _tributeBloodHpPaidSnapActive = true;
        _tributeBloodPlayerHpPaid = Mathf.Max(0, hpPaid);
    }

    public void SetTributeBloodEnemyHpPaidSnapshot(int hpPaid)
    {
        _tributeBloodHpPaidSnapActive = true;
        _tributeBloodEnemyHpPaid = Mathf.Max(0, hpPaid);
    }

    public bool TryGetTributeBloodHpPaidSnapshot(
        PlayerStatus attacker,
        PlayerStatus playerStatus,
        PlayerStatus enemyStatus,
        out int hpPaid)
    {
        hpPaid = 0;
        if (!_tributeBloodHpPaidSnapActive || attacker == null)
            return false;

        if (ReferenceEquals(attacker, playerStatus))
        {
            hpPaid = _tributeBloodPlayerHpPaid;
            return true;
        }

        if (ReferenceEquals(attacker, enemyStatus))
        {
            hpPaid = _tributeBloodEnemyHpPaid;
            return true;
        }

        return false;
    }

    public void ClearTributeBloodHpPaidSnapshot()
    {
        _tributeBloodHpPaidSnapActive = false;
        _tributeBloodPlayerHpPaid = 0;
        _tributeBloodEnemyHpPaid = 0;
    }

    public bool IsPostDeathChainAttackDisplayActive =>
        _postDeathChainAttackDisplay != null && _postDeathChainAttackDisplay.Count > 0;

    public IReadOnlyList<CardData> GetPostDeathChainAttackDisplayCards() => _postDeathChainAttackDisplay;

    public Side GetPostDeathChainAttackDisplaySide() => _postDeathChainAttackDisplaySide;

    public void SetPostDeathChainAttackDisplay(IReadOnlyList<CardData> cards, Side deadSide)
    {
        _postDeathChainAttackDisplay = cards != null && cards.Count > 0
            ? new List<CardData>(cards)
            : null;
        _postDeathChainAttackDisplaySide = deadSide;
    }

    public void ClearPostDeathChainAttackDisplay()
    {
        _postDeathChainAttackDisplay = null;
    }

    public void SetReflectionAttackTotalDisplayAfterSlide(
        List<CardData> attackCards,
        bool totalAtkOnPlayerSide,
        PlayerStatus reflectionBlessingsAttacker,
        PlayerStatus reflectionBlessingsDefender,
        int? displayStrengthOverride = null)
    {
        _reflectionAtkDisplayStrengthOverride = displayStrengthOverride;
        _reflectionAtkCardsForTotalDisplay.Clear();
        if (attackCards != null)
            _reflectionAtkCardsForTotalDisplay.AddRange(attackCards);
        _reflectionAtkTotalActive = _reflectionAtkCardsForTotalDisplay.Count > 0;
        _reflectionAtkTotalOnPlayerSide = totalAtkOnPlayerSide;
        _reflectionAtkBlessAttacker = reflectionBlessingsAttacker;
        _reflectionAtkBlessDefender = reflectionBlessingsDefender;
    }

    public void ClearReflectionAttackTotalDisplay()
    {
        _reflectionAtkTotalActive = false;
        _reflectionAtkCardsForTotalDisplay.Clear();
        _reflectionAtkBlessAttacker = null;
        _reflectionAtkBlessDefender = null;
        _reflectionAtkDisplayStrengthOverride = null;
    }

    public int? GetReflectionAttackDisplayStrengthOverride() => _reflectionAtkDisplayStrengthOverride;

    public bool IsSuppressingEnemyStaleAttackerInTotalByOrb() => _suppressEnemyStaleAttackerInTotalByOrb;

    public void SetSuppressEnemyStaleAttackerInTotalByOrb(bool value)
        => _suppressEnemyStaleAttackerInTotalByOrb = value;

    public PlayerStatus GetReflectionAttackBlessingAttacker() => _reflectionAtkBlessAttacker;

    public PlayerStatus GetReflectionAttackBlessingDefender() => _reflectionAtkBlessDefender;

    public bool IsReflectionAttackTotalDisplayActive()
        => _reflectionAtkTotalActive && _reflectionAtkCardsForTotalDisplay.Count > 0;

    public bool ReflectionAttackTotalOnPlayerSide => _reflectionAtkTotalOnPlayerSide;

    public List<CardData> GetReflectionAttackCardsForTotalDisplay() => _reflectionAtkCardsForTotalDisplay;
}
