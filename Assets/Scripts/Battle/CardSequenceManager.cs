using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// カードシーケンス管理を担当するクラス
/// BattleManagerからカード演出・処理関連の機能を移設
/// 
/// 【役割】
/// - カード使用時の演出フロー管理
/// - カードの順次表示
/// - カードの処理（裏返しなど）
/// - 戦闘解決への準備
/// 
/// 【責任範囲】
/// - カード演出シーケンスの実行
/// - カード処理（単一・複数）
/// - 戦闘用攻撃カードの取得
/// </summary>
public class CardSequenceManager : MonoBehaviour
{
    // BattleManagerへの参照
    private BattleManager battleManager;
    private BattleProcessor battleProcessor;
    private HandRefillService handRefill;
    private CardStatsDisplay cardStatsDisplay;

    /// <summary>MagicPanel 使用で裏面追加したカード（ダメージ後に表向け）</summary>
    private readonly List<CardData> _magicPanelBonusDrawsPendingReveal = new();

    /// <summary>マジカルエクスプロージョン演出内で魔法の MP 消費・プール処理を済ませたため <see cref="ProcessMultipleCardsAsync"/> で魔法ループをスキップする。</summary>
    private bool _skipMagicProcessingInProcessCardsBecauseMagicalExplosion;

    /// <summary>大魔法「詠唱開始」演出中。カウントダウン演出と競合させない。</summary>
    public bool IsArchMagicCastIntroInProgress { get; private set; }

    /// <summary>大魔法カウントダウン演出中。EnterAttackPhase からの二重起動を防ぐ。</summary>
    public bool IsArchMagicCountdownInProgress { get; private set; }

    /// <summary>
    /// 初期化
    /// </summary>
    public void Initialize(BattleManager battleManager, BattleProcessor battleProcessor, 
                          HandRefillService handRefill, CardStatsDisplay cardStatsDisplay)
    {
        this.battleManager = battleManager;
        this.battleProcessor = battleProcessor;
        this.handRefill = handRefill;
        this.cardStatsDisplay = cardStatsDisplay;
    }

