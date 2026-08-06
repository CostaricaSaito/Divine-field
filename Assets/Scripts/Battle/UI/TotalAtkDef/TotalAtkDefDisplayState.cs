using System.Collections.Generic;

/// <summary>TotalATKDEF 表示の可変状態（シーケンス・ロック・抑制フラグ）。</summary>
public class TotalAtkDefDisplayState
{
    private readonly List<CardData> currentSequenceCards = new List<CardData>();
    private string currentSequenceType = "";
    private Side sequenceOwnerSide = Side.Player;

    private bool _attackSequenceRevealInProgress;
    private Side _attackSequenceRevealAttackerSide = Side.Player;

    private bool _godRagePlayerAtkDisplayLocked;
    private string _godRagePlayerAtkDisplayRichText;

    private bool _magicalExplosionPreRampLocked;
    private int _magicalExplosionPreRampAtkDisplayValue;

    private bool _magicalExplosionPlayerAtkDisplayLocked;
    private string _magicalExplosionPlayerAtkDisplayRichText;

    internal bool SuppressMagicalExplosionPredictionDuringSequenceReveal { get; private set; }

    private bool _millionDollarBazookaPlayerAtkDisplayLocked;
    private string _millionDollarBazookaPlayerAtkDisplayRichText;

    private bool _millionDollarBazookaPreRampLocked;
    private int _millionDollarBazookaPreRampAtkDisplayValue;

    internal bool SuppressMillionDollarBazookaPredictionDuringSequenceReveal { get; private set; }

    private bool _hammadnessPreRampLocked;
    private int _hammadnessPreRampAtkDisplayValue;

    private bool _hammadnessPlayerAtkDisplayLocked;
    private string _hammadnessPlayerAtkDisplayRichText;

    internal bool SuppressHammadnessPredictionDuringSequenceReveal { get; private set; }

    private bool _tributeBloodPreRampLocked;
    private int _tributeBloodPreRampAtkDisplayValue;

    private bool _tributeBloodPlayerAtkDisplayLocked;
    private string _tributeBloodPlayerAtkDisplayRichText;

    internal bool SuppressTributeBloodPredictionDuringSequenceReveal { get; private set; }

    internal bool SuppressSpellbookElementDuringSequenceReveal { get; private set; }

    internal bool AttackDisplaySuppressGodRageDouble { get; private set; }
    internal bool AttackDisplaySuppressMagicalSwordBonus { get; private set; }

    private bool _magicalSwordRampAtkDisplayLocked;
    private string _magicalSwordRampAtkDisplayRichText;

    public IReadOnlyList<CardData> CurrentSequenceCards => currentSequenceCards;
    public string CurrentSequenceType => currentSequenceType;
    public Side SequenceOwnerSide => sequenceOwnerSide;
    public bool AttackSequenceRevealInProgress => _attackSequenceRevealInProgress;
    public Side AttackSequenceRevealAttackerSide => _attackSequenceRevealAttackerSide;

    public void SetSequenceCards(List<CardData> cards, string cardType)
    {
        SetSequenceCards(cards, cardType, Side.Player);
    }

    public void SetSequenceCards(List<CardData> cards, string cardType, Side ownerSide)
    {
        currentSequenceCards.Clear();
        if (cards != null)
            currentSequenceCards.AddRange(cards);
        currentSequenceType = cardType ?? "";
        sequenceOwnerSide = ownerSide;
    }

    public void BeginAttackSequenceReveal(Side attackerSide)
    {
        _attackSequenceRevealInProgress = true;
        _attackSequenceRevealAttackerSide = attackerSide;
    }

    public void EndAttackSequenceReveal()
    {
        _attackSequenceRevealInProgress = false;
    }

