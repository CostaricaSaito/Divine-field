using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TotalATKDEF表示を管理するクラス
/// BattleManagerからTotalATKDEF表示関連の処理を移設
/// </summary>
public class CardStatsDisplay : MonoBehaviour
{
    [Header("TotalATKDEF表示（プレイヤー）")]
    [SerializeField] private GameObject totalATKDEFButton;
    [SerializeField] private TMP_Text atkdefText;
    [Tooltip("未指定時は totalATKDEFButton 上の Image を使用。自分攻撃モードで赤系に着色。")]
    [SerializeField] private Image playerTotalAtkDefBackground;
    [Tooltip("TotalATKDEF の合算属性アイコン（表示テキスト・色と同じ合算ルール。無属性は非表示。CardSheet と同じ Resources/Attributes）。")]
    [SerializeField] private Image atkdefElement;

    [Header("TotalATKDEF表示（敵）")]
    [SerializeField] private GameObject totalATKDEFButtonEnemy;
    [SerializeField] private TMP_Text atkdefTextEnemy;
    [Tooltip("敵側 TotalATKDEF の合算属性アイコン。無属性は非表示。")]
    [SerializeField] private Image atkdefElementEnemy;

    private TotalAtkDefDisplayState _state;
    private TotalAtkDefPowerCalculator _power;
    private TotalAtkDefPanelResolver _resolver;

