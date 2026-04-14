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
        ProcessCards(selectedCards, cardType);

        // 選択状態をクリア（ProcessCardsで既に設定済み）
        BattleUIManager.I?.ClearAllSelections();
        cardStatsDisplay?.UpdateDisplay();

        // ④戦闘解決処理
        var atk = (battleManager.AttackerPublic == PlayerType.Player) ? battleManager.GetPlayerStatus() : battleManager.GetEnemyStatus();
        var def = (battleManager.DefenderPublic == PlayerType.Player) ? battleManager.GetPlayerStatus() : battleManager.GetEnemyStatus();
        var defHand = (battleManager.DefenderPublic == PlayerType.Player) ? battleManager.playerHand : battleManager.cpuHand;

        List<CardData> attackCards = GetAttackCardsForCombat(selectedCards);

        // 戦闘解決を呼び出し
        if (cardType == "攻撃")
        {
            // 攻撃カードの場合、防御カードは単一またはnull
            var selectedDefenseCard = battleManager.GetSelectedDefenseCard();
            await battleProcessor.ResolveCombatAsync(attackCards, selectedDefenseCard, atk, def, defHand);
        }
        else
        {
            // 防御カードの場合、複数防御カード対応
            await battleProcessor.ResolveCombatAsync(attackCards, selectedCards, atk, def, defHand);
        }

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
    /// カード処理（攻撃・防御共通）
    /// </summary>
    private void ProcessCards(List<CardData> cards, string cardType)
    {
        if (cards.Count > 1)
        {
            Debug.Log($"[CardSequenceManager] 複数{cardType}カード選択中: {cards.Count}枚。全てのカードを処理します。");
            ProcessMultipleCards(cards, cardType);
        }
        else
        {
            Debug.Log($"[CardSequenceManager] 単一{cardType}カード選択中。カードを処理します。");
            ProcessSingleCard(cards[0], cardType);
        }
    }

    /// <summary>
    /// 複数カードの処理（攻撃・防御共通）
    /// </summary>
    private void ProcessMultipleCards(List<CardData> cards, string cardType)
    {
        // 魔法カードと通常カードに分別
        var magicCards = cards.FindAll(c => c.cardType == CardType.Magic);
        var normalCards = cards.FindAll(c => c.cardType != CardType.Magic);

        // 魔法カードのプール処理
        foreach (var magic in magicCards)
        {
            bool isFromHand = battleManager.playerHand.Contains(magic);
            ApplyMagicCardToPool(magic, isFromHand);
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

        // 手札の魔法カードのみ手札から削除（プールカードは手札に存在しない）
        foreach (var magic in magicCards)
        {
            if (battleManager.playerHand.Contains(magic) && magic.cardUI != null)
            {
                int slotIndex = magic.cardUI.transform.GetSiblingIndex();
                handRefill?.RecordPlayerUseSlot(slotIndex);
                battleProcessor.UseCard(magic, battleManager.playerHand);
                Debug.Log($"[CardSequenceManager] 手札魔法カードを手札から削除: {magic.cardName}");
            }
        }
    }

    /// <summary>
    /// 単一カードの処理（攻撃・防御共通）
    /// </summary>
    private void ProcessSingleCard(CardData card, string cardType)
    {
        // 魔法カード（単独型・組み合わせ型とも）の場合は特殊処理
        if (card.cardType == CardType.Magic)
        {
            Debug.Log($"[CardSequenceManager] 魔法カード処理: {card.cardName} (組み合わせ={card.isCombinationMagic})");
            bool isFromHand = battleManager.playerHand.Contains(card);
            ApplyMagicCardToPool(card, isFromHand);
            battleManager.SetCurrentAttackCard(card);

            if (isFromHand && card.cardUI != null)
            {
                int slotIndex = card.cardUI.transform.GetSiblingIndex();
                handRefill?.RecordPlayerUseSlot(slotIndex);
                battleProcessor.UseCard(card, battleManager.playerHand);
            }
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
    /// MP消費 → プール操作 → (プール使用時)カードドロー
    /// </summary>
    private void ApplyMagicCardToPool(CardData card, bool isFromHand)
    {
        if (MagicPoolManager.I == null) return;

        // MP消費
        var playerStatus = battleManager.GetPlayerStatus();
        if (playerStatus != null && card.mpCost > 0)
        {
            playerStatus.UseMP(card.mpCost);
            Debug.Log($"[CardSequenceManager] MP消費: {card.cardName} -{card.mpCost}MP (残り={playerStatus.currentMP})");
            BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
        }

        if (isFromHand)
        {
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
            // MagicPanel から使用 → 使用回数を消費 + 手札1枚ドロー
            MagicPoolManager.I.ConsumeUse(card);
            Debug.Log($"[CardSequenceManager] ConsumeUse: {card.cardName}");

            // プールカード使用時は手札が1枚純増
            if (battleManager.playerHand.Count < battleManager.GetHandMaxCount())
            {
                battleManager.DrawOneCard();
                Debug.Log("[CardSequenceManager] MagicPanel使用による手札1枚追加");
            }
        }
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
