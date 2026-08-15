using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    // 外部からアクセス可能なプロパティ
    public Sprite CardBackSprite => cardBackSprite;

    //========================
    // カードデータ
    //========================
    [SerializeField] private CardData[] allCards; // 全カードの読み込み済み配列

    [SerializeField] private CardDrawTableSO drawTable;

    /// <summary>重み展開済み抽選プール（Ultimate 除外。テンプレート参照を weight 回重复）。</summary>
    private List<CardData> _weightedDrawPool;

    /// <summary>SuperRare 以上の重み展開済み抽選プール（現実改変用）。</summary>
    private List<CardData> _superRarePlusDrawPool;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Debug: next player-side weighted draw uses SuperRare+ pool (one-shot).</summary>
    public static bool DebugForceNextPlayerDrawSuperRarePlus { get; set; }
#endif

    /// <summary>element が闇のテンプレートのみ（ダークプリパレーション抽選用）。</summary>
    private CardData[] _darkCardTemplates;

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
    public void Initialize(
        PlayerStatus playerStatus,
        PlayerStatus enemyStatus,
        Transform handPanel,
        GameObject cardUIPrefab,
        Sprite cardBackSprite)
    {
        this.playerStatus = playerStatus;
        this.enemyStatus = enemyStatus;
        this.handPanel = handPanel;
        this.cardUIPrefab = cardUIPrefab;
        this.cardBackSprite = cardBackSprite;

        // カードデータの読み込み（Resources/ Cards フォルダ）
        allCards = Resources.LoadAll<CardData>("Cards");
        if (allCards == null || allCards.Length == 0)
        {
            Debug.LogError("[CardDealer] Cards フォルダから CardData を読み込めませんでした");
        }
        else
        {
            // オンライン対戦の決定的乱数のため、両クライアントで並び順を固定する
            System.Array.Sort(allCards, (a, b) => string.CompareOrdinal(a?.name, b?.name));
            Debug.Log($"[CardDealer] 読み込まれたカード数: {allCards.Length}");
            DisasterCatalog.RegisterCardTemplates(allCards);
        }

        BuildDarkCardTemplatePool();
        BuildWeightedDrawPool();
        BuildSuperRarePlusDrawPool();
    }

    private void EnsureDrawTable()
    {
        if (drawTable != null) return;
        drawTable = Resources.Load<CardDrawTableSO>("CardDrawTable");
        if (drawTable == null)
            Debug.LogWarning("[CardDealer] CardDrawTable not found in Resources/CardDrawTable");
    }

    private void BuildWeightedDrawPool()
    {
        EnsureDrawTable();
        _weightedDrawPool = CardDrawWeightPool.BuildExpandedTemplatePool(allCards, drawTable);
        if (_weightedDrawPool.Count == 0)
            Debug.LogWarning("[CardDealer] Weighted draw pool is empty. Check CardDrawTable and card weights.");
        else
            Debug.Log($"[CardDealer] Weighted draw pool entries: {_weightedDrawPool.Count}");
    }

    private void BuildSuperRarePlusDrawPool()
    {
        EnsureDrawTable();
        _superRarePlusDrawPool = CardDrawWeightPool.BuildSuperRarePlusExpandedTemplatePool(allCards, drawTable);
        if (_superRarePlusDrawPool.Count == 0)
            Debug.LogWarning("[CardDealer] SuperRare+ draw pool is empty. Check card rarities and weights.");
        else
            Debug.Log($"[CardDealer] SuperRare+ draw pool entries: {_superRarePlusDrawPool.Count}");
    }

    private void BuildDarkCardTemplatePool()
    {
        if (allCards == null || allCards.Length == 0)
        {
            _darkCardTemplates = System.Array.Empty<CardData>();
            return;
        }
        _darkCardTemplates = allCards.Where(c => c != null && c.element == ElementType.Dark
            && c.cardType != CardType.Ultimate && c.cardType != CardType.Disaster).ToArray();
        if (_darkCardTemplates.Length == 0)
            Debug.LogWarning("[CardDealer] 闇属性の CardData がありません。ダークプリパレーションは通常抽選にフォールバックします。");
        else
            Debug.Log($"[CardDealer] 闇属性カード（開幕1枚目抽選用）: {_darkCardTemplates.Length} 種");
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
    /// <param name="count">配布枚数（プレイヤー・CPU 同数）</param>
    /// <returns>配布完了まで待機</returns>
    public IEnumerator DealCards(List<CardData> playerHand, List<CardData> cpuHand, int count)
    {
        yield return DealOpeningHands(playerHand, cpuHand, count, count);
    }

    /// <summary>
    /// 開幕手札を配る。プレイヤー／CPU で枚数が違ってもよい（ガルーダで 12 vs 10 など）。
    /// すべて配り終えてから一斉表向け・バトル開始 SE を行う。
    /// </summary>
    public IEnumerator DealOpeningHands(List<CardData> playerHand, List<CardData> cpuHand, int playerTarget, int cpuTarget)
    {
        ClearPlayerHandUI();
        activeCardUIs.Clear();
        playerHand.Clear();
        cpuHand.Clear();

        bool playerDarkPrep = playerStatus != null && playerStatus.summonData != null
            && playerStatus.summonData.IsDiabolosDarkPreparation();
        bool enemyDarkPrep = enemyStatus != null && enemyStatus.summonData != null
            && enemyStatus.summonData.IsDiabolosDarkPreparation();

        while (playerHand.Count < playerTarget || cpuHand.Count < cpuTarget)
        {
            if (playerHand.Count < playerTarget && cpuHand.Count < cpuTarget)
            {
                var playerCardInstance = DrawOpeningCardInstance(playerHand.Count == 0 && playerDarkPrep, PlayerType.Player);
                var enemyCardInstance = DrawOpeningCardInstance(cpuHand.Count == 0 && enemyDarkPrep, PlayerType.Enemy);
                if (playerCardInstance == null || enemyCardInstance == null)
                {
                    Debug.LogError("[CardDealer] DealOpeningHands: カード生成に失敗しました");
                    yield break;
                }
                playerHand.Add(playerCardInstance);
                cpuHand.Add(enemyCardInstance);
                var ui = CreateCardUIForHand(playerCardInstance);
                if (ui != null) activeCardUIs.Add(ui);
                BattleUIManager.I?.UpdateStatus(
                    BattleManager.I?.GetPlayerStatus(),
                    BattleManager.I?.GetEnemyStatus());
                CardDealAudio.Play(playerCardInstance, true);
            }
            else if (playerHand.Count < playerTarget)
            {
                var playerCardInstance = DrawOpeningCardInstance(playerHand.Count == 0 && playerDarkPrep, PlayerType.Player);
                if (playerCardInstance == null)
                {
                    Debug.LogError("[CardDealer] DealOpeningHands: プレイヤーカード生成に失敗しました");
                    yield break;
                }
                playerHand.Add(playerCardInstance);
                var ui = CreateCardUIForHand(playerCardInstance);
                if (ui != null) activeCardUIs.Add(ui);
                BattleUIManager.I?.UpdateStatus(
                    BattleManager.I?.GetPlayerStatus(),
                    BattleManager.I?.GetEnemyStatus());
                CardDealAudio.Play(playerCardInstance, true);
            }
            else
            {
                var enemyCardInstance = DrawOpeningCardInstance(cpuHand.Count == 0 && enemyDarkPrep, PlayerType.Enemy);
                if (enemyCardInstance == null)
                {
                    Debug.LogError("[CardDealer] DealOpeningHands: CPUカード生成に失敗しました");
                    yield break;
                }
                cpuHand.Add(enemyCardInstance);
                BattleUIManager.I?.UpdateStatus(
                    BattleManager.I?.GetPlayerStatus(),
                    BattleManager.I?.GetEnemyStatus());
            }

            yield return new WaitForSeconds(0.15f);
        }

        if (playerDarkPrep || enemyDarkPrep)
            yield return SummonDiabolosOpening.RunAfterDealBeforeRevealRoutine(playerStatus, enemyStatus);

        yield return new WaitForSeconds(0.5f);
        foreach (var ui in activeCardUIs)
            ui?.Reveal();
        SoundEffectPlayer.I?.Play("Assets/SE/バトル開始.mp3");
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

    /// <summary>開幕1枚目：ダークプリパレーションなら闇プール、それ以外は通常。</summary>
    private CardData DrawOpeningCardInstance(bool useDarkPoolFirst, PlayerType forSide)
    {
        if (useDarkPoolFirst)
            return DrawRandomDarkCardInstance(forSide);
        return DrawRandomCardInstance(forSide);
    }

    /// <summary>
    /// カードデータから1枚ランダムに選んでカードインスタンスを返す
    /// </summary>
    /// <returns>生成されたカードインスタンス</returns>
    private CardData DrawRandomCardInstance(PlayerType forSide)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugForceNextPlayerDrawSuperRarePlus && forSide == PlayerType.Player)
        {
            DebugForceNextPlayerDrawSuperRarePlus = false;
            var forced = DrawSuperRarePlusRandomCard(forSide);
            if (forced != null)
            {
                Debug.Log($"[CardDealer] Debug forced SuperRare+ draw: {forced.cardName} ({forced.rarity})");
                return forced;
            }

            Debug.LogWarning("[CardDealer] Debug SuperRare+ draw failed; falling back to normal pool.");
        }
#endif

        if (_weightedDrawPool == null || _weightedDrawPool.Count == 0)
            BuildWeightedDrawPool();

        var template = CardDrawWeightPool.PickTemplate(_weightedDrawPool, forSide);
        if (template == null)
        {
            Debug.LogWarning("[CardDealer] No drawable card template in weighted pool");
            return null;
        }

        var instance = ScriptableObject.Instantiate(template);
        instance.name = template.name;
        instance.cardUI = null;
        return instance;
    }

    /// <summary>闇属性（<see cref="CardData.element"/> == Dark）のテンプレートから1枚。プールが空なら通常抽選。</summary>
    private CardData DrawRandomDarkCardInstance(PlayerType forSide)
    {
        if (_darkCardTemplates == null || _darkCardTemplates.Length == 0)
            return DrawRandomCardInstance(forSide);

        var template = _darkCardTemplates[BattleRandom.DrawRange(forSide, 0, _darkCardTemplates.Length)];
        if (template == null) return DrawRandomCardInstance(forSide);

        var instance = ScriptableObject.Instantiate(template);
        instance.name = template.name;
        instance.cardUI = null;
        return instance;
    }

    /// <summary>
    /// ランダムカードを取得する（外部用・プレイヤー側の抽選ストリームを使用）
    /// </summary>
    /// <returns>生成されたカードインスタンス</returns>
    public CardData DrawRandomCard() => DrawRandomCard(PlayerType.Player);

    /// <summary>
    /// ランダムカードを取得する（外部用）。オンライン同期のため、どちらの手札向けかを指定する。
    /// </summary>
    public CardData DrawRandomCard(PlayerType forSide)
    {
        return DrawRandomCardInstance(forSide);
    }

    /// <summary>SuperRare 以上のテンプレートから1枚（現実改変・BattleRandom 同期）。</summary>
    public CardData DrawSuperRarePlusRandomCard(PlayerType forSide)
    {
        if (_superRarePlusDrawPool == null || _superRarePlusDrawPool.Count == 0)
            BuildSuperRarePlusDrawPool();

        var template = CardDrawWeightPool.PickTemplate(_superRarePlusDrawPool, forSide);
        if (template == null)
        {
            Debug.LogWarning("[CardDealer] No drawable SuperRare+ template in pool");
            return null;
        }

        return InstantiateCardFromTemplate(template);
    }

    /// <summary>
    /// カード名（cardName）からテンプレートを検索する（オンライン同期の手札補正用）。
    /// </summary>
    public CardData FindTemplateByName(string cardName)
    {
        if (string.IsNullOrEmpty(cardName) || allCards == null) return null;
        foreach (var c in allCards)
        {
            if (c != null && c.cardName == cardName)
                return c;
        }
        return null;
    }

    /// <summary>表示名（cardName）または asset 名でテンプレートを検索（オンライン・デバッグ注入用）。</summary>
    public CardData FindTemplateByDisplayOrAssetName(string displayOrAssetName)
    {
        if (string.IsNullOrEmpty(displayOrAssetName) || allCards == null) return null;
        foreach (var c in allCards)
        {
            if (c == null) continue;
            if (c.cardName == displayOrAssetName || c.name == displayOrAssetName)
                return c;
        }
        return null;
    }

    /// <summary>
    /// テンプレート（ScriptableObject アセット）からランタイム用の複製を生成。手札追加・デバッグ用。
    /// </summary>
    public CardData InstantiateCardFromTemplate(CardData template)
    {
        if (template == null) return null;
        var instance = ScriptableObject.Instantiate(template);
        instance.name = template.name;
        instance.cardUI = null;
        return instance;
    }

    /// <summary>
    /// プレイヤー手札用 UI を1枚生成してオブジェクト化
    /// </summary>
    /// <param name="instance">カードインスタンス</param>
    /// <returns>生成されたCardUI</returns>
    public CardUI CreateCardUIForHand(CardData instance)
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
        ui.Setup(instance, cardBackSprite, playerHandRareBackPresentation: true);

        // 現在紐付け（このインスタンスを指すUI）
        instance.cardUI = ui;
        return ui;
    }
}