    private void Awake()
    {
        _state = new TotalAtkDefDisplayState();
        _power = new TotalAtkDefPowerCalculator(_state);
        _resolver = new TotalAtkDefPanelResolver(_state, _power);

        if (totalATKDEFButton != null)
            totalATKDEFButton.SetActive(false);
        if (totalATKDEFButtonEnemy != null)
            totalATKDEFButtonEnemy.SetActive(false);
        if (atkdefElement != null)
            atkdefElement.gameObject.SetActive(false);
        if (atkdefElementEnemy != null)
            atkdefElementEnemy.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (totalATKDEFButton != null)
        {
            var btn = totalATKDEFButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnPlayerTotalAtkDefButtonClicked);
        }
    }

    public void HideAllForGameEnd()
    {
        if (totalATKDEFButton != null)
            totalATKDEFButton.SetActive(false);
        if (totalATKDEFButtonEnemy != null)
            totalATKDEFButtonEnemy.SetActive(false);
        if (atkdefElement != null)
            atkdefElement.gameObject.SetActive(false);
        if (atkdefElementEnemy != null)
            atkdefElementEnemy.gameObject.SetActive(false);
    }

    private void OnPlayerTotalAtkDefButtonClicked()
    {
        var bm = BattleManager.I;
        if (bm == null) return;
        if (bm.CurrentState != GameState.AttackPhase || bm.CurrentTurnOwner != PlayerType.Player)
            return;
        var ps = bm.GetPlayerStatus();
        if (ps != null && ps.HasConfusionEffect())
            return;
        bm.TogglePlayerSelfAttackTargetMode();
    }

    private static readonly Color ConfusionAtkDefBackgroundColor = new Color(1f, 0.92f, 0.45f);

    private void ApplyPlayerSelfAttackTargetBackground()
    {
        if (totalATKDEFButton == null || !totalATKDEFButton.activeSelf) return;
        var img = playerTotalAtkDefBackground != null
            ? playerTotalAtkDefBackground
            : totalATKDEFButton.GetComponent<Image>();
        if (img == null) return;
        var bm = BattleManager.I;
        var ps = bm != null ? bm.GetPlayerStatus() : null;
        bool confusedPlayer = ps != null && ps.HasConfusionEffect()
            && bm.CurrentState == GameState.AttackPhase
            && bm.CurrentTurnOwner == PlayerType.Player;
        var btn = totalATKDEFButton.GetComponent<Button>();
        if (btn != null)
            btn.interactable = !confusedPlayer;

        if (confusedPlayer)
        {
            img.color = ConfusionAtkDefBackgroundColor;
            return;
        }

        if (bm != null
            && (bm.IsPostDeathSequenceActive || PostDeathCombatContext.Active != null))
        {
            img.color = Color.white;
            return;
        }

        bool red = bm != null
            && bm.CurrentState == GameState.AttackPhase
            && bm.CurrentTurnOwner == PlayerType.Player
            && bm.IsPlayerSelfAttackTargetMode;
        img.color = red ? new Color(0.92f, 0.42f, 0.42f) : Color.white;
    }

    public bool IsPlayerAttackSelectionNumericAtkZero(IReadOnlyList<CardData> attackCards) =>
        _resolver.IsPlayerAttackSelectionNumericAtkZero(attackCards);

    public void SetSequenceCards(List<CardData> cards, string cardType) =>
        _state.SetSequenceCards(cards, cardType);

    public void SetSequenceCards(List<CardData> cards, string cardType, Side ownerSide) =>
        _state.SetSequenceCards(cards, cardType, ownerSide);

    public void BeginAttackSequenceReveal(Side attackerSide) =>
        _state.BeginAttackSequenceReveal(attackerSide);

    public void EndAttackSequenceReveal() =>
        _state.EndAttackSequenceReveal();

    public void ClearSequenceCards() =>
        _state.ClearSequenceCards();

    public void ClearAllAttackSequenceDisplayLocks() =>
        _state.ClearAllAttackSequenceDisplayLocks();

    public void ClearSequenceCardsAndAttackDisplayLocks() =>
        _state.ClearSequenceCardsAndAttackDisplayLocks();

    public void ClearHammadnessAttackDisplayLocks() =>
        _state.ClearHammadnessAttackDisplayLocks();

    public void ClearHammadnessPlayerAtkDisplayLockOnly() =>
        _state.ClearHammadnessPlayerAtkDisplayLockOnly();

    public void SetSuppressHammadnessPredictionDuringSequenceReveal(bool value) =>
        _state.SetSuppressHammadnessPredictionDuringSequenceReveal(value);

    public void SetHammadnessPreRampAttackDisplay(int displayedAtkStrength) =>
        _state.SetHammadnessPreRampAttackDisplay(displayedAtkStrength);

    public void ClearMagicalExplosionAttackDisplayLocks() =>
        _state.ClearMagicalExplosionAttackDisplayLocks();

    public void ClearMagicalExplosionPlayerAtkDisplayLockOnly() =>
        _state.ClearMagicalExplosionPlayerAtkDisplayLockOnly();

    public void SetSuppressMagicalExplosionPredictionDuringSequenceReveal(bool value) =>
        _state.SetSuppressMagicalExplosionPredictionDuringSequenceReveal(value);

    public void ClearMillionDollarBazookaAttackDisplayLocks() =>
        _state.ClearMillionDollarBazookaAttackDisplayLocks();

    public void ClearMillionDollarBazookaPlayerAtkDisplayLockOnly() =>
        _state.ClearMillionDollarBazookaPlayerAtkDisplayLockOnly();

    public void SetSuppressMillionDollarBazookaPredictionDuringSequenceReveal(bool value) =>
        _state.SetSuppressMillionDollarBazookaPredictionDuringSequenceReveal(value);

    public void SetMillionDollarBazookaPreRampAttackDisplay(int displayedAtkStrength) =>
        _state.SetMillionDollarBazookaPreRampAttackDisplay(displayedAtkStrength);

    public void SetSuppressTributeBloodPredictionDuringSequenceReveal(bool value) =>
        _state.SetSuppressTributeBloodPredictionDuringSequenceReveal(value);

    public void SetTributeBloodPreRampAttackDisplay(int displayedAtkStrength) =>
        _state.SetTributeBloodPreRampAttackDisplay(displayedAtkStrength);

    public void ClearTributeBloodAttackDisplayLocks() =>
        _state.ClearTributeBloodAttackDisplayLocks();

    public void SetSuppressSpellbookElementDuringSequenceReveal(bool value) =>
        _state.SetSuppressSpellbookElementDuringSequenceReveal(value);

    public void ConfigureAttackSequenceRevealSuppressions(List<CardData> selectedCards) =>
        _state.ConfigureAttackSequenceRevealSuppressions(selectedCards);

    public void ClearAttackSequenceRevealSuppressions() =>
        _state.ConfigureAttackSequenceRevealSuppressions(null);

    public void BeginSequentialCardRevealModifierSuppressions(List<CardData> selectedCards, int magicalSwordBonus) =>
        _state.BeginSequentialCardRevealModifierSuppressions(selectedCards, magicalSwordBonus);

    public void SetAttackModifierRevealPhase(bool suppressMagicalSwordBonus, bool suppressGodRageDouble) =>
        _state.SetAttackModifierRevealPhase(suppressMagicalSwordBonus, suppressGodRageDouble);

    public void ClearAttackModifierRevealSuppressions() =>
        _state.ClearAttackModifierRevealSuppressions();

    public void SetMagicalExplosionPreRampAttackDisplay(int displayedAtkStrength) =>
        _state.SetMagicalExplosionPreRampAttackDisplay(displayedAtkStrength);

    public void UpdateDisplay()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null)
        {
            Debug.LogWarning("[CardStatsDisplay] BattleManagerが設定されていません");
            return;
        }

        UpdatePlayerDisplay();
        UpdateEnemyDisplay();
    }

    private void UpdatePlayerDisplay()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null)
        {
            if (totalATKDEFButton != null)
                totalATKDEFButton.SetActive(false);
            if (atkdefElement != null)
                atkdefElement.gameObject.SetActive(false);
            return;
        }

        if (totalATKDEFButton == null)
        {
            Debug.LogWarning("[CardStatsDisplay] totalATKDEFButtonが設定されていません");
            return;
        }

        bool shouldHide = _resolver.ShouldHidePlayer();
        totalATKDEFButton.SetActive(!shouldHide);

        if (shouldHide)
        {
            if (atkdefElement != null)
                atkdefElement.gameObject.SetActive(false);
            return;
        }

        if (atkdefText != null)
        {
            atkdefText.text = _resolver.GetPlayerDisplayText();
            ApplyPlayerTotalAtkDefTextStyle();
        }
        else
        {
            Debug.LogWarning("[CardStatsDisplay] ATKDEFtextが設定されていません");
        }

        ApplyPlayerSelfAttackTargetBackground();
        ApplyTotalAtkDefElementImage(atkdefElement, _resolver.GetPlayerCombinedElement());
    }

    private void UpdateEnemyDisplay()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return;

        if (totalATKDEFButtonEnemy == null)
        {
            Debug.LogWarning("[CardStatsDisplay] totalATKDEFButtonEnemyが設定されていません");
            return;
        }

        bool shouldHide = _resolver.ShouldHideEnemy();
        totalATKDEFButtonEnemy.SetActive(!shouldHide);

        if (shouldHide)
        {
            if (atkdefElementEnemy != null)
                atkdefElementEnemy.gameObject.SetActive(false);
            return;
        }

        if (atkdefTextEnemy != null)
        {
            atkdefTextEnemy.text = _resolver.GetEnemyDisplayText();
            ApplyEnemyTotalAtkDefTextStyle();
        }
        else
        {
            Debug.LogWarning("[CardStatsDisplay] ATKDEFtextEnemyが設定されていません");
        }

        ApplyEnemyConfusionBackground();
        ApplyTotalAtkDefElementImage(atkdefElementEnemy, _resolver.GetEnemyCombinedElement());
    }

    private void ApplyPlayerTotalAtkDefTextStyle()
    {
        if (atkdefText == null) return;
        if (_resolver.IsShowingLockedIncomingAttackDisplay(forEnemyPanel: false))
        {
            atkdefText.richText = true;
            return;
        }
        var bm = BattleManager.I;
        if (bm != null && bm.CurrentState == GameState.AttackPhase && bm.CurrentTurnOwner == PlayerType.Player)
        {
            var sel = BattleUIManager.I?.GetSelectedAttackCards();
            if (sel != null && IsPlayerAttackSelectionNumericAtkZero(sel))
            {
                ApplyAttackLabelTextStyle(atkdefText, new Color(0.15f, 0.15f, 0.18f));
                return;
            }
        }
        ApplyAttackLabelTextStyle(atkdefText, ElementHelper.GetElementColor(_resolver.GetPlayerCombinedElement()));
    }

    private void ApplyEnemyTotalAtkDefTextStyle()
    {
        if (atkdefTextEnemy == null) return;
        if (_resolver.IsShowingLockedIncomingAttackDisplay(forEnemyPanel: true))
        {
            atkdefTextEnemy.richText = true;
            return;
        }
        ApplyAttackLabelTextStyle(atkdefTextEnemy, ElementHelper.GetElementColor(_resolver.GetEnemyCombinedElement()));
    }

    private static void ApplyTotalAtkDefElementImage(Image image, ElementType combinedElement)
    {
        if (image == null) return;
        if (combinedElement == ElementType.None)
        {
            image.gameObject.SetActive(false);
            return;
        }
        var sprite = ElementHelper.LoadIcon(combinedElement);
        if (sprite == null)
        {
            image.gameObject.SetActive(false);
            return;
        }
        image.sprite = sprite;
        image.gameObject.SetActive(true);
    }

    private void ApplyEnemyConfusionBackground()
    {
        if (totalATKDEFButtonEnemy == null || !totalATKDEFButtonEnemy.activeSelf) return;
        var bm = BattleManager.I;
        if (bm == null) return;
        var es = bm.GetEnemyStatus();
        bool confusedEnemy = es != null && es.HasConfusionEffect()
            && bm.CurrentState == GameState.AttackPhase
            && bm.CurrentTurnOwner == PlayerType.Enemy
            && bm.GetCurrentAttackCard() != null;
        var img = totalATKDEFButtonEnemy.GetComponent<Image>();
        if (img == null) return;
        img.color = confusedEnemy ? ConfusionAtkDefBackgroundColor : Color.white;
    }

    public int ComputeMagicalExplosionRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings) =>
        _power.ComputeMagicalExplosionRampFrom(cards, attacker, defenderForBlessings);

    public int ComputeMagicalExplosionRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings) =>
        _power.ComputeMagicalExplosionRampTo(cards, attacker, defenderForBlessings);

    public async Task PlayMagicalExplosionAttackRampAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        CardData meCard,
        int meSheetAtkTarget,
        int fromTotal,
        int toTotal,
        float totalDurationSec,
        CancellationToken cancellationToken)
    {
        _state.UnlockMagicalExplosionPreRamp();
        _state.UnlockMillionDollarBazookaPreRamp();
        string lockLabel = _power.FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
        var rampText = GetSequenceOwnerAtkDefText();
        var rampElement = GetSequenceOwnerAtkDefElementImage();
        if (rampText == null || totalDurationSec <= 0f || fromTotal == toTotal)
        {
            _state.LockMagicalExplosionPlayerAtkDisplay(lockLabel);
            UpdateDisplay();
            return;
        }

        var meSheet = TryGetCardSheet(meCard);
        int defPow = meCard != null ? meCard.defensePower : 0;
        int hi = Mathf.Max(fromTotal, toTotal);
        ApplyRampElementStyle(rampText, rampElement, attackCards);
        await RunAtkCountUpRampAsync(rampText, fromTotal, toTotal, totalDurationSec, cancellationToken, (v, t) =>
        {
            if (meSheet != null)
            {
                int meAtkVal = v == hi ? meSheetAtkTarget : Mathf.RoundToInt(Mathf.Lerp(0f, meSheetAtkTarget, t));
                meSheet.SetAtkDefenseNumbers(meAtkVal, defPow);
            }
        });
        if (meSheet != null && meSheetAtkTarget >= 0)
            meSheet.SetAtkDefenseNumbers(meSheetAtkTarget, defPow);

        _state.LockMagicalExplosionPlayerAtkDisplay(lockLabel);
        UpdateDisplay();
    }

    public int ComputeMillionDollarBazookaRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings) =>
        _power.ComputeMillionDollarBazookaRampFrom(cards, attacker, defenderForBlessings);

    public int ComputeMillionDollarBazookaRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings) =>
        _power.ComputeMillionDollarBazookaRampTo(cards, attacker, defenderForBlessings);

    public void LockMillionDollarBazookaPlayerAttackDisplay(List<CardData> attackCards, PlayerStatus atk)
    {
        _state.UnlockMillionDollarBazookaPreRamp();
        _state.LockMillionDollarBazookaPlayerAtkDisplay(
            _power.FormatAttackPowerDisplayLabel(attackCards, atk, null, true));
    }

    public int ComputeTributeBloodRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings) =>
        _power.ComputeTributeBloodRampFrom(cards, attacker, defenderForBlessings);

    public int ComputeTributeBloodRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings) =>
        _power.ComputeTributeBloodRampTo(cards, attacker, defenderForBlessings);

    public async Task PlayTributeBloodAttackRampAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        CardData tbCard,
        int tbSheetAtkTarget,
        int hpPaid,
        int fromTotal,
        int toTotal,
        float totalDurationSec,
        CancellationToken cancellationToken)
    {
        _state.UnlockTributeBloodPreRamp();
        string lockLabel = _power.FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
        var rampText = GetSequenceOwnerAtkDefText();
        var rampElement = GetSequenceOwnerAtkDefElementImage();
        if (rampText == null || totalDurationSec <= 0f || fromTotal == toTotal)
        {
            _state.LockTributeBloodPlayerAtkDisplay(lockLabel);
            ApplyTributeBloodHpCost(atk, hpPaid);
            UpdateDisplay();
            return;
        }

        var tbSheet = TryGetCardSheet(tbCard);
        int defPow = tbCard != null ? tbCard.defensePower : 0;
        int hpReduced = 0;
        int lo = Mathf.Min(fromTotal, toTotal);
        int hi = Mathf.Max(fromTotal, toTotal);
        ApplyRampElementStyle(rampText, rampElement, attackCards);
        await RunAtkCountUpRampAsync(rampText, fromTotal, toTotal, totalDurationSec, cancellationToken, (v, t) =>
        {
            if (v > lo && hpReduced < hpPaid && atk != null)
            {
                atk.ApplyRawHpDamage(1);
                hpReduced++;
                BattleUIManager.I?.UpdateStatus(BattleManager.I?.GetPlayerStatus(), BattleManager.I?.GetEnemyStatus());
            }
            if (tbSheet != null)
            {
                int tbAtkVal = v == hi ? tbSheetAtkTarget : Mathf.RoundToInt(Mathf.Lerp(0f, tbSheetAtkTarget, t));
                tbSheet.SetAtkDefenseNumbers(tbAtkVal, defPow);
            }
        });
        if (hpReduced < hpPaid)
            ApplyTributeBloodHpCost(atk, hpPaid - hpReduced);
        if (tbSheet != null && tbSheetAtkTarget >= 0)
            tbSheet.SetAtkDefenseNumbers(tbSheetAtkTarget, defPow);

        _state.LockTributeBloodPlayerAtkDisplay(lockLabel);
        UpdateDisplay();
    }

    public int ComputeHammadnessRampFrom(List<CardData> cards, PlayerStatus attacker) =>
        _power.ComputeHammadnessRampFrom(cards, attacker);

    public int ComputeHammadnessRampTo(List<CardData> cards, PlayerStatus attacker) =>
        _power.ComputeHammadnessRampTo(cards, attacker);

    public async Task PlayHammadnessAttackRampAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        CardData hammadnessCard,
        int hammadnessSheetAtkTarget,
        int fromTotal,
        int toTotal,
        float totalDurationSec,
        CancellationToken cancellationToken)
    {
        _state.UnlockHammadnessPreRamp();
        string lockLabel = _power.FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
        var rampText = GetSequenceOwnerAtkDefText();
        var rampElement = GetSequenceOwnerAtkDefElementImage();
        if (rampText == null || totalDurationSec <= 0f || fromTotal == toTotal)
        {
            _state.LockHammadnessPlayerAtkDisplay(lockLabel);
            UpdateDisplay();
            return;
        }

        var hammadnessSheet = TryGetCardSheet(hammadnessCard);
        int defPow = hammadnessCard != null ? hammadnessCard.defensePower : 0;
        int hi = Mathf.Max(fromTotal, toTotal);
        ApplyRampElementStyle(rampText, rampElement, attackCards);
        await RunAtkCountUpRampAsync(rampText, fromTotal, toTotal, totalDurationSec, cancellationToken, (v, t) =>
        {
            if (hammadnessSheet != null)
            {
                int sheetAtkVal = v == hi ? hammadnessSheetAtkTarget : Mathf.RoundToInt(Mathf.Lerp(0f, hammadnessSheetAtkTarget, t));
                hammadnessSheet.SetAtkDefenseNumbers(sheetAtkVal, defPow);
            }
        });
        if (hammadnessSheet != null && hammadnessSheetAtkTarget >= 0)
            hammadnessSheet.SetAtkDefenseNumbers(hammadnessSheetAtkTarget, defPow);

        _state.LockHammadnessPlayerAtkDisplay(lockLabel);
        UpdateDisplay();
    }

    private static void ApplyAttackLabelTextStyle(TMP_Text tmp, Color elementTint)
    {
        if (tmp == null) return;
        tmp.richText = true;
        tmp.color = elementTint;
    }

    public int ComputeGodRageRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings) =>
        _power.ComputeGodRageRampFrom(cards, attacker, defenderForBlessings);

    public int ComputeGodRageRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings) =>
        _power.ComputeGodRageRampTo(cards, attacker, defenderForBlessings);

    public async Task PlayGodRageAttackRampAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        int from,
        int to,
        float totalDurationSec,
        CancellationToken cancellationToken)
    {
        _state.ClearMagicalExplosionPlayerAtkDisplayLockOnly();
        _state.ClearMillionDollarBazookaPlayerAtkDisplayLockOnly();
        _state.ClearHammadnessPlayerAtkDisplayLockOnly();
        _state.ClearTributeBloodPlayerAtkDisplayLockOnly();
        _state.ClearMillionDollarBazookaPlayerAtkDisplayForGodRageRamp();
        _state.UnlockMillionDollarBazookaPreRamp();
        _state.ClearMagicalSwordRampAttackDisplayLock();
        _state.ClearAttackModifierRevealSuppressions();

        var rampText = GetSequenceOwnerAtkDefText();
        if (rampText == null || totalDurationSec <= 0f)
        {
            _state.ClearGodRageAttackDisplayLock();
            UpdateDisplay();
            return;
        }

        string lockLabel = _power.FormatGodRageDoubledAttackPowerDisplayLabel(attackCards, atk, def);
        if (from == to)
        {
            _state.LockGodRagePlayerAtkDisplay(lockLabel);
            UpdateDisplay();
            return;
        }

        rampText.richText = false;
        rampText.color = new Color(0.2f, 0.85f, 0.35f);
        SoundEffectPlayer.I?.Play("Assets/SE/ロボット合体2.mp3");
        await RunAtkCountUpRampAsync(rampText, from, to, totalDurationSec, cancellationToken, null);

        _state.LockGodRagePlayerAtkDisplay(lockLabel);
        UpdateDisplay();
    }

    public int ComputeMagicalSwordDisplayRampFrom(List<CardData> attackCards, PlayerStatus atk, PlayerStatus def) =>
        _power.ComputeMagicalSwordDisplayRampFrom(attackCards, atk, def);

    public int ComputeMagicalSwordDisplayRampTo(List<CardData> attackCards, PlayerStatus atk, PlayerStatus def) =>
        _power.ComputeMagicalSwordDisplayRampTo(attackCards, atk, def);

    public async Task PlayMagicalSwordAttackRampAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        CardData msCard,
        int boost,
        float totalDurationSec,
        CancellationToken cancellationToken)
    {
        _state.ClearMagicalExplosionPlayerAtkDisplayLockOnly();
        _state.ClearMillionDollarBazookaPlayerAtkDisplayLockOnly();
        _state.ClearHammadnessPlayerAtkDisplayLockOnly();
        _state.ClearGodRageAttackDisplayLock();
        _state.ClearMagicalSwordRampAttackDisplayLock();
        _state.SetAttackDisplaySuppressMagicalSwordBonus(false);
        if (GodrageRules.IsGodrageDoublingCombo(attackCards))
            _state.SetAttackDisplaySuppressGodRageDouble(true);
        var rampText = GetSequenceOwnerAtkDefText();
        if (msCard == null || boost <= 0 || totalDurationSec <= 0f || rampText == null)
        {
            UpdateDisplay();
            return;
        }
        int fromTotal = ComputeMagicalSwordDisplayRampFrom(attackCards, atk, def);
        int toTotal = ComputeMagicalSwordDisplayRampTo(attackCards, atk, def);
        if (fromTotal == toTotal)
        {
            UpdateDisplay();
            return;
        }
        int fromSheet = msCard.attackPower;
        int toSheet = fromSheet + boost;
        var msSh = TryGetCardSheet(msCard);
        int defPow = msCard.defensePower;
        int hi = Mathf.Max(fromTotal, toTotal);
        rampText.richText = false;
        rampText.color = new Color(0.2f, 0.86f, 0.32f, 1f);
        SoundEffectPlayer.I?.Play("Assets/SE/ロボット合体2.mp3");
        await RunAtkCountUpRampAsync(rampText, fromTotal, toTotal, totalDurationSec, cancellationToken, (v, t) =>
        {
            if (msSh != null)
            {
                int sAtk = v == hi ? toSheet : Mathf.RoundToInt(Mathf.Lerp(fromSheet, toSheet, t));
                msSh.SetAtkDefenseNumbers(sAtk, defPow);
            }
        });
        if (msSh != null)
            msSh.SetAtkDefenseNumbers(toSheet, defPow);
        int finalMsDisplay = ComputeMagicalSwordDisplayRampTo(attackCards, atk, def);
        if (finalMsDisplay > 0)
            _state.LockMagicalSwordRampAttackDisplay(finalMsDisplay);
        UpdateDisplay();
    }

    private TMP_Text GetSequenceOwnerAtkDefText() =>
        _state.SequenceOwnerSide == Side.Enemy ? atkdefTextEnemy : atkdefText;

    private Image GetSequenceOwnerAtkDefElementImage() =>
        _state.SequenceOwnerSide == Side.Enemy ? atkdefElementEnemy : atkdefElement;

    private static CardSheetDisplay TryGetCardSheet(CardData card)
    {
        if (card == null || BattleUIManager.I == null) return null;
        return BattleUIManager.I.TryGetCardSheetDisplayForCardData(card, out var sh) ? sh : null;
    }

    private void ApplyRampElementStyle(TMP_Text rampText, Image rampElement, List<CardData> attackCards)
    {
        rampText.richText = false;
        var el = attackCards != null && attackCards.Count > 0
            ? ElementHelper.GetCombinedElement(attackCards)
            : ElementType.None;
        rampText.color = ElementHelper.GetElementColor(el);
        ApplyTotalAtkDefElementImage(rampElement, el);
    }

    private static void ApplyTributeBloodHpCost(PlayerStatus atk, int hpCost)
    {
        if (hpCost <= 0 || atk == null) return;
        atk.ApplyRawHpDamage(hpCost);
        BattleUIManager.I?.UpdateStatus(BattleManager.I?.GetPlayerStatus(), BattleManager.I?.GetEnemyStatus());
    }

    private static async Task RunAtkCountUpRampAsync(
        TMP_Text rampText,
        int fromTotal,
        int toTotal,
        float totalDurationSec,
        CancellationToken cancellationToken,
        System.Action<int, float> onStep)
    {
        int lo = Mathf.Min(fromTotal, toTotal);
        int hi = Mathf.Max(fromTotal, toTotal);
        int span = hi - lo;
        float stepSec = span > 0 ? totalDurationSec / span : 0f;
        float invSpan = span > 0 ? 1f / span : 0f;
        for (int v = lo; v <= hi; v++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            float t = (v - lo) * invSpan;
            onStep?.Invoke(v, t);
            rampText.text = $"ATK {v}";
            if (v < hi && stepSec > 0f)
                await Task.Delay(TimeSpan.FromSeconds(stepSec), cancellationToken);
        }
    }
}
