using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// 手札の「消費→裏置き→TurnEndで新規補充」までを一括管理
public class HandRefillService : MonoBehaviour
{
    [Header("依存（必須）")]
    [SerializeField] private Transform handPanel;
    [SerializeField] private GameObject cardUIPrefab;
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip cardDealSE;
    [SerializeField] private CardDealer cardDealer;

    // 裏スロット（プレイヤーのみUI表示あり）
    private struct BackSlot { public int index; public CardUI ui; }
    private readonly List<BackSlot> _playerBackSlotsThisTurn = new();

    // 敵はUI無し→枚数だけ覚えておけばOK
    private int _enemyUsedCountThisTurn = 0;

    // ---- 設定（シーンからアサイン） ----
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
        // …既存チェックはそのまま
    }

    // 攻撃/回復などで使ったカードのスロット位置に「裏」カードを作成
    public void RecordPlayerUseSlot(int siblingIndex)
    {
        if (siblingIndex < 0 || handPanel == null || cardUIPrefab == null) return;

        var go = GameObject.Instantiate(cardUIPrefab, handPanel);
        var ui = go.GetComponent<CardUI>();
        if (ui != null)
        {
            ui.Setup(null, cardBackSprite);   // 中身は後で差し替えるのでnullでもOK
            ui.button.interactable = false;   // 次ターンまで操作不可
        }
        go.transform.SetSiblingIndex(siblingIndex);
        _playerBackSlotsThisTurn.Add(new BackSlot { index = siblingIndex, ui = ui });
    }

    // 敵がカードを使った回数を記録（攻撃/防御どちらでも）
    public void RecordEnemyUse() => _enemyUsedCountThisTurn++;

    // TurnEnd：裏スロットを新カードに差し替え（1枚ずつ音→リビール）、敵も枚数だけ補充
    public async Task RefillAtTurnEndAsync(List<CardData> playerHand, List<CardData> enemyHand, CancellationToken ct)
    {
        // プレイヤー：裏→新カード
        for (int i = 0; i < _playerBackSlotsThisTurn.Count; i++)
        {
            if (ct.IsCancellationRequested) return;

            var slot = _playerBackSlotsThisTurn[i];
            if (slot.ui == null) continue;

            var newCard = DrawRandomCard();
            if (newCard == null) continue;

            playerHand.Add(newCard);

            slot.ui.Setup(newCard, cardBackSprite); // 裏でセット
            slot.ui.button.interactable = false;

            await Task.Delay(150, ct);
            if (audioSource && cardDealSE) audioSource.PlayOneShot(cardDealSE);

            slot.ui.Reveal();       // 表にする
            newCard.cardUI = slot.ui;

            await Task.Delay(100, ct);
        }
        _playerBackSlotsThisTurn.Clear();

        // 敵：UI無しで枚数だけ補充
        for (int i = 0; i < _enemyUsedCountThisTurn; i++)
        {
            var c = DrawRandomCard();
            if (c != null) enemyHand.Add(c);
            await Task.Delay(50, ct); // ほんの僅かなウェイト
        }
        _enemyUsedCountThisTurn = 0;
    }

    // CardDealer からカードを1枚引く（CardDealerに public API を用意してください）
    private CardData DrawRandomCard()
    {
        // 反射はやめる。公開APIを直呼び
        return (cardDealer != null) ? cardDealer.DrawRandomCard() : null;
    }
}