    public bool TryGetSequenceAttackLockedDisplayText(out string text)
    {
        text = null;
        if (_magicalExplosionPlayerAtkDisplayLocked && !string.IsNullOrEmpty(_magicalExplosionPlayerAtkDisplayRichText))
        {
            text = _magicalExplosionPlayerAtkDisplayRichText;
            return true;
        }
        if (_magicalExplosionPreRampLocked)
        {
            text = $"ATK {_magicalExplosionPreRampAtkDisplayValue}";
            return true;
        }
        if (_millionDollarBazookaPlayerAtkDisplayLocked && !string.IsNullOrEmpty(_millionDollarBazookaPlayerAtkDisplayRichText))
        {
            text = _millionDollarBazookaPlayerAtkDisplayRichText;
            return true;
        }
        if (_millionDollarBazookaPreRampLocked)
        {
            text = $"ATK {_millionDollarBazookaPreRampAtkDisplayValue}";
            return true;
        }
        if (_tributeBloodPlayerAtkDisplayLocked && !string.IsNullOrEmpty(_tributeBloodPlayerAtkDisplayRichText))
        {
            text = _tributeBloodPlayerAtkDisplayRichText;
            return true;
        }
        if (_tributeBloodPreRampLocked)
        {
            text = $"ATK {_tributeBloodPreRampAtkDisplayValue}";
            return true;
        }
        if (_hammadnessPlayerAtkDisplayLocked && !string.IsNullOrEmpty(_hammadnessPlayerAtkDisplayRichText))
        {
            text = _hammadnessPlayerAtkDisplayRichText;
            return true;
        }
        if (_hammadnessPreRampLocked)
        {
            text = $"ATK {_hammadnessPreRampAtkDisplayValue}";
            return true;
        }
        if (_godRagePlayerAtkDisplayLocked && !string.IsNullOrEmpty(_godRagePlayerAtkDisplayRichText))
        {
            text = _godRagePlayerAtkDisplayRichText;
            return true;
        }
        if (_magicalSwordRampAtkDisplayLocked
            && !string.IsNullOrEmpty(_magicalSwordRampAtkDisplayRichText))
        {
            text = _magicalSwordRampAtkDisplayRichText;
            return true;
        }
        return false;
    }

    public void ClearSequenceCards()
    {
        currentSequenceCards.Clear();
        currentSequenceType = "";
        sequenceOwnerSide = Side.Player;
    }

    public void ClearAllAttackSequenceDisplayLocks()
    {
        ClearGodRageAttackDisplayLock();
        ClearMagicalSwordRampAttackDisplayLock();
        ClearAttackModifierRevealSuppressions();
        ClearMagicalExplosionAttackDisplayLocks();
        ClearMillionDollarBazookaAttackDisplayLocks();
        ClearHammadnessAttackDisplayLocks();
        ClearTributeBloodAttackDisplayLocks();
        EndAttackSequenceReveal();
    }

    public void ClearSequenceCardsAndAttackDisplayLocks()
    {
        ClearSequenceCards();
        ClearAllAttackSequenceDisplayLocks();
    }

    public void ClearGodRageAttackDisplayLock()
    {
        _godRagePlayerAtkDisplayLocked = false;
        _godRagePlayerAtkDisplayRichText = null;
    }

    public void ClearHammadnessAttackDisplayLocks()
    {
        _hammadnessPreRampLocked = false;
        _hammadnessPreRampAtkDisplayValue = 0;
        _hammadnessPlayerAtkDisplayLocked = false;
        _hammadnessPlayerAtkDisplayRichText = null;
        SuppressHammadnessPredictionDuringSequenceReveal = false;
    }

    public void ClearHammadnessPlayerAtkDisplayLockOnly()
    {
        _hammadnessPlayerAtkDisplayLocked = false;
        _hammadnessPlayerAtkDisplayRichText = null;
    }

    public void SetSuppressHammadnessPredictionDuringSequenceReveal(bool value)
    {
        SuppressHammadnessPredictionDuringSequenceReveal = value;
    }

    public void SetHammadnessPreRampAttackDisplay(int displayedAtkStrength)
    {
        _hammadnessPreRampAtkDisplayValue = displayedAtkStrength;
        _hammadnessPreRampLocked = true;
    }

    public void ClearMagicalExplosionAttackDisplayLocks()
    {
        _magicalExplosionPreRampLocked = false;
        _magicalExplosionPreRampAtkDisplayValue = 0;
        _magicalExplosionPlayerAtkDisplayLocked = false;
        _magicalExplosionPlayerAtkDisplayRichText = null;
        SuppressMagicalExplosionPredictionDuringSequenceReveal = false;
        SuppressSpellbookElementDuringSequenceReveal = false;
    }

