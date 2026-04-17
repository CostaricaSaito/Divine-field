using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// 裏向きの「使用済み」カードをTurnEndで新カードに置き換えまで一括管理
public class HandRefillService : MonoBehaviour
{
    [Header("依存関係（必須）")]
    [SerializeField] private Transform handPanel;
    [SerializeField] private GameObject cardUIPrefab;
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField] private CardDealer cardDealer;

    // 裏向きスロット（プレイヤーのUI表示用）
    private struct BackSlot { public int index; public CardUI ui; public CardData usedCard; }
    private readonly List<BackSlot> _playerBackSlotsThisTurn = new();

    // 敵の使用済みカードを記録（プレイヤーと同様に置き換えのため）
    private readonly List<CardData> _enemyUsedCardsThisTurn = new();

    // ---- 設定（インスペクターから、または手動） ----
    public void Initialize(Transform handPanel, GameObject cardUIPrefab, Sprite back, CardDealer dealer)
    {
        this.handPanel = handPanel;
        this.cardUIPrefab = cardUIPrefab;
        this.cardBackSprite = back;
        this.cardDealer = dealer;
    }

    private void Awake()
    {
        if (cardDealer == null) Debug.LogError("[HandRefillService] cardDealer is null");
        // 初期化チェックはこのまま
    }

    // 攻撃/回復などで使ったカードのスロット位置を記録（既存のUIを再利用）
    public void RecordPlayerUseSlot(int siblingIndex)
    {
        if (siblingIndex < 0 || handPanel == null)
        {
            Debug.LogWarning($"[HandRefillService] RecordPlayerUseSlot: 無効なパラメータ (siblingIndex: {siblingIndex}, handPanel: {handPanel != null})");
            return;
        }

        // 既存のUIオブジェクトを取得
        var existingUI = handPanel.GetChild(siblingIndex)?.GetComponent<CardUI>();
        if (existingUI != null)
        {
            // 使用済みカードを取得（Setupを呼ぶ前に取得する必要がある）
            CardData usedCard = existingUI.GetCardData();
            
            if (usedCard == null)
            {
                Debug.LogWarning($"[HandRefillService] RecordPlayerUseSlot: スロット {siblingIndex} のカードデータがnullです（既に使用済みの可能性があります）");
                return;
            }
            
            Debug.Log($"[HandRefillService] RecordPlayerUseSlot: カード記録 - {usedCard.cardName} (スロット: {siblingIndex})");
            
            // 既存のUIを裏向きにする
            existingUI.Setup(null, cardBackSprite, playerHandRareBackPresentation: false);
            existingUI.button.interactable = false;
            _playerBackSlotsThisTurn.Add(new BackSlot { index = siblingIndex, ui = existingUI, usedCard = usedCard });
        }
        else
        {
            Debug.LogWarning($"[HandRefillService] スロット {siblingIndex} のUIが見つかりません");
        }
    }

    // 敵のカード使用を記録（使用済みカードを記録）
    /// <remarks>同一ターン・同一インスタンスの二重記録を防ぐ（通常攻撃後の介入で同じ防御カードを再度使う場合など）。</remarks>
    public void RecordEnemyUse(CardData usedCard)
    {
        if (usedCard == null) return;
        int id = usedCard.GetInstanceID();
        for (int i = 0; i < _enemyUsedCardsThisTurn.Count; i++)
        {
            var c = _enemyUsedCardsThisTurn[i];
            if (c != null && c.GetInstanceID() == id)
                return;
        }
        _enemyUsedCardsThisTurn.Add(usedCard);
    }

    /// <summary>
    /// TurnEnd 介入で手札から破棄する前に呼ぶ。補充待ちリストと手札を同期し、通常の Refill と同様に置換する。
    /// （介入で先に Remove すると IndexOf 失敗・UI 不整合になる）
    /// </summary>
    public async Task FinalizeInterventionDiscardedCardAsync(
        CardData source,
        PlayerType interventionOwner,
        List<CardData> playerHand,
        List<CardData> enemyHand,
        CancellationToken ct)
    {
        if (source == null) return;

        if (interventionOwner == PlayerType.Player)
        {
            if (playerHand == null || !playerHand.Contains(source)) return;

            for (int i = 0; i < _playerBackSlotsThisTurn.Count; i++)
            {
                var slot = _playerBackSlotsThisTurn[i];
                if (slot.usedCard == null || slot.usedCard.GetInstanceID() != source.GetInstanceID())
                    continue;

                await ReplacePlayerBackSlotAsync(slot, playerHand, ct);
                _playerBackSlotsThisTurn.RemoveAt(i);
                if (playerHand.Contains(source))
                    playerHand.Remove(source);
                DestroyCardDataInstance(source);
                return;
            }

            int idx = playerHand.IndexOf(source);
            if (idx >= 0)
                await InterventionReplaceFaceUpPlayerSlotAsync(idx, source, playerHand, ct);

            if (playerHand.Contains(source))
                playerHand.Remove(source);
            DestroyCardDataInstance(source);
        }
        else
        {
            if (enemyHand == null || !enemyHand.Contains(source)) return;

            _enemyUsedCardsThisTurn.RemoveAll(c => c != null && c.GetInstanceID() == source.GetInstanceID());

            int idx = enemyHand.IndexOf(source);
            if (idx >= 0)
            {
                var newCard = DrawRandomCard();
                if (newCard != null)
                {
                    enemyHand[idx] = newCard;
                    Debug.Log($"[HandRefillService] 介入: 敵手札置換 {source.cardName} → {newCard.cardName} (index={idx})");
                }
                else
                    Debug.LogWarning($"[HandRefillService] 介入: 敵のドロー失敗 ({source.cardName})");

                await Task.Delay(50, ct);
            }

            if (enemyHand.Contains(source))
                enemyHand.Remove(source);
            DestroyCardDataInstance(source);
        }
    }

    private static void DestroyCardDataInstance(CardData card)
    {
        if (card == null) return;
        UnityEngine.Object.Destroy(card);
    }

    /// <summary>介入で RecordPlayerUseSlot されていない表向きカードを、TurnEnd 補充と同様に置き換え。</summary>
    private async Task InterventionReplaceFaceUpPlayerSlotAsync(
        int handIndex,
        CardData source,
        List<CardData> playerHand,
        CancellationToken ct)
    {
        CardUI ui = source?.cardUI;
        if (ui == null && handPanel != null && handIndex >= 0 && handIndex < handPanel.childCount)
            ui = handPanel.GetChild(handIndex)?.GetComponent<CardUI>();

        var newCard = DrawRandomCard();
        if (newCard == null)
        {
            Debug.LogWarning($"[HandRefillService] 介入: プレイヤー表向きカードの置換に失敗 ({source?.cardName})");
            return;
        }

        playerHand[handIndex] = newCard;
        newCard.cardUI = ui;
        if (ui != null)
        {
            ui.Setup(newCard, cardBackSprite, playerHandRareBackPresentation: true);
            if (ui.GetCardData() != newCard)
                ui.Setup(newCard, cardBackSprite, playerHandRareBackPresentation: true);
            ui.button.interactable = true;
            await Task.Delay(150, ct);
            CardDealAudio.Play(newCard);
            ui.Reveal();
            await Task.Delay(100, ct);
        }
    }

    private async Task ReplacePlayerBackSlotAsync(BackSlot slot, List<CardData> playerHand, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        if (slot.ui == null) return;

        var newCard = DrawRandomCard();
        if (newCard == null)
        {
            Debug.LogWarning("[HandRefillService] カードの取得に失敗しました（裏向きスロット）");
            slot.ui.gameObject.SetActive(false);
            return;
        }

        if (slot.usedCard != null && playerHand != null)
        {
            int index = playerHand.IndexOf(slot.usedCard);
            if (index >= 0)
            {
                playerHand[index] = newCard;
                Debug.Log($"[HandRefillService] 使用済みカードを新しいカードで置き換え: {slot.usedCard?.cardName ?? "null"} → {newCard?.cardName ?? "null"} (インデックス: {index})");
            }
            else
            {
                Debug.LogError($"[HandRefillService] 使用済みカードが見つかりません: {slot.usedCard?.cardName ?? "null"} (手札枚数: {playerHand.Count})");
            }
        }
        else
        {
            Debug.LogError($"[HandRefillService] 使用済みカードが記録されていません (手札枚数: {playerHand?.Count ?? 0})");
        }

        newCard.cardUI = slot.ui;
        slot.ui.Setup(newCard, cardBackSprite, playerHandRareBackPresentation: true);
        if (slot.ui.GetCardData() != newCard)
        {
            Debug.LogWarning("[HandRefillService] CardUIのcardDataが新しいカードと一致しません。再設定します。");
            slot.ui.Setup(newCard, cardBackSprite, playerHandRareBackPresentation: true);
        }

        slot.ui.button.interactable = true;

        await Task.Delay(150, ct);
        CardDealAudio.Play(newCard);
        slot.ui.Reveal();
        await Task.Delay(100, ct);
    }

    // TurnEnd：裏向きスロットを新カードに置き換え（1枚ずつ順次処理）、敵も使用済みカードを新しいカードで置き換え
    public async Task RefillAtTurnEndAsync(List<CardData> playerHand, List<CardData> enemyHand, CancellationToken ct)
    {
        // プレイヤー：裏向きスロットを新カードに置き換え
        for (int i = 0; i < _playerBackSlotsThisTurn.Count; i++)
        {
            if (ct.IsCancellationRequested) return;

            var slot = _playerBackSlotsThisTurn[i];
            await ReplacePlayerBackSlotAsync(slot, playerHand, ct);
        }
        _playerBackSlotsThisTurn.Clear();

        // 敵：通常のカード使用時は手札枚数は変動しない
        // 使用済みカードを新しいカードで置き換える（削除も追加もしない）
        for (int i = 0; i < _enemyUsedCardsThisTurn.Count; i++)
        {
            if (ct.IsCancellationRequested) return;

            var usedCard = _enemyUsedCardsThisTurn[i];
            if (usedCard == null) continue;

            var newCard = DrawRandomCard();
            if (newCard == null)
            {
                Debug.LogWarning($"[HandRefillService] 敵のカード取得に失敗しました (使用済みカード: {usedCard?.cardName ?? "null"})");
                continue;
            }

            if (enemyHand != null)
            {
                int index = enemyHand.IndexOf(usedCard);
                if (index >= 0)
                {
                    // 使用済みカードを新しいカードで置き換え（手札枚数は変わらない）
                    enemyHand[index] = newCard;
                    Debug.Log($"[HandRefillService] 敵の使用済みカードを新しいカードで置き換え: {usedCard?.cardName ?? "null"} → {newCard?.cardName ?? "null"} (インデックス: {index})");
                }
                else
                {
                    // 既に先の処理で置換済み、または重複記録の残り（敵は同一防御を複数回参照しうる）
                    Debug.LogWarning(
                        $"[HandRefillService] 敵の使用済みカードが手札にありません（スキップ）: {usedCard?.cardName ?? "null"} (手札枚数: {enemyHand.Count})");
                }
            }

            await Task.Delay(50, ct); // 短い間隔のエフェクト
        }
        _enemyUsedCardsThisTurn.Clear();
    }

    // CardDealer からカードを1枚取得（CardDealer の public API を用意してください）
    private CardData DrawRandomCard()
    {
        // 暫定実装。CardDealer の public API を用意してください
        return (cardDealer != null) ? cardDealer.DrawRandomCard() : null;
    }

    /// <summary>
    /// カードを1枚ドローして手札に追加（裏面のまま）。ドローした CardData を返す。
    /// </summary>
    /// <param name="trailingDelayMs">配布後の待機（ms）。0 で即時。</param>
    /// <param name="playSoundOnDraw">true のとき配布時に <see cref="CardDealAudio"/> を鳴らす（戦闘後に表向けする用途は false 推奨）</param>
    public async Task<CardData> DrawCardAsync(List<CardData> hand, int trailingDelayMs = 200, bool playSoundOnDraw = true)
    {
        if (hand == null || cardDealer == null)
        {
            Debug.LogWarning("[HandRefillService] DrawCardAsync: パラメータがnullです");
            return null;
        }

        var newCard = DrawRandomCard();
        if (newCard == null)
        {
            Debug.LogWarning("[HandRefillService] DrawCardAsync: カードの取得に失敗しました");
            return null;
        }

        hand.Add(newCard);
        Debug.Log($"[HandRefillService] カードドロー: {newCard.cardName}");

        var ui = cardDealer.CreateCardUIForHand(newCard);
        if (ui != null)
        {
            Debug.Log($"[HandRefillService] カードUI生成完了: {newCard.cardName}");
        }

        if (playSoundOnDraw)
            CardDealAudio.Play(newCard);

        if (trailingDelayMs > 0)
            await Task.Delay(trailingDelayMs);

        return newCard;
    }

    /// <summary>
    /// MagicPanel ボーナスドロー等：ダメージ処理後に表向け（TurnEnd の Refill と同じ間隔・SE）
    /// </summary>
    public async Task RevealDrawnCardAfterCombatAsync(CardData card, CancellationToken ct = default)
    {
        if (card?.cardUI == null) return;

        if (card.cardUI.button != null)
            card.cardUI.button.interactable = true;

        await Task.Delay(150, ct);
        if (ct.IsCancellationRequested) return;

        CardDealAudio.Play(card);

        card.cardUI.Reveal();

        await Task.Delay(100, ct);
    }
}