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

        if (cardType == "攻撃" && MagicalExplosionRules.ContainsMagicalExplosion(selectedCards))
            cardStatsDisplay?.SetSuppressMagicalExplosionPredictionDuringSequenceReveal(true);

        battleManager.ClearMagicalExplosionComboMpPoolSnapshot();
        battleManager.ClearConfusionAttackTargetResolvedForDisplay();

        _magicPanelBonusDrawsPendingReveal.Clear();

        // 演出中のカードリストを初期化
        cardStatsDisplay?.SetSequenceCards(new List<CardData>(), cardType);

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

        // クリア後のインターバル（まっさらな状態を維持）
        await Task.Delay(300, cancellationToken);

        // ②カードを順次表示（0.5秒インターバル）
        for (int i = 0; i < selectedCards.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (cardType == "攻撃" && MagicalExplosionRules.ContainsMagicalExplosion(selectedCards))
                    cardStatsDisplay?.SetSuppressMagicalExplosionPredictionDuringSequenceReveal(false);
                return;
            }

            var card = selectedCards[i];
            BattleUIManager.I?.ShowCardDetail(card, side);
            
            var sequenceCards = new List<CardData>(selectedCards.GetRange(0, i + 1));
            cardStatsDisplay?.SetSequenceCards(sequenceCards, cardType);
            cardStatsDisplay?.UpdateDisplay();
            
            // カード表示効果音を再生（Addressables使用）
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            
            Debug.Log($"[CardSequenceManager] {cardType}カード表示: {card.cardName} ({i + 1}/{selectedCards.Count})");
            
            // すべてのカード表示後に0.5秒待機（最後のカードも選択枠を表示）
            await Task.Delay(500, cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            if (cardType == "攻撃" && MagicalExplosionRules.ContainsMagicalExplosion(selectedCards))
                cardStatsDisplay?.SetSuppressMagicalExplosionPredictionDuringSequenceReveal(false);
            return;
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

        // ③カードの処理
        await ProcessCardsAsync(selectedCards, cardType);

        // 選択状態をクリア（ProcessCardsで既に設定済み）
        BattleUIManager.I?.ClearAllSelections();
        cardStatsDisplay?.UpdateDisplay();

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
            bool targetSelf = UnityEngine.Random.Range(0, 2) == 0;
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

        List<CardData> attackCards = GetAttackCardsForCombat(selectedCards);

        if (cardType == "攻撃")
        {
            bool finished = await ResolvePlayerAttackCombatAsync(attackCards, atk, def, defHand, cancellationToken);
            battleManager.ClearPlayerSelfAttackTargetMode();
            if (!finished)
                return;
        }
        else
        {
            bool playerPhysicalReflect = selectedCards.Count == 1
                && ReflectionRules.IsPhysicalReflectionCard(selectedCards[0])
                && battleManager.DefenderPublic == PlayerType.Player
                && ReflectionRules.CanReflectPhysical(attackCards);
            bool playerMagicReflect = selectedCards.Count == 1
                && ReflectionRules.IsMagicReflectionCard(selectedCards[0])
                && battleManager.DefenderPublic == PlayerType.Player
                && ReflectionRules.CanReflectMagic(attackCards);
            bool playerPhysicalBlock = selectedCards.Count == 1
                && BlockingRules.IsPhysicalBlockingCard(selectedCards[0])
                && battleManager.DefenderPublic == PlayerType.Player
                && BlockingRules.CanBlockPhysical(attackCards);

            if (playerPhysicalReflect || playerMagicReflect)
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

        await RevealMagicPanelBonusDrawsAsync(cancellationToken);

        if (cancellationToken.IsCancellationRequested) return;

        // ダメージ処理完了後、全カード表示と演出リストをクリア
        BattleUIManager.I?.HideAllCardDetails();
        cardStatsDisplay?.ClearSequenceCards();
        battleManager.SetCurrentAttackCard(null);
        cardStatsDisplay?.UpdateDisplay();

        // カード確定後の処理
        battleManager.SetGameState(GameState.CombatResolvePhase);
        battleManager.ClearMagicalExplosionComboMpPoolSnapshot();
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
        CancellationToken cancellationToken)
    {
        var primary = HitRateRules.GetPrimaryForHitRate(attackCards);
        int finalPct = HitRateRules.ComputeFinalHitPercent(primary, atk, def);
        bool hit = HitRateRules.RollHit(finalPct);

        if (!hit)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ニュッ1.mp3");
            BattleUIManager.I?.ShowMissPopup(def);
            await DamagePopup.WaitAfterPopupLifetimeAsync(DamagePopup.DefaultFadeDurationIfUnknown, cancellationToken);
            await RevealMagicPanelBonusDrawsAsync(cancellationToken);
            BattleUIManager.I?.HideAllCardDetails();
            cardStatsDisplay?.ClearSequenceCards();
            battleManager.SetCurrentAttackCard(null);
            cardStatsDisplay?.UpdateDisplay();
            battleManager.SetGameState(GameState.CombatResolvePhase);
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

        bool meGodCombo = MagicalExplosionRules.ContainsMagicalExplosion(attackCards)
            && GodrageRules.IsGodrageDoublingCombo(attackCards);
        bool godRageOnlyCombo = GodrageRules.IsGodrageDoublingCombo(attackCards)
            && !MagicalExplosionRules.ContainsMagicalExplosion(attackCards);

        if (atk == battleManager.GetPlayerStatus() && cardStatsDisplay != null)
        {
            if (meGodCombo)
            {
                await Task.Delay(1000, cancellationToken);
                int fromAtk = cardStatsDisplay.ComputeGodRageRampFrom(attackCards, atk, def);
                int toAtk = cardStatsDisplay.ComputeGodRageRampTo(attackCards, atk, def);
                await cardStatsDisplay.PlayGodRageAttackRampAsync(attackCards, atk, def, fromAtk, toAtk, 0.2f, cancellationToken);
            }
            else if (godRageOnlyCombo)
            {
                await Task.Delay(500, cancellationToken);
                int fromAtk = cardStatsDisplay.ComputeGodRageRampFrom(attackCards, atk, def);
                int toAtk = cardStatsDisplay.ComputeGodRageRampTo(attackCards, atk, def);
                await cardStatsDisplay.PlayGodRageAttackRampAsync(attackCards, atk, def, fromAtk, toAtk, 0.2f, cancellationToken);
            }
        }

        bool selfAttack = ReferenceEquals(atk, def);
        if (selfAttack)
        {
            await battleProcessor.ResolveCombatAsync(attackCards, (CardData)null, atk, def, defHand, skipHitCheck: true);
            return true;
        }

        await battleManager.PickAndDisplayEnemyDefenseAfterPlayerHitAsync(attackCards);

        var selectedDefenseCard = battleManager.GetSelectedDefenseCard();
        bool showYurusuDuringCombat =
            battleManager.DefenderPublic == PlayerType.Enemy && selectedDefenseCard == null && BattleUIManager.I != null;
        if (showYurusuDuringCombat)
            BattleUIManager.I.ShowYurusuDisplay();

        // 大魔法（ArchMagic）は反射・無効化を受けない
        bool archMagicActivation = ArchMagicRules.ContainsArchMagic(attackCards);
        bool enemyPhysicalReflect = !archMagicActivation
            && selectedDefenseCard != null
            && ReflectionRules.IsPhysicalReflectionCard(selectedDefenseCard)
            && ReflectionRules.CanReflectPhysical(attackCards);
        bool enemyMagicReflect = !archMagicActivation
            && selectedDefenseCard != null
            && ReflectionRules.IsMagicReflectionCard(selectedDefenseCard)
            && ReflectionRules.CanReflectMagic(attackCards);
        bool enemyPhysicalBlock = !archMagicActivation
            && selectedDefenseCard != null
            && BlockingRules.IsPhysicalBlockingCard(selectedDefenseCard)
            && BlockingRules.CanBlockPhysical(attackCards);

        try
        {
            if (enemyPhysicalReflect || enemyMagicReflect)
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
            else
            {
                await battleProcessor.ResolveCombatAsync(attackCards, selectedDefenseCard, atk, def, defHand, skipHitCheck: true);
            }
        }
        finally
        {
            if (showYurusuDuringCombat)
                BattleUIManager.I?.HideYurusuButton();
        }

        if (selectedDefenseCard != null && !enemyPhysicalReflect && !enemyMagicReflect && !enemyPhysicalBlock)
        {
            handRefill?.RecordEnemyUse(selectedDefenseCard);
            battleProcessor.UseCard(selectedDefenseCard, defHand);
        }

        return true;
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
    /// 単一カードの処理（攻撃・防御共通）
    /// </summary>
    private async System.Threading.Tasks.Task ProcessSingleCardAsync(CardData card, string cardType)
    {
        // 魔法カード（単独型・組み合わせ型とも）の場合は特殊処理
        if (card.cardType == CardType.Magic)
        {
            Debug.Log($"[CardSequenceManager] 魔法カード処理: {card.cardName} (組み合わせ={card.isCombinationMagic})");
            bool isFromHand = BattleUIManager.I == null
                || !BattleUIManager.I.IsPlayerMagicCardUiOnMagicPanel(card);
            await ApplyMagicCardToPoolAsync(card, isFromHand);
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
    private async System.Threading.Tasks.Task ApplyMagicCardToPoolAsync(CardData card, bool isFromHand)
    {
        if (MagicPoolManager.I == null) return;

        // MP消費（眼精疲労で倍率）
        var playerStatus = battleManager.GetPlayerStatus();
        if (playerStatus != null && card.mpCost > 0)
        {
            int pay = playerStatus.GetEffectiveMagicMpCost(card.mpCost);
            playerStatus.UseMP(pay);
            Debug.Log($"[CardSequenceManager] MP消費: {card.cardName} -{pay}MP (残り={playerStatus.currentMP})");
            BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
        }

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
                await BattleUIManager.I.PlayMagicFlyHandToPanelAsync(card, card.cardUI.transform as RectTransform, slot);
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
            await ApplyMagicCardToPoolAsync(magic, isFromHand);
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

    // ==================== 大魔法（ArchMagic） ====================

    /// <summary>
    /// 大魔法の「詠唱開始」フロー。カード演出→500ms→ポップアップ「魔力が吹き荒れる」+SE→200ms→背景差し替え（1000ms）→TurnEnd。
    /// </summary>
    public async Task StartArchMagicCastIntroAsync(CardData archMagicCard, Side side, CancellationToken cancellationToken)
    {
        if (archMagicCard == null) return;

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
        if (side == Side.Player && archMagicCard.cardUI != null)
        {
            int slotIndex = archMagicCard.cardUI.transform.GetSiblingIndex();
            handRefill?.RecordPlayerUseSlot(slotIndex);
        }
        else
        {
            handRefill?.RecordEnemyUse(archMagicCard);
        }
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

        // 詠唱状態を開始
        int turns = ArchMagicRules.GetCastTurns(archMagicCard);
        atk?.BeginArchMagicCasting(archMagicCard, turns);

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
        if (owner == null || !owner.IsCastingArchMagic)
        {
            battleManager.SetGameState(GameState.CombatResolvePhase);
            return;
        }

        var card = owner.archMagicCastingCard;

        // 1. ポップアップ + SE
        SoundEffectPlayer.I?.Play("Assets/SE/power19.wav");
        ImportantPopup focusPopup = BattleUIManager.I?.ShowImportantPopup("魔力を集中しろ！", new Color(0.55f, 0.7f, 0.95f), ownerSide);
        float focusLife = focusPopup != null ? focusPopup.SequenceLifetimeSeconds : ImportantPopup.DefaultSequenceLifetimeIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(focusLife, cancellationToken);

        // ポップアップ待ち中にキャンセルされていたらここで終了
        if (owner.archMagicCancelPending || !owner.IsCastingArchMagic)
        {
            battleManager.SetGameState(GameState.CombatResolvePhase);
            return;
        }

        // 2. インターバル（詠唱カウントダウン全体を長めに）
        await Task.Delay(400, cancellationToken);

        // 3. ディム＋中央フェードイン（残り= current）
        int remainingBefore = owner.archMagicRemainingTurns;
        Sprite spr = card != null ? card.cardImage : null;
        if (BattleUIManager.I != null)
            await BattleUIManager.I.FadeInArchMagicCastOverlayAsync(spr, remainingBefore, 520, cancellationToken);

        // 4. 視認用インターバル
        await Task.Delay(400, cancellationToken);

        // 5. 残りターンを -1 + SE
        owner.DecrementArchMagicRemainingTurns();
        BattleUIManager.I?.UpdateArchMagicCastOverlayRemaining(owner.archMagicRemainingTurns);
        SoundEffectPlayer.I?.Play("Assets/SE/心臓の鼓動2.mp3");

        // カウントダウン数値・SE の視認用
        await Task.Delay(1200, cancellationToken);

        // 6. オーバーレイをフェードアウト
        if (BattleUIManager.I != null)
            await BattleUIManager.I.FadeOutArchMagicCastOverlayAsync(480, cancellationToken);

        // 7. 残りが 0 になったら発動フローへ、そうでなければ TurnEnd
        if (owner.archMagicRemainingTurns <= 0)
        {
            await Task.Delay(350, cancellationToken);
            string releaseName = ArchMagicRules.GetReleaseDisplayName(card);
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
        cardStatsDisplay?.SetSequenceCards(new List<CardData> { card }, "攻撃");
        cardStatsDisplay?.UpdateDisplay();
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        battleManager.SetCurrentAttackCard(card);

        await Task.Delay(500, cancellationToken);

        var attackCards = new List<CardData> { card };
        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();
        var def = ReferenceEquals(owner, player) ? enemy : player;
        var defHand = ReferenceEquals(owner, player) ? battleManager.cpuHand : battleManager.playerHand;

        try
        {
            if (ownerSide == Side.Player)
            {
                // プレイヤー発動：通常の攻撃ルートに乗せる（反射・無効化は内部でスキップ）
                await ResolvePlayerAttackCombatAsync(attackCards, owner, def, defHand, cancellationToken);
            }
            else
            {
                // 敵発動（将来の拡張用・現状は AI が大魔法を選ばない想定の単純解決）
                await battleProcessor.ResolveCombatAsync(attackCards, (CardData)null, owner, def, defHand, skipHitCheck: true);
            }
        }
        finally
        {
            owner?.ClearArchMagicCastingState();
            var bgm = BattleBgmController.Instance;
            if (bgm != null)
                await bgm.CrossfadeFromArchMagicBackgroundAsync(1000, cancellationToken);
        }

        BattleUIManager.I?.HideAllCardDetails();
        cardStatsDisplay?.ClearSequenceCards();
        battleManager.SetCurrentAttackCard(null);
        cardStatsDisplay?.UpdateDisplay();
        battleManager.SetGameState(GameState.CombatResolvePhase);
    }

    /// <summary>
    /// 詠唱キャンセル演出。「詠唱失敗」ポップアップ + ガラスが割れる2.mp3 → 背景を 1000ms で復帰。
    /// ダメージ適用側で <see cref="PlayerStatus.archMagicCancelPending"/> が立っているときに呼ぶ。
    /// </summary>
    public async Task RunArchMagicCastCancelAsync(PlayerStatus owner, CancellationToken cancellationToken)
    {
        if (owner == null) return;
        owner.archMagicCancelPending = false;

        SoundEffectPlayer.I?.Play("Assets/SE/ガラスが割れる2.mp3");
        DamagePopup popup = BattleUIManager.I?.ShowInfoPopupOnCardPanel("詠唱失敗", new Color(0.85f, 0.25f, 0.2f));
        float life = popup != null ? popup.fadeDuration : DamagePopup.DefaultFadeDurationIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(life, cancellationToken);

        var bgmRestore = BattleBgmController.Instance;
        if (bgmRestore != null)
            await bgmRestore.CrossfadeFromArchMagicBackgroundAsync(1000, cancellationToken);

        // 詠唱状態は TakeDamage 側で既にクリア済み
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
                        || card.isPrimaryAttack || card.isAdditionalAttack)
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
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            Debug.Log($"[CardSequenceManager] 敵の攻撃カード: {currentAttackCard?.cardName ?? "なし"}");
            return new List<CardData> { currentAttackCard };
        }
    }
}