    public void ClearMagicalExplosionPlayerAtkDisplayLockOnly()
    {
        _magicalExplosionPlayerAtkDisplayLocked = false;
        _magicalExplosionPlayerAtkDisplayRichText = null;
    }

    public void SetSuppressMagicalExplosionPredictionDuringSequenceReveal(bool value)
    {
        SuppressMagicalExplosionPredictionDuringSequenceReveal = value;
    }

    public void ClearMillionDollarBazookaAttackDisplayLocks()
    {
        _millionDollarBazookaPreRampLocked = false;
        _millionDollarBazookaPreRampAtkDisplayValue = 0;
        _millionDollarBazookaPlayerAtkDisplayLocked = false;
        _millionDollarBazookaPlayerAtkDisplayRichText = null;
        SuppressMillionDollarBazookaPredictionDuringSequenceReveal = false;
    }

    public void ClearMillionDollarBazookaPlayerAtkDisplayLockOnly()
    {
        _millionDollarBazookaPlayerAtkDisplayLocked = false;
        _millionDollarBazookaPlayerAtkDisplayRichText = null;
    }

    public void SetSuppressMillionDollarBazookaPredictionDuringSequenceReveal(bool value)
    {
        SuppressMillionDollarBazookaPredictionDuringSequenceReveal = value;
    }

    public void SetMillionDollarBazookaPreRampAttackDisplay(int displayedAtkStrength)
    {
        _millionDollarBazookaPreRampAtkDisplayValue = displayedAtkStrength;
        _millionDollarBazookaPreRampLocked = true;
    }

    public void SetSuppressTributeBloodPredictionDuringSequenceReveal(bool value)
    {
        SuppressTributeBloodPredictionDuringSequenceReveal = value;
    }

    public void SetTributeBloodPreRampAttackDisplay(int displayedAtkStrength)
    {
        _tributeBloodPreRampAtkDisplayValue = displayedAtkStrength;
        _tributeBloodPreRampLocked = true;
    }

    public void ClearTributeBloodAttackDisplayLocks()
    {
        _tributeBloodPreRampLocked = false;
        _tributeBloodPreRampAtkDisplayValue = 0;
        _tributeBloodPlayerAtkDisplayLocked = false;
        _tributeBloodPlayerAtkDisplayRichText = null;
        SuppressTributeBloodPredictionDuringSequenceReveal = false;
    }

    public void SetSuppressSpellbookElementDuringSequenceReveal(bool value)
    {
        SuppressSpellbookElementDuringSequenceReveal = value;
    }

    public void SetAttackModifierRevealPhase(bool suppressMagicalSwordBonus, bool suppressGodRageDouble)
    {
        AttackDisplaySuppressMagicalSwordBonus = suppressMagicalSwordBonus;
        AttackDisplaySuppressGodRageDouble = suppressGodRageDouble;
    }

    public void ClearAttackModifierRevealSuppressions()
    {
        AttackDisplaySuppressGodRageDouble = false;
        AttackDisplaySuppressMagicalSwordBonus = false;
    }

    public void ClearMagicalSwordRampAttackDisplayLock()
    {
        _magicalSwordRampAtkDisplayLocked = false;
        _magicalSwordRampAtkDisplayRichText = null;
    }

    internal void LockMagicalSwordRampAttackDisplay(int finalDisplayedAtk)
    {
        if (finalDisplayedAtk <= 0) return;
        _magicalSwordRampAtkDisplayRichText =
            $"<color={TotalAtkDefPowerCalculator.GodRageAtkBaseGreenHex}>ATK {finalDisplayedAtk}</color>";
        _magicalSwordRampAtkDisplayLocked = true;
    }

    public void SetMagicalExplosionPreRampAttackDisplay(int displayedAtkStrength)
    {
        _magicalExplosionPreRampAtkDisplayValue = displayedAtkStrength;
        _magicalExplosionPreRampLocked = true;
    }

    public void UnlockMagicalExplosionPreRamp()
    {
        _magicalExplosionPreRampLocked = false;
    }

    public void LockMagicalExplosionPlayerAtkDisplay(string richText)
    {
        _magicalExplosionPlayerAtkDisplayRichText = richText;
        _magicalExplosionPlayerAtkDisplayLocked = true;
    }

