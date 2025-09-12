using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CardDealer : MonoBehaviour
{
    // 注入される参照
    private PlayerStatus playerStatus;
    private PlayerStatus enemyStatus;
    private Transform handPanel;
    private GameObject cardUIPrefab;
    private Sprite cardBackSprite;
    private AudioSource audioSource;
    private AudioClip cardDealSE;
    private AudioClip cardRevealSE;

    // マスターデータ（ScriptableObjectの雛形群）
    [SerializeField] private CardData[] allCards; // 既存の読み込み済みプール

    // 今回配ったプレイヤー手札のUI
    private readonly List<CardUI> activeCardUIs = new();

    public void Initialize(
        PlayerStatus playerStatus,
        PlayerStatus enemyStatus,
        Transform handPanel,
        GameObject cardUIPrefab,
        Sprite cardBackSprite,
        AudioSource audioSource,
        AudioClip cardDealSE,
        AudioClip cardRevealSE)
    {
        this.playerStatus = playerStatus;
        this.enemyStatus = enemyStatus;
        this.handPanel = handPanel;
        this.cardUIPrefab = cardUIPrefab;
        this.cardBackSprite = cardBackSprite;
        this.audioSource = audioSource;
        this.cardDealSE = cardDealSE;
        this.cardRevealSE = cardRevealSE;

        // マスター読み込み（Resources/ Cards 配下）
        allCards = Resources.LoadAll<CardData>("Cards");
        if (allCards == null || allCards.Length == 0)
            Debug.LogError("[CardDealer] Cards フォルダから CardData を読み込めませんでした");
        else
            Debug.Log($"[CardDealer] 読み込んだカード数: {allCards.Length}");
    }

    /// <summary>プレイヤー/CPUへカードを配る（プレイヤーUI生成付き）</summary>
    public IEnumerator DealCards(List<CardData> playerHand, List<CardData> cpuHand, int count)
    {
        // 既存UIクリア
        ClearPlayerHandUI();
        activeCardUIs.Clear();
        playerHand.Clear();
        cpuHand.Clear();

        // 配布ループ
        for (int i = 0; i < count; i++)
        {
            // ★ ランタイム複製（各枚が独立した cardUI を持てるように）
            var playerCardInstance = DrawRandomCardInstance();
            var enemyCardInstance = DrawRandomCardInstance();

            playerHand.Add(playerCardInstance);
            cpuHand.Add(enemyCardInstance);

            // プレイヤー用 UI 生成
            var ui = CreateCardUIForHand(playerCardInstance);
            if (ui != null) activeCardUIs.Add(ui);

            // SE
            if (audioSource && cardDealSE) audioSource.PlayOneShot(cardDealSE);

            yield return new WaitForSeconds(0.15f);
        }

        // 表向け演出
        yield return new WaitForSeconds(0.5f);
        foreach (var ui in activeCardUIs) ui?.Reveal();
        if (audioSource && cardRevealSE) audioSource.PlayOneShot(cardRevealSE);

        // （任意）AttackSelect のグレー化を即反映
        BattleUIManager.I?.RefreshAttackInteractivity(BattleManager.I.playerHand);
    }

    //================ 内部ヘルパ =================

    private void ClearPlayerHandUI()
    {
        if (handPanel == null) return;
        for (int i = handPanel.childCount - 1; i >= 0; i--)
            Destroy(handPanel.GetChild(i).gameObject);
    }

    /// <summary>マスターから1枚引いてランタイム複製を返す</summary>
    private CardData DrawRandomCardInstance()
    {
        if (allCards == null || allCards.Length == 0) return null;

        var template = allCards[Random.Range(0, allCards.Length)];
        if (template == null) return null;

        var instance = ScriptableObject.Instantiate(template);
        instance.name = template.name; // デバッグしやすく
        instance.cardUI = null;          // 重要：共有状態の痕跡を消す
        return instance;
    }

    public CardData DrawRandomCard()
    {
        if (allCards == null || allCards.Length == 0) return null;
        var src = allCards[Random.Range(0, allCards.Length)];
        if (src == null) return null;

        var instance = ScriptableObject.Instantiate(src);
        instance.cardUI = null; // UIは後で紐付け
        return instance;
    }


    /// <summary>プレイヤー手札に UI を1枚生成してバインド</summary>
    private CardUI CreateCardUIForHand(CardData instance)
    {
        if (instance == null || cardUIPrefab == null || handPanel == null)
        {
            Debug.LogWarning("[CardDealer] CreateCardUIForHand: 引数/参照不足");
            return null;
        }

        var go = Instantiate(cardUIPrefab, handPanel);
        var ui = go.GetComponent<CardUI>();
        if (ui == null)
        {
            Debug.LogError("[CardDealer] cardUIPrefab に CardUI が付いていません");
            return null;
        }

        // あなたの CardUI の API 名に合わせる（Setup / SetCard / Bind のいずれか）
        // ここでは Setup(CardData, Sprite) を想定
        ui.Setup(instance, cardBackSprite);

        // 相互リンク（このインスタンス専用のUI）
        instance.cardUI = ui;
        return ui;
    }

}
