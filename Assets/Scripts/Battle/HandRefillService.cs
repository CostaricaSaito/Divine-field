using System.Collections.Generic;
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
    private struct BackSlot { public int index; public CardUI ui; }
    private readonly List<BackSlot> _playerBackSlotsThisTurn = new();

    // 敵はUI表示しないので使用回数だけ記録
    private int _enemyUsedCountThisTurn = 0;

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
        if (siblingIndex < 0 || handPanel == null) return;

        // 既存のUIオブジェクトを取得
        var existingUI = handPanel.GetChild(siblingIndex)?.GetComponent<CardUI>();
        if (existingUI != null)
        {
            // 既存のUIを裏向きにする
            existingUI.Setup(null, cardBackSprite);
            existingUI.button.interactable = false;
            _playerBackSlotsThisTurn.Add(new BackSlot { index = siblingIndex, ui = existingUI });
        }
        else
        {
            Debug.LogWarning($"[HandRefillService] スロット {siblingIndex} のUIが見つかりません");
        }
    }

    // 敵のカード使用回数を記録（攻撃/防御どちらでも）
    public void RecordEnemyUse() => _enemyUsedCountThisTurn++;

    // TurnEnd：裏向きスロットを新カードに置き換え（1枚ずつ順次処理）、敵は手札に追加
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

            // 手札に新しいカードを追加
            playerHand.Add(newCard);

            // 裏向きのUIに新しいカードをセットアップ
            slot.ui.Setup(newCard, cardBackSprite);
            slot.ui.button.interactable = true; // 新しいカードは使用可能にする

            await Task.Delay(150, ct);
            if (audioSource && cardDealSE) audioSource.PlayOneShot(cardDealSE);

            slot.ui.Reveal();       // 表向きに
            newCard.cardUI = slot.ui;

            await Task.Delay(100, ct);
        }
        _playerBackSlotsThisTurn.Clear();

        // 敵：UI表示せずに手札に追加
        for (int i = 0; i < _enemyUsedCountThisTurn; i++)
        {
            var c = DrawRandomCard();
            if (c != null) enemyHand.Add(c);
            await Task.Delay(50, ct); // 短い間隔のエフェクト
        }
        _enemyUsedCountThisTurn = 0;
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