using System.Collections.Generic;

/// <summary>Combat snapshot store facade and reflection / post-death TOTALATKDEF state.</summary>
public partial class BattleManager
{
    public void SetEnemyAttackComboForCombat(List<CardData> cards)
        => _combatSnapshots.SetEnemyAttackComboForCombat(cards);

    public void ClearEnemyAttackComboForCombat()
        => _combatSnapshots.ClearEnemyAttackComboForCombat();

    public void SetOnlineEnemyAttackCombo(List<CardData> cards)
        => _combatSnapshots.SetOnlineEnemyAttackCombo(cards);

    public void ClearOnlineEnemyAttackCombo() => _combatSnapshots.ClearOnlineEnemyAttackCombo();

    public void SetPlayerAttackComboForCombat(List<CardData> cards)
    {
        _combatSnapshots.SetPlayerAttackComboForCombat(cards);
        _combatSnapshots.SetPlayerAttackEffectTargetModeRedSnapshot(_playerSelfAttackTargetMode);
    }

    public void ClearPlayerAttackComboForCombat()
        => _combatSnapshots.ClearPlayerAttackComboForCombat();

    /// <summary>Use 確定時点の TOTAL 赤トグル（攻撃演出中も有効）。</summary>
    public void CommitPlayerAttackEffectTargetForCombat()
        => _combatSnapshots.SetPlayerAttackEffectTargetModeRedSnapshot(_playerSelfAttackTargetMode);

    public bool GetPlayerAttackEffectTargetToggleRedForCombat()
    {
        if (_combatSnapshots.TryGetPlayerAttackEffectTargetModeRedSnapshot(out bool red))
            return red;
        return _playerSelfAttackTargetMode;
    }

    public void SetConfusionAttackTargetResolvedForDisplay(bool targetsSelf)
        => _combatSnapshots.SetConfusionAttackTargetResolvedForDisplay(targetsSelf);

    public void ClearConfusionAttackTargetResolvedForDisplay()
        => _combatSnapshots.ClearConfusionAttackTargetResolvedForDisplay();

    public bool TryGetConfusionAttackTargetResolved(out bool targetsSelf)
        => _combatSnapshots.TryGetConfusionAttackTargetResolved(out targetsSelf);

    public void SetMagicalExplosionComboMpPoolSnapshot(int mpRemainingBeforeMeDrain)
        => _combatSnapshots.SetMagicalExplosionComboMpPoolSnapshot(mpRemainingBeforeMeDrain);

    public bool TryGetMagicalExplosionComboMpPoolSnapshot(out int mp)
        => _combatSnapshots.TryGetMagicalExplosionComboMpPoolSnapshot(out mp);

    public void ClearMagicalExplosionComboMpPoolSnapshot()
        => _combatSnapshots.ClearMagicalExplosionComboMpPoolSnapshot();

    public void SetMillionDollarBazookaComboGpPoolSnapshot(int gpRemainingBeforeDrain)
        => _combatSnapshots.SetMillionDollarBazookaComboGpPoolSnapshot(gpRemainingBeforeDrain);

    public bool TryGetMillionDollarBazookaComboGpPoolSnapshot(out int gp)
        => _combatSnapshots.TryGetMillionDollarBazookaComboGpPoolSnapshot(out gp);

    public void ClearMillionDollarBazookaComboGpPoolSnapshot()
        => _combatSnapshots.ClearMillionDollarBazookaComboGpPoolSnapshot();

    public void SetHammadnessRollSnapshot(int rolledAttackPower)
        => _combatSnapshots.SetHammadnessRollSnapshot(rolledAttackPower);

    public bool TryGetHammadnessRollSnapshot(out int rolledAttackPower)
        => _combatSnapshots.TryGetHammadnessRollSnapshot(out rolledAttackPower);

    public void ClearHammadnessRollSnapshot()
        => _combatSnapshots.ClearHammadnessRollSnapshot();

    public int MagicalSwordAttackPowerBonus => _combatSnapshots.MagicalSwordAttackPowerBonus;

    public void SetMagicalSwordAttackPowerBonus(int value)
        => _combatSnapshots.SetMagicalSwordAttackPowerBonus(value);

    public void ClearMagicalSwordAttackPowerBonus()
        => _combatSnapshots.ClearMagicalSwordAttackPowerBonus();

    public bool MagicalSwordPlayerPreMeRampVisualDone
        => _combatSnapshots.MagicalSwordPlayerPreMeRampVisualDone;

    public void SetMagicalSwordPlayerPreMeRampVisualDone(bool value)
        => _combatSnapshots.SetMagicalSwordPlayerPreMeRampVisualDone(value);

    public void ClearMagicalSwordPlayerAttackState()
        => _combatSnapshots.ClearMagicalSwordPlayerAttackState();

    public int MagicalSwordEnemyAttackPowerBonus => _combatSnapshots.MagicalSwordEnemyAttackPowerBonus;

    public void SetMagicalSwordEnemyAttackPowerBonus(int value)
        => _combatSnapshots.SetMagicalSwordEnemyAttackPowerBonus(value);

    public void ClearMagicalSwordEnemyAttackPowerBonus()
        => _combatSnapshots.ClearMagicalSwordEnemyAttackPowerBonus();

