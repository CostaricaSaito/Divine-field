using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Player hand click, MagicPanel selection, UseButton, attack/defense confirm, and input locks.
/// </summary>
public sealed class PlayerInputController
{
    private readonly IPlayerInputHost _host;

    private bool _isProcessingUseButton;
    private bool _playerDefenseCombatResolving;

    public PlayerInputController(IPlayerInputHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public bool IsUseButtonLocked => _isProcessingUseButton;
    public bool IsPlayerDefenseCombatResolving => _playerDefenseCombatResolving;

    public bool IsProcessingUseButton
    {
        get => _isProcessingUseButton;
        set => _isProcessingUseButton = value;
    }

    public void ReleaseCardSequenceInputLocks()
    {
        _isProcessingUseButton = false;
        BattleUIManager.I?.SetHandClickable(true);
        BattleUIManager.I?.RefreshUseButton();
        BattleUIManager.I?.UpdateEconomicActionButtons();
        HandReloadController.I?.RefreshReloadEntryButton();
    }

    public void ResetDefenseUseButtonLocks()
    {
        _isProcessingUseButton = false;
        _playerDefenseCombatResolving = false;
        BattleUIManager.I?.HideYurusuButton();
        BattleUIManager.I?.UpdateEconomicActionButtons();
        HandReloadController.I?.RefreshReloadEntryButton();
    }

    public void ResetAllLocks()
    {
        _isProcessingUseButton = false;
        _playerDefenseCombatResolving = false;
        BattleUIManager.I?.UpdateEconomicActionButtons();
        HandReloadController.I?.RefreshReloadEntryButton();
    }

    public void SetSelectedCard(CardUI ui)
    {
        if (ui == null) return;
        var card = ui.GetCardData();
        if (card == null) return;

        if (HandReloadController.I != null && HandReloadController.I.IsReloadPopupContentOpen)
        {
            HandReloadController.I.OnHandCardClickedForReload(card);
            return;
        }

        if (UltimateReloadFlow.IsPopupOpen)
        {
            UltimateReloadFlow.OnHandCardClicked(card);
            return;
        }

        if (_host.IsPlayerDefenseInputActive())
        {
            if (!CardRules.IsUsableInDefensePhase(card))
            {
                Debug.LogWarning($"このカードは防御フェーズでは使えません: {card.cardName} ({card.cardType})");
                return;
            }

            _host.SelectedDefenseCard = card;
            BattleUIManager.I?.ShowCardDetail(card, Side.Player);
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            _host.UpdateTotalATKDefDisplay();
            BattleManager.I?.RefreshPlayerDefensePhaseInteractivity();
            return;
        }

        if (_host.CurrentState == GameState.AttackPhase && _host.Attacker == PlayerType.Player)
        {
            if (_host.SellFeature != null && _host.SellFeature.IsSellModeActive())
            {
                _host.SellFeature.OnCardSelected(card);
                return;
            }

            if (!CardRules.IsUsableInAttackPhase(card))
            {
                Debug.LogWarning($"このカードは攻撃フェーズでは使えません: {card.cardName} ({card.cardType})");
                return;
            }

            _host.SelectedCard = card;
            BattleUIManager.I?.ShowCardDetail(card, Side.Player);
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            _host.UpdateTotalATKDefDisplay();
            return;
        }

        if (_host.CurrentState != GameState.AttackPhase && _host.CurrentState != GameState.DefensePhase)
        {
            Debug.Log(
                $"カード選択は現在できません - State: {_host.CurrentState}, Attacker: {_host.Attacker}, " +
                $"Defender: {_host.Defender}, Card: {card?.cardName}");
        }
    }

    public void SelectMagicPoolCard(CardData card)
    {
        if (card == null) return;

        bool fromAttack = _host.CurrentState == GameState.AttackPhase && _host.Attacker == PlayerType.Player;
        bool fromDefense = _host.IsPlayerDefenseInputActive()
            && CardRules.IsUsableInDefensePhase(card);

        if (!fromAttack && !fromDefense)
        {
            Debug.Log($"[PlayerInput] MagicPanel card select blocked: State={_host.CurrentState}");
            return;
        }

        if (fromDefense)
        {
            var incoming = _host.GetIncomingAttackSnapshotForDefenseUi();
            if (BlockingRules.IsPhysicalBlockingCard(card)
                && (incoming == null || !BlockingRules.CanUsePhysicalBlockingAgainstAttack(card, incoming)))
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel(
                    "無属性の物理攻撃にのみ使えます", new Color(0.85f, 0.25f, 0.2f));
                return;
            }

            if (card.cardType == CardType.Magic && _host.PlayerStatus != null
                && !BlockingRules.CanAffordMagicDefenseMp(card, _host.PlayerStatus))
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("MPが足りない", new Color(0.95f, 0.22f, 0.2f));
                return;
            }
        }