    /// <summary>
    /// カード演出シーケンスを開始（攻撃・防御共通）
    /// ①表示ゾーンクリア → ②カード順次表示（0.5秒インターバル） → ③カード処理 → ④戦闘解決
    /// </summary>
    public async Task StartCardSequenceAsync(List<CardData> selectedCards, string cardType, Side side, 
                                            CancellationToken cancellationToken)
    {
        Debug.Log($"[CardSequenceManager] {cardType}カード演出開始: {selectedCards.Count}枚");
        if (cardType == "攻撃" && side == Side.Player)
            PlayerAttackTotalDisplayFlow.ResetAttackSequenceDisplayLocks(cardStatsDisplay);

        // 大魔法（ArchMagic）は通常の攻撃シーケンスではなく、詠唱開始フローへ分岐する。
        if (cardType == "攻撃" && ArchMagicRules.ContainsArchMagic(selectedCards))
        {
            var archCard = ArchMagicRules.FindArchMagic(selectedCards);
            if (archCard != null)
            {
                await StartArchMagicCastIntroAsync(archCard, side, cancellationToken);
                return;
            }
        }

        if (cardType == "攻撃")
        {
            cardStatsDisplay?.BeginAttackSequenceReveal(side);
            cardStatsDisplay?.SetSequenceCards(new List<CardData>(), cardType, side);
            cardStatsDisplay?.UpdateDisplay();
        }

        if (cardType == "攻撃" && side == Side.Player)
        {
            battleManager.ClearMagicalSwordPlayerAttackState();
            if (MagicalSwordRules.ContainsMagicalSword(selectedCards)
                && MagicalSwordRules.TryGetFirstMagicalSwordRule(selectedCards, out var msRule))
            {
                var p = battleManager.GetPlayerStatus();
                if (p != null
                    && MagicalSwordRules.CanAffordOptionalMagicalSwordAfterOtherComboMagic(
                        selectedCards, p, msRule.optionalMpCost))
                {
                    var ch = await MPCostPopupUI.ShowAndWaitAsync(cancellationToken);
                    if (ch == MPCostPopupUI.Choice.PayMpForBoost)
                    {
                        p.UseMP(msRule.optionalMpCost);
                        battleManager.SetMagicalSwordAttackPowerBonus(msRule.attackPowerBonus);
                        BattleUIManager.I?.UpdateStatus(
                            battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
                    }
                    else
                        battleManager.SetMagicalSwordAttackPowerBonus(0);
                }
                else
                    battleManager.SetMagicalSwordAttackPowerBonus(0);

                // オンライン：支払い選択を相手へ通知（辞退・支払い不可でも必ず送る。相手側はこれを待っている）
                if (battleManager.IsOnlineMatch)
                {
                    bool msPaid = battleManager.MagicalSwordAttackPowerBonus > 0;
                    NetworkBattleBridge.SendMagicalSwordChoice(
                        msPaid,
                        battleManager.MagicalSwordAttackPowerBonus,
                        msPaid ? msRule.optionalMpCost : 0);
                }
            }
        }

        if (cardType == "攻撃" && side == Side.Player)
        {
            if (TributeBloodRules.ContainsTributeBlood(selectedCards)
                && TributeBloodRules.TryGetFirstTributeBloodRule(selectedCards, out var tbRule))
            {
                var pTb = battleManager.GetPlayerStatus();
                if (pTb != null)
                {
                    int hpPaid = await TributeBloodPopupUI.ShowAndWaitAsync(pTb, tbRule, cancellationToken);
                    battleManager.SetTributeBloodPlayerHpPaidSnapshot(hpPaid);
                    BattleUIManager.I?.UpdateStatus(
                        battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
                    if (battleManager.IsOnlineMatch)
                        NetworkBattleBridge.SendTributeBloodChoice(hpPaid);
                }
                else
                    battleManager.SetTributeBloodPlayerHpPaidSnapshot(0);
            }
        }

        if (cardType == "攻撃")
            cardStatsDisplay?.ConfigureAttackSequenceRevealSuppressions(selectedCards);

        bool spellbookElementRevealPending = cardType == "攻撃"
            && SpellbookRules.NeedsElementRevealSequence(selectedCards);

        // 防御シーケンスでは消さない。相手攻撃の ME / 気狂いハンマー等スナップショットは
        // 反射・パリィ・通常防御のダメージ計算まで必要。
        if (cardType == "攻撃")
        {
            battleManager.ClearMagicalExplosionComboMpPoolSnapshot();
            battleManager.ClearMillionDollarBazookaComboGpPoolSnapshot();
            battleManager.ClearHammadnessRollSnapshot();
            battleManager.ClearConfusionAttackTargetResolvedForDisplay();
        }

        _magicPanelBonusDrawsPendingReveal.Clear();

        // 演出中のカードリストを初期化（BeginAttackSequenceReveal は冒頭で済）
        cardStatsDisplay?.SetSequenceCards(new List<CardData>(), cardType, side);

        // ①表示ゾーンをクリア
        BattleUIManager.I?.ClearAllSelections();
        if (cardType == "防御")
        {
            BattleUIManager.I?.HidePlayerCardDetails();
        }
        else
        {
            BattleUIManager.I?.HideAllCardDetails();
        }

        if (cardType == "攻撃")
            cardStatsDisplay?.UpdateDisplay();

        // クリア後のインターバル（まっさらな状態を維持）
        await Task.Delay(300, cancellationToken);

        if (cardType == "攻撃" && side == Side.Player)
        {
            PlayerAttackTotalDisplayFlow.EnterSequentialCardReveal_SuppressPendingModifierRamps(
                cardStatsDisplay,
                selectedCards,
                battleManager.MagicalSwordAttackPowerBonus);
        }

        // ②カードを順次表示（0.5秒インターバル）
        for (int i = 0; i < selectedCards.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (cardType == "攻撃")
                    cardStatsDisplay?.ClearAttackSequenceRevealSuppressions();
                if (cardType == "攻撃" && side == Side.Player)
                    PlayerAttackTotalDisplayFlow.ResetAttackSequenceDisplayLocks(cardStatsDisplay);
                return;
            }

            var card = selectedCards[i];
            BattleUIManager.I?.ShowCardDetail(card, side);
            
            var sequenceCards = new List<CardData>(selectedCards.GetRange(0, i + 1));
            cardStatsDisplay?.SetSequenceCards(sequenceCards, cardType, side);
            cardStatsDisplay?.UpdateDisplay();
            
            // カード表示効果音を再生（Addressables使用）
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            
            Debug.Log($"[CardSequenceManager] {cardType}カード表示: {card.cardName} ({i + 1}/{selectedCards.Count})");
            
            // すべてのカード表示後に0.5秒待機（最後のカードも選択枠を表示）
            await Task.Delay(500, cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            if (cardType == "攻撃")
                cardStatsDisplay?.ClearAttackSequenceRevealSuppressions();
            if (cardType == "攻撃" && side == Side.Player)
                PlayerAttackTotalDisplayFlow.ResetAttackSequenceDisplayLocks(cardStatsDisplay);
            return;
        }

        if (cardType == "攻撃" && side == Side.Player
            && MagicalExplosionRules.ContainsMagicalExplosion(selectedCards)
            && MagicalSwordRules.ContainsMagicalSword(selectedCards)
            && battleManager.MagicalSwordAttackPowerBonus > 0
            && cardStatsDisplay != null
            && MagicalSwordRules.TryGetFirstMagicalSwordRule(selectedCards, out var msRForPreMeRamp))
        {
            var psPre = battleManager.GetPlayerStatus();
            var esPre = battleManager.GetEnemyStatus();
            PlayerStatus atkPre = battleManager.AttackerPublic == PlayerType.Player ? psPre : esPre;
            if (psPre != null && esPre != null && ReferenceEquals(atkPre, psPre))
            {
                PlayerStatus defPre;
                if (battleManager.IsPlayerSelfAttackTargetMode && ReferenceEquals(atkPre, psPre))
                    defPre = psPre;
                else
                    defPre = ReferenceEquals(atkPre, psPre) ? esPre : psPre;
                var msCardPre = MagicalSwordRules.FindFirstMagicalSwordCard(selectedCards);
                if (msCardPre != null)
                {
                    await cardStatsDisplay.PlayMagicalSwordAttackRampAsync(
                        selectedCards,
                        psPre,
                        defPre,
                        msCardPre,
                        msRForPreMeRamp.attackPowerBonus,
                        0.2f,
                        cancellationToken);
                    battleManager.SetMagicalSwordPlayerPreMeRampVisualDone(true);
                }
            }
        }

        if (cardType == "攻撃" && MagicalExplosionRules.ContainsMagicalExplosion(selectedCards))
        {
            PlayerStatus ps = battleManager.GetPlayerStatus();
            PlayerStatus es = battleManager.GetEnemyStatus();
            PlayerStatus atkOwner = battleManager.AttackerPublic == PlayerType.Player ? ps : es;
            if (atkOwner != null)
            {
                PlayerStatus defForBless;
                if (battleManager.IsPlayerSelfAttackTargetMode && ReferenceEquals(atkOwner, ps))
                    defForBless = ps;
                else
                    defForBless = ReferenceEquals(atkOwner, ps) ? es : ps;

                await RunMagicalExplosionAttackIntroAsync(selectedCards, atkOwner, defForBless, cancellationToken);
            }
            else
            {
                cardStatsDisplay?.SetSuppressMagicalExplosionPredictionDuringSequenceReveal(false);
            }
        }

        if (cardType == "攻撃" && MillionDollarBazookaRules.ContainsMillionDollarBazooka(selectedCards))
        {
            PlayerStatus psMdb = battleManager.GetPlayerStatus();
            PlayerStatus esMdb = battleManager.GetEnemyStatus();
            PlayerStatus atkOwnerMdb = battleManager.AttackerPublic == PlayerType.Player ? psMdb : esMdb;
            if (atkOwnerMdb != null)
            {
                PlayerStatus defForBlessMdb;
                if (battleManager.IsPlayerSelfAttackTargetMode && ReferenceEquals(atkOwnerMdb, psMdb))
                    defForBlessMdb = psMdb;
                else
                    defForBlessMdb = ReferenceEquals(atkOwnerMdb, psMdb) ? esMdb : psMdb;

                await RunMillionDollarBazookaAttackIntroAsync(selectedCards, atkOwnerMdb, defForBlessMdb, cancellationToken);
            }
            else
            {
                cardStatsDisplay?.SetSuppressMillionDollarBazookaPredictionDuringSequenceReveal(false);
            }
        }

        if (cardType == "攻撃" && TributeBloodRules.ContainsTributeBlood(selectedCards))
        {
            PlayerStatus psTb = battleManager.GetPlayerStatus();
            PlayerStatus esTb = battleManager.GetEnemyStatus();
            PlayerStatus atkOwnerTb = battleManager.AttackerPublic == PlayerType.Player ? psTb : esTb;
            if (atkOwnerTb != null)
            {
                PlayerStatus defForBlessTb;
                if (battleManager.IsPlayerSelfAttackTargetMode && ReferenceEquals(atkOwnerTb, psTb))
                    defForBlessTb = psTb;
                else
                    defForBlessTb = ReferenceEquals(atkOwnerTb, psTb) ? esTb : psTb;

                await RunTributeBloodAttackIntroAsync(selectedCards, atkOwnerTb, defForBlessTb, cancellationToken);
            }
            else
            {
                cardStatsDisplay?.SetSuppressTributeBloodPredictionDuringSequenceReveal(false);
            }
        }

        if (cardType == "攻撃" && HammadnessRules.ContainsHammadness(selectedCards))
        {
            PlayerStatus psHam = battleManager.GetPlayerStatus();
            PlayerStatus esHam = battleManager.GetEnemyStatus();
            PlayerStatus atkOwnerHam = battleManager.AttackerPublic == PlayerType.Player ? psHam : esHam;
            if (atkOwnerHam != null)
            {
                await RunHammadnessAttackIntroAsync(selectedCards, atkOwnerHam, cancellationToken);
            }
            else
            {
                cardStatsDisplay?.SetSuppressHammadnessPredictionDuringSequenceReveal(false);
            }
        }

        if (cardType == "攻撃" && side == Side.Player && selectedCards != null && selectedCards.Count > 0)
        {
            PlayerStatus psRamp = battleManager.GetPlayerStatus();
            PlayerStatus esRamp = battleManager.GetEnemyStatus();
            if (psRamp != null && esRamp != null
                && battleManager.AttackerPublic == PlayerType.Player)
            {
                PlayerStatus defRamp;
                if (battleManager.IsPlayerSelfAttackTargetMode)
                    defRamp = psRamp;
                else
                    defRamp = esRamp;
                await PlayAttackModifierRampsAsync(
                    selectedCards, psRamp, defRamp, cancellationToken);
            }
            else
            {
                cardStatsDisplay?.ClearAttackModifierRevealSuppressions();
                cardStatsDisplay?.UpdateDisplay();
            }
        }

        if (cardType == "攻撃" && spellbookElementRevealPending
            && SpellbookRules.TryGetForcedComboElement(selectedCards, out var spellbookFlashElement))
        {
            try
            {
                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                cardStatsDisplay?.ClearAttackSequenceRevealSuppressions();
                if (side == Side.Player)
                    PlayerAttackTotalDisplayFlow.ResetAttackSequenceDisplayLocks(cardStatsDisplay);
                return;
            }

            const float spellbookColorFlashMs = 50f;
            Color spellbookFlashColor = ElementHelper.GetElementColor(spellbookFlashElement);
            SoundEffectPlayer.I?.Play("Assets/SE/power19.wav");
            BattleUIManager.I?.PlayFullscreenColorFlashMs(spellbookFlashColor, spellbookColorFlashMs);
            try
            {
                await Task.Delay((int)spellbookColorFlashMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                cardStatsDisplay?.ClearAttackSequenceRevealSuppressions();
                if (side == Side.Player)
                    PlayerAttackTotalDisplayFlow.ResetAttackSequenceDisplayLocks(cardStatsDisplay);
                return;
            }

            cardStatsDisplay?.SetSuppressSpellbookElementDuringSequenceReveal(false);
            cardStatsDisplay?.UpdateDisplay();
        }

        // ③カードの処理
        if (cardType == "攻撃" && side == Side.Player && selectedCards != null && selectedCards.Count > 0)
        {
            battleManager.SetPlayerAttackComboForCombat(selectedCards);
            cardStatsDisplay?.EndAttackSequenceReveal();
        }

        await ProcessCardsAsync(selectedCards, cardType);

        // 選択状態をクリア（ProcessCardsで既に設定済み）
        BattleUIManager.I?.ClearAllSelections();
        cardStatsDisplay?.UpdateDisplay();

        // PostDeath 道連れ：防御掲出のみ（戦闘解決は DeadlyChainFlow が担当）
        if (cardType == "防御" && battleManager.IsPostDeathSequenceActive)
        {
            await BattleUIManager.I?.ShowPlayerDefenseCardsPresentationSequenceAsync(selectedCards);
            return;
        }

        // ④戦闘解決処理
        PlayerStatus atk;
        PlayerStatus def;
        List<CardData> defHand;

        PlayerStatus player = battleManager.GetPlayerStatus();
        PlayerStatus enemy = battleManager.GetEnemyStatus();
        atk = battleManager.AttackerPublic == PlayerType.Player ? player : enemy;

        if (cardType == "攻撃" && atk != null && atk.HasConfusionEffect())
        {
            battleManager.ClearPlayerSelfAttackTargetMode();
            bool targetSelf = BattleRandom.Range(0, 2) == 0;
            battleManager.SetConfusionAttackTargetResolvedForDisplay(targetSelf);
            if (targetSelf)
            {
                def = atk;
                defHand = ReferenceEquals(atk, player) ? battleManager.playerHand : battleManager.cpuHand;
            }
            else
            {
                def = ReferenceEquals(atk, player) ? enemy : player;
                defHand = ReferenceEquals(def, player) ? battleManager.playerHand : battleManager.cpuHand;
            }
        }
        else if (cardType == "攻撃"
            && battleManager.AttackerPublic == PlayerType.Player
            && battleManager.IsPlayerSelfAttackTargetMode)
        {
            atk = player;
            def = player;
            defHand = battleManager.playerHand;
        }
        else
        {
            atk = battleManager.AttackerPublic == PlayerType.Player ? player : enemy;
            def = battleManager.DefenderPublic == PlayerType.Player ? player : enemy;
            defHand = battleManager.DefenderPublic == PlayerType.Player ? battleManager.playerHand : battleManager.cpuHand;
        }

        if (cardType == "攻撃" && atk != null && atk.HasConfusionEffect())
        {
            cardStatsDisplay?.UpdateDisplay();
            battleManager.UpdateTotalATKDEFDisplay();
            await Task.Delay(500, cancellationToken);
            if (ReferenceEquals(atk, def) && atk == player)
            {
                SoundEffectPlayer.I?.Play("Assets/SE/ヒヨコが頭の上を回る.mp3");
                DamagePopup confusionPopup = BattleUIManager.I != null
                    ? BattleUIManager.I.ShowInfoPopupOnCardPanel("わけがわからない！", new Color(0.95f, 0.85f, 0.35f))
                    : null;
                float popupLifetimeSec = confusionPopup != null
                    ? confusionPopup.fadeDuration
                    : DamagePopup.DefaultFadeDurationIfUnknown;
                await DamagePopup.WaitAfterPopupLifetimeAsync(popupLifetimeSec, cancellationToken);
            }
        }

        List<CardData> attackCards = cardType == "防御"
            ? GetAttackCardsForCombat(null)
            : GetAttackCardsForCombat(selectedCards);

        if (cardType == "攻撃")
        {
            bool finished = await ResolvePlayerAttackCombatAsync(attackCards, atk, def, defHand, cancellationToken);
            battleManager.ClearPlayerSelfAttackTargetMode();
            if (!finished || battleManager.IsGameEndTriggered)
                return;
        }
        else
        {
            if (CardRules.IncomingRequiresFullOnlyReactiveDefense(attackCards)
                && attackCards != null && attackCards.Count > 0)
            {
                bool playerReflect = selectedCards.Count == 1
                    && battleManager.DefenderPublic == PlayerType.Player
                    && ReflectionRules.CanReflectIncoming(selectedCards[0], attackCards);

                if (playerReflect)
                {
                    if (ReflectionRules.ShouldUseImmediateEffectReflectionFlow(attackCards))
                    {
                        await ImmediateEffectReflectionFlow.RunPlayerInitiatedAsync(
                            battleManager,
                            battleProcessor,
                            handRefill,
                            attackCards,
                            selectedCards[0],
                            atk,
                            def,
                            cancellationToken,
                            reflectionCardAlreadyConsumed: true);
                    }
                    else
                    {
                        await PhysicalReflectionFlow.RunPlayerInitiatedAsync(
                            battleManager,
                            battleProcessor,
                            handRefill,
                            battleManager.GetEnemyAI(),
                            attackCards,
                            selectedCards[0],
                            cancellationToken);
                    }
                }
                else if (attackCards.Count == 1 && attackCards[0] != null)
                {
                    await battleProcessor.ResolveImmediateEffectAsync(attackCards[0], atk, def);
                }

                await RunAfterCombatSharedCleanupAsync(cancellationToken);
                return;
            }

            bool playerPhysicalReflect = selectedCards.Count == 1
                && battleManager.DefenderPublic == PlayerType.Player
                && ReflectionRules.CanReflectIncoming(selectedCards[0], attackCards)
                && !ReflectionRules.ShouldUseImmediateEffectReflectionFlow(attackCards);
            bool playerMagicReflect = playerPhysicalReflect;
            bool playerImmediateReflect = selectedCards.Count == 1
                && battleManager.DefenderPublic == PlayerType.Player
                && ReflectionRules.CanReflectIncoming(selectedCards[0], attackCards)
                && ReflectionRules.ShouldUseImmediateEffectReflectionFlow(attackCards);
            bool playerPhysicalBlock = selectedCards.Count == 1
                && battleManager.DefenderPublic == PlayerType.Player
                && BlockingRules.CanUsePhysicalBlockingAgainstAttack(selectedCards[0], attackCards);
            bool playerParry = selectedCards.Count == 1
                && battleManager.DefenderPublic == PlayerType.Player
                && ParryRules.RequiresParryExclusiveLock(selectedCards[0], attackCards);

            if (playerParry)
            {
                bool skipSharedTail = await ParryFlow.RunPlayerInitiatedAsync(
                    battleManager,
                    battleProcessor,
                    handRefill,
                    battleManager.GetEnemyAI(),
                    attackCards,
                    selectedCards[0],
                    this,
                    cancellationToken);
                if (skipSharedTail)
                    return;
            }
            else if (playerImmediateReflect)
            {
                await ImmediateEffectReflectionFlow.RunPlayerInitiatedAsync(
                    battleManager,
                    battleProcessor,
                    handRefill,
                    attackCards,
                    selectedCards[0],
                    atk,
                    def,
                    cancellationToken,
                    reflectionCardAlreadyConsumed: true);
            }
            else if (playerPhysicalReflect || playerMagicReflect)
            {
                await PhysicalReflectionFlow.RunPlayerInitiatedAsync(
                    battleManager,
                    battleProcessor,
                    handRefill,
                    battleManager.GetEnemyAI(),
                    attackCards,
                    selectedCards[0],
                    cancellationToken);
            }
            else if (playerPhysicalBlock)
            {
                await BlockingNullifyFlow.RunPlayerInitiatedAsync(
                    battleManager,
                    attackCards,
                    selectedCards[0],
                    cancellationToken);
            }
            else
            {
                // 防御カードの場合、複数防御カード対応（敵の攻撃は既に命中判定済み）
                bool skipHit = battleManager.AttackerPublic == PlayerType.Enemy;
                await battleProcessor.ResolveCombatAsync(attackCards, selectedCards, atk, def, defHand, skipHit);
            }
        }

        if (cancellationToken.IsCancellationRequested) return;

        await RunAfterCombatSharedCleanupAsync(cancellationToken);
    }

    /// <summary>戦闘シーケンス終了時の共有後処理（MagicPanel 表向け・UI クリア・CombatResolve 遷移）。</summary>
    public async Task RunAfterCombatSharedCleanupAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return;
        if (battleManager.IsGameEndTriggered) return;

        if (await battleManager.TryPreparePlayerDualBladeSecondDefenseIfNeededAsync(cancellationToken))
            return;

        await RevealMagicPanelBonusDrawsAsync(cancellationToken);

        if (cancellationToken.IsCancellationRequested) return;

        BattleUIManager.I?.HideAllCardDetails();
        cardStatsDisplay?.ClearSequenceCardsAndAttackDisplayLocks();
        battleManager.SetCurrentAttackCard(null);
        cardStatsDisplay?.UpdateDisplay();

        battleManager.SetGameState(GameState.CombatResolvePhase);
        battleManager.ClearMagicalExplosionComboMpPoolSnapshot();
        battleManager.ClearMillionDollarBazookaComboGpPoolSnapshot();
        battleManager.ClearTributeBloodHpPaidSnapshot();
        battleManager.ClearHammadnessRollSnapshot();
        battleManager.ClearMagicalSwordPlayerAttackState();
        battleManager.ClearMagicalSwordEnemyAttackState();
        battleManager.ClearPlayerAttackComboForCombat();
    }

    /// <summary>
    /// 双剣1本目解決後の手順4〜5：CardDisplay 系をクリアし、使用した攻撃カード（デュアリズム含むコンボ）を一度に載せ直す。
    /// </summary>
    private async Task PresentDualBladeSecondStrikeAttackRevealAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        CancellationToken cancellationToken)
    {
        if (attackCards == null || attackCards.Count == 0) return;
        if (cancellationToken.IsCancellationRequested) return;

        // 4. 一度クリア
        BattleUIManager.I?.ClearAllCardDisplaysAndSelectionImmediate();
        cardStatsDisplay?.ClearSequenceCards();
        cardStatsDisplay?.UpdateDisplay();

        await Task.Delay(300, cancellationToken);
        if (cancellationToken.IsCancellationRequested) return;

        bool attackerIsPlayer = ReferenceEquals(atk, battleManager.GetPlayerStatus());
        Side displaySide = attackerIsPlayer ? Side.Player : Side.Enemy;

        var full = battleManager.GetAttackCardsForCombatPublic();
        if (full == null || full.Count == 0)
        {
            full = new List<CardData>(attackCards.Count);
            for (int i = 0; i < attackCards.Count; i++)
            {
                if (attackCards[i] != null) full.Add(attackCards[i]);
            }
        }
        if (full.Count == 0) return;
        BattleUIManager.I?.ShowCardSheetsVisualOnlyBatch(full, displaySide);
        cardStatsDisplay?.SetSequenceCards(full, "攻撃", displaySide);
        cardStatsDisplay?.UpdateDisplay();
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");

        if (!cancellationToken.IsCancellationRequested)
            await Task.Delay(500, cancellationToken);
    }

    /// <summary>
    /// プレイヤー攻撃：命中→（的中演出）→敵防御→戦闘。ミス時は TurnEnd まで。
    /// 介入など、通常の攻撃シーケンス外からも呼べる。
    /// </summary>
    /// <returns>通常終了で true。ミスで TurnEnd 済みのとき false。</returns>
    public async Task<bool> ResolvePlayerAttackCombatAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        List<CardData> defHand,
        CancellationToken cancellationToken,
        int dualBladeStrikeIndex = 0)
    {
        var primary = HitRateRules.GetPrimaryForHitRate(attackCards);
        int finalPct = HitRateRules.ComputeFinalHitPercent(
            primary, atk, def, HitRateRules.ShouldApplyAttackerSmokeForCombat(primary));
        bool hit = HitRateRules.RollHit(finalPct);

        if (!hit)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ニュッ1.mp3");
            BattleUIManager.I?.ShowMissPopup(def);
            await DamagePopup.WaitAfterPopupLifetimeAsync(DamagePopup.DefaultFadeDurationIfUnknown, cancellationToken);
            if (await battleManager.TryHandleDeathIfAnyAsync(cancellationToken))
                return false;
            await RevealMagicPanelBonusDrawsAsync(cancellationToken);
            BattleUIManager.I?.HideAllCardDetails();
            cardStatsDisplay?.ClearSequenceCardsAndAttackDisplayLocks();
            battleManager.SetCurrentAttackCard(null);
            cardStatsDisplay?.UpdateDisplay();
            battleManager.SetGameState(GameState.CombatResolvePhase);
            battleManager.ClearMagicalExplosionComboMpPoolSnapshot();
            battleManager.ClearMillionDollarBazookaComboGpPoolSnapshot();
            battleManager.ClearTributeBloodHpPaidSnapshot();
            battleManager.ClearHammadnessRollSnapshot();
            battleManager.ClearMagicalSwordPlayerAttackState();
            return false;
        }