    public void LockMillionDollarBazookaPlayerAtkDisplay(string richText)
    {
        _millionDollarBazookaPlayerAtkDisplayRichText = richText;
        _millionDollarBazookaPlayerAtkDisplayLocked = true;
    }

    public void UnlockMillionDollarBazookaPreRamp()
    {
        _millionDollarBazookaPreRampLocked = false;
    }

    public void UnlockTributeBloodPreRamp()
    {
        _tributeBloodPreRampLocked = false;
    }

    public void LockTributeBloodPlayerAtkDisplay(string richText)
    {
        _tributeBloodPlayerAtkDisplayRichText = richText;
        _tributeBloodPlayerAtkDisplayLocked = true;
    }

    public void ClearTributeBloodPlayerAtkDisplayLockOnly()
    {
        _tributeBloodPlayerAtkDisplayLocked = false;
        _tributeBloodPlayerAtkDisplayRichText = null;
    }

    public void UnlockHammadnessPreRamp()
    {
        _hammadnessPreRampLocked = false;
    }

    public void LockHammadnessPlayerAtkDisplay(string richText)
    {
        _hammadnessPlayerAtkDisplayRichText = richText;
        _hammadnessPlayerAtkDisplayLocked = true;
    }

    public void LockGodRagePlayerAtkDisplay(string richText)
    {
        _godRagePlayerAtkDisplayRichText = richText;
        _godRagePlayerAtkDisplayLocked = true;
    }

    public void ClearMillionDollarBazookaPlayerAtkDisplayForGodRageRamp()
    {
        _millionDollarBazookaPlayerAtkDisplayLocked = false;
        _millionDollarBazookaPlayerAtkDisplayRichText = null;
    }

    public void SetAttackDisplaySuppressMagicalSwordBonus(bool value)
    {
        AttackDisplaySuppressMagicalSwordBonus = value;
    }

    public void SetAttackDisplaySuppressGodRageDouble(bool value)
    {
        AttackDisplaySuppressGodRageDouble = value;
    }

    /// <summary>
    /// 攻撃カード掲出開始時：特殊カードの TOTAL 予測（ME／MDB／TB／Ham／魔導書属性）を一括設定する。
    /// </summary>
    public void ConfigureAttackSequenceRevealSuppressions(IReadOnlyList<CardData> selectedCards)
    {
        if (selectedCards == null || selectedCards.Count == 0)
        {
            SuppressMagicalExplosionPredictionDuringSequenceReveal = false;
            SuppressMillionDollarBazookaPredictionDuringSequenceReveal = false;
            SuppressTributeBloodPredictionDuringSequenceReveal = false;
            SuppressHammadnessPredictionDuringSequenceReveal = false;
            SuppressSpellbookElementDuringSequenceReveal = false;
            return;
        }

        SuppressMagicalExplosionPredictionDuringSequenceReveal =
            MagicalExplosionRules.ContainsMagicalExplosion(selectedCards);
        SuppressMillionDollarBazookaPredictionDuringSequenceReveal =
            MillionDollarBazookaRules.ContainsMillionDollarBazooka(selectedCards);
        SuppressTributeBloodPredictionDuringSequenceReveal =
            TributeBloodRules.ContainsTributeBlood(selectedCards);
        SuppressHammadnessPredictionDuringSequenceReveal =
            HammadnessRules.ContainsHammadness(selectedCards);
        SuppressSpellbookElementDuringSequenceReveal =
            SpellbookRules.NeedsElementRevealSequence(selectedCards);
    }

    /// <summary>掲出中：MS 上乗せ・ゴッドレイジ 2 倍を TOTAL から一時除外（ランプまで）。</summary>
    public void BeginSequentialCardRevealModifierSuppressions(
        IReadOnlyList<CardData> selectedCards,
        int magicalSwordOptionalPowerBonusIfPaid)
    {
        if (selectedCards == null) return;
        bool suppressMs = MagicalSwordRules.ContainsMagicalSword(selectedCards)
            && magicalSwordOptionalPowerBonusIfPaid > 0;
        bool suppressGod = GodrageRules.IsGodrageDoublingCombo(selectedCards);
        SetAttackModifierRevealPhase(suppressMs, suppressGod);
    }
}
