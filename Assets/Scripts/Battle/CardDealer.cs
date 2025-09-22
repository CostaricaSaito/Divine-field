using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// カードの配布とUI管理を担当するクラス
/// 
/// 【役割】
/// - カードの配布（プレイヤー・敵）
/// - カードUIの生成・管理
/// - カードの表示・非表示制御
/// - カード配布時の演出（SE、アニメーション）
/// 
/// 【責任範囲】
/// - 手札の初期化
/// - カードUIの生成・破棄
/// - カードの表示状態管理
/// - 配布演出の制御
/// 
/// 【他のクラスとの関係】
/// - BattleController: カード配布の要求
/// - CardUI: 個別カードのUI管理
/// - BattleUIManager: カード表示の制御
/// - HandRefillService: 手札補充の連携
/// </summary>
public class CardDealer : MonoBehaviour
{
    //========================
    // 依存関係
    //========================
    private PlayerStatus playerStatus;
    private PlayerStatus enemyStatus;
    private Transform handPanel;
    private GameObject cardUIPrefab;
    private Sprite cardBackSprite;
    private AudioSource audioSource;
    private AudioClip cardDealSE;
    private AudioClip cardRevealSE;

    // 外部からアクセス可能なプロパティ
    public Sprite CardBackSprite => cardBackSprite;

    //========================
    // カードデータ
    //========================
    [SerializeField] private CardData[] allCards; // 全カードの読み込み済み配列

    //========================
    // UI管理
    //========================
    private readonly List<CardUI> activeCardUIs = new(); // 生成済みプレイヤー手札UI

    /// <summary>
    /// 初期化処理
    /// 
    /// 【処理内容】
    /// 各システムへの参照を設定し、カード配布の準備を行う
    /// </summary>
    /// <param name="playerStatus">プレイヤーのステータス</param>
    /// <param name="enemyStatus">敵のステータス</param>
    /// <param name="handPanel">手札UIの親パネル</param>
    /// <param name="cardUIPrefab">カードUIのプレハブ</param>
    /// <param name="cardBackSprite">カードの裏面画像</param>
    /// <param name="audioSource">音響ソース</param>
    /// <param name="cardDealSE">カード配布SE</param>
    /// <param name="cardRevealSE">カード表示SE</param>
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

        // カードデータの読み込み（Resources/ Cards フォルダ）
        allCards = Resources.LoadAll<CardData>("Cards");
        if (allCards == null || allCards.Length == 0)
            Debug.LogError("[CardDealer] Cards フォルダから CardData を読み込めませんでした");
        else
            Debug.Log($"[CardDealer] 読み込まれたカード数: {allCards.Length}");
    }

    /// <summary>
    /// プレイヤー/CPUにカードを配布する（プレイヤーUIを生成）
    /// 
    /// 【処理内容】
    /// 1. 既存UIのクリア
    /// 2. 指定枚数分のカードを配布
    /// 3. プレイヤー用UIの生成
    /// 4. 配布演出（SE、アニメーション）
    /// 5. カードの表示
    /// </summary>
    /// <param name="playerHand">プレイヤーの手札</param>
    /// <param name="cpuHand">CPUの手札</param>
    /// <param name="count">配布枚数</param>
    /// <returns>配布完了まで待機</returns>
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
            // カードインスタンスの生成（各プレイヤー用に独立した cardUI を生成するように）
            var playerCardInstance = DrawRandomCardInstance();
            var enemyCardInstance = DrawRandomCardInstance();

            playerHand.Add(playerCardInstance);
            cpuHand.Add(enemyCardInstance);

            // プレイヤー用 UI 生成
            var ui = CreateCardUIForHand(playerCardInstance);
            if (ui != null) activeCardUIs.Add(ui);

            // SE再生
            if (audioSource && cardDealSE) audioSource.PlayOneShot(cardDealSE);

            yield return new WaitForSeconds(0.15f);
        }

        // 表示演出
        yield return new WaitForSeconds(0.5f);
        foreach (var ui in activeCardUIs) ui?.Reveal();
        if (audioSource && cardRevealSE) audioSource.PlayOneShot(cardRevealSE);

        // （任意）AttackSelect のオーバーレイを無効化
        BattleUIManager.I?.RefreshAttackInteractivity(BattleManager.I.playerHand, CardRules.GetAttackChoices(BattleManager.I.playerHand));
    }

    //====================================================
    // Private: 内部処理
    //====================================================

    /// <summary>
    /// プレイヤー手札UIをクリアする
    /// </summary>
    private void ClearPlayerHandUI()
    {
        if (handPanel == null) return;
        for (int i = handPanel.childCount - 1; i >= 0; i--)
            Destroy(handPanel.GetChild(i).gameObject);
    }

    /// <summary>
    /// カードデータから1枚ランダムに選んでカードインスタンスを返す
    /// </summary>
    /// <returns>生成されたカードインスタンス</returns>
    private CardData DrawRandomCardInstance()
    {
        if (allCards == null || allCards.Length == 0) return null;

        var template = allCards[Random.Range(0, allCards.Length)];
        if (template == null) return null;

        var instance = ScriptableObject.Instantiate(template);
        instance.name = template.name; // デバッグしやすく
        instance.cardUI = null;          // 重要：後でUIを生成する際の重複を防ぐ
        return instance;
    }

    /// <summary>
    /// ランダムカードを取得する（外部用）
    /// </summary>
    /// <returns>生成されたカードインスタンス</returns>
    public CardData DrawRandomCard()
    {
        if (allCards == null || allCards.Length == 0)
        {
            Debug.LogWarning("[CardDealer] allCardsがnullまたは空です");
            return null;
        }
        
        var src = allCards[Random.Range(0, allCards.Length)];
        if (src == null)
        {
            Debug.LogWarning("[CardDealer] 選択されたカードテンプレートがnullです");
            return null;
        }

        var instance = ScriptableObject.Instantiate(src);
        if (instance == null)
        {
            Debug.LogWarning("[CardDealer] カードインスタンスの生成に失敗しました");
            return null;
        }
        
        instance.cardUI = null; // UIは後で生成
        return instance;
    }

    /// <summary>
    /// プレイヤー手札用 UI を1枚生成してオブジェクト化
    /// </summary>
    /// <param name="instance">カードインスタンス</param>
    /// <returns>生成されたCardUI</returns>
    private CardUI CreateCardUIForHand(CardData instance)
    {
        if (instance == null || cardUIPrefab == null || handPanel == null)
        {
            Debug.LogWarning("[CardDealer] CreateCardUIForHand: パラメータ/参照不足");
            return null;
        }

        var go = Instantiate(cardUIPrefab, handPanel);
        var ui = go.GetComponent<CardUI>();
        if (ui == null)
        {
            Debug.LogError("[CardDealer] cardUIPrefab に CardUI が付いていません");
            return null;
        }

        // 適切な CardUI の API に合わせる（Setup / SetCard / Bind のいずれか）
        // 現在は Setup(CardData, Sprite) を想定
        ui.Setup(instance, cardBackSprite);

        // 現在紐付け（このインスタンスを指すUI）
        instance.cardUI = ui;
        return ui;
    }
}