        if (finalPct < 100)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/小パンチ.mp3");
            float sec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowCombatHitConfirmedPopup(def)
                : DamagePopup.DefaultFadeDurationIfUnknown;
            await DamagePopup.WaitAfterPopupLifetimeAsync(sec, cancellationToken);
        }

        bool atkIsPlayer = ReferenceEquals(atk, battleManager.GetPlayerStatus());
        if (cardStatsDisplay != null && dualBladeStrikeIndex == 0 && !atkIsPlayer)
            await PlayAttackModifierRampsAsync(attackCards, atk, def, cancellationToken);

        bool selfAttack = ReferenceEquals(atk, def);
        if (selfAttack)
        {
            await battleProcessor.ResolveCombatAsync(attackCards, (CardData)null, atk, def, defHand, skipHitCheck: true);
            if (DualBladeDualismRules.ContainsDualBladeDualism(attackCards)
                && dualBladeStrikeIndex == 0
                && !atk.IsDead() && !def.IsDead())
            {
                await battleProcessor.ResolveCombatAsync(attackCards, (CardData)null, atk, def, defHand, skipHitCheck: true);
            }
            return true;
        }

        await battleManager.PickAndDisplayEnemyDefenseAfterPlayerHitAsync(attackCards);

        var defenseCards = battleManager.GetEnemyDefenseCardsForCombat();
        CardData selectedDefenseCard = defenseCards.Count > 0 ? defenseCards[0] : null;
        bool showYurusuDuringCombat =
            battleManager.DefenderPublic == PlayerType.Enemy && defenseCards.Count == 0 && BattleUIManager.I != null;

        bool enemyPhysicalReflect = selectedDefenseCard != null
            && ReflectionRules.CanReflectIncoming(selectedDefenseCard, attackCards)
            && !ReflectionRules.ShouldUseImmediateEffectReflectionFlow(attackCards);
        bool enemyMagicReflect = enemyPhysicalReflect;
        bool enemyImmediateReflect = selectedDefenseCard != null
            && ReflectionRules.CanReflectIncoming(selectedDefenseCard, attackCards)
            && ReflectionRules.ShouldUseImmediateEffectReflectionFlow(attackCards);
        bool enemyPhysicalBlock = selectedDefenseCard != null
            && BlockingRules.CanUsePhysicalBlockingAgainstAttack(selectedDefenseCard, attackCards);
        bool enemyParry = selectedDefenseCard != null
            && ParryRules.RequiresParryExclusiveLock(selectedDefenseCard, attackCards);

        using (YurusuDisplayScope.ShowIf(showYurusuDuringCombat))
        {
            if (enemyImmediateReflect)
            {
                await ImmediateEffectReflectionFlow.RunEnemyDefenderReflectsPlayerImmediateAsync(
                    battleManager,
                    battleProcessor,
                    handRefill,
                    attackCards,
                    selectedDefenseCard,
                    atk,
                    cancellationToken);
            }
            else if (enemyPhysicalReflect || enemyMagicReflect)
            {
                await PhysicalReflectionFlow.RunEnemyDefenderReflectsPlayerAttackAsync(
                    battleManager,
                    battleProcessor,
                    handRefill,
                    battleManager.GetEnemyAI(),
                    attackCards,
                    selectedDefenseCard,
                    cancellationToken);
            }
            else if (enemyParry)
            {
                await ParryFlow.RunEnemyDefenderParriesPlayerAttackAsync(
                    battleManager,
                    battleProcessor,
                    handRefill,
                    battleManager.GetEnemyAI(),
                    attackCards,
                    selectedDefenseCard,
                    cancellationToken);
            }
            else if (enemyPhysicalBlock)
            {
                await BlockingNullifyFlow.RunEnemyDefenderNullifiesAsync(
                    battleManager,
                    battleProcessor,
                    handRefill,
                    attackCards,
                    selectedDefenseCard,
                    cancellationToken);
            }
            else if (CardRules.IncomingRequiresFullOnlyReactiveDefense(attackCards)
                && attackCards.Count == 1 && attackCards[0] != null)
            {
                await Task.Delay(DamagePopup.PreImmediateEffectDelayMs, cancellationToken);
                await battleProcessor.ResolveImmediateEffectAsync(attackCards[0], atk, def);
            }
            else
            {
                if (defenseCards.Count > 1)
                    await battleProcessor.ResolveCombatAsync(attackCards, defenseCards, atk, def, defHand, skipHitCheck: true);
                else
                    await battleProcessor.ResolveCombatAsync(attackCards, selectedDefenseCard, atk, def, defHand, skipHitCheck: true);
            }
        }

        // オンラインの魔法防御は RemotePlayerAgent 側で記録・プール処理済みのため二重計上しない
        bool skipPostCombatEnemyDefenseUse = enemyPhysicalReflect || enemyMagicReflect || enemyImmediateReflect
            || enemyPhysicalBlock || enemyParry
            || (attackCards != null && CardRules.IncomingRequiresFullOnlyReactiveDefense(attackCards));
        if (defenseCards.Count > 0 && !skipPostCombatEnemyDefenseUse)
        {
            foreach (var defCard in defenseCards)
            {
                if (defCard == null) continue;
                if (battleManager != null && battleManager.IsOnlineMatch && defCard.cardType == CardType.Magic)
                    continue;
                handRefill?.RecordEnemyUse(defCard);
                battleProcessor.UseCard(defCard, defHand);
            }
        }

        if (DualBladeDualismRules.ContainsDualBladeDualism(attackCards)
            && dualBladeStrikeIndex == 0
            && !atk.IsDead() && !def.IsDead())
        {
            await PresentDualBladeSecondStrikeAttackRevealAsync(attackCards, atk, cancellationToken);
            return await ResolvePlayerAttackCombatAsync(
                attackCards, atk, def, defHand, cancellationToken, 1);
        }

        if (await battleManager.TryHandleDeathIfAnyAsync(cancellationToken))
            return false;

        return true;
    }

    /// <summary>
    /// カード掲出・特殊 intro 完了後（または混乱自己攻撃など通常シーケンス外）の MS／ゴッドレイジ緑字ランプ。
    /// </summary>
    public async Task PlayAttackModifierRampsAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        CancellationToken cancellationToken,
        int dualBladeStrikeIndex = 0)
    {
        if (cardStatsDisplay == null || dualBladeStrikeIndex != 0 || attackCards == null || atk == null)
            return;

        bool atkIsPlayer = ReferenceEquals(atk, battleManager.GetPlayerStatus());
        bool atkIsEnemy = ReferenceEquals(atk, battleManager.GetEnemyStatus());
        if (!atkIsPlayer && !atkIsEnemy) return;

        var meGodCombo = MagicalExplosionRules.ContainsMagicalExplosion(attackCards)
            && GodrageRules.IsGodrageDoublingCombo(attackCards);
        var mdbGodCombo = MillionDollarBazookaRules.ContainsMillionDollarBazooka(attackCards)
            && GodrageRules.IsGodrageDoublingCombo(attackCards);
        var hammadnessGodCombo = HammadnessRules.ContainsHammadness(attackCards)
            && GodrageRules.IsGodrageDoublingCombo(attackCards);
        var godRageOnlyCombo = GodrageRules.IsGodrageDoublingCombo(attackCards)
            && !MagicalExplosionRules.ContainsMagicalExplosion(attackCards)
            && !MillionDollarBazookaRules.ContainsMillionDollarBazooka(attackCards)
            && !HammadnessRules.ContainsHammadness(attackCards);
        bool playGodRamps = meGodCombo || mdbGodCombo || hammadnessGodCombo || godRageOnlyCombo;

        int msBonusRuntime = atkIsPlayer
            ? battleManager.MagicalSwordAttackPowerBonus
            : battleManager.MagicalSwordEnemyAttackPowerBonus;
        bool hasMsSword = MagicalSwordRules.ContainsMagicalSword(attackCards);
        bool msBoost = hasMsSword && msBonusRuntime > 0;
        bool preMeMsDone = atkIsPlayer
            ? battleManager.MagicalSwordPlayerPreMeRampVisualDone
            : battleManager.MagicalSwordEnemyPreMeRampVisualDone;
        bool needMsRampInResolve = msBoost && !preMeMsDone;
        MagicalSwordRules.TryGetFirstMagicalSwordRule(attackCards, out var msRuleForRamp);
        int attackBoost = msRuleForRamp != null ? msRuleForRamp.attackPowerBonus : 0;
        var msDataCard = MagicalSwordRules.FindFirstMagicalSwordCard(attackCards);

        if (needMsRampInResolve && msDataCard != null && attackBoost > 0)
        {
            if (!playGodRamps)
                await Task.Delay(500, cancellationToken);
            await cardStatsDisplay.PlayMagicalSwordAttackRampAsync(
                attackCards, atk, def, msDataCard, attackBoost, 0.2f, cancellationToken);
        }

        if (playGodRamps)
        {
            if (needMsRampInResolve && msDataCard != null && attackBoost > 0)
                await Task.Delay(500, cancellationToken);
            else if (meGodCombo || mdbGodCombo || hammadnessGodCombo)
                await Task.Delay(1000, cancellationToken);
            else
                await Task.Delay(500, cancellationToken);
            int fromAtk = cardStatsDisplay.ComputeGodRageRampFrom(attackCards, atk, def);
            int toAtk = cardStatsDisplay.ComputeGodRageRampTo(attackCards, atk, def);
            await cardStatsDisplay.PlayGodRageAttackRampAsync(
                attackCards, atk, def, fromAtk, toAtk, 0.2f, cancellationToken);
        }

        cardStatsDisplay.ClearAttackModifierRevealSuppressions();
        cardStatsDisplay.UpdateDisplay();
    }

    /// <summary>
    /// カード処理（攻撃・防御共通）
    /// </summary>
    private async System.Threading.Tasks.Task ProcessCardsAsync(List<CardData> cards, string cardType)
    {
        if (cards.Count > 1)
        {
            Debug.Log($"[CardSequenceManager] 複数{cardType}カード選択中: {cards.Count}枚。全てのカードを処理します。");
            await ProcessMultipleCardsAsync(cards, cardType);
        }
        else
        {
            Debug.Log($"[CardSequenceManager] 単一{cardType}カード選択中。カードを処理します。");
            await ProcessSingleCardAsync(cards[0], cardType);
        }
    }

    /// <summary>
    /// 複数カードの処理（攻撃・防御共通）
    /// </summary>
    private async System.Threading.Tasks.Task ProcessMultipleCardsAsync(List<CardData> cards, string cardType)
    {
        // 魔法カードと通常カードに分別
        var magicCards = cards.FindAll(c => c.cardType == CardType.Magic);
        var normalCards = cards.FindAll(c => c.cardType != CardType.Magic);

        // 魔法カードのプール処理
        if (!_skipMagicProcessingInProcessCardsBecauseMagicalExplosion)
        {
            foreach (var magic in magicCards)
            {
                bool isFromHand = BattleUIManager.I == null
                    || !BattleUIManager.I.IsPlayerMagicCardUiOnMagicPanel(magic);
                await ApplyMagicCardToPoolAsync(magic, isFromHand);
                Debug.Log($"[CardSequenceManager] 魔法カード {magic.cardName} をプール処理 (fromHand={isFromHand}, combination={magic.isCombinationMagic})");
            }
        }
        else
        {
            _skipMagicProcessingInProcessCardsBecauseMagicalExplosion = false;
            Debug.Log("[CardSequenceManager] マジカルエクスプロージョン演出で魔法処理済みのためスキップ");
        }

        // 攻撃カードの場合は最初のカードを currentAttackCard に設定
        if (cardType == "攻撃" && normalCards.Count > 0)
        {
            battleManager.SetCurrentAttackCard(normalCards[0]);
        }

        if (cardType == "防御")
        {
            CardData defPick = null;
            if (normalCards.Count > 0)
                defPick = normalCards[0];
            else if (magicCards.Count > 0)
                defPick = magicCards[0];
            battleManager.SetSelectedDefenseCard(defPick);
        }

        // 通常カードのみ UseCard で手札から除去
        foreach (var card in normalCards)
        {
            if (card?.cardUI == null) continue;

            int slotIndex = card.cardUI.transform.GetSiblingIndex();
            handRefill?.RecordPlayerUseSlot(slotIndex);
            battleProcessor.UseCard(card, battleManager.playerHand);
            Debug.Log($"[CardSequenceManager] {cardType}カード処理: {card.cardName} (スロット: {slotIndex})");
        }

        // 手札の魔法カードの裏面化・UseCard は ApplyMagicCardToPoolAsync 内で実施済み
    }

    /// <summary>
    /// 単一カードの処理（攻撃・防御共通）。
    /// <see cref="CardType.Magic"/> は攻撃・防御のどちらのフェーズでもあり得る。<paramref name="cardType"/>（"攻撃"／"防御"）で
    /// <see cref="BattleManager.SetCurrentAttackCard"/> と <see cref="BattleManager.SetSelectedDefenseCard"/> を切り替える。
    /// </summary>
    private async System.Threading.Tasks.Task ProcessSingleCardAsync(CardData card, string cardType)
    {
        // 魔法：MP・MagicPanel は共通。解決時の役割だけフェーズ（cardType）で分岐する。
        if (card.cardType == CardType.Magic)
        {
            Debug.Log($"[CardSequenceManager] 魔法カード処理: {card.cardName} (組み合わせ={card.isCombinationMagic})");
            bool isFromHand = BattleUIManager.I == null
                || !BattleUIManager.I.IsPlayerMagicCardUiOnMagicPanel(card);
            await ApplyMagicCardToPoolAsync(card, isFromHand);
            if (cardType == "防御")
                battleManager.SetSelectedDefenseCard(card);
            else
                battleManager.SetCurrentAttackCard(card);
            return;
        }

        if (cardType == "防御")
        {
            battleManager.SetSelectedDefenseCard(card);
        }
        else
        {
            battleManager.SetSelectedCard(card);
            battleManager.SetCurrentAttackCard(card);
        }

        int normalSlotIndex = (card.cardUI != null) ? card.cardUI.transform.GetSiblingIndex() : -1;
        if (normalSlotIndex >= 0) handRefill?.RecordPlayerUseSlot(normalSlotIndex);
        battleProcessor.UseCard(card, battleManager.playerHand);
        Debug.Log($"[CardSequenceManager] 単一{cardType}カード処理: {card.cardName} (スロット: {normalSlotIndex})");
    }

    /// <summary>
    /// 魔法カードを MagicPool に適用する内部ヘルパー
    /// MP消費 → (手札からなら飛行アニメ) → プール操作 → (プール使用時)カードドロー
    /// </summary>
    /// <summary>
    /// 反射連鎖・パリィ再防御など、<see cref="StartCardSequenceAsync"/> を通さない経路で魔法をプールへ載せる。
    /// </summary>
    /// <summary>敵の魔法防御（<<アイアンクラッド>> 等）：MP 消費と手札／プール処理。</summary>
    public async Task ApplyEnemyMagicDefenseFromHandOrPoolAsync(CardData card)
    {
        if (card == null || card.cardType != CardType.Magic) return;
        bool isFromHand = BattleUIManager.I == null
            || !BattleUIManager.I.IsEnemyMagicCardUiOnMagicPanel(card);
        await ApplyEnemyMagicCardToPoolAsync(card, isFromHand);
    }

    public async Task ApplyMagicCardToPoolForReflectionOrParryDefenseAsync(CardData card, CancellationToken cancellationToken = default)
    {
        if (card == null || card.cardType != CardType.Magic) return;
        if (cancellationToken.IsCancellationRequested) return;
        bool isFromHand = BattleUIManager.I == null
            || !BattleUIManager.I.IsPlayerMagicCardUiOnMagicPanel(card);
        await ApplyMagicCardToPoolAsync(card, isFromHand);
    }

    private async System.Threading.Tasks.Task ApplyMagicCardToPoolAsync(CardData card, bool isFromHand)
    {
        if (card == null) return;

        // MP消費（眼精疲労で倍率）— MagicPoolManager の有無に関わらず先に適用
        var playerStatus = battleManager.GetPlayerStatus();
        if (playerStatus != null && card.mpCost > 0)
        {
            int pay = playerStatus.GetEffectiveMagicMpCost(card.mpCost);
            playerStatus.UseMP(pay);
            Debug.Log($"[CardSequenceManager] MP消費: {card.cardName} -{pay}MP (残り={playerStatus.currentMP})");
            BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
        }

        if (MagicPoolManager.I == null) return;

        if (isFromHand)
        {
            // 飛行演出と同時に手札枠は通常攻撃と同様に裏面表示（Record→Use の順を厳守）
            if (card.cardUI != null)
            {
                int slotIndex = card.cardUI.transform.GetSiblingIndex();
                handRefill?.RecordPlayerUseSlot(slotIndex);
                battleProcessor.UseCard(card, battleManager.playerHand);
            }

            if (card.cardUI != null && BattleUIManager.I != null && card.cardImage != null)
            {
                int slot = MagicPoolManager.I.GetPredictedPlayerSlotIndex(card);
                RectTransform handFlyRt = card.cardUI.cardImage != null
                    ? card.cardUI.cardImage.rectTransform
                    : card.cardUI.transform as RectTransform;
                await BattleUIManager.I.PlayMagicFlyHandToPanelAsync(card, handFlyRt, slot);
            }

            var drawCallback = GetDrawCardCallback();
            bool result = MagicPoolManager.I.TryUseMagicCard(
                card,
                battleManager.playerHand,
                battleManager.GetHandMaxCount(),
                drawCallback);
            Debug.Log($"[CardSequenceManager] TryUseMagicCard: {card.cardName} -> {result}");
        }
        else
        {
            // ① MagicPanel から使用を確定 → ② 直後に裏面で 1 枚追加（③ 表向けは ResolveCombat 後）
            MagicPoolManager.I.ConsumeUse(card);
            Debug.Log($"[CardSequenceManager] ConsumeUse: {card.cardName}");

            var drawn = await battleManager.DrawOneCardAsync(trailingDelayMs: 0, playSoundOnDraw: false);
            if (drawn != null)
                _magicPanelBonusDrawsPendingReveal.Add(drawn);
            Debug.Log("[CardSequenceManager] MagicPanel使用による手札1枚追加（裏面・戦闘後に表向け）");
        }
    }

    /// <summary>
    /// MagicPanel ボーナスドローの表向け（TurnEnd の手札更新と同じテンポ）
    /// </summary>
    private async Task RevealMagicPanelBonusDrawsAsync(CancellationToken ct)
    {
        if (handRefill == null || _magicPanelBonusDrawsPendingReveal.Count == 0) return;

        for (int i = 0; i < _magicPanelBonusDrawsPendingReveal.Count; i++)
        {
            if (ct.IsCancellationRequested) return;
            var card = _magicPanelBonusDrawsPendingReveal[i];
            if (card == null) continue;
            await handRefill.RevealDrawnCardAfterCombatAsync(card, ct);
        }

        _magicPanelBonusDrawsPendingReveal.Clear();
    }

    /// <summary>
    /// 手札追加ドローのコールバックを取得する
    /// </summary>
    private System.Action GetDrawCardCallback()
    {
        return () =>
        {
            // BattleManager のドローメソッドを呼び出す
            battleManager.DrawOneCard();
        };
    }

    /// <summary>
    /// オンライン／複数枚：相手攻撃を CardSequenceManager と同じ手順で表示（戦闘解決は行わない）。
    /// </summary>
    public async Task PresentOnlineEnemyAttackSequenceAsync(
        List<CardData> selectedCards,
        CancellationToken cancellationToken)
    {
        if (selectedCards == null || selectedCards.Count == 0 || battleManager == null)
            return;

        Debug.Log($"[CardSequenceManager] Enemy attack presentation: {selectedCards.Count} cards");

        cardStatsDisplay?.ConfigureAttackSequenceRevealSuppressions(selectedCards);

        bool spellbookElementRevealPending = SpellbookRules.NeedsElementRevealSequence(selectedCards);

        battleManager.ClearMagicalExplosionComboMpPoolSnapshot();
        battleManager.ClearMillionDollarBazookaComboGpPoolSnapshot();
        battleManager.ClearHammadnessRollSnapshot();
        battleManager.ClearConfusionAttackTargetResolvedForDisplay();

        cardStatsDisplay?.SetSequenceCards(new List<CardData>(), "攻撃", Side.Enemy);
        BattleUIManager.I?.ClearAllSelections();
        BattleUIManager.I?.HideAllCardDetails();
        cardStatsDisplay?.UpdateDisplay();
        await Task.Delay(300, cancellationToken);

        PlayerAttackTotalDisplayFlow.EnterSequentialCardReveal_SuppressPendingModifierRamps(
            cardStatsDisplay,
            selectedCards,
            battleManager.MagicalSwordEnemyAttackPowerBonus);

        for (int i = 0; i < selectedCards.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var card = selectedCards[i];
            BattleUIManager.I?.ShowCardDetail(card, Side.Enemy);
            var sequenceCards = new List<CardData>(selectedCards.GetRange(0, i + 1));
            cardStatsDisplay?.SetSequenceCards(sequenceCards, "攻撃", Side.Enemy);
            cardStatsDisplay?.UpdateDisplay();
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            await Task.Delay(500, cancellationToken);
        }

        var psPre = battleManager.GetPlayerStatus();
        var esPre = battleManager.GetEnemyStatus();
        if (MagicalExplosionRules.ContainsMagicalExplosion(selectedCards)
            && MagicalSwordRules.ContainsMagicalSword(selectedCards)
            && battleManager.MagicalSwordEnemyAttackPowerBonus > 0
            && cardStatsDisplay != null
            && MagicalSwordRules.TryGetFirstMagicalSwordRule(selectedCards, out var msRForPreMeRamp)
            && esPre != null && psPre != null)
        {
            var msCardPre = MagicalSwordRules.FindFirstMagicalSwordCard(selectedCards);
            if (msCardPre != null)
            {
                await cardStatsDisplay.PlayMagicalSwordAttackRampAsync(
                    selectedCards,
                    esPre,
                    psPre,
                    msCardPre,
                    msRForPreMeRamp.attackPowerBonus,
                    0.2f,
                    cancellationToken);
                battleManager.SetMagicalSwordEnemyPreMeRampVisualDone(true);
            }
        }

        if (MagicalExplosionRules.ContainsMagicalExplosion(selectedCards) && esPre != null)
            await RunMagicalExplosionAttackIntroAsync(selectedCards, esPre, psPre, cancellationToken);
        else
            cardStatsDisplay?.SetSuppressMagicalExplosionPredictionDuringSequenceReveal(false);

        if (MillionDollarBazookaRules.ContainsMillionDollarBazooka(selectedCards) && esPre != null)
            await RunMillionDollarBazookaAttackIntroAsync(selectedCards, esPre, psPre, cancellationToken);
        else
            cardStatsDisplay?.SetSuppressMillionDollarBazookaPredictionDuringSequenceReveal(false);

        if (TributeBloodRules.ContainsTributeBlood(selectedCards) && esPre != null)
            await RunTributeBloodAttackIntroAsync(selectedCards, esPre, psPre, cancellationToken);
        else
            cardStatsDisplay?.SetSuppressTributeBloodPredictionDuringSequenceReveal(false);

        if (HammadnessRules.ContainsHammadness(selectedCards) && esPre != null)
            await RunHammadnessAttackIntroAsync(selectedCards, esPre, cancellationToken);
        else
            cardStatsDisplay?.SetSuppressHammadnessPredictionDuringSequenceReveal(false);

        if (esPre != null && psPre != null)
        {
            cardStatsDisplay?.SetSequenceCards(selectedCards, "攻撃", Side.Enemy);
            cardStatsDisplay?.UpdateDisplay();
            await PlayAttackModifierRampsAsync(selectedCards, esPre, psPre, cancellationToken);
        }

        if (spellbookElementRevealPending
            && SpellbookRules.TryGetForcedComboElement(selectedCards, out var spellbookFlashElement))
        {
            await Task.Delay(500, cancellationToken);
            const float spellbookColorFlashMs = 50f;
            Color spellbookFlashColor = ElementHelper.GetElementColor(spellbookFlashElement);
            SoundEffectPlayer.I?.Play("Assets/SE/power19.wav");
            BattleUIManager.I?.PlayFullscreenColorFlashMs(spellbookFlashColor, spellbookColorFlashMs);
            await Task.Delay((int)spellbookColorFlashMs, cancellationToken);
            cardStatsDisplay?.SetSuppressSpellbookElementDuringSequenceReveal(false);
            cardStatsDisplay?.UpdateDisplay();
        }

        await ProcessEnemyAttackCardsAsync(selectedCards);

        CardData primaryNormal = null;
        foreach (var c in selectedCards)
        {
            if (c != null && c.cardType != CardType.Magic)
            {
                primaryNormal = c;
                break;
            }
        }
        battleManager.SetCurrentAttackCard(primaryNormal != null ? primaryNormal : selectedCards[0]);
        if (selectedCards.Count > 1 || MagicalExplosionRules.ContainsMagicalExplosion(selectedCards)
            || MillionDollarBazookaRules.ContainsMillionDollarBazooka(selectedCards)
            || TributeBloodRules.ContainsTributeBlood(selectedCards)
            || HammadnessRules.ContainsHammadness(selectedCards))
            battleManager.SetOnlineEnemyAttackCombo(selectedCards);

        battleManager.SetEnemyAttackComboForCombat(selectedCards);
        cardStatsDisplay?.EndAttackSequenceReveal();
        cardStatsDisplay?.UpdateDisplay();
    }

    private async Task ProcessEnemyAttackCardsAsync(List<CardData> cards)
    {
        if (cards == null || cards.Count == 0) return;

        var magicCards = cards.FindAll(c => c != null && c.cardType == CardType.Magic);
        var normalCards = cards.FindAll(c => c != null && c.cardType != CardType.Magic);

        if (!_skipMagicProcessingInProcessCardsBecauseMagicalExplosion)
        {
            foreach (var magic in magicCards)
            {
                bool isFromHand = BattleUIManager.I == null
                    || !BattleUIManager.I.IsEnemyMagicCardUiOnMagicPanel(magic);
                await ApplyEnemyMagicCardToPoolAsync(magic, isFromHand);
            }
        }
        else
        {
            _skipMagicProcessingInProcessCardsBecauseMagicalExplosion = false;
        }

        foreach (var card in normalCards)
        {
            handRefill?.RecordEnemyUse(card);
            battleProcessor.UseCard(card, battleManager.cpuHand);
        }
    }

    private async Task ApplyEnemyMagicCardToPoolAsync(CardData card, bool isFromHand)
    {
        if (MagicPoolManager.I == null || card == null) return;

        var enemyStatus = battleManager.GetEnemyStatus();
        if (enemyStatus != null && card.mpCost > 0)
        {
            int pay = enemyStatus.GetEffectiveMagicMpCost(card.mpCost);
            enemyStatus.UseMP(pay);
            BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), enemyStatus);
        }

        if (isFromHand)
        {
            handRefill?.RecordEnemyUse(card);
            battleProcessor.UseCard(card, battleManager.cpuHand);
            MagicPoolManager.I.TryUseMagicCard(
                card,
                battleManager.cpuHand,
                battleManager.GetHandMaxCount(),
                () =>
                {
                    var drawn = battleManager.cardDealer?.DrawRandomCard(PlayerType.Enemy);
                    if (drawn != null)
                        battleManager.cpuHand.Add(drawn);
                },
                PlayerType.Enemy);
        }
        else
        {
            MagicPoolManager.I.ConsumeUse(card, PlayerType.Enemy);
            var drawn = battleManager.cardDealer?.DrawRandomCard(PlayerType.Enemy);
            if (drawn != null)
                battleManager.cpuHand.Add(drawn);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 敵攻撃：マジカルエクスプロージョンをプレイヤーと同じ手順（1 枚出す→<see cref="RunMagicalExplosionAttackIntroAsync"/>）で処理し、その後手札から除去する。
    /// </summary>
    public async Task PresentEnemyMagicalExplosionAttackAsync(CardData meCard, CancellationToken cancellationToken)
    {
        if (meCard == null || battleManager == null || !MagicalExplosionRules.IsMagicalExplosionCard(meCard))
            return;

        battleManager.ClearMagicalExplosionComboMpPoolSnapshot();
        battleManager.ClearMillionDollarBazookaComboGpPoolSnapshot();
        battleManager.ClearHammadnessRollSnapshot();
        cardStatsDisplay?.SetSuppressMagicalExplosionPredictionDuringSequenceReveal(true);

        BattleUIManager.I?.ClearAllSelections();
        BattleUIManager.I?.HideAllCardDetails();
        cardStatsDisplay?.SetSequenceCards(new List<CardData>(), "攻撃", Side.Enemy);
        await Task.Delay(300, cancellationToken);

        BattleUIManager.I?.ShowCardDetail(meCard, Side.Enemy);
        var revealed = new List<CardData> { meCard };
        cardStatsDisplay?.SetSequenceCards(revealed, "攻撃", Side.Enemy);
        cardStatsDisplay?.UpdateDisplay();
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        await Task.Delay(500, cancellationToken);

        var ps = battleManager.GetPlayerStatus();
        var es = battleManager.GetEnemyStatus();
        if (es == null || ps == null)
        {
            cardStatsDisplay?.SetSuppressMagicalExplosionPredictionDuringSequenceReveal(false);
            return;
        }

        await RunMagicalExplosionAttackIntroAsync(revealed, es, ps, cancellationToken);

        handRefill?.RecordEnemyUse(meCard);
        battleProcessor.UseCard(meCard, battleManager.cpuHand);
        battleManager.SetCurrentAttackCard(meCard);
    }

    /// <summary>
    /// 全シート表示後：200ms → 魔法 MP 消費 → SE・白フラッシュ・MP 全喪失 → TOTAL / ME シートのカウントアップ。
    /// </summary>
    private async Task RunMagicalExplosionAttackIntroAsync(
        List<CardData> selectedCards,
        PlayerStatus atk,
        PlayerStatus defForBless,
        CancellationToken cancellationToken)
    {
        if (cardStatsDisplay == null)
            return;
        if (atk == null || selectedCards == null)
        {
            cardStatsDisplay.SetSuppressMagicalExplosionPredictionDuringSequenceReveal(false);
            return;
        }

        int fromTotal = cardStatsDisplay.ComputeMagicalExplosionRampFrom(selectedCards, atk, defForBless);
        cardStatsDisplay.SetMagicalExplosionPreRampAttackDisplay(fromTotal);
        cardStatsDisplay.SetSuppressMagicalExplosionPredictionDuringSequenceReveal(false);
        cardStatsDisplay.UpdateDisplay();

        await Task.Delay(500, cancellationToken);

        var magicCards = selectedCards.FindAll(c =>
            c != null && c.cardType == CardType.Magic && !MagicalExplosionRules.IsMagicalExplosionCard(c));
        foreach (var magic in magicCards)
        {
            bool isFromHand = BattleUIManager.I == null
                || !BattleUIManager.I.IsPlayerMagicCardUiOnMagicPanel(magic);
            bool attackerIsPlayer = ReferenceEquals(atk, battleManager.GetPlayerStatus());
            if (attackerIsPlayer)
                await ApplyMagicCardToPoolAsync(magic, isFromHand);
            else
                await ApplyEnemyMagicCardToPoolAsync(magic, isFromHand);
        }

        _skipMagicProcessingInProcessCardsBecauseMagicalExplosion = true;

        int mpRemain = atk.currentMP;
        battleManager.SetMagicalExplosionComboMpPoolSnapshot(mpRemain);

        SoundEffectPlayer.I?.Play("Assets/SE/マジカルエクスプロージョン.mp3");
        BattleUIManager.I?.PlayFullscreenWhiteFlashMs(50f);
        atk.UseMP(mpRemain);
        BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());

        int toTotal = cardStatsDisplay.ComputeMagicalExplosionRampTo(selectedCards, atk, defForBless);

        CardData meCard = null;
        for (int i = 0; i < selectedCards.Count; i++)
        {
            var c = selectedCards[i];
            if (MagicalExplosionRules.IsMagicalExplosionCard(c))
            {
                meCard = c;
                break;
            }
        }

        int meSheetAtk = mpRemain * 2;

        await cardStatsDisplay.PlayMagicalExplosionAttackRampAsync(
            selectedCards,
            atk,
            meCard,
            meSheetAtk,
            fromTotal,
            toTotal,
            0.2f,
            cancellationToken);
    }

    /// <summary>
    /// After all sheets: magic MP cost -> SE + white flash + drain all GP -> count up TOTAL / bazooka sheet ATK.
    /// </summary>
    private async Task RunMillionDollarBazookaAttackIntroAsync(
        List<CardData> selectedCards,
        PlayerStatus atk,
        PlayerStatus defForBless,
        CancellationToken cancellationToken)
    {
        if (cardStatsDisplay == null)
            return;
        if (atk == null || selectedCards == null)
        {
            cardStatsDisplay.SetSuppressMillionDollarBazookaPredictionDuringSequenceReveal(false);
            return;
        }

        int fromTotal = cardStatsDisplay.ComputeMillionDollarBazookaRampFrom(selectedCards, atk, defForBless);
        cardStatsDisplay.SetMillionDollarBazookaPreRampAttackDisplay(fromTotal);
        cardStatsDisplay.SetSuppressMillionDollarBazookaPredictionDuringSequenceReveal(false);
        cardStatsDisplay.UpdateDisplay();

        await Task.Delay(500, cancellationToken);

        var magicCards = selectedCards.FindAll(c =>
            c != null && c.cardType == CardType.Magic && !MillionDollarBazookaRules.IsMillionDollarBazookaCard(c));
        foreach (var magic in magicCards)
        {
            bool isFromHand = BattleUIManager.I == null
                || !BattleUIManager.I.IsPlayerMagicCardUiOnMagicPanel(magic);
            bool attackerIsPlayer = ReferenceEquals(atk, battleManager.GetPlayerStatus());
            if (attackerIsPlayer)
                await ApplyMagicCardToPoolAsync(magic, isFromHand);
            else
                await ApplyEnemyMagicCardToPoolAsync(magic, isFromHand);
        }

        _skipMagicProcessingInProcessCardsBecauseMagicalExplosion = true;

        int gpRemain = atk.currentGP;
        battleManager.SetMillionDollarBazookaComboGpPoolSnapshot(gpRemain);

        MillionDollarBazookaRules.TryGetFirstMillionDollarBazookaRule(selectedCards, out var mdbRule);
        int bazookaSheetAtk = MillionDollarBazookaRules.ComputeDamageBonusFromGp(gpRemain, mdbRule);

        SoundEffectPlayer.I?.Play("Assets/SE/マジカルエクスプロージョン.mp3");
        BattleUIManager.I?.PlayFullscreenWhiteFlashMs(50f);
        atk.UseGP(gpRemain);
        BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());

        int toTotal = cardStatsDisplay.ComputeMillionDollarBazookaRampTo(selectedCards, atk, defForBless);
        CardData bazookaCard = MillionDollarBazookaRules.FindFirstMillionDollarBazookaCard(selectedCards);

        await cardStatsDisplay.PlayMagicalExplosionAttackRampAsync(
            selectedCards,
            atk,
            bazookaCard,
            bazookaSheetAtk,
            fromTotal,
            toTotal,
            0.2f,
            cancellationToken);
        cardStatsDisplay.ClearMagicalExplosionPlayerAtkDisplayLockOnly();
        cardStatsDisplay.LockMillionDollarBazookaPlayerAttackDisplay(selectedCards, atk);
        cardStatsDisplay.UpdateDisplay();
    }

    /// <summary>
    /// After all sheets: 500ms -> SE + white flash -> count up TOTAL / Tribute Blood ATK; HP drops during ramp.
    /// </summary>
    private async Task RunTributeBloodAttackIntroAsync(
        List<CardData> selectedCards,
        PlayerStatus atk,
        PlayerStatus defForBless,
        CancellationToken cancellationToken)
    {
        if (cardStatsDisplay == null)
            return;
        if (atk == null || selectedCards == null)
        {
            cardStatsDisplay.SetSuppressTributeBloodPredictionDuringSequenceReveal(false);
            return;
        }

        int fromTotal = cardStatsDisplay.ComputeTributeBloodRampFrom(selectedCards, atk, defForBless);
        cardStatsDisplay.SetTributeBloodPreRampAttackDisplay(fromTotal);
        cardStatsDisplay.SetSuppressTributeBloodPredictionDuringSequenceReveal(false);
        cardStatsDisplay.UpdateDisplay();

        await Task.Delay(500, cancellationToken);

        int hpPaid = TributeBloodRules.GetActiveHpPaid(selectedCards, atk);
        TributeBloodRules.TryGetFirstTributeBloodRule(selectedCards, out var tbRule);
        int tbSheetAtk = TributeBloodRules.ComputeDamageBonusFromHpPaid(hpPaid, tbRule);

        SoundEffectPlayer.I?.Play("Assets/SE/マジカルエクスプロージョン.mp3");
        BattleUIManager.I?.PlayFullscreenWhiteFlashMs(50f);

        int toTotal = cardStatsDisplay.ComputeTributeBloodRampTo(selectedCards, atk, defForBless);
        CardData tbCard = TributeBloodRules.FindFirstTributeBloodCard(selectedCards);

        await cardStatsDisplay.PlayTributeBloodAttackRampAsync(
            selectedCards,
            atk,
            tbCard,
            tbSheetAtk,
            hpPaid,
            fromTotal,
            toTotal,
            0.2f,
            cancellationToken);
    }

    /// <summary>
    /// 全シート表示後：500ms → SE・白フラッシュ → ランダム ATK 決定 → TOTAL / 気狂いハンマー シートのカウントアップ。
    /// </summary>
    private async Task RunHammadnessAttackIntroAsync(
        List<CardData> selectedCards,
        PlayerStatus atk,
        CancellationToken cancellationToken)
    {
        if (cardStatsDisplay == null)
            return;
        if (atk == null || selectedCards == null)
        {
            cardStatsDisplay.SetSuppressHammadnessPredictionDuringSequenceReveal(false);
            return;
        }

        int fromTotal = cardStatsDisplay.ComputeHammadnessRampFrom(selectedCards, atk);
        cardStatsDisplay.SetHammadnessPreRampAttackDisplay(fromTotal);
        cardStatsDisplay.SetSuppressHammadnessPredictionDuringSequenceReveal(false);
        cardStatsDisplay.UpdateDisplay();

        await Task.Delay(500, cancellationToken);

        int rolled = HammadnessRules.RollRandomAttackPower();
        battleManager.SetHammadnessRollSnapshot(rolled);

        SoundEffectPlayer.I?.Play(OrbDefenseReactionFlow.OrbGaugeRecoverySe);
        BattleUIManager.I?.PlayFullscreenWhiteFlashMs(50f);

        int toTotal = cardStatsDisplay.ComputeHammadnessRampTo(selectedCards, atk);
        CardData hammadnessCard = HammadnessRules.FindFirstHammadnessCard(selectedCards);

        await cardStatsDisplay.PlayHammadnessAttackRampAsync(
            selectedCards,
            atk,
            hammadnessCard,
            rolled,
            fromTotal,
            toTotal,
            0.2f,
            cancellationToken);
    }

    // ==================== 大魔法（ArchMagic） ====================

    /// <summary>
    /// 大魔法の「詠唱開始」フロー。カード演出→500ms→ポップアップ「魔力が吹き荒れる」+SE→200ms→背景差し替え（1000ms）→TurnEnd。
    /// 「残り x ターン」オーバーレイは出さない（次の AttackSelect で <see cref="RunArchMagicCastingTurnAsync"/> が担当）。
    /// </summary>
    public async Task StartArchMagicCastIntroAsync(CardData archMagicCard, Side side, CancellationToken cancellationToken)
    {
        if (archMagicCard == null) return;

        IsArchMagicCastIntroInProgress = true;
        try
        {
            await StartArchMagicCastIntroCoreAsync(archMagicCard, side, cancellationToken);
        }
        finally
        {
            IsArchMagicCastIntroInProgress = false;
            BattleUIManager.I?.HideArchMagicCastOverlayImmediate();
        }
    }

    private async Task StartArchMagicCastIntroCoreAsync(CardData archMagicCard, Side side, CancellationToken cancellationToken)
    {
        // 表示ゾーンクリア
        BattleUIManager.I?.ClearAllSelections();
        BattleUIManager.I?.HideAllCardDetails();
        cardStatsDisplay?.SetSequenceCards(new List<CardData>(), "攻撃");
        await Task.Delay(300, cancellationToken);

        // カード表示
        BattleUIManager.I?.ShowCardDetail(archMagicCard, side);
        cardStatsDisplay?.SetSequenceCards(new List<CardData> { archMagicCard }, "攻撃");
        cardStatsDisplay?.UpdateDisplay();
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");

        // MP 消費＋手札から除去（大魔法は MagicPool に入れない）
        var atk = side == Side.Player ? battleManager.GetPlayerStatus() : battleManager.GetEnemyStatus();
        if (atk != null && archMagicCard.mpCost > 0)
        {
            atk.UseMP(archMagicCard.mpCost);
            BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
        }
        var ownHand = side == Side.Player ? battleManager.playerHand : battleManager.cpuHand;
        if (ownHand == battleManager.playerHand)
        {
            if (archMagicCard.cardUI != null)
            {
                int slotIndex = archMagicCard.cardUI.transform.GetSiblingIndex();
                handRefill?.RecordPlayerUseSlot(slotIndex);
            }
        }
        else
            handRefill?.RecordEnemyUse(archMagicCard);
        battleProcessor.UseCard(archMagicCard, ownHand);

        // 500ms インターバル
        await Task.Delay(500, cancellationToken);

        // 「魔力が吹き荒れる」ポップアップ + 詠唱開始 SE
        SoundEffectPlayer.I?.Play("Assets/SE/大魔法詠唱開始.mp3");
        ImportantPopup castPopup = BattleUIManager.I?.ShowImportantPopup("魔力が吹き荒れる", new Color(0.75f, 0.45f, 0.95f), side);
        float castLife = castPopup != null ? castPopup.SequenceLifetimeSeconds : ImportantPopup.DefaultSequenceLifetimeIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(castLife, cancellationToken);

        // 200ms インターバル
        await Task.Delay(200, cancellationToken);

        // 背景を 1000ms かけて差し替え（アルファフェード）
        Sprite bgSprite = ArchMagicRules.GetBackgroundSprite(archMagicCard);
        if (bgSprite != null && BattleBgmController.Instance != null)
            await BattleBgmController.Instance.CrossfadeToArchMagicBackgroundAsync(bgSprite, 1000, cancellationToken);

        // 詠唱状態を開始（効果対象はこの時点の TOTAL ターゲットで固定）
        int turns = ArchMagicRules.GetCastTurns(archMagicCard);
        var ps = battleManager.GetPlayerStatus();
        var es = battleManager.GetEnemyStatus();
        PlayerStatus spellTarget;
        if (side == Side.Player)
        {
            spellTarget = battleManager.IsPlayerSelfAttackTargetMode ? ps : es;
            battleManager.ClearPlayerSelfAttackTargetMode();
        }
        else if (battleManager.GetEnemyAI() is RemotePlayerAgent remoteAgent && remoteAgent.LastAttackTargetSelf)
            spellTarget = es;
        else
            spellTarget = ps;
        atk?.BeginArchMagicCasting(archMagicCard, turns, spellTarget);

        Side chanterSide = side;
        BattleUIManager.I?.ShowArchMagicBarrier(chanterSide, atk != null ? atk.archMagicBarrierRemaining : 0);

        // 表示を片付けて TurnEnd
        BattleUIManager.I?.HideAllCardDetails();
        cardStatsDisplay?.ClearSequenceCards();
        battleManager.SetCurrentAttackCard(null);
        cardStatsDisplay?.UpdateDisplay();
        battleManager.SetGameState(GameState.CombatResolvePhase);
    }

    /// <summary>
    /// 大魔法の詠唱ターン演出（自分ターン開始時）。
    /// 「魔力を集中しろ！」ポップアップ→200ms→中央フェードイン→200ms→残り -1 +SE→残りが 0 なら発動。
    /// </summary>
    public async Task RunArchMagicCastingTurnAsync(PlayerStatus owner, Side ownerSide, CancellationToken cancellationToken)
    {
        if (IsArchMagicCastIntroInProgress || IsArchMagicCountdownInProgress)
            return;

        if (owner == null || !owner.IsCastingArchMagic)
        {
            battleManager.SetGameState(GameState.CombatResolvePhase);
            return;
        }

        IsArchMagicCountdownInProgress = true;
        try
        {
            await RunArchMagicCastingTurnCoreAsync(owner, ownerSide, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            BattleUIManager.I?.HideArchMagicCastOverlayImmediate();
            if (owner != null && owner.IsCastingArchMagic
                && battleManager.CurrentState == GameState.AttackPhase)
            {
                battleManager.SetGameState(GameState.CombatResolvePhase);
            }
            throw;
        }
        finally
        {
            IsArchMagicCountdownInProgress = false;
        }
    }

    private async Task RunArchMagicCastingTurnCoreAsync(PlayerStatus owner, Side ownerSide, CancellationToken cancellationToken)
    {
        if (owner == null || !owner.IsCastingArchMagic)
        {
            battleManager.SetGameState(GameState.CombatResolvePhase);
            return;
        }

        BattleUIManager.I?.HideArchMagicCastOverlayImmediate();

        var card = owner.archMagicCastingCard;

        // 1. ポップアップ + SE
        SoundEffectPlayer.I?.Play("Assets/SE/power19.wav");
        ImportantPopup focusPopup = BattleUIManager.I?.ShowImportantPopup("魔力を集中しろ！", new Color(0.55f, 0.7f, 0.95f), ownerSide);
        float focusLife = focusPopup != null ? focusPopup.SequenceLifetimeSeconds : ImportantPopup.DefaultSequenceLifetimeIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(focusLife, cancellationToken);

        // ポップアップ待ち中にキャンセルされていたらここで終了
        if (owner.archMagicCancelPending || !owner.IsCastingArchMagic)
        {
            Side ownerSideCancel = ownerSide;
            BattleUIManager.I?.HideArchMagicBarrier(ownerSideCancel);
            battleManager.SetGameState(GameState.CombatResolvePhase);
            return;
        }

        // 2. インターバル（詠唱カウントダウン全体を長めに）
        await Task.Delay(400, cancellationToken);

        // 3. ディム＋中央フェードイン（残り= current）
        int remainingBefore = owner.archMagicRemainingTurns;
        int barrierBefore = owner.archMagicBarrierRemaining;
        Sprite spr = card != null ? card.cardImage : null;
        if (BattleUIManager.I != null)
            await BattleUIManager.I.FadeInArchMagicCastOverlayAsync(spr, remainingBefore, barrierBefore, cancellationToken);

        // 4. 視認用インターバル
        await Task.Delay(400, cancellationToken);

        // 5. 残りターンを -1 + SE
        owner.DecrementArchMagicRemainingTurns();
        BattleUIManager.I?.UpdateArchMagicCastOverlayRemaining(owner.archMagicRemainingTurns, owner.archMagicBarrierRemaining);
        SoundEffectPlayer.I?.Play("Assets/SE/心臓の鼓動2.mp3");

        // カウントダウン数値・SE の視認用
        await Task.Delay(1200, cancellationToken);

        // 6. カウントダウン終了時はフェードアウト（次の AttackSelect で再度フェードイン）
        if (BattleUIManager.I != null)
            await BattleUIManager.I.FadeOutArchMagicCastOverlayAsync(cancellationToken);

        // 7. 残りが 0 になったら発動フローへ、そうでなければ TurnEnd
        if (owner.archMagicRemainingTurns <= 0)
        {
            await Task.Delay(350, cancellationToken);
            string releaseName = ArchMagicRules.GetReleaseDisplayName(card);
            SoundEffectPlayer.I?.Play("Assets/SE/教会の鐘1.mp3");
            ImportantPopup rel = BattleUIManager.I?.ShowImportantPopup($"【{releaseName}】解放", new Color(0.95f, 0.85f, 0.3f), ownerSide);
            float rlife = rel != null ? rel.SequenceLifetimeSeconds : ImportantPopup.DefaultSequenceLifetimeIfUnknown;
            await DamagePopup.WaitAfterPopupLifetimeAsync(rlife, cancellationToken);

            await RunArchMagicActivationAsync(owner, ownerSide, card, cancellationToken);
        }
        else
        {
            battleManager.SetGameState(GameState.CombatResolvePhase);
        }
    }

    /// <summary>
    /// 大魔法の発動フロー。カード表示→相手の DefenseSelect→戦闘解決。反射・無効化は無視。
    /// </summary>
    public async Task RunArchMagicActivationAsync(PlayerStatus owner, Side ownerSide, CardData card, CancellationToken cancellationToken)
    {
        if (card == null)
        {
            var bgm = BattleBgmController.Instance;
            if (bgm != null)
                await bgm.CrossfadeFromArchMagicBackgroundAsync(1000, cancellationToken);
            owner?.ClearArchMagicCastingState();
            battleManager.SetGameState(GameState.CombatResolvePhase);
            return;
        }

        // CardDisplayPanel に Prefab を表示（詠唱中は手札選択が無効のため ShowCardDetail は使わない）
        BattleUIManager.I?.ShowInterventionAttackSheet(card, ownerSide);
        BattleUIManager.I?.PlayFullscreenWhiteFlashMs(50f);
        cardStatsDisplay?.SetSequenceCards(new List<CardData> { card }, "攻撃");
        cardStatsDisplay?.UpdateDisplay();
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        battleManager.SetCurrentAttackCard(card);

        await Task.Delay(500, cancellationToken);

        var attackCards = new List<CardData> { card };
        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();
        PlayerStatus def = owner.archMagicEffectTarget;
        if (def == null || (!ReferenceEquals(def, player) && !ReferenceEquals(def, enemy)))
            def = ReferenceEquals(owner, player) ? enemy : player;
        var defHand = ReferenceEquals(def, player) ? battleManager.playerHand : battleManager.cpuHand;

        bool enteredOnlineEnemyDefense = false;
        try
        {
            if (ownerSide == Side.Player)
            {
                // プレイヤー発動：通常の攻撃ルートに乗せる（反射・無効化は内部でスキップ）
                await ResolvePlayerAttackCombatAsync(attackCards, owner, def, defHand, cancellationToken);
            }
            else if (battleManager.IsOnlineMatch)
            {
                battleManager.SetGameState(GameState.DefensePhase);
                enteredOnlineEnemyDefense = true;
            }
            else
            {
                // 敵発動（オフライン AI は大魔法を選ばない想定の単純解決）
                await battleProcessor.ResolveCombatAsync(attackCards, (CardData)null, owner, def, defHand, skipHitCheck: true);
            }
        }
        finally
        {
            owner?.ClearArchMagicCastingState();
            BattleUIManager.I?.HideArchMagicCastOverlayImmediate();
            BattleUIManager.I?.HideArchMagicBarrier(ownerSide);
            var bgm = BattleBgmController.Instance;
            if (bgm != null)
                await bgm.CrossfadeFromArchMagicBackgroundAsync(1000, cancellationToken);
        }

        if (enteredOnlineEnemyDefense)
            return;

        BattleUIManager.I?.HideAllCardDetails();
        cardStatsDisplay?.ClearSequenceCards();
        battleManager.SetCurrentAttackCard(null);
        cardStatsDisplay?.UpdateDisplay();
        battleManager.SetGameState(GameState.CombatResolvePhase);
    }

    /// <summary>
    /// 詠唱キャンセル演出。「詠唱中断」ポップアップ → 背景を 1000ms で復帰。
    /// バリア破壊演出は BarriarDamage 側で再生済みの場合、割れる SE は省略する。
    /// </summary>
    public async Task RunArchMagicCastCancelAsync(PlayerStatus owner, CancellationToken cancellationToken)
    {
        if (owner == null) return;

        bool breakFxAlreadyPlayed = owner.archMagicBarrierBreakFxPlayed;
        owner.archMagicCancelPending = false;
        owner.archMagicBarrierBreakFxPlayed = false;

        BattleUIManager.I?.HideArchMagicCastOverlayImmediate();
        Side ownerSide = ReferenceEquals(owner, battleManager.GetPlayerStatus()) ? Side.Player : Side.Enemy;
        BattleUIManager.I?.HideArchMagicBarrier(ownerSide);

        if (!breakFxAlreadyPlayed)
            SoundEffectPlayer.I?.Play("Assets/SE/ガラスが割れる2.mp3");

        float life = BattleUIManager.I != null
            ? BattleUIManager.I.ShowMessagePopupForTarget(owner, "詠唱中断", new Color(0.85f, 0.25f, 0.2f))
            : DamagePopup.DefaultFadeDurationIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(life, cancellationToken);

        var bgmRestore = BattleBgmController.Instance;
        if (bgmRestore != null)
            await bgmRestore.CrossfadeFromArchMagicBackgroundAsync(1000, cancellationToken);

        // 詠唱状態は TakeDamage 側で既にクリア済み
    }

    /// <summary>
    /// 顕現スキル：プレースホルダー演出後にカード表示→1000ms→戦闘解決（大魔法系の反射ルール）。
    /// </summary>
    public async Task RunManifestationSkillSequenceAsync(PlayerStatus summoner, PlayerStatus opponent)
    {
        if (battleManager == null || summoner == null || opponent == null) return;
        var summon = summoner.summonData;
        var template = summon != null ? summon.manifestationCard : null;
        if (template == null)
        {
            Debug.LogWarning("[CardSequenceManager] manifestationCard が未設定です");
            return;
        }

        var card = battleManager.cardDealer != null
            ? battleManager.cardDealer.InstantiateCardFromTemplate(template)
            : null;
        if (card == null) return;

        Side side = ReferenceEquals(summoner, battleManager.GetPlayerStatus()) ? Side.Player : Side.Enemy;

        BattleUIManager.I?.ShowInterventionAttackSheet(card, side);
        BattleUIManager.I?.PlayFullscreenWhiteFlashMs(50f);
        cardStatsDisplay?.SetSequenceCards(new List<CardData> { card }, "攻撃", side);
        cardStatsDisplay?.UpdateDisplay();
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        battleManager.SetCurrentAttackCard(card);

        await Task.Delay(1000, CancellationToken.None);

        var attackCards = new List<CardData> { card };

        if (side == Side.Player)
        {
            bool finished = await ResolvePlayerAttackCombatAsync(
                attackCards, summoner, opponent, battleManager.cpuHand, CancellationToken.None);
            battleManager.ClearPlayerSelfAttackTargetMode();
            if (!finished || battleManager.IsGameEndTriggered) return;
            await RunAfterCombatSharedCleanupAsync(CancellationToken.None);
        }
        else
        {
            await battleManager.PresentEnemyManifestationAttackToPlayerDefenseAsync(attackCards, CancellationToken.None);
        }
    }

    /// <summary>
    /// 戦闘用攻撃カードを取得
    /// </summary>
    private List<CardData> GetAttackCardsForCombat(List<CardData> selectedCards = null)
    {
        if (battleManager.AttackerPublic == PlayerType.Player)
        {
            Debug.Log("[CardSequenceManager] プレイヤーの攻撃カードを取得中...");
            
            // selectedCardsパラメータが提供されている場合はそれを使用
            if (selectedCards != null)
            {
                var attackCards = new List<CardData>();
                foreach (var card in selectedCards)
                {
                    if (card.cardType == CardType.Attack || card.cardType == CardType.Magic
                        || card.cardType == CardType.Ultimate
                        || card.cardType == CardType.ArchMagic
                        || card.cardType == CardType.Recovery
                        || card.cardType == CardType.Special)
                    {
                        attackCards.Add(card);
                    }
                }
                Debug.Log($"[CardSequenceManager] selectedCardsから取得した攻撃カード数: {attackCards.Count}");
                return attackCards;
            }
            
            var uiAttackCards = BattleUIManager.I?.GetSelectedAttackCards() ?? new List<CardData>();
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            if (uiAttackCards.Count == 0 && currentAttackCard != null)
            {
                uiAttackCards = new List<CardData> { currentAttackCard };
            }
            return uiAttackCards;
        }
        else
        {
            return battleManager.GetAttackCardsForCombatPublic();
        }
    }
}
