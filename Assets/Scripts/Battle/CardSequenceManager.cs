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
            if (cancellationToken.IsCancellationRequested) return;

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

        if (cancellationToken.IsCancellationRequested) return;

        // ③カードの処理
        await ProcessCardsAsync(selectedCards, cardType);

        // 選択状態をクリア（ProcessCardsで既に設定済み）
        BattleUIManager.I?.ClearAllSelections();
        cardStatsDisplay?.UpdateDisplay();

        // ④戦闘解決処理
        var atk = (battleManager.AttackerPublic == PlayerType.Player) ? battleManager.GetPlayerStatus() : battleManager.GetEnemyStatus();
        var def = (battleManager.DefenderPublic == PlayerType.Player) ? battleManager.GetPlayerStatus() : battleManager.GetEnemyStatus();
        var defHand = (battleManager.DefenderPublic == PlayerType.Player) ? battleManager.playerHand : battleManager.cpuHand;

        List<CardData> attackCards = GetAttackCardsForCombat(selectedCards);

        if (cardType == "攻撃")
        {
            bool finished = await ResolvePlayerAttackCombatAsync(attackCards, atk, def, defHand, cancellationToken);
            if (!finished)
                return;
        }
        else
        {
            if (selectedCards.Count == 1
                && ReflectionRules.IsPhysicalReflectionCard(selectedCards[0])
                && battleManager.DefenderPublic == PlayerType.Player
                && ReflectionRules.CanReflectPhysical(attackCards))
            {
                await PhysicalReflectionFlow.RunPlayerInitiatedAsync(
                    battleManager,
                    battleProcessor,
                    handRefill,
                    battleManager.GetEnemyAI(),
                    attackCards,
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
        battleManager.SetGameState(GameState.TurnEnd);
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
            await Task.Delay(TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown), cancellationToken);
            await Task.Delay(DamagePopup.PostPopupIntervalMs, cancellationToken);
            await RevealMagicPanelBonusDrawsAsync(cancellationToken);
            BattleUIManager.I?.HideAllCardDetails();
            cardStatsDisplay?.ClearSequenceCards();
            battleManager.SetCurrentAttackCard(null);
            cardStatsDisplay?.UpdateDisplay();
            battleManager.SetGameState(GameState.TurnEnd);
            return false;
        }

        if (finalPct < 100)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/小パンチ.mp3");
            float sec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowCombatHitConfirmedPopup(def)
                : DamagePopup.DefaultFadeDurationIfUnknown;
            await Task.Delay(TimeSpan.FromSeconds(sec), cancellationToken);
            await Task.Delay(DamagePopup.PostPopupIntervalMs, cancellationToken);
        }

        await battleManager.PickAndDisplayEnemyDefenseAfterPlayerHitAsync(attackCards);

        var selectedDefenseCard = battleManager.GetSelectedDefenseCard();
        bool showYurusuDuringCombat =
            battleManager.DefenderPublic == PlayerType.Enemy && selectedDefenseCard == null && BattleUIManager.I != null;
        if (showYurusuDuringCombat)
            BattleUIManager.I.ShowYurusuDisplay();

        try
        {
            await battleProcessor.ResolveCombatAsync(attackCards, selectedDefenseCard, atk, def, defHand, skipHitCheck: true);
        }
        finally
        {
            if (showYurusuDuringCombat)
                BattleUIManager.I?.HideYurusuButton();
        }

        if (selectedDefenseCard != null)
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
        foreach (var magic in magicCards)
        {
            bool isFromHand = battleManager.playerHand.Contains(magic);
            await ApplyMagicCardToPoolAsync(magic, isFromHand);
            Debug.Log($"[CardSequenceManager] 魔法カード {magic.cardName} をプール処理 (fromHand={isFromHand}, combination={magic.isCombinationMagic})");
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
            bool isFromHand = battleManager.playerHand.Contains(card);
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
