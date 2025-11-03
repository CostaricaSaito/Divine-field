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
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip cardDealSE;
    [SerializeField] private CardDealer cardDealer;

    // 裏向きスロット（プレイヤーのUI表示用）
    private struct BackSlot { public int index; public CardUI ui; public CardData usedCard; }
    private readonly List<BackSlot> _playerBackSlotsThisTurn = new();

    // 敵の使用済みカードを記録（プレイヤーと同様に置き換えのため）
    private readonly List<CardData> _enemyUsedCardsThisTurn = new();

    // ---- 設定（インスペクターから、または手動） ----
    public void Initialize(Transform handPanel, GameObject cardUIPrefab, Sprite back, AudioSource src, AudioClip deal, CardDealer dealer)
    {
        this.handPanel = handPanel;
        this.cardUIPrefab = cardUIPrefab;
        this.cardBackSprite = back;
        this.audioSource = src;
        this.cardDealSE = deal;
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
            existingUI.Setup(null, cardBackSprite);
            existingUI.button.interactable = false;
            _playerBackSlotsThisTurn.Add(new BackSlot { index = siblingIndex, ui = existingUI, usedCard = usedCard });
        }
        else
        {
            Debug.LogWarning($"[HandRefillService] スロット {siblingIndex} のUIが見つかりません");
        }
    }

    // 敵のカード使用を記録（使用済みカードを記録）
    public void RecordEnemyUse(CardData usedCard)
    {
        if (usedCard != null)
        {
            _enemyUsedCardsThisTurn.Add(usedCard);
        }
    }

    // TurnEnd：裏向きスロットを新カードに置き換え（1枚ずつ順次処理）、敵も使用済みカードを新しいカードで置き換え
    public async Task RefillAtTurnEndAsync(List<CardData> playerHand, List<CardData> enemyHand, CancellationToken ct)
    {
        // プレイヤー：裏向きスロットを新カードに置き換え
        for (int i = 0; i < _playerBackSlotsThisTurn.Count; i++)
        {
            if (ct.IsCancellationRequested) return;

            var slot = _playerBackSlotsThisTurn[i];
            if (slot.ui == null) continue;

            var newCard = DrawRandomCard();
            if (newCard == null)
            {
                // カードが取得できない場合は、スロットを無効化
                Debug.LogWarning($"[HandRefillService] カードの取得に失敗しました (スロット {i})");
                slot.ui.gameObject.SetActive(false);
                continue;
            }

            // 通常のカード使用時は手札枚数は変動しない
            // 使用済みカードを新しいカードで置き換える（削除も追加もしない）
            if (slot.usedCard != null && playerHand != null)
            {
                int index = playerHand.IndexOf(slot.usedCard);
                if (index >= 0)
                {
                    // 使用済みカードを新しいカードで置き換え（手札枚数は変わらない）
                    playerHand[index] = newCard;
                    Debug.Log($"[HandRefillService] 使用済みカードを新しいカードで置き換え: {slot.usedCard?.cardName ?? "null"} → {newCard?.cardName ?? "null"} (インデックス: {index})");
                }
                else
                {
                    // 使用済みカードが見つからない場合（エラー）
                    // 手札枚数を変えないため、追加しない
                    Debug.LogError($"[HandRefillService] 使用済みカードが見つかりません: {slot.usedCard?.cardName ?? "null"} (手札枚数: {playerHand.Count})");
                }
            }
            else
            {
                // 使用済みカードが記録されていない場合（エラー）
                // 手札枚数を変えないため、追加しない
                Debug.LogError($"[HandRefillService] 使用済みカードが記録されていません (手札枚数: {playerHand?.Count ?? 0})");
            }

            // 新しいカードのcardUIを先に設定（Setupの前に設定する必要がある）
            newCard.cardUI = slot.ui;

            // 裏向きのUIに新しいカードをセットアップ
            // これにより、CardUIのcardDataが新しいカードに更新される
            slot.ui.Setup(newCard, cardBackSprite);
            
            // 念のため、CardUIのcardDataが新しいカードを参照していることを確認
            if (slot.ui.GetCardData() != newCard)
            {
                Debug.LogWarning($"[HandRefillService] CardUIのcardDataが新しいカードと一致しません。再設定します。");
                slot.ui.Setup(newCard, cardBackSprite);
            }
            
            slot.ui.button.interactable = true; // 新しいカードは使用可能にする

            await Task.Delay(150, ct);
            if (audioSource && cardDealSE) audioSource.PlayOneShot(cardDealSE);

            slot.ui.Reveal();       // 表向きに

            await Task.Delay(100, ct);
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
                    // 使用済みカードが見つからない場合（エラー）
                    // 手札枚数を変えないため、追加しない
                    Debug.LogError($"[HandRefillService] 敵の使用済みカードが見つかりません: {usedCard?.cardName ?? "null"} (手札枚数: {enemyHand.Count})");
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
    /// カードを1枚ドローして手札に追加
    /// </summary>
    public async Task DrawCardAsync(List<CardData> hand)
    {
        if (hand == null || cardDealer == null)
        {
            Debug.LogWarning("[HandRefillService] DrawCardAsync: パラメータがnullです");
            return;
        }

        var newCard = DrawRandomCard();
        if (newCard == null)
        {
            Debug.LogWarning("[HandRefillService] DrawCardAsync: カードの取得に失敗しました");
            return;
        }

        // 手札に追加
        hand.Add(newCard);
        Debug.Log($"[HandRefillService] カードドロー: {newCard.cardName}");

        // カードUIを生成
        var ui = cardDealer.CreateCardUIForHand(newCard);
        if (ui != null)
        {
            Debug.Log($"[HandRefillService] カードUI生成完了: {newCard.cardName}");
        }

        // 効果音再生
        if (audioSource != null && cardDealSE != null)
        {
            audioSource.PlayOneShot(cardDealSE);
        }

        // 短い待機時間
        await Task.Delay(200);
    }
}