        _host.SelectedCard = card;
        BattleUIManager.I?.ShowCardDetail(card, Side.Player);
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        _host.UpdateTotalATKDefDisplay();
        Debug.Log(
            $"[PlayerInput] MagicPanel card selected: {card.cardName} " +
            $"(remaining {MagicPoolManager.I?.GetRemainingUses(card)})");
    }

    public void OnUseButtonPressed()
    {
        if (!_host.IsAdHocDefenseWaitActive()
            && (_isProcessingUseButton || _playerDefenseCombatResolving))
            return;

        if (_host.IsPlayerDefenseInputActive())
        {
            _isProcessingUseButton = true;
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.RefreshUseButton();
            BattleUIManager.I?.UpdateEconomicActionButtons();
            HandReloadController.I?.RefreshReloadEntryButton();

            if (_host.IsAdHocDefenseWaitActive())
            {
                _host.TrySubmitAdHocPlayerDefense();
                return;
            }

            HandleDefenseUse();
            return;
        }

        _isProcessingUseButton = true;
        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.RefreshUseButton();
        BattleUIManager.I?.UpdateEconomicActionButtons();
        HandReloadController.I?.RefreshReloadEntryButton();

        if (_host.CurrentState == GameState.AttackPhase && _host.Attacker == PlayerType.Player)
        {
            if (_host.PlayerStatus != null && _host.PlayerStatus.HasFreezeEffect())
            {
                UnlockUseButton();
                return;
            }

            HandleAttackUse();
        }
        else
        {
            _isProcessingUseButton = false;
            BattleUIManager.I?.RefreshUseButton();
        }
    }

    public void HandleNoDefenseCard()
    {
        if (_playerDefenseCombatResolving)
            return;

        _ = HandleNoDefenseCardAsync();
    }

    private async Task HandleNoDefenseCardAsync()
    {
        if (_playerDefenseCombatResolving)
            return;

        _playerDefenseCombatResolving = true;
        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.SetUseButtonInteractable(false);

        var token = _host.GetPhaseToken();

        try
        {
            if (_host.CurrentAttackCard != null
                && EconomicActionNames.IsEconomicAttack(_host.CurrentAttackCard.cardName))
            {
                BattleUIManager.I?.ClearAllSelections();
                _host.UpdateTotalATKDefDisplay();
                _host.SetGameState(GameState.DefenseConfirmPhase);
                return;
            }

            BattleUIManager.I?.ClearAllSelections();
            _host.UpdateTotalATKDefDisplay();

            var atk = (_host.Attacker == PlayerType.Player) ? _host.PlayerStatus : _host.EnemyStatus;
            var def = (_host.Defender == PlayerType.Player) ? _host.PlayerStatus : _host.EnemyStatus;
            var defHand = (_host.Defender == PlayerType.Player) ? _host.PlayerHand : _host.CpuHand;

            List<CardData> attackCards = _host.GetAttackCardsForCombat();

            if (attackCards != null && attackCards.Count == 1 && attackCards[0] != null
                && CardRules.IncomingRequiresFullOnlyReactiveDefense(attackCards))
            {
                await _host.BattleProcessor.ResolveImmediateEffectAsync(attackCards[0], atk, def);
                if (token.IsCancellationRequested) return;
                ClearCombatSnapshots();
                BattleUIManager.I?.HideAllCardDetails();
                _host.ClearCardStatsSequenceAndAttackLocks();
                _host.CurrentAttackCard = null;
                _host.CardStatsDisplay?.UpdateDisplay();
                _host.SetGameState(GameState.CombatResolvePhase);
                return;
            }

            bool skipHit = _host.Attacker == PlayerType.Enemy;
            await _host.BattleProcessor.ResolveCombatAsync(
                attackCards, (CardData)null, atk, def, defHand, skipHit);

            if (token.IsCancellationRequested) return;
            if (await _host.TryHandleDeathIfAnyAsync(token)) return;

            if (await _host.TryPreparePlayerDualBladeSecondDefenseIfNeededAsync(token))
                return;

            ClearCombatSnapshots();
            BattleUIManager.I?.HideAllCardDetails();
            _host.ClearCardStatsSequenceAndAttackLocks();
            _host.CurrentAttackCard = null;
            _host.CardStatsDisplay?.UpdateDisplay();
            _host.SetGameState(GameState.CombatResolvePhase);
        }
        finally
        {
            _playerDefenseCombatResolving = false;
            _isProcessingUseButton = false;
        }
    }

    private void HandleAttackUse()
    {
        var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
        if (selectedAttackCards == null || selectedAttackCards.Count == 0)
        {
            if (_host.IsOnlineMatch && CardRules.GetAttackChoices(_host.PlayerHand).Count == 0)
            {
                Debug.Log("[PlayerInput] Online attack pass (prayer)");
                NetworkBattleBridge.SendAttackSelection(null);
                _host.SetGameState(GameState.CombatResolvePhase);
                return;
            }

            Debug.LogWarning("攻撃カードが選択されていません");
            UnlockUseButton();
            return;
        }

        foreach (var c in selectedAttackCards)
        {
            if (c != null && c.cardType == CardType.Magic && _host.PlayerStatus.IsMagicUseForbidden())
            {
                UnlockUseButton();
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("魔法が使用できません", new Color(0.95f, 0.25f, 0.2f));
                BattleUIManager.I?.SetHandClickable(true);
                _host.UpdateTotalATKDefDisplay();
                return;
            }
        }

        if (!AttackComboSelectionRules.IsValidAttackSelection(selectedAttackCards))
        {
            UnlockUseButton();
            BattleUIManager.I?.ShowInfoPopupOnCardPanel("先に攻撃カードを選んでください", new Color(0.85f, 0.35f, 0.15f));
            BattleUIManager.I?.SetHandClickable(true);
            _host.UpdateTotalATKDefDisplay();
            return;
        }

        int totalMagicMp = _host.PlayerStatus.GetTotalEffectiveMagicMpForCards(selectedAttackCards);
        if (totalMagicMp > _host.PlayerStatus.currentMP)
        {
            UnlockUseButton();
            BattleUIManager.I?.ShowInfoPopupOnCardPanel("MPが足りません", new Color(0.95f, 0.25f, 0.2f));
            BattleUIManager.I?.SetHandClickable(true);
            _host.UpdateTotalATKDefDisplay();
            return;
        }

        if (_host.IsOnlineMatch)
            NetworkBattleBridge.SendAttackSelection(selectedAttackCards, _host.IsPlayerSelfAttackTargetMode);

        if (selectedAttackCards.Count == 1 && CardRules.IsImmediateAction(selectedAttackCards[0]))
        {
            var card = selectedAttackCards[0];
            int slotIndex = (card.cardUI != null) ? card.cardUI.transform.GetSiblingIndex() : -1;

            bool magicFromMagicPanel = card.cardType == CardType.Magic
                && BattleUIManager.I != null
                && BattleUIManager.I.IsPlayerMagicCardUiOnMagicPanel(card);
            if (slotIndex >= 0 && !magicFromMagicPanel)
                _host.HandRefill?.RecordPlayerUseSlot(slotIndex);

            _ = RunImmediateAttackSingleCardAsync(card, slotIndex);
            return;
        }

        if (_host.CardSequenceManager != null)
        {
            if (_host.CardStatsDisplay != null && !ArchMagicRules.ContainsArchMagic(selectedAttackCards))
            {
                PlayerAttackTotalDisplayFlow.ResetAttackSequenceDisplayLocks(_host.CardStatsDisplay);
                _host.CardStatsDisplay.BeginAttackSequenceReveal(Side.Player);
                _host.CardStatsDisplay.SetSequenceCards(new List<CardData>(), "攻撃", Side.Player);
                _host.CardStatsDisplay.UpdateDisplay();
            }

            _ = RunPlayerAttackCardSequenceSafelyAsync(selectedAttackCards, _host.GetPhaseToken());
        }
        else
        {
            Debug.LogError("[PlayerInput] CardSequenceManager is not assigned");
        }
    }

    private void HandleDefenseUse()
    {
        if (_host.IsAdHocDefenseWaitActive())
        {
            _host.TrySubmitAdHocPlayerDefense();
            return;
        }

        if (_playerDefenseCombatResolving)
            return;

        if (_host.IsPlayerChantingArchMagicWhileDefending())
        {
            _host.TryAutoPassPlayerDefenseIfChantingArchMagic();
            return;
        }

        var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
        if (selectedDefenseCards == null || selectedDefenseCards.Count == 0)
        {
            if (_host.IsOnlineMatch)
                NetworkBattleBridge.SendDefenseSelection(null);

            Debug.Log("[PlayerInput] Accept damage without defense card (yurusu)");
            HandleNoDefenseCard();
            return;
        }

        if (_host.Defender == PlayerType.Player
            && _host.PlayerStatus != null
            && _host.PlayerStatus.HasRestraintEffect()
            && selectedDefenseCards.Count > 1)
        {
            BattleUIManager.I?.ShowInfoPopupOnCardPanel("体が重い", new Color(0.22f, 0.24f, 0.38f));
            return;
        }

        if (_host.Defender == PlayerType.Player && selectedDefenseCards.Count > 0
            && _host.CurrentAttackCard != null
            && EconomicActionNames.IsEconomicAttack(_host.CurrentAttackCard.cardName))
        {
            BattleUIManager.I?.ShowInfoPopupOnCardPanel(
                "経済アクションは「許す」のみ有効です", new Color(0.95f, 0.55f, 0.15f));
            UnlockUseButton();
            return;
        }

        if (_host.Defender == PlayerType.Player && selectedDefenseCards.Count > 0)
        {
            var incoming = _host.GetIncomingAttackSnapshotForDefenseUi();
            foreach (var defCard in selectedDefenseCards)
            {
                if (defCard == null) continue;
                if (defCard.cardType == CardType.Magic && _host.PlayerStatus != null
                    && !BlockingRules.CanAffordMagicDefenseMp(defCard, _host.PlayerStatus))
                {
                    BattleUIManager.I?.ShowInfoPopupOnCardPanel("MPが足りない", new Color(0.95f, 0.22f, 0.2f));
                    return;
                }

                if (BlockingRules.IsPhysicalBlockingCard(defCard)
                    && (incoming == null
                        || !BlockingRules.CanUsePhysicalBlockingAgainstAttack(defCard, incoming)))
                {
                    BattleUIManager.I?.ShowInfoPopupOnCardPanel(
                        "無属性の物理攻撃にのみ使えます", new Color(0.85f, 0.25f, 0.2f));
                    return;
                }
            }
        }

        if (_host.IsOnlineMatch)
            NetworkBattleBridge.SendDefenseSelection(selectedDefenseCards);

        if (_host.CardSequenceManager != null)
        {
            _playerDefenseCombatResolving = true;
            _ = RunPlayerDefenseCardSequenceAsync(selectedDefenseCards);
        }
        else
        {
            Debug.LogError("[PlayerInput] CardSequenceManager is not assigned");
        }
    }

    private async Task RunImmediateAttackSingleCardAsync(CardData card, int slotIndex)
    {
        PlayerStatus immediateEffectTarget = ComputeImmediateEffectTargetForPlayerAttack(card);

        bool isMagic = card != null && card.cardType == CardType.Magic;
        bool useMagicPanel = isMagic && MagicPoolManager.I != null;
        bool fromMagicPanel =
            useMagicPanel && BattleUIManager.I != null && BattleUIManager.I.IsPlayerMagicCardUiOnMagicPanel(card);

        var tok = _host.GetPhaseToken();

        try
        {
            try
            {
                await _host.PlayAttackConfirmPresentationAsync(card, Side.Player, tok);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (useMagicPanel)
            {
                if (!fromMagicPanel && MagicPoolManager.I != null
                    && !MagicPoolManager.I.CanAddToPool(card, PlayerType.Player))
                {
                    BattleUIManager.I?.ShowInfoPopupOnCardPanel(
                        "マジックパネルに空きがありません", new Color(0.95f, 0.25f, 0.2f));
                    return;
                }

                if (_host.PlayerStatus != null && card.mpCost > 0)
                {
                    int pay = _host.PlayerStatus.GetEffectiveMagicMpCost(card.mpCost);
                    _host.PlayerStatus.UseMP(pay);
                    _host.UpdateBattleStatusUi();
                }

                if (fromMagicPanel)
                {
                    MagicPoolManager.I.ConsumeUse(card);
                    var drawn = await _host.DrawOneCardAsync(trailingDelayMs: 0, playSoundOnDraw: false);
                    if (drawn != null && _host.HandRefill != null)
                        await _host.HandRefill.RevealDrawnCardAfterCombatAsync(drawn, tok);
                }
                else
                {
                    RectTransform handRt = null;
                    if (card.cardUI != null)
                    {
                        handRt = card.cardUI.cardImage != null
                            ? card.cardUI.cardImage.rectTransform
                            : card.cardUI.transform as RectTransform;
                    }

                    if (handRt != null && BattleUIManager.I != null && card.cardImage != null)
                    {
                        int slot = MagicPoolManager.I.GetPredictedPlayerSlotIndex(card);
                        await BattleUIManager.I.PlayMagicFlyHandToPanelAsync(card, handRt, slot);
                    }

                    _host.BattleProcessor.UseCard(card, _host.PlayerHand);

                    System.Action drawCb = () => _host.DrawOneCard();
                    MagicPoolManager.I.TryUseMagicCard(card, _host.PlayerHand, _host.GetHandMaxCount(), drawCb);
                }
            }
            else
            {
                _host.BattleProcessor.UseCard(card, _host.PlayerHand);
            }

            _host.CurrentAttackCard = card;
            _host.SelectedCard = null;
            _host.UpdateBattleStatusUi();
            _host.UpdateTotalATKDefDisplay();
            _host.ClearPlayerSelfAttackTargetMode();

            bool skipDefenseForImmediate =
                card.specialCardEffect is DisasterTriggerEffectSO
                || immediateEffectTarget == _host.PlayerStatus;

            if (skipDefenseForImmediate)
            {
                await ResolveImmediateEffectAsync(
                    card,
                    slotIndex,
                    immediateEffectTarget == _host.PlayerStatus ? _host.PlayerStatus : null);
                return;
            }

            _host.SetGameState(GameState.DefensePhase);
        }
        finally
        {
            if (_host.IsAdHocDefenseWaitActive())
            {
                _isProcessingUseButton = false;
                BattleUIManager.I?.RefreshUseButton();
            }
            else
            {
                UnlockUseButton();
            }
        }
    }

    private async Task ResolveImmediateEffectAsync(
        CardData card, int slotIndex, PlayerStatus presetEffectTarget = null)
    {
        await Task.Delay(DamagePopup.PreImmediateEffectDelayMs, _host.GetPhaseToken());
        Debug.Log("[PlayerInput] Pre-immediate-effect interval complete");

        PlayerStatus effectTarget = presetEffectTarget;
        if (effectTarget == null)
        {
            if (_host.PlayerStatus != null && _host.PlayerStatus.HasConfusionEffect())
            {
                _host.ClearPlayerSelfAttackTargetMode();
                effectTarget = BattleRandom.Range(0, 2) == 0 ? _host.PlayerStatus : _host.EnemyStatus;
            }
            else
            {
                effectTarget = ComputeImmediateEffectTargetForPlayerAttack(card);
            }
        }
        else if (_host.PlayerStatus != null && _host.PlayerStatus.HasConfusionEffect())
        {
            _host.ClearPlayerSelfAttackTargetMode();
        }

        // Long disaster sequence manages hand input itself; release Use lock before orchestrator runs.
        if (card.specialCardEffect is DisasterTriggerEffectSO)
        {
            _isProcessingUseButton = false;
            BattleUIManager.I?.RefreshUseButton();
        }

        var phaseToken = _host.GetPhaseToken();
        await _host.BattleProcessor.ResolveImmediateEffectAsync(
            card, _host.PlayerStatus, effectTarget, phaseToken);

        _host.ClearPlayerSelfAttackTargetMode();
        _host.SelectedCard = null;
        _host.UpdateBattleStatusUi();
        _host.UpdateTotalATKDefDisplay();

        if (_host.CardSequenceManager != null)
            await _host.RunAfterCombatSharedCleanupAsync(_host.GetPhaseToken());
        else
        {
            BattleUIManager.I?.HideAllCardDetails();
            _host.CardStatsDisplay?.ClearSequenceCards();
            _host.CurrentAttackCard = null;
            _host.CardStatsDisplay?.UpdateDisplay();
            _host.SetGameState(GameState.CombatResolvePhase);
        }
    }

    private async Task RunPlayerAttackCardSequenceSafelyAsync(
        List<CardData> selectedAttackCards, CancellationToken cancellationToken)
    {
        if (_host.CardSequenceManager == null) return;

        try
        {
            await _host.CardSequenceManager.StartCardSequenceAsync(
                selectedAttackCards, "攻撃", Side.Player, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[PlayerInput] Player attack card sequence cancelled");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ReleaseCardSequenceInputLocks();
        }
    }

    private async Task RunPlayerDefenseCardSequenceAsync(List<CardData> selectedDefenseCards)
    {
        try
        {
            if (_host.CardSequenceManager != null)
                await _host.CardSequenceManager.StartCardSequenceAsync(
                    selectedDefenseCards, "防御", Side.Player, _host.GetPhaseToken());
        }
        finally
        {
            _playerDefenseCombatResolving = false;
        }
    }

    private PlayerStatus ComputeImmediateEffectTargetForPlayerAttack(CardData card)
    {
        if (_host.PlayerStatus != null && _host.PlayerStatus.HasConfusionEffect())
            return BattleRandom.Range(0, 2) == 0 ? _host.PlayerStatus : _host.EnemyStatus;
        bool recoverOrFountain = card != null
            && (CardRules.IsRecoveryCard(card) || MagicFountainRules.IsMagicFountainCard(card));
        if (recoverOrFountain)
            return _host.IsPlayerSelfAttackTargetMode ? _host.EnemyStatus : _host.PlayerStatus;
        return _host.IsPlayerSelfAttackTargetMode ? _host.PlayerStatus : _host.EnemyStatus;
    }

    private void UnlockUseButton()
    {
        _isProcessingUseButton = false;
        BattleUIManager.I?.SetHandClickable(true);
        BattleUIManager.I?.RefreshUseButton();
        BattleUIManager.I?.UpdateEconomicActionButtons();
        HandReloadController.I?.RefreshReloadEntryButton();
    }

    private void ClearCombatSnapshots()
    {
        _host.ClearMagicalExplosionComboMpPoolSnapshot();
        _host.ClearMillionDollarBazookaComboGpPoolSnapshot();
        _host.ClearTributeBloodHpPaidSnapshot();
        _host.ClearHammadnessRollSnapshot();
    }
}