    public bool MagicalSwordEnemyPreMeRampVisualDone
        => _combatSnapshots.MagicalSwordEnemyPreMeRampVisualDone;

    public void SetMagicalSwordEnemyPreMeRampVisualDone(bool value)
        => _combatSnapshots.SetMagicalSwordEnemyPreMeRampVisualDone(value);

    public void ClearMagicalSwordEnemyAttackState()
        => _combatSnapshots.ClearMagicalSwordEnemyAttackState();

    public int GigaFlareAttackPowerBonus => _combatSnapshots.GigaFlareAttackPowerBonus;

    public void SetGigaFlareAttackPowerBonus(int value)
        => _combatSnapshots.SetGigaFlareAttackPowerBonus(value);

    public int GigaFlareEnemyAttackPowerBonus => _combatSnapshots.GigaFlareEnemyAttackPowerBonus;

    public void SetGigaFlareEnemyAttackPowerBonus(int value)
        => _combatSnapshots.SetGigaFlareEnemyAttackPowerBonus(value);

    public void ClearGigaFlarePlayerAttackState()
        => _combatSnapshots.ClearGigaFlarePlayerAttackState();

    public void ClearGigaFlareEnemyAttackState()
        => _combatSnapshots.ClearGigaFlareEnemyAttackState();

    public void SetTributeBloodPlayerHpPaidSnapshot(int hpPaid)
        => _combatSnapshots.SetTributeBloodPlayerHpPaidSnapshot(hpPaid);

    public void SetTributeBloodEnemyHpPaidSnapshot(int hpPaid)
        => _combatSnapshots.SetTributeBloodEnemyHpPaidSnapshot(hpPaid);

    public bool TryGetTributeBloodHpPaidSnapshot(PlayerStatus attacker, out int hpPaid)
        => _combatSnapshots.TryGetTributeBloodHpPaidSnapshot(attacker, playerStatus, enemyStatus, out hpPaid);

    public void ClearTributeBloodHpPaidSnapshot()
        => _combatSnapshots.ClearTributeBloodHpPaidSnapshot();

    public bool IsRagnarokDisasterTriggeredThisAttack
        => _combatSnapshots.IsRagnarokDisasterTriggeredThisAttack;

    public void MarkRagnarokDisasterTriggeredThisAttack()
        => _combatSnapshots.MarkRagnarokDisasterTriggeredThisAttack();

    public void ClearRagnarokDisasterTriggeredThisAttack()
        => _combatSnapshots.ClearRagnarokDisasterTriggeredThisAttack();

    public void SetCurrentAttackCard(CardData card)
        => _combatSnapshots.SetCurrentAttackCard(card);

    public CardData GetCurrentAttackCard() => _combatSnapshots.GetCurrentAttackCard();

    public bool IsPostDeathChainAttackDisplayActive => _combatSnapshots.IsPostDeathChainAttackDisplayActive;

    public IReadOnlyList<CardData> GetPostDeathChainAttackDisplayCards()
        => _combatSnapshots.GetPostDeathChainAttackDisplayCards();

    public Side GetPostDeathChainAttackDisplaySide()
        => _combatSnapshots.GetPostDeathChainAttackDisplaySide();

    public void SetPostDeathChainAttackDisplay(IReadOnlyList<CardData> cards, Side deadSide)
    {
        _combatSnapshots.SetPostDeathChainAttackDisplay(cards, deadSide);
        UpdateTotalATKDEFDisplay();
    }

    public void ClearPostDeathChainAttackDisplay()
    {
        _combatSnapshots.ClearPostDeathChainAttackDisplay();
        UpdateTotalATKDEFDisplay();
    }

    /// <summary>
    /// Reflection / parry / orb counter TOTAL: ATK shown only on <paramref name="onPlayerSide"/> panel
    /// (the panel holding the attack sheets). Call again after every slide.
    /// </summary>
    public void SetReflectionAttackTotalDisplay(List<CardData> attackCards, bool onPlayerSide, int attackPower)
    {
        _combatSnapshots.SetReflectionAttackTotalDisplay(attackCards, onPlayerSide, attackPower);
        UpdateTotalATKDEFDisplay();
    }

    public void ClearReflectionAttackTotalDisplay()
    {
        _combatSnapshots.ClearReflectionAttackTotalDisplay();
        UpdateTotalATKDEFDisplay();
    }

    public bool IsSuppressingEnemyStaleAttackerInTotalByOrb()
        => _combatSnapshots.IsSuppressingEnemyStaleAttackerInTotalByOrb();

    public void SetSuppressEnemyStaleAttackerInTotalByOrb(bool v)
        => _combatSnapshots.SetSuppressEnemyStaleAttackerInTotalByOrb(v);

    public bool IsReflectionAttackTotalDisplayActive()
        => _combatSnapshots.IsReflectionAttackTotalDisplayActive();

    public bool ReflectionAttackTotalOnPlayerSide
        => _combatSnapshots.ReflectionAttackTotalOnPlayerSide;

    public int ReflectionAttackPower => _combatSnapshots.ReflectionAttackPower;

    public List<CardData> GetReflectionAttackCardsForTotalDisplay()
        => _combatSnapshots.GetReflectionAttackCardsForTotalDisplay();
}
