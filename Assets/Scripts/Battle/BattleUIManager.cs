using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public enum Side { Player, Enemy }

/// <summary>
/// バトル画面の UI 表示・操作を司るマネージャクラス
///
/// 【主な機能】
/// - ステータス表示の更新
/// - カード詳細の表示・非表示
/// - ボタンの状態操作（使用／祈り）と「許す」表示オブジェクト
/// - ポップアップの表示（ダメージ、ミス）
/// - 手札の操作制御（選択／キャンセル）
///
/// 【責務範囲】
/// - UI 要素の表示・非表示
/// - UI 要素の状態変更
/// - カード選択の管理
/// - アニメーションの制御
///
/// 【他のクラスとの関係】
/// - BattleManager: UI 更新の指示を受ける
/// - CardSheetDisplay: カード詳細の表示
/// - DamagePopup: ダメージ表示
///
/// 【注意事項】
/// - シングルトンパターンは含まない（必要に応じて使用側が取得）
/// - エラー処理は外部に任せる
/// - マルチスレッドでの更新は行わない
/// </summary>
public class BattleUIManager : MonoBehaviour
{
    public static BattleUIManager I;

    //==== フィールド =====
    [Header("UI 要素")]
    [SerializeField] private BattleStatusUI statusUI;
    [SerializeField] private Button useButton;
    [Header("許す表示（四角オブジェクト・非インタラクティブ）")]
    [SerializeField] private GameObject yurusuDisplay;
    [SerializeField] private TMP_Text useButtonLabelTMP;
    [SerializeField] private Text useButtonLabelUGUI;
    [SerializeField] private Image useButtonImage;

    [Header("ターン表示（Canvas の TurnCount / TurnCountText）")]
    [SerializeField] private TMP_Text turnCountText;
    private bool _turnCountTextStyled;

    [Header("ポップアップ")]
    [SerializeField] private GameObject damagePopupPrefab;
    [Tooltip("未設定時は Resources.Load(\"Prefab/ImportantPopup\") を試す")]
    [SerializeField] private GameObject importantPopupPrefab;
    [Tooltip("未設定時は Resources.Load(\"Prefab/OjyouPopup\") を試す")]
    [SerializeField] private GameObject ojyouPopupPrefab;
    [SerializeField] private Canvas uiCanvas;

    [Header("カード詳細表示")]
    [SerializeField] private GameObject cardSheetPrefab;
    [SerializeField] private Transform playerCardDisplayPanel;
    [SerializeField] private Transform enemyCardDisplayPanel;
    [SerializeField] private MagicPanelUI magicPanelUI;

    [Header("魔法：手札→MagicPanel 飛行アニメ")]
    [SerializeField] private float magicHandToPanelDuration = 0.2f;

    [Header("大魔法（ArchMagic）詠唱カウントダウン")]
    [Tooltip("未設定時は TMP 既定フォント。推奨: Assets/TextMesh Pro/Fonts/DFSoge9 SDF")]
    [SerializeField] private TMPro.TMP_FontAsset archMagicCastCountdownFont;
    [Tooltip("「残り N ターン」の N 部分のみ、ベースフォントに対する TMP リッチサイズ（%）。例: 185")]
    [SerializeField] [Range(100, 260)] private int archMagicCountdownNumberSizePercent = 185;
    [Tooltip("カウントダウン行の背後に敷く白ボックスのアルファ（0〜1）")]
    [SerializeField] [Range(0f, 1f)] private float archMagicCountdownBackdropAlpha = 0.42f;

    [Header("Use ボタン設定")]
    [SerializeField] private Color useButtonNormalColor = new Color(0.2f, 0.5f, 1f, 1f);
    [SerializeField] private Color useButtonDangerColor = new Color(0.9f, 0.2f, 0.25f, 1f);
    [SerializeField] private Color useButtonPrayColor = new Color(1f, 0.95f, 0.6f, 1f);

    [Header("カード管理")]
    [SerializeField] private CardLayoutManager cardLayoutManager;
    [SerializeField] private CardSelectionManager cardSelectionManager;

    [Header("拘束：防御フェーズ「体が重い」")]
    [Tooltip("未指定なら CardLayoutManager と同じ計算で2枚目スロット相当に配置。環境でずれる場合のみ指定。")]
    [SerializeField] private RectTransform restraintHeavySlotPlayerOverride;
    [SerializeField] private RectTransform restraintHeavySlotEnemyOverride;
    private GameObject restraintHeavyGoPlayer;
    private GameObject restraintHeavyGoEnemy;

    [Header("経済アクション")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button exchangeButton;
    [SerializeField] private TMP_Text buyCooldownText;
    [SerializeField] private TMP_Text sellCooldownText;
    [SerializeField] private TMP_Text exchangeCooldownText;

    [Header("確認ポップアップ")]
    [SerializeField] private GameObject confirmPopupPrefab; // BuyConfirmPopup 用
    [SerializeField] private GameObject sellConfirmPopupPrefab; // SellConfirmPopup 用
    [SerializeField] private GameObject exchangePopupPrefab; // ExchangePopup 用
    [SerializeField] private Canvas popupCanvas;

    // プライベート変数
    private readonly List<GameObject> activeCardSheets = new();
    private enum UseButtonMode { Use, Allow, Pray, MpShortage }

    private Color _defaultUseButtonLabelColor = Color.white;
    private Sprite _cachedUseButtonSprite;
    private bool _useButtonHasRainbowGeneratedSprite;
    private bool _useButtonHasBlockingSilverStyle;
    private bool _useButtonHasArchMagicCastStyle;
    private Texture2D _rainbowUseButtonTexture;
    private Sprite _rainbowUseButtonSprite;
    private Texture2D _archMagicUseButtonTexture;
    private Sprite _archMagicUseButtonSprite;
    private float _defaultUseButtonLabelOutlineWidth;
    private Color _defaultUseButtonLabelOutlineColor = Color.black;
    private GameObject _fullscreenWhiteFlashGo;
    private GameObject _gameSetOverlayGo;

    [Header("ゲーム終了：GAMESET 表示")]
    [Tooltip("中間点・フェード前の基準スケール（1.0 = Rect の描画大きさに対する乗数）。")]
    [SerializeField] private float gameSetDisplayScale = 1.1f;
    [Tooltip("出現直後の大きさは「基準のこの倍率」。例:5 で基準の 5 倍。")]
    [SerializeField] private float gameSetStartScaleFactor = 5f;
    [SerializeField] private float gameSetShrinkToBaseDuration = 0.2f;
    [SerializeField] private float gameSetExpandDuration = 1f;
    [Tooltip("中間点からの最終拡大。例:1.5 で基準の 1.5 倍まで。")]
    [SerializeField] private float gameSetEndScaleOfBase = 1.5f;
    [SerializeField] private float gameSetFadeOutDuration = 0.4f;
    [Tooltip("GameSet スケール・フェードのイージング（前半で変化量が大きく、後半はゆるやか＝Out 系推奨）。")]
    [SerializeField] private LeanTweenType gameSetScaleEase = LeanTweenType.easeOutCubic;
    [SerializeField] private LeanTweenType gameSetFadeEase = LeanTweenType.easeOutCubic;

    // ポップアップ状態管理
    private bool isHandInputBlocked = false;
    private bool isBuyPopupOpen = false;
    private GameObject currentBuyPopup = null; // 購入確認ポップアップの参照

    private Coroutine _fogVisionAfterPopupCoroutine;

    //==== 初期化 =====
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        // ボタンコンポーネントの自動取得
        if (useButton != null)
        {
            if (useButtonLabelTMP == null) useButtonLabelTMP = useButton.GetComponentInChildren<TMP_Text>(true);
            if (useButtonLabelUGUI == null) useButtonLabelUGUI = useButton.GetComponentInChildren<Text>(true);
            if (useButtonImage == null) useButtonImage = useButton.targetGraphic as Image;
            useButton.interactable = false;
        }

        if (yurusuDisplay != null)
            yurusuDisplay.SetActive(false);

        if (useButtonLabelTMP != null)
        {
            _defaultUseButtonLabelColor = useButtonLabelTMP.color;
            _defaultUseButtonLabelOutlineWidth = useButtonLabelTMP.outlineWidth;
            _defaultUseButtonLabelOutlineColor = useButtonLabelTMP.outlineColor;
        }
        if (useButtonImage != null)
            _cachedUseButtonSprite = useButtonImage.sprite;
    }

    /// <summary>
    /// <see cref="SummonTurnCounterState"/> に基づき「ターンN」を表示。黒字・白アウトラインは初回のみ適用。
    /// </summary>
    public void RefreshTurnCountDisplay(SummonTurnCounterState counters)
    {
        if (turnCountText == null || counters == null) return;
        EnsureTurnCountTextStyle();
        turnCountText.text = $"ターン{counters.CurrentBattleTurnDisplay}";
    }

    private void EnsureTurnCountTextStyle()
    {
        if (_turnCountTextStyled || turnCountText == null) return;
        turnCountText.color = Color.black;
        if (turnCountText.outlineWidth < 0.08f)
            turnCountText.outlineWidth = 0.22f;
        turnCountText.outlineColor = Color.white;
        _turnCountTextStyled = true;
    }

    /// <summary>
    /// 相手が防具を使わなかった／使えなかったときに「許す」を表示する。非表示は <see cref="HideYurusuButton"/>（戦闘解決の await 完了後）。
    /// </summary>
    public void ShowYurusuDisplay()
    {
        if (yurusuDisplay == null) return;
        yurusuDisplay.SetActive(true);
    }

    public void HideYurusuButton()
    {
        if (yurusuDisplay == null) return;
        yurusuDisplay.SetActive(false);
    }

    //==== パブリックAPI：ステータス表示 =====
    public void UpdateStatus(PlayerStatus player, PlayerStatus enemy)
    {
        // 手札の枚数を取得（通常は現在の手札枚数を参照）
        int playerHandCount = BattleManager.I?.playerHand?.Count ?? 0;
        int enemyHandCount = BattleManager.I?.cpuHand?.Count ?? 0;

        statusUI?.UpdateStatus(player, enemy, playerHandCount, enemyHandCount);
    }

    //==== パブリックAPI：カード詳細表示 =====
    public void ShowCardDetail(CardData card, Side side)
    {
        if (card == null)
        {
            Debug.LogWarning("[BattleUIManager] ShowCardDetail: card is null");
            return;
        }

        // 既に選択されているカードの場合は選択解除
        if (cardSelectionManager.IsCardSelected(card))
        {
            // カード選択をキャンセル
            Debug.Log($"[BattleUIManager] カード選択をキャンセル: {card.cardName}");
            CancelCardSelection(card);
            return;
        }

        // カード選択を追加（上限チェックは内部で実行）
        if (cardSelectionManager.AddCardSelection(card))
        {
            // カード表示
            DisplayCard(card, side);

            // プレイヤーのカード選択時に UseButton を有効化（演出中は除く）
            if (side == Side.Player && !BattleManager.I.IsUseButtonLocked)
            {
                SetUseButtonInteractable(true);
            }

            // 効果対象を既定（相手）へ戻してから TotalATKDEF を同期
            BattleManager.I?.ResetPlayerEffectTargetToDefaultForCurrentAttackSelection();
            BattleManager.I?.UpdateTotalATKDEFDisplay();

            if (side == Side.Player
                && BattleManager.I != null
                && BattleManager.I.CurrentState == GameState.AttackPhase
                && BattleManager.I.CurrentTurnOwner == PlayerType.Player
                && !BattleManager.I.IsReflectionChainDefensePending())
            {
                var h = BattleManager.I.playerHand;
                RefreshAttackInteractivity(h, CardRules.GetAttackChoices(h));
            }
            else if (side == Side.Player
                && BattleManager.I != null
                && (BattleManager.I.CurrentState == GameState.DefensePhase
                    || (BattleManager.I.CurrentState == GameState.CombatResolvePhase && BattleManager.I.IsInterventionDefenseWaitActive())))
                BattleManager.I.RefreshPlayerDefensePhaseInteractivity();
            else if (side == Side.Player && BattleManager.I != null && BattleManager.I.IsReflectionChainDefensePending())
                UpdateDefenseButtonLabel();
        }
    }

    /// <summary>現在表示中の CardSheet から <paramref name="card"/> と同一アセット参照のシートを検索（最後に生成されたもの）。</summary>
    public bool TryGetCardSheetDisplayForCardData(CardData card, out CardSheetDisplay display)
    {
        display = null;
        if (card == null) return false;
        for (int i = activeCardSheets.Count - 1; i >= 0; i--)
        {
            var go = activeCardSheets[i];
            if (go == null) continue;
            var sh = go.GetComponent<CardSheetDisplay>();
            if (sh == null) continue;
            if (ReferenceEquals(sh.GetCurrentCardData(), card))
            {
                display = sh;
                return true;
            }
        }

        return false;
    }

    public void HideAllCardDetails()
    {
        foreach (var go in activeCardSheets)
        {
            if (go != null) Destroy(go);
        }
        activeCardSheets.Clear();
        cardSelectionManager.ClearAllSelections();
        UpdateHandCardHighlights();
        BattleManager.I?.ClearSelectedCards();
        HideRestraintHeavyOverlays();
    }

    /// <summary>
    /// プレイヤー側のカード表示のみクリア（敵側は残す）
    /// </summary>
    public void HidePlayerCardDetails()
    {
        for (int i = activeCardSheets.Count - 1; i >= 0; i--)
        {
            var go = activeCardSheets[i];
            if (go == null) { activeCardSheets.RemoveAt(i); continue; }
            if (go.transform.parent == playerCardDisplayPanel)
            {
                Destroy(go);
                activeCardSheets.RemoveAt(i);
            }
        }
        cardSelectionManager.ClearAllSelections();
        UpdateHandCardHighlights();
    }

    //==== パブリックAPI：カード選択管理 =====
    public List<CardData> GetSelectedCards()
    {
        return cardSelectionManager.GetSelectedCards();
    }

    public List<CardData> GetSelectedAttackCards()
    {
        return cardSelectionManager.GetSelectedAttackCards();
    }

    public List<CardData> GetSelectedDefenseCards()
    {
        return cardSelectionManager.GetSelectedDefenseCards();
    }

    //==== パブリックAPI：ボタン管理 =====
    public void SetUseButtonLabel(string text)
    {
        if (useButton == null) return;

        RestoreUseButtonFromReflectionRainbowIfNeeded();
        RestoreUseButtonFromBlockingSilverIfNeeded();
        if (text != "詠唱開始")
            RestoreUseButtonFromArchMagicCastIfNeeded();

        if (useButtonLabelTMP != null) useButtonLabelTMP.text = text;
        if (useButtonLabelUGUI != null) useButtonLabelUGUI.text = text;

        // 大魔法：ピンク字・白縁・紫→水色グラデーション背景
        if (text == "詠唱開始")
        {
            ApplyArchMagicCastUseButtonStyle();
            return;
        }

        if (useButtonLabelTMP != null) useButtonLabelTMP.color = _defaultUseButtonLabelColor;
        if (useButtonLabelUGUI != null) useButtonLabelUGUI.color = _defaultUseButtonLabelColor;

        var mode = text == "許す" ? UseButtonMode.Allow
                 : text == "祈り" ? UseButtonMode.Pray
                 : text == "MPが足りない" || text == "魔法使用不可" ? UseButtonMode.MpShortage
                 : UseButtonMode.Use;
        ApplyUseButtonMode(mode);
    }

    public void SetUseButtonInteractable(bool interactable)
    {
        if (useButton != null) useButton.interactable = interactable;
    }

    /// <summary>
    /// 手札カードのクリック受付のみを切り替える（見た目は変更しない）
    /// </summary>
    public void SetHandClickable(bool clickable)
    {
        isHandInputBlocked = !clickable;
        var hand = BattleManager.I?.playerHand;
        if (hand == null) return;
        foreach (var card in hand)
        {
            if (card?.cardUI == null) continue;
            var cg = card.cardUI.GetComponent<CanvasGroup>();
            if (cg == null) cg = card.cardUI.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = clickable;
        }
    }

    //==== パブリックAPI：手札管理 =====
    public void SetHandInteractivity(List<CardData> hand, bool interactable)
    {
        if (hand == null) return;
        foreach (var c in hand) SetCardInteractable(c, interactable);
    }

    public void SetCardInteractable(CardData card, bool interactable)
    {
        if (card?.cardUI == null) return;

        var btn = card.cardUI.button;
        if (btn != null) btn.interactable = interactable;

        var cg = card.cardUI.GetComponent<CanvasGroup>();
        if (cg == null) cg = card.cardUI.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = interactable ? 1f : 0.5f;
        cg.blocksRaycasts = isHandInputBlocked ? false : interactable;
    }

    public void UpdateHandInteractivity(List<CardData> hand, List<CardData> allowedCards = null)
    {
        if (hand == null) return;

        // allowedCards が null の場合はすべてのカードを使用可能にする
        if (allowedCards == null)
        {
            foreach (var card in hand)
            {
                if (card?.cardUI == null) continue;
                SetCardInteractable(card, true);
            }
            return;
        }

        // 参照比較ではなく、カードの cardUI を基準に比較する
        var allowedCardUIs = new HashSet<CardUI>();
        foreach (var allowedCard in allowedCards)
        {
            if (allowedCard?.cardUI != null)
            {
                allowedCardUIs.Add(allowedCard.cardUI);
            }
        }

        foreach (var card in hand)
        {
            if (card?.cardUI == null) continue;
            // cardUI を基準に比較（新しいカードが置き換わっても、cardUI が同じなら一致する）
            bool canUse = allowedCardUIs.Contains(card.cardUI);
            SetCardInteractable(card, canUse);
        }
    }

    public void SetPrayModeUI(List<CardData> hand)
    {
        SetUseButtonLabel("祈り");
        SetUseButtonInteractable(true);
        SetHandInteractivity(hand, false);
    }

    public void RefreshAttackInteractivity(List<CardData> hand, List<CardData> attackableCards)
    {
        var currentAttack = GetSelectedAttackCards();
        var filtered = AttackComboSelectionRules.FilterAttackChoicesForCurrentSelection(
            attackableCards, currentAttack);
        UpdateHandInteractivity(hand, filtered);
        SetUseButtonLabel("使用");
    }

    public void RefreshDefenseInteractivity(List<CardData> hand, List<CardData> defenseCards)
    {
        UpdateHandInteractivity(hand, defenseCards);
        SetUseButtonLabel("許す");
        SetUseButtonInteractable(true);
        SyncRestraintHeavyOverlay();
    }

    /// <summary>
    /// 防御側が拘束中のとき、カード表示パネル上の「体が重い」枠を表示（2枚目スロット相当またはオーバーライドRect）。
    /// </summary>
    public void SyncRestraintHeavyOverlay()
    {
        HideRestraintHeavyOverlays();
        if (BattleManager.I == null) return;
        if (BattleManager.I.CurrentState != GameState.DefensePhase) return;

        var bm = BattleManager.I;
        if (bm.DefenderPublic == PlayerType.Player && bm.GetPlayerStatus().HasRestraintEffect())
            ShowRestraintHeavyOverlay(Side.Player);
        else if (bm.DefenderPublic == PlayerType.Enemy && bm.GetEnemyStatus().HasRestraintEffect())
            ShowRestraintHeavyOverlay(Side.Enemy);
    }

    private void HideRestraintHeavyOverlays()
    {
        if (restraintHeavyGoPlayer != null) restraintHeavyGoPlayer.SetActive(false);
        if (restraintHeavyGoEnemy != null) restraintHeavyGoEnemy.SetActive(false);
    }

    private void ShowRestraintHeavyOverlay(Side side)
    {
        var go = GetOrCreateRestraintHeavyOverlay(side);
        if (go == null) return;
        LayoutRestraintHeavyOverlay(go, side);
        go.SetActive(true);
    }

    private GameObject GetOrCreateRestraintHeavyOverlay(Side side)
    {
        if (side == Side.Player)
        {
            if (restraintHeavyGoPlayer == null)
                restraintHeavyGoPlayer = BuildRestraintHeavyOverlay(Side.Player);
            return restraintHeavyGoPlayer;
        }
        if (restraintHeavyGoEnemy == null)
            restraintHeavyGoEnemy = BuildRestraintHeavyOverlay(Side.Enemy);
        return restraintHeavyGoEnemy;
    }

    private GameObject BuildRestraintHeavyOverlay(Side side)
    {
        var go = new GameObject("RestraintHeavyOverlay");
        var img = go.AddComponent<Image>();
        img.color = new Color(0.07f, 0.1f, 0.18f, 0.9f);
        img.raycastTarget = false;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "体が重い";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.9f, 0.91f, 0.96f);
        if (useButtonLabelTMP != null && useButtonLabelTMP.font != null)
            tmp.font = useButtonLabelTMP.font;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 96f;
        tmp.fontSize = 72f;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;

        return go;
    }

    private void LayoutRestraintHeavyOverlay(GameObject go, Side side)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();

        RectTransform anchor = side == Side.Player ? restraintHeavySlotPlayerOverride : restraintHeavySlotEnemyOverride;
        Transform panel = side == Side.Player ? playerCardDisplayPanel : enemyCardDisplayPanel;
        var panelRt = panel as RectTransform;

        if (anchor != null)
        {
            go.transform.SetParent(anchor, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            go.transform.SetAsLastSibling();
            return;
        }

        if (panelRt == null) return;

        float panelHeight = panelRt.rect.height;
        float cardH = cardLayoutManager != null ? cardLayoutManager.LayoutCardHeight : 120f;
        float topY = cardLayoutManager != null
            ? cardLayoutManager.GetSecondSlotTopYForPanelHeight(panelHeight)
            : -cardH - 10f;

        // 上端・左右は2枚目スロット上端に合わせ、下端だけ CardDisplayPanel の底まで伸ばす
        float bottomY = -panelHeight;

        go.transform.SetParent(panelRt, false);
        rt.anchorMin = new Vector2(0, 1f);
        rt.anchorMax = new Vector2(1, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0, bottomY);
        rt.offsetMax = new Vector2(0, topY);
        rt.localScale = Vector3.one;
        go.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Intro 時点でのカード表示（グレーアウトなし）
    /// </summary>
    /// <param name="archMagicChantUseButtonLabel">
    /// true のとき「詠唱開始」のまま（大魔法詠唱中の攻撃フェーズ差し替え時など。「使用」に戻さない）。
    /// </param>
    public void SetIntroModeUI(List<CardData> hand, bool archMagicChantUseButtonLabel = false)
    {
        HideRestraintHeavyOverlays();
        if (archMagicChantUseButtonLabel)
        {
            SetUseButtonLabel("詠唱開始");
            SetUseButtonInteractable(false);
        }
        else
            SetUseButtonLabel("使用");
        SetHandInteractivity(hand, true); // すべてのカードを有効にする（グレーアウトなし）
    }

    //==== パブリックAPI：ポップアップ =====
    /// <returns>表示したポップアップが Destroy されるまでの秒数（<see cref="DamagePopup.fadeDuration"/>）。生成失敗時は 0。</returns>
    public float ShowDamagePopup(int amount, PlayerStatus target)
    {
        if (amount > 0)
            Debug.Log($"[BattleUIManager] ダメージポップアップ表示: {amount}ダメージ 対象 {target?.DisplayName ?? "null"}");
        else
            Debug.Log($"[BattleUIManager] ダメージポップアップ表示: 無傷 対象 {target?.DisplayName ?? "null"}");

        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattleUIManager] ポップアップの生成に失敗しました");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            bool hitPlayer = (target == BattleManager.I.GetPlayerStatus());
            damageText.SetupDamage(amount, hitPlayer);
            Debug.Log($"[BattleUIManager] ダメージポップアップ設定完了: {amount}ダメージ");
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattleUIManager] DamagePopup コンポーネントが見つかりません");
        return 0f;
    }

    /// <summary>状態異常一括解除時、次のポップを出すまでの間隔（秒）。</summary>
    public const float StatusAilmentBulkClearStaggerSeconds = 0.2f;

    /// <summary>
    /// 一括解除済みの異常タイプを、付与時と同じ配色の <see cref="DamagePopup"/> で 0.2 秒ずつ重ね表示し、
    /// 最後のポップの寿命＋ポストインターバルまで待つ。
    /// </summary>
    public async Task PlayStatusAilmentBulkClearPresentationAsync(
        IReadOnlyList<StatusEffectType> clearedTypesOrdered,
        PlayerStatus target,
        CancellationToken cancellationToken = default)
    {
        if (clearedTypesOrdered == null || clearedTypesOrdered.Count == 0) return;

        float lastFade = DamagePopup.DefaultFadeDurationIfUnknown;
        for (int i = 0; i < clearedTypesOrdered.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var effectType = clearedTypesOrdered[i];
            if (effectType == StatusEffectType.None) continue;

            var popupGo = SpawnPopupFor(target);
            var damageText = popupGo != null ? popupGo.GetComponent<DamagePopup>() : null;
            if (damageText != null)
            {
                string name = StatusEffectPresentation.GetDisplayName(effectType);
                if (string.IsNullOrEmpty(name))
                    name = effectType.ToString();
                StatusEffectPresentation.GetPopupColors(effectType, out Color bg, out Color fg);
                damageText.SetupStatusAilmentGrant(name, bg, fg);
                lastFade = damageText.fadeDuration;
            }

            if (i < clearedTypesOrdered.Count - 1)
                await Task.Delay(TimeSpan.FromSeconds(StatusAilmentBulkClearStaggerSeconds), cancellationToken);
        }

        await DamagePopup.WaitAfterPopupLifetimeAsync(lastFade, cancellationToken);
    }

    /// <summary>
    /// 物理／魔法反射「弾き返す」ポップアップ。戻り値は <see cref="DamagePopup.fadeDuration"/>（秒）。
    /// </summary>
    /// <param name="magicReflection">魔法反射時は <see cref="ReflectionBounceAudio.Magic"/> を再生。</param>
    public float ShowReflectionBouncePopup(PlayerStatus target, bool magicReflection = false)
    {
        StartCoroutine(CoFullscreenWhiteFlashMs(50f));
        SoundEffectPlayer.I?.Play(magicReflection ? ReflectionBounceAudio.Magic : ReflectionBounceAudio.Physical);
        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattleUIManager] 反射ポップアップ生成に失敗");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.SetupReflectionBounce();
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattleUIManager] DamagePopup が見つかりません（反射）");
        return 0f;
    }

    /// <summary>無効化「護身」ポップアップ。戻り値は <see cref="DamagePopup.fadeDuration"/>（秒）。</summary>
    public float ShowBlockingNullifyPopup(PlayerStatus target)
    {
        StartCoroutine(CoFullscreenWhiteFlashMs(50f));
        SoundEffectPlayer.I?.Play(BlockingNullifyAudio.Physical);
        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattleUIManager] 無効化ポップアップ生成に失敗");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.SetupBlockingNullify();
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattleUIManager] DamagePopup が見つかりません（無効化）");
        return 0f;
    }

    /// <summary>
    /// 反射で表示中の攻撃カードシートを、パネル間で横スライド（線形・既定500ms）する。
    /// </summary>
    public Task SlideReflectionAttackSheetsAsync(
        List<CardData> attackCards,
        bool slideTowardPlayer,
        float durationSec,
        CancellationToken cancellationToken = default)
    {
        if (attackCards == null || attackCards.Count == 0 || cardLayoutManager == null)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        StartCoroutine(CoSlideReflectionAttackSheets(attackCards, slideTowardPlayer, durationSec, cancellationToken, () =>
        {
            if (!tcs.Task.IsCompleted)
                tcs.TrySetResult(true);
        }));
        return tcs.Task;
    }

    private IEnumerator CoSlideReflectionAttackSheets(
        List<CardData> attackCards,
        bool slideTowardPlayer,
        float durationSec,
        CancellationToken cancellationToken,
        System.Action onComplete)
    {
        Transform sourcePanel = slideTowardPlayer ? enemyCardDisplayPanel : playerCardDisplayPanel;
        Transform targetPanel = slideTowardPlayer ? playerCardDisplayPanel : enemyCardDisplayPanel;
        if (sourcePanel == null || targetPanel == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        var sheetsOrdered = new List<GameObject>();
        foreach (var ac in attackCards)
        {
            if (ac == null) continue;
            GameObject found = null;
            foreach (var go in activeCardSheets)
            {
                if (go == null) continue;
                var disp = go.GetComponent<CardSheetDisplay>();
                if (disp != null && disp.GetCardData() == ac)
                {
                    found = go;
                    break;
                }
            }
            if (found != null)
                sheetsOrdered.Add(found);
        }

        if (sheetsOrdered.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        var dstRt = targetPanel as RectTransform;
        if (cardLayoutManager != null && dstRt != null)
            cardLayoutManager.SetLayoutPanelRect(dstRt);

        var srcRt = sourcePanel as RectTransform;
        Vector3 delta = dstRt.position - srcRt.position;
        delta.y = 0f;
        delta.z = 0f;

        var starts = new Vector3[sheetsOrdered.Count];
        var ends = new Vector3[sheetsOrdered.Count];
        for (int i = 0; i < sheetsOrdered.Count; i++)
        {
            var rt = sheetsOrdered[i].transform as RectTransform;
            if (rt == null) continue;
            starts[i] = rt.position;
            ends[i] = starts[i] + delta;
        }

        float dur = Mathf.Max(0.02f, durationSec);
        float elapsed = 0f;
        while (elapsed < dur)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            for (int i = 0; i < sheetsOrdered.Count; i++)
            {
                var go = sheetsOrdered[i];
                if (go == null) continue;
                var rt = go.transform as RectTransform;
                if (rt == null) continue;
                rt.position = Vector3.Lerp(starts[i], ends[i], t);
            }
            yield return null;
        }

        cardLayoutManager.SetSelectedCards(attackCards);
        cardLayoutManager.SetActiveCardSheets(activeCardSheets);
        foreach (var go in sheetsOrdered)
        {
            if (go == null) continue;
            go.transform.SetParent(targetPanel, false);
            cardLayoutManager.SetupCardPosition(go, targetPanel);
        }

        onComplete?.Invoke();
    }

    /// <summary>
    /// 闇属性：通常の超過ダメージ適用後の「残りHP分」表示（紫背景）。SE は呼び出し側で鳴らす。
    /// </summary>
    /// <returns><see cref="DamagePopup.fadeDuration"/>（秒）。失敗時は 0。</returns>
    public float ShowDarkFollowupDamagePopup(int amount, PlayerStatus target)
    {
        Debug.Log($"[BattleUIManager] 闇フォローダメージポップアップ: {amount} 対象 {target?.DisplayName ?? "null"}");

        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattleUIManager] 闇ポップアップの生成に失敗しました");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            bool hitPlayer = (target == BattleManager.I.GetPlayerStatus());
            damageText.SetupDarkFollowupDamage(amount, hitPlayer);
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattleUIManager] DamagePopup コンポーネントが見つかりません");
        return 0f;
    }

    /// <summary>
    /// 状態異常が付与されたとき（ダメージポップと同じプレハブ）。表示成功時に SE を再生。
    /// </summary>
    public void ShowStatusAilmentGrantPopup(StatusEffectType type, PlayerStatus target)
    {
        if (target == null || type == StatusEffectType.None) return;

        string name = StatusEffectPresentation.GetDisplayName(type);
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning($"[BattleUIManager] 状態異常の表示名がありません: {type}");
            return;
        }

        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattleUIManager] 状態異常ポップアップの生成に失敗しました");
            return;
        }

        StatusEffectPresentation.GetPopupColors(type, out Color bg, out Color fg);
        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            SoundEffectPlayer.I?.Play(StatusEffectApplyFeedback.GrantSoundAddress);
            damageText.SetupStatusAilmentGrant(name, bg, fg);
            Debug.Log($"[BattleUIManager] 状態異常ポップアップ: {name}");

            // 濃霧：付与ポップアップの表示完了＋規定インターバル後まで、画面の濃霧演出（背景・オーバーレイ・パネル）を遅延する
            if (type == StatusEffectType.Fog
                && BattleManager.I != null
                && target == BattleManager.I.GetPlayerStatus()
                && statusUI != null)
            {
                statusUI.SetDeferFogVisionVisuals(true);
                if (_fogVisionAfterPopupCoroutine != null)
                    StopCoroutine(_fogVisionAfterPopupCoroutine);
                float waitSec = DamagePopup.TotalSecondsAfterPopupShown(damageText.fadeDuration);
                _fogVisionAfterPopupCoroutine = StartCoroutine(CoRevealFogVisionVisualsAfterStatusPopup(waitSec));
            }
        }
        else
            Debug.LogWarning("[BattleUIManager] DamagePopup コンポーネントが見つかりません");
    }

    /// <summary>
    /// 濃霧ポップアップ寿命（fade）と <see cref="DamagePopup.PostPopupIntervalMs"/> 経過後に濃霧画面演出を有効化。
    /// </summary>
    private IEnumerator CoRevealFogVisionVisualsAfterStatusPopup(float waitSeconds)
    {
        yield return new WaitForSeconds(waitSeconds);
        _fogVisionAfterPopupCoroutine = null;
        if (statusUI != null)
            statusUI.SetDeferFogVisionVisuals(false);
        if (BattleManager.I != null)
            UpdateStatus(BattleManager.I.GetPlayerStatus(), BattleManager.I.GetEnemyStatus());
    }

    /// <summary>回復ポップアップを表示。<see cref="DamagePopup"/> の寿命待機には戻り値を <see cref="DamagePopup.WaitAfterPopupLifetimeAsync"/> に渡す。</summary>
    /// <returns><see cref="DamagePopup.fadeDuration"/>（秒）。生成失敗時は 0。</returns>
    public float ShowHealPopup(int amount, string statType, PlayerStatus target)
    {
        Debug.Log($"[BattleUIManager] 回復ポップアップ表示: {statType}{amount}回復 対象 {target?.DisplayName ?? "null"}");

        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattleUIManager] ポップアップの生成に失敗しました");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            string displayText = $"{statType}{amount}回復";
            Color displayColor = Color.green; // 回復は緑色
            damageText.Setup(displayText, displayColor);
            Debug.Log($"[BattleUIManager] 回復ポップアップ設定完了: {statType}{amount}回復");
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattleUIManager] DamagePopup コンポーネントが見つかりません");
        return 0f;
    }

    public void ShowMissPopup(PlayerStatus target)
    {
        Debug.Log($"[BattleUIManager] ミスポップアップ表示 対象 {target?.DisplayName ?? "null"}");

        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattleUIManager] ミスポップアップの生成に失敗しました");
            return;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.Setup("ミス", Color.yellow);
            Debug.Log("[BattleUIManager] ミスポップアップ設定完了");
        }
        else
        {
            Debug.LogWarning("[BattleUIManager] DamagePopup コンポーネントが見つかりません");
        }
    }

    /// <summary>命中時（100% 未満のみ呼び出す想定）。SE は呼び出し側。</summary>
    /// <returns>フェード秒（待機の目安）</returns>
    public float ShowCombatHitConfirmedPopup(PlayerStatus target)
    {
        var popup = SpawnPopupFor(target);
        if (popup == null)
            return DamagePopup.DefaultFadeDurationIfUnknown;

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.Setup("的中", new Color(1f, 0.92f, 0.35f));
            return damageText.fadeDuration;
        }

        return DamagePopup.DefaultFadeDurationIfUnknown;
    }

    /// <summary>
    /// ステータス付近に任意メッセージのポップアップ（病系は改行入りで2行表示等）。
    /// </summary>
    /// <returns>ポップアップの <see cref="DamagePopup.fadeDuration"/>（秒）。失敗時は 0。</returns>
    public float ShowMessagePopupForTarget(PlayerStatus target, string message, Color color)
    {
        if (target == null || string.IsNullOrEmpty(message)) return 0f;

        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattleUIManager] ShowMessagePopupForTarget: ポップアップ生成に失敗");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.Setup(message, color);
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattleUIManager] ShowMessagePopupForTarget: DamagePopup がありません");
        return 0f;
    }

    /// <summary>対象のカードパネル中央にポップアップを生成し <see cref="DamagePopup"/> を返す（病系シーケンス用）。</summary>
    public DamagePopup SpawnDamagePopupForTarget(PlayerStatus target)
    {
        var go = SpawnPopupFor(target);
        return go != null ? go.GetComponent<DamagePopup>() : null;
    }

    /// <summary>
    /// プレイヤーの CardDisplayPanel 中央に情報ポップアップを表示
    /// （MP不足、魔法容量不足など）
    /// </summary>
    /// <returns>生成した <see cref="DamagePopup"/>（失敗時は null。非同期で寿命 <see cref="DamagePopup.fadeDuration"/> を待つ用途に使う）</returns>
    public DamagePopup ShowInfoPopupOnCardPanel(string message, Color color)
    {
        if (damagePopupPrefab == null || playerCardDisplayPanel == null) return null;

        var go = Instantiate(damagePopupPrefab, playerCardDisplayPanel, false);
        ApplyDamagePopupLayoutToPanelCenter(go.transform as RectTransform);

        var popup = go.GetComponent<DamagePopup>();
        if (popup != null) popup.Setup(message, color);
        return popup;
    }

    /// <summary>
    /// 重要メッセージ用。Canvas の水平中心 × 指定側 <see cref="CardDisplayPanel"/> の縦位置に配置（カードパネル子ではない）。
    /// </summary>
    /// <returns>生成した <see cref="ImportantPopup"/>（失敗時は null）</returns>
    public ImportantPopup ShowImportantPopup(string message, Color color, Side cardPanelSide)
    {
        GameObject prefab = importantPopupPrefab != null
            ? importantPopupPrefab
            : Resources.Load<GameObject>("Prefab/ImportantPopup");
        if (prefab == null || uiCanvas == null) return null;

        var go = Instantiate(prefab, uiCanvas.transform, false);
        ApplyImportantPopupLayout(go.transform as RectTransform, cardPanelSide);

        var popup = go.GetComponent<ImportantPopup>();
        if (popup != null)
            popup.Setup(message, color);
        else
            Debug.LogWarning("[BattleUIManager] ImportantPopup コンポーネントが見つかりません");
        return popup;
    }

    /// <summary>
    /// 「往生」ポップアップを表示する（ゲーム終了時）。指定側の CardDisplayPanel の子として生成し、
    /// 中央配置から <see cref="OjyouPopup"/> がパネル上端まで上昇しながらフェードする。
    /// </summary>
    public OjyouPopup ShowOjyouPopup(Side side)
    {
        GameObject prefab = ojyouPopupPrefab != null
            ? ojyouPopupPrefab
            : Resources.Load<GameObject>("Prefab/OjyouPopup");
        if (prefab == null)
        {
            Debug.LogWarning("[BattleUIManager] OjyouPopup プレハブが見つかりません");
            return null;
        }

        Transform parent = side == Side.Player ? playerCardDisplayPanel : enemyCardDisplayPanel;
        if (parent == null)
        {
            Debug.LogWarning("[BattleUIManager] CardDisplayPanel が未設定のため OjyouPopup を表示できません");
            return null;
        }

        var go = Instantiate(prefab, parent, false);
        ApplyDamagePopupLayoutToPanelCenter(go.transform as RectTransform);

        var popup = go.GetComponent<OjyouPopup>();
        if (popup != null)
            popup.Setup("往生", Color.black);
        else
            Debug.LogWarning("[BattleUIManager] OjyouPopup コンポーネントが見つかりません");
        return popup;
    }

    /// <summary>
    /// ゲーム終了時にバトル用 UI（カード表示パネル・TotalATKDEF・UseButton・許す・経済アクション）を一括で隠す／非アクティブ化する。
    /// 手札のタップも <see cref="SetHandClickable"/> で封鎖する。
    /// </summary>
    public void HideBattleUIForGameEnd()
    {
        if (playerCardDisplayPanel != null)
            playerCardDisplayPanel.gameObject.SetActive(false);
        if (enemyCardDisplayPanel != null)
            enemyCardDisplayPanel.gameObject.SetActive(false);

        if (useButton != null)
            useButton.gameObject.SetActive(false);
        if (yurusuDisplay != null)
            yurusuDisplay.SetActive(false);

        if (buyButton != null) buyButton.interactable = false;
        if (sellButton != null) sellButton.interactable = false;
        if (exchangeButton != null) exchangeButton.interactable = false;

        SetHandClickable(false);

        Debug.Log("[BattleUIManager] ゲーム終了：バトル UI を非表示化しました");
    }

    /// <summary>
    /// Canvas の X 中心と、<paramref name="cardPanelSide"/> の CardDisplayPanel の Y 中心を合わせた位置にルートを置く。
    /// </summary>
    private void ApplyImportantPopupLayout(RectTransform popupRt, Side cardPanelSide)
    {
        if (popupRt == null || uiCanvas == null) return;

        var canvasRt = uiCanvas.transform as RectTransform;
        if (canvasRt == null) return;

        Transform panelTf = cardPanelSide == Side.Player ? playerCardDisplayPanel : enemyCardDisplayPanel;
        var panelRt = panelTf as RectTransform;

        popupRt.anchorMin = new Vector2(0.5f, 0.5f);
        popupRt.anchorMax = new Vector2(0.5f, 0.5f);
        popupRt.pivot = new Vector2(0.5f, 0.5f);
        popupRt.localScale = Vector3.one;

        Vector3 canvasCenterWorld = canvasRt.TransformPoint(canvasRt.rect.center);
        Vector3 panelCenterWorld = panelRt != null
            ? panelRt.TransformPoint(panelRt.rect.center)
            : canvasCenterWorld;

        Vector3 mixedWorld = new Vector3(canvasCenterWorld.x, panelCenterWorld.y, panelCenterWorld.z);
        Vector3 localInCanvas = canvasRt.InverseTransformPoint(mixedWorld);
        popupRt.anchoredPosition = new Vector2(localInCanvas.x, localInCanvas.y);
        popupRt.SetAsLastSibling();
    }

    //==== プライベートメソッド：カード選択管理 =====
    private void CancelCardSelection(CardData card)
    {
        bool removed = cardSelectionManager.CancelCardSelection(card);
        Debug.Log($"[BattleUIManager] カード選択をキャンセル: {card.cardName} (削除成功: {removed}, selectedCards数: {cardSelectionManager.SelectedCardCount})");

        // 表示されているカードシートを削除
        RemoveCardFromDisplay(card);

        // 手札のハイライト更新
        UpdateHandCardHighlights();

        // カードレイアウトの更新
        cardLayoutManager.SetActiveCardSheets(activeCardSheets);
        cardLayoutManager.SetSelectedCards(cardSelectionManager.GetSelectedCards());
        cardLayoutManager.HandleCardCancellation();

        // BattleManager の更新
        UpdateBattleManagerAfterCancel();

        BattleManager.I?.ResetPlayerEffectTargetToDefaultForCurrentAttackSelection();
        // TotalATKDEF 表示を更新（選択が空の場合は非表示になる）
        BattleManager.I?.UpdateTotalATKDEFDisplay();

        // 攻撃フェーズでカードが0枚になったら UseButton を無効化
        if (BattleManager.I?.CurrentState == GameState.AttackPhase
            && cardSelectionManager.SelectedCardCount == 0)
        {
            SetUseButtonInteractable(false);
        }
    }

    public void ClearAllSelections()
    {
        cardSelectionManager.ClearAllSelections();
        UpdateHandCardHighlights();
        BattleManager.I?.ClearSelectedCards();
    }

    private void UpdateHandCardHighlights()
    {
        var handCards = FindObjectsOfType<CardUI>();

        foreach (var cardUI in handCards)
        {
            if (cardUI == null) continue;

            var cardData = cardUI.GetCardData();
            if (cardData == null) continue;

            bool isSelected = cardSelectionManager.IsCardSelected(cardData);
            cardUI.SetHighlight(isSelected);
        }
    }

    //==== プライベートメソッド：カード表示 =====
    private void DisplayCard(CardData card, Side side)
    {
        Transform parent = (side == Side.Player) ? playerCardDisplayPanel : enemyCardDisplayPanel;

        if (cardSheetPrefab != null && parent != null)
        {
            var go = Instantiate(cardSheetPrefab, parent);
            if (!parent.gameObject.activeSelf) parent.gameObject.SetActive(true);
            if (!go.activeSelf) go.SetActive(true);

            var display = go.GetComponent<CardSheetDisplay>();
            if (display != null)
            {
                PlayerStatus mpOwner = side == Side.Player
                    ? BattleManager.I?.GetPlayerStatus()
                    : BattleManager.I?.GetEnemyStatus();
                display.Setup(card, mpOwner);
            }

            activeCardSheets.Add(go);

            // レイアウトマネージャの更新
            cardLayoutManager.SetActiveCardSheets(activeCardSheets);
            cardLayoutManager.SetSelectedCards(cardSelectionManager.GetSelectedCards());

            // カード位置の設定
            cardLayoutManager.SetupCardPosition(go, parent);
            UpdateHandCardHighlights();
            return;
        }

        // フォールバック処理
        HandleCardDisplayFallback(card, side);
    }




    //==== プライベートメソッド：アニメーション =====
    private System.Collections.IEnumerator StackCardAnimation(GameObject cardObj, float targetX, float targetY)
    {
        var rt = cardObj.transform as RectTransform;
        if (rt == null) yield break;

        Vector3 startPos = new Vector3(0, 0, 0);
        Vector3 endPos = new Vector3(targetX, targetY, 0);
        Vector3 startScale = Vector3.one;
        Vector3 endScale = Vector3.one * 0.9f; // cardScale の既定値

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            rt.anchoredPosition = Vector3.Lerp(startPos, endPos, t);
            rt.localScale = Vector3.Lerp(startScale, endScale, t);

            yield return null;
        }

        rt.anchoredPosition = endPos;
        rt.localScale = endScale;
    }

    //==== プライベートメソッド：ボタン管理 =====
    private void ApplyUseButtonMode(UseButtonMode mode)
    {
        if (useButton == null) return;
        var img = useButtonImage ?? (useButton.targetGraphic as Image);
        if (img == null) return;

        img.color = mode == UseButtonMode.Allow || mode == UseButtonMode.MpShortage ? useButtonDangerColor
                 : mode == UseButtonMode.Pray ? useButtonPrayColor
                 : useButtonNormalColor;
    }

    //==== プライベートメソッド：ポップアップ =====
    private GameObject SpawnPopupFor(PlayerStatus target)
    {
        Debug.Log($"[BattleUIManager] ポップアップ生成 - damagePopupPrefab: {damagePopupPrefab != null}, uiCanvas: {uiCanvas != null}");

        if (damagePopupPrefab == null || uiCanvas == null)
        {
            Debug.LogWarning("[BattleUIManager] DamagePopup / Canvas が設定されていません");
            return null;
        }

        bool isPlayer = target != null && target == BattleManager.I?.GetPlayerStatus();
        Transform parent = isPlayer ? playerCardDisplayPanel : enemyCardDisplayPanel;
        if (parent == null)
        {
            Debug.LogWarning("[BattleUIManager] CardDisplayPanel / EnemyCardDisplayPanel が未設定のため Canvas 直下に出します");
            parent = uiCanvas != null ? uiCanvas.transform : null;
        }
        if (parent == null) return null;

        var go = Instantiate(damagePopupPrefab, parent, false);
        ApplyDamagePopupLayoutToPanelCenter(go.transform as RectTransform);
        Debug.Log($"[BattleUIManager] ポップアップを {(isPlayer ? "CardDisplayPanel" : "EnemyCardDisplayPanel")} 中央に配置");
        return go;
    }

    /// <summary>
    /// 親パネル中央に重なるよう、ルート RectTransform を中央アンカー・位置0にそろえる。
    /// （プレハブが stretch のときのズレを上書きする）
    /// </summary>
    private static void ApplyDamagePopupLayoutToPanelCenter(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();
    }

    //==== プライベートメソッド：ヘルパー =====
    private void RemoveCardFromDisplay(CardData card)
    {
        if (card == null) return;
        int id = card.GetInstanceID();
        for (int i = activeCardSheets.Count - 1; i >= 0; i--)
        {
            var cardObj = activeCardSheets[i];
            if (cardObj == null) continue;

            var cardDisplay = cardObj.GetComponent<CardSheetDisplay>();
            var displayed = cardDisplay?.GetCardData();
            if (displayed != null && displayed.GetInstanceID() == id)
            {
                Destroy(cardObj);
                activeCardSheets.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 表示中のカードシート（CardDisplay / EnemyDisplay のいずれか）を CardData で特定して破棄。反射「弾き返す」ポップアップ消滅後など。
    /// </summary>
    public void DestroyCardSheetForCardData(CardData card)
    {
        if (card == null) return;
        RemoveCardFromDisplay(card);
        if (cardSelectionManager != null && cardSelectionManager.IsCardSelected(card))
            cardSelectionManager.CancelCardSelection(card);
        cardLayoutManager?.SetActiveCardSheets(activeCardSheets);
        cardLayoutManager?.SetSelectedCards(cardSelectionManager != null ? cardSelectionManager.GetSelectedCards() : new List<CardData>());
        cardLayoutManager?.HandleCardCancellation();
        UpdateHandCardHighlights();
    }

    /// <summary>
    /// 指定パネル上の該当 CardData のシートだけを破棄。攻撃表示と同一インスタンスの反射カードで
    /// <see cref="DestroyCardSheetForCardData"/> を呼ぶと跳ね返し前の攻撃シートまで消えるのを防ぐ。
    /// </summary>
    public void DestroyCardSheetsForCardDataOnPanel(CardData card, Side side)
    {
        if (card == null) return;
        Transform panel = side == Side.Player ? playerCardDisplayPanel : enemyCardDisplayPanel;
        if (panel == null) return;
        int id = card.GetInstanceID();
        bool removed = false;
        for (int i = activeCardSheets.Count - 1; i >= 0; i--)
        {
            var cardObj = activeCardSheets[i];
            if (cardObj == null) continue;
            if (cardObj.transform.parent != panel) continue;
            var cardDisplay = cardObj.GetComponent<CardSheetDisplay>();
            var displayed = cardDisplay?.GetCardData();
            if (displayed != null && displayed.GetInstanceID() == id)
            {
                Destroy(cardObj);
                activeCardSheets.RemoveAt(i);
                removed = true;
            }
        }
        if (!removed) return;
        if (cardSelectionManager != null && cardSelectionManager.IsCardSelected(card))
            cardSelectionManager.CancelCardSelection(card);
        cardLayoutManager?.SetActiveCardSheets(activeCardSheets);
        cardLayoutManager?.SetSelectedCards(cardSelectionManager != null ? cardSelectionManager.GetSelectedCards() : new List<CardData>());
        cardLayoutManager?.HandleCardCancellation();
        UpdateHandCardHighlights();
    }

    /// <summary>
    /// 同一パネルに同じ CardData のシートが複数あるとき、<see cref="activeCardSheets"/> 上で最後に追加された1枚だけ破棄（反射バウンスの重複除去）。
    /// </summary>
    public void DestroyMostRecentCardSheetOnPanelForCardData(CardData card, Side side)
    {
        if (card == null) return;
        Transform panel = side == Side.Player ? playerCardDisplayPanel : enemyCardDisplayPanel;
        if (panel == null) return;
        int id = card.GetInstanceID();
        for (int i = activeCardSheets.Count - 1; i >= 0; i--)
        {
            var cardObj = activeCardSheets[i];
            if (cardObj == null) continue;
            if (cardObj.transform.parent != panel) continue;
            var cardDisplay = cardObj.GetComponent<CardSheetDisplay>();
            var displayed = cardDisplay?.GetCardData();
            if (displayed != null && displayed.GetInstanceID() == id)
            {
                Destroy(cardObj);
                activeCardSheets.RemoveAt(i);
                if (cardSelectionManager != null && cardSelectionManager.IsCardSelected(card))
                    cardSelectionManager.CancelCardSelection(card);
                cardLayoutManager?.SetActiveCardSheets(activeCardSheets);
                cardLayoutManager?.SetSelectedCards(cardSelectionManager != null ? cardSelectionManager.GetSelectedCards() : new List<CardData>());
                cardLayoutManager?.HandleCardCancellation();
                UpdateHandCardHighlights();
                return;
            }
        }
    }

    private void UpdateBattleManagerAfterCancel()
    {
        if (cardSelectionManager.HasNoSelectedCards())
        {
            BattleManager.I?.ClearSelectedCards();
            // 選択が空になった直後も手札のグレーアウトを戻す（拘束で1枚だけ許可→キャンセル時にここが無いと他防御が復帰しない）
            if (BattleManager.I != null)
            {
                if (BattleManager.I.IsReflectionChainDefensePending())
                    BattleManager.I.RefreshReflectionChainInteractivityIfPending();
                else if (BattleManager.I.CurrentState == GameState.DefensePhase
                    || (BattleManager.I.CurrentState == GameState.CombatResolvePhase && BattleManager.I.IsInterventionDefenseWaitActive()))
                    BattleManager.I.RefreshPlayerDefensePhaseInteractivity();
                else if (BattleManager.I.CurrentState == GameState.AttackPhase
                         && BattleManager.I.CurrentTurnOwner == PlayerType.Player)
                {
                    var hand = BattleManager.I.playerHand;
                    RefreshAttackInteractivity(hand, CardRules.GetAttackChoices(hand));
                }
            }
        }
        else if (BattleManager.I != null)
        {
            if (BattleManager.I.CurrentState == GameState.AttackPhase
                && !BattleManager.I.IsReflectionChainDefensePending())
            {
                if (BattleManager.I.CurrentTurnOwner == PlayerType.Player)
                {
                    var hand = BattleManager.I.playerHand;
                    RefreshAttackInteractivity(hand, CardRules.GetAttackChoices(hand));
                }
                var selectedAttackCards = GetSelectedAttackCards();
                if (selectedAttackCards.Count == 0)
                {
                    BattleManager.I.ClearSelectedCards();
                }
                else
                {
                    BattleManager.I.UpdateTotalATKDEFDisplay();
                }
            }
            else if (BattleManager.I.CurrentState == GameState.DefensePhase
                     || (BattleManager.I.CurrentState == GameState.CombatResolvePhase && BattleManager.I.IsInterventionDefenseWaitActive())
                     || BattleManager.I.IsReflectionChainDefensePending())
            {
                BattleManager.I.UpdateTotalATKDEFDisplay();
                UpdateDefenseButtonLabel();
                if (BattleManager.I.IsReflectionChainDefensePending())
                    BattleManager.I.RefreshReflectionChainInteractivityIfPending();
                else
                    BattleManager.I.RefreshPlayerDefensePhaseInteractivity();
            }
        }
    }

    /// <summary>
    /// 防御フェーズのボタンラベルを更新
    /// </summary>
    public void UpdateDefenseButtonLabel()
    {
        var bm = BattleManager.I;
        if (bm == null) return;
        bool defenseUi = bm.CurrentState == GameState.DefensePhase && bm.DefenderPublic == PlayerType.Player;
        bool interventionDefense = bm.CurrentState == GameState.CombatResolvePhase && bm.IsInterventionDefenseWaitActive();
        bool reflectionChainWait = bm.IsReflectionChainDefensePending();
        if (!defenseUi && !interventionDefense && !reflectionChainWait)
            return;

        var selectedDefenseCards = GetSelectedDefenseCards();

        List<CardData> incomingAttack = null;
        if (defenseUi)
            incomingAttack = bm.GetAttackCardsForCombatPublic();
        else if (interventionDefense)
            incomingAttack = bm.GetInterventionDefenseAttackSnapshot() ?? bm.GetAttackCardsForCombatPublic();
        else if (reflectionChainWait)
            incomingAttack = bm.GetReflectionChainAttackSnapshot();

        bool showBounce = incomingAttack != null && incomingAttack.Count > 0
            && selectedDefenseCards.Count == 1
            && selectedDefenseCards[0] != null
            && ReflectionRules.RequiresReflectionExclusiveLock(selectedDefenseCards[0], incomingAttack);

        if (showBounce)
        {
            ApplyReflectionBounceUseButtonStyle();
            SetUseButtonInteractable(true);
            return;
        }

        bool showBlockingNullify = incomingAttack != null && incomingAttack.Count > 0
            && selectedDefenseCards.Count == 1
            && selectedDefenseCards[0] != null
            && BlockingRules.RequiresBlockingExclusiveLock(selectedDefenseCards[0], incomingAttack);

        if (showBlockingNullify)
        {
            ApplyBlockingNullifyUseButtonStyle();
            SetUseButtonInteractable(true);
            return;
        }

        if (selectedDefenseCards.Count > 0)
            SetUseButtonLabel("使用");
        else
            SetUseButtonLabel("許す");
        SetUseButtonInteractable(true);
    }

    private void ApplyReflectionBounceUseButtonStyle()
    {
        if (useButton == null) return;

        RestoreUseButtonFromBlockingSilverIfNeeded();
        RestoreUseButtonFromArchMagicCastIfNeeded();

        EnsureRainbowUseButtonSprite();

        var img = useButtonImage ?? (useButton.targetGraphic as Image);
        if (img != null && _rainbowUseButtonSprite != null)
        {
            img.sprite = _rainbowUseButtonSprite;
            img.color = Color.white;
            _useButtonHasRainbowGeneratedSprite = true;
        }

        if (useButtonLabelTMP != null)
        {
            useButtonLabelTMP.text = "弾き返す";
            useButtonLabelTMP.color = Color.white;
        }

        if (useButtonLabelUGUI != null)
        {
            useButtonLabelUGUI.text = "弾き返す";
            useButtonLabelUGUI.color = Color.white;
        }
    }

    /// <summary>無効化が有効なとき：灰色のボタン＋黒字の「防衛」。</summary>
    private void ApplyBlockingNullifyUseButtonStyle()
    {
        if (useButton == null) return;

        RestoreUseButtonFromReflectionRainbowIfNeeded();
        RestoreUseButtonFromArchMagicCastIfNeeded();

        var img = useButtonImage ?? (useButton.targetGraphic as Image);
        if (img != null)
        {
            if (_cachedUseButtonSprite != null)
                img.sprite = _cachedUseButtonSprite;
            img.color = new Color(0.72f, 0.72f, 0.76f, 1f);
        }

        _useButtonHasBlockingSilverStyle = true;

        if (useButtonLabelTMP != null)
        {
            useButtonLabelTMP.text = "防衛";
            useButtonLabelTMP.color = Color.black;
        }

        if (useButtonLabelUGUI != null)
        {
            useButtonLabelUGUI.text = "防衛";
            useButtonLabelUGUI.color = Color.black;
        }
    }

    private void RestoreUseButtonFromBlockingSilverIfNeeded()
    {
        if (!_useButtonHasBlockingSilverStyle) return;

        var img = useButtonImage ?? (useButton?.targetGraphic as Image);
        if (img != null)
        {
            img.sprite = _cachedUseButtonSprite;
            ApplyUseButtonMode(UseButtonMode.Use);
        }

        _useButtonHasBlockingSilverStyle = false;
    }

    private void EnsureRainbowUseButtonSprite()
    {
        if (_rainbowUseButtonSprite != null) return;

        const int w = 256;
        const int h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        for (int y = 0; y < h; y++)
        {
            float fy = h <= 1 ? 0.5f : y / (float)(h - 1);
            for (int x = 0; x < w; x++)
            {
                float fx = w <= 1 ? 0.5f : x / (float)(w - 1);
                float t = Mathf.Clamp01((fx + (1f - fy)) * 0.5f);
                float hue = Mathf.Repeat(t * 0.95f + 0.72f, 1f);
                tex.SetPixel(x, y, Color.HSVToRGB(hue, 0.68f, 1f));
            }
        }

        tex.Apply();
        _rainbowUseButtonTexture = tex;
        _rainbowUseButtonSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private void RestoreUseButtonFromReflectionRainbowIfNeeded()
    {
        if (!_useButtonHasRainbowGeneratedSprite) return;

        var img = useButtonImage ?? (useButton?.targetGraphic as Image);
        if (img != null)
        {
            img.sprite = _cachedUseButtonSprite;
            img.color = Color.white;
        }

        if (_rainbowUseButtonTexture != null)
        {
            Destroy(_rainbowUseButtonTexture);
            _rainbowUseButtonTexture = null;
        }

        if (_rainbowUseButtonSprite != null)
        {
            Destroy(_rainbowUseButtonSprite);
            _rainbowUseButtonSprite = null;
        }

        _useButtonHasRainbowGeneratedSprite = false;
    }

    /// <summary>大魔法「詠唱開始」時：ラベル #C400A8・白縁、ボタン背景は左 #9b55fc → 右 #09f9e4 のグラデーション。</summary>
    private void ApplyArchMagicCastUseButtonStyle()
    {
        if (useButton == null) return;

        EnsureArchMagicGradientUseButtonSprite();

        var img = useButtonImage ?? (useButton.targetGraphic as Image);
        if (img != null && _archMagicUseButtonSprite != null)
        {
            img.sprite = _archMagicUseButtonSprite;
            img.color = Color.white;
            _useButtonHasArchMagicCastStyle = true;
        }

        var pink = new Color(0xC4 / 255f, 0x00 / 255f, 0xA8 / 255f, 1f);
        if (useButtonLabelTMP != null)
        {
            useButtonLabelTMP.text = "詠唱開始";
            useButtonLabelTMP.color = pink;
            useButtonLabelTMP.outlineColor = Color.white;
            useButtonLabelTMP.outlineWidth = 0.22f;
        }

        if (useButtonLabelUGUI != null)
        {
            useButtonLabelUGUI.text = "詠唱開始";
            useButtonLabelUGUI.color = pink;
        }
    }

    private void EnsureArchMagicGradientUseButtonSprite()
    {
        if (_archMagicUseButtonSprite != null) return;

        const int w = 256;
        const int h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var left = new Color(0x9b / 255f, 0x55 / 255f, 0xfc / 255f, 1f);
        var right = new Color(0x09 / 255f, 0xf9 / 255f, 0xe4 / 255f, 1f);

        for (int x = 0; x < w; x++)
        {
            float t = w <= 1 ? 0.5f : x / (float)(w - 1);
            Color c = Color.Lerp(left, right, t);
            for (int y = 0; y < h; y++)
                tex.SetPixel(x, y, c);
        }

        tex.Apply();
        _archMagicUseButtonTexture = tex;
        _archMagicUseButtonSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private void RestoreUseButtonFromArchMagicCastIfNeeded()
    {
        if (!_useButtonHasArchMagicCastStyle) return;

        var img = useButtonImage ?? (useButton?.targetGraphic as Image);
        if (img != null)
        {
            if (_cachedUseButtonSprite != null)
                img.sprite = _cachedUseButtonSprite;
            img.color = Color.white;
        }

        if (useButtonLabelTMP != null)
        {
            useButtonLabelTMP.outlineWidth = _defaultUseButtonLabelOutlineWidth;
            useButtonLabelTMP.outlineColor = _defaultUseButtonLabelOutlineColor;
        }

        if (_archMagicUseButtonTexture != null)
        {
            Destroy(_archMagicUseButtonTexture);
            _archMagicUseButtonTexture = null;
        }

        if (_archMagicUseButtonSprite != null)
        {
            Destroy(_archMagicUseButtonSprite);
            _archMagicUseButtonSprite = null;
        }

        _useButtonHasArchMagicCastStyle = false;
    }

    /// <summary>反射の弾き返しと同じ全画面白フラッシュ（ミリ秒）。劣勢時レアドロー等からも利用。</summary>
    public void PlayFullscreenWhiteFlashMs(float durationMs)
    {
        StartCoroutine(CoFullscreenWhiteFlashMs(durationMs));
    }

    private IEnumerator CoFullscreenWhiteFlashMs(float durationMs)
    {
        if (uiCanvas == null) yield break;

        if (_fullscreenWhiteFlashGo == null)
        {
            var go = new GameObject("FullscreenWhiteFlash");
            go.transform.SetParent(uiCanvas.transform, false);
            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            _fullscreenWhiteFlashGo = go;
        }

        _fullscreenWhiteFlashGo.transform.SetAsLastSibling();
        _fullscreenWhiteFlashGo.SetActive(true);
        yield return new WaitForSecondsRealtime(durationMs * 0.001f);
        if (_fullscreenWhiteFlashGo != null)
            _fullscreenWhiteFlashGo.SetActive(false);
    }

    private const string GameSetSpriteAddress = "Assets/Images/06_UIパーツ/GAMESET.png";
    private const string PostOjyouGameGongSeAddress = "Assets/SE/試合終了のゴング.mp3";

    /// <summary>
    /// 往生アニメ終了直後：反射「弾き返し」と同じ全画面白フラッシュ → 中央に GAMESET 大表示＋ゴング SE。一定時間後に画像を消す。
    /// </summary>
    public async Task ShowPostOjyouFlashAndGameSetAsync(CancellationToken ct = default)
    {
        if (uiCanvas == null) return;

        PlayFullscreenWhiteFlashMs(50f);
        try
        {
            await Task.Delay(50, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_gameSetOverlayGo != null)
        {
            Destroy(_gameSetOverlayGo);
            _gameSetOverlayGo = null;
        }

        var h = Addressables.LoadAssetAsync<Sprite>(GameSetSpriteAddress);
        var tcs = new TaskCompletionSource<Sprite>();
        h.Completed += op =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
                tcs.TrySetResult(op.Result);
            else
            {
                Debug.LogWarning("[BattleUIManager] GAMESET スプライトの読み込みに失敗: " + GameSetSpriteAddress);
                tcs.TrySetResult(null);
            }
        };

        Sprite sprite;
        try
        {
            sprite = await tcs.Task;
        }
        catch (Exception)
        {
            sprite = null;
        }

        if (sprite == null) return;

        var go = new GameObject("GameSetOverlay");
        go.transform.SetParent(uiCanvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.1f);
        rt.anchorMax = new Vector2(0.9f, 0.9f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.type = Image.Type.Simple;
        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        _gameSetOverlayGo = go;
        go.transform.SetAsLastSibling();

        SoundEffectPlayer.I?.Play(PostOjyouGameGongSeAddress);

        // 出現: 大きさ ~5x・真っ白 → 0.1s で基準 → 1s で基準の 1.5 倍 → フェードアウト
        try
        {
            await AnimateGameSetOverlayAsync(rt, img, cg, ct);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_gameSetOverlayGo != null)
            {
                Destroy(_gameSetOverlayGo);
                _gameSetOverlayGo = null;
            }
        }
    }

    private async Task AnimateGameSetOverlayAsync(RectTransform rt, Image img, CanvasGroup canvasGroup, CancellationToken ct)
    {
        if (rt == null || img == null) return;

        GameObject go = rt.gameObject;
        float baseScale = Mathf.Max(0.01f, gameSetDisplayScale);
        float fromScale = baseScale * Mathf.Max(0.1f, gameSetStartScaleFactor);
        float midScale = baseScale;
        float toScale = baseScale * Mathf.Max(0.1f, gameSetEndScaleOfBase);

        img.color = Color.white;
        img.material = null;
        rt.localScale = new Vector3(fromScale, fromScale, 1f);

        float dur0 = Mathf.Max(0.01f, gameSetShrinkToBaseDuration);
        float dur1 = Mathf.Max(0.01f, gameSetExpandDuration);
        var easeS = gameSetScaleEase;

        void ApplyScale(float s)
        {
            if (rt != null) rt.localScale = new Vector3(s, s, 1f);
        }

        try
        {
            await LeanTweenValueFloatWithEaseAsync(
                go, ApplyScale, fromScale, midScale, dur0, easeS, ct);
            if (rt != null)
                rt.localScale = new Vector3(midScale, midScale, 1f);
            await LeanTweenValueFloatWithEaseAsync(
                go, ApplyScale, midScale, toScale, dur1, easeS, ct);
            if (rt != null)
                rt.localScale = new Vector3(toScale, toScale, 1f);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        if (canvasGroup == null) return;
        try
        {
            float durFade = Mathf.Max(0.01f, gameSetFadeOutDuration);
            await LeanTweenValueFloatWithEaseAsync(
                go, a => { if (canvasGroup != null) canvasGroup.alpha = a; },
                1f, 0f, durFade, gameSetFadeEase, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    /// <summary>LeanTween で float をトゥイーン。easeOut 系は前半の変化が大きく、後半は緩やかに見える。</summary>
    private static async Task LeanTweenValueFloatWithEaseAsync(
        GameObject go,
        Action<float> onUpdate,
        float from,
        float to,
        float time,
        LeanTweenType ease,
        CancellationToken ct)
    {
        if (go == null || onUpdate == null) return;
        if (time < 0.0001f)
        {
            onUpdate(to);
            return;
        }
        onUpdate(from);
        var tcs = new TaskCompletionSource<bool>();
        var reg = ct.Register(() =>
        {
            if (go != null) LeanTween.cancel(go);
            tcs.TrySetCanceled();
        });
        try
        {
            LeanTween.value(go, onUpdate, from, to, time)
                .setEase(ease)
                .setIgnoreTimeScale(true)
                .setOnComplete(() =>
                {
                    if (!tcs.Task.IsCompleted)
                        tcs.TrySetResult(true);
                });
            await tcs.Task.ConfigureAwait(true);
        }
        finally
        {
            reg.Dispose();
        }
    }

    /// <summary>介入発動時のメッセージ（病系処理より前）。</summary>
    public void ShowInterventionIntroPopup(PlayerStatus attackerStatus)
    {
        if (attackerStatus == null) return;
        SoundEffectPlayer.I?.Play("Assets/SE/介入.mp3");
        StatusEffectPresentation.GetPopupColors(StatusEffectType.Intervention, out _, out Color textColor);
        ShowMessagePopupForTarget(attackerStatus, "未知の力が\n放たれる", textColor);
    }

    /// <summary>介入攻撃カードを表示パネル先頭に出す（選択マネージャには載せない）。</summary>
    public void ShowInterventionAttackSheet(CardData card, Side side)
    {
        if (card == null) return;
        Transform parent = side == Side.Player ? playerCardDisplayPanel : enemyCardDisplayPanel;
        if (cardSheetPrefab == null || parent == null) return;

        var go = Instantiate(cardSheetPrefab, parent);
        if (!parent.gameObject.activeSelf) parent.gameObject.SetActive(true);
        if (!go.activeSelf) go.SetActive(true);

        var display = go.GetComponent<CardSheetDisplay>();
        if (display != null)
        {
            PlayerStatus mpOwner = side == Side.Player
                ? BattleManager.I?.GetPlayerStatus()
                : BattleManager.I?.GetEnemyStatus();
            display.Setup(card, mpOwner);
        }

        activeCardSheets.Add(go);
        var single = new List<CardData> { card };
        cardLayoutManager.SetActiveCardSheets(activeCardSheets);
        cardLayoutManager.SetSelectedCards(single);
        cardLayoutManager.SetupCardPosition(go, parent);
        UpdateHandCardHighlights();
    }

    /// <summary>
    /// 攻撃選択中：魔法の合算MP（眼精疲労の倍率・群発の使用不可）に応じて使用ボタンを更新。
    /// </summary>
    public void RefreshUseButtonForMpAndSelection()
    {
        if (useButton == null || BattleManager.I == null || cardSelectionManager == null) return;

        var bm = BattleManager.I;
        if (bm.CurrentState != GameState.AttackPhase || bm.CurrentTurnOwner != PlayerType.Player)
            return;

        if (bm.IsUseButtonLocked)
            return;

        var ps = bm.GetPlayerStatus();
        if (ps == null) return;

        var selected = cardSelectionManager.GetSelectedCards();
        if (selected == null || selected.Count == 0)
        {
            // 大魔法詠唱中：演出で選択がクリアされても「詠唱開始」表示を維持（「使用」に戻さない）
            if (ps.IsCastingArchMagic)
            {
                SetUseButtonLabel("詠唱開始");
                SetUseButtonInteractable(false);
                return;
            }
            SetUseButtonLabel("使用");
            SetUseButtonInteractable(false);
            return;
        }

        // 大魔法（ArchMagic）：単独使用・ラベルは「詠唱開始」・MP は archMagic の mpCost のみを確認
        var archMagic = ArchMagicRules.FindArchMagic(selected);
        if (archMagic != null)
        {
            if (archMagic.mpCost > ps.currentMP)
            {
                SetUseButtonLabel("MPが足りない");
                SetUseButtonInteractable(false);
                return;
            }
            SetUseButtonLabel("詠唱開始");
            SetUseButtonInteractable(true);
            return;
        }

        foreach (var c in selected)
        {
            if (c != null && c.cardType == CardType.Magic && ps.IsMagicUseForbidden())
            {
                SetUseButtonLabel("魔法使用不可");
                SetUseButtonInteractable(false);
                return;
            }
        }

        int magicTotal = ps.GetTotalEffectiveMagicMpForCards(selected);
        if (magicTotal > ps.currentMP)
        {
            SetUseButtonLabel("MPが足りない");
            SetUseButtonInteractable(false);
            return;
        }

        SetUseButtonLabel("使用");
        SetUseButtonInteractable(true);
    }

    private void HandleCardDisplayFallback(CardData card, Side side)
    {
        if (cardSheetPrefab == null)
        {
            Debug.LogWarning("[BattleUIManager] cardSheetPrefab が設定されていません。CardDisplayController へのフォールバック処理を実行します。");
        }
        if ((side == Side.Player ? playerCardDisplayPanel : enemyCardDisplayPanel) == null)
        {
            Debug.LogWarning("[BattleUIManager] CardDisplayPanel が設定されていません。CardDisplayController へのフォールバック処理を実行します。side=" + side);
        }

        var controller = FindObjectOfType<CardDisplayController>(true);
        if (controller != null)
        {
            controller.ShowCard(card);
        }
        else
        {
            Debug.LogError("[BattleUIManager] すべての表示方法が利用できません。cardSheetPrefab / panel が設定されていない、CardDisplayController も見つかりません。");
        }
    }

    //==== 経済アクション =====

    /// <summary>
    /// 経済アクションボタンの状態を更新
    /// </summary>
    public void UpdateEconomicActionButtons()
    {
        if (EconomicAction.I == null) return;

        // ゲーム終了処理中は経済アクションを再アクティブ化しない
        if (BattleManager.I != null && BattleManager.I.IsGameEndTriggered)
        {
            if (buyButton != null) buyButton.interactable = false;
            if (sellButton != null) sellButton.interactable = false;
            if (exchangeButton != null) exchangeButton.interactable = false;
            return;
        }

        // 買うボタン
        if (buyButton != null)
        {
            bool canBuy = EconomicAction.I.CanBuy();
            buyButton.interactable = canBuy;
            if (buyCooldownText != null)
                buyCooldownText.text = canBuy ? "" : EconomicAction.I.GetBuyCooldown().ToString();
            buyButton.image.color = canBuy ? Color.white : Color.gray;
        }

        // 売るボタン
        if (sellButton != null)
        {
            bool canSell = EconomicAction.I.CanSell();
            sellButton.interactable = canSell;
            if (sellCooldownText != null)
                sellCooldownText.text = canSell ? "" : EconomicAction.I.GetSellCooldown().ToString();
            sellButton.image.color = canSell ? Color.white : Color.gray;
        }

        // 交換ボタン
        if (exchangeButton != null)
        {
            bool canExchange = EconomicAction.I.CanExchange();
            exchangeButton.interactable = canExchange;
            if (exchangeCooldownText != null)
                exchangeCooldownText.text = canExchange ? "" : EconomicAction.I.GetExchangeCooldown().ToString();
            exchangeButton.image.color = canExchange ? Color.white : Color.gray;
        }

        Debug.Log($"[BattleUIManager] 経済アクションボタン更新完了");
    }

    /// <summary>
    /// 買うボタンが押されたときの処理
    /// </summary>
    public void OnBuyButtonPressed()
    {
        if (EconomicAction.I == null || !EconomicAction.I.CanBuy())
        {
            Debug.LogWarning("[BattleUIManager] 買うアクションは使用できません");
            return;
        }

        // 買い自体が進行中（ポップアップ表示中または買いモード中）の場合はキャンセルのみ
        if (isBuyPopupOpen || (BattleManager.I != null && BattleManager.I.IsBuyProcessActive()))
        {
            Debug.Log("[BattleUIManager] 買いアクション進行中 → キャンセル");
            CancelBuyPopup();
            BattleManager.I?.CancelCurrentEconomicAction();
            return;
        }

        // 既にカードが選択されている場合はキャンセル
        if (cardSelectionManager != null && cardSelectionManager.SelectedCardCount > 0)
        {
            Debug.Log("[BattleUIManager] 既にカードが選択されているため、買いアクションをキャンセルします");
            cardSelectionManager.ClearAllSelections();
            BattleUIManager.I?.HideAllCardDetails();
            return;
        }

        // 他の経済アクションが進行中ならキャンセルしてから開始
        BattleManager.I?.CancelCurrentEconomicAction();

        // 購入ボタン押下時の効果音
        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す3.mp3");

        Debug.Log("[BattleUIManager] 買いアクション確認ポップアップ表示");
        ShowBuyConfirmPopup();
    }

    /// <summary>
    /// 売るボタンが押されたときの処理
    /// </summary>
    public void OnSellButtonPressed()
    {
        if (EconomicAction.I == null || !EconomicAction.I.CanSell())
        {
            Debug.LogWarning("[BattleUIManager] 売るアクションは使用できません");
            return;
        }

        // 他の経済アクションが進行中ならキャンセルしてから開始
        BattleManager.I?.CancelCurrentEconomicAction();

        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す3.mp3");

        Debug.Log("[BattleUIManager] 売るアクション実行");
        BattleManager.I?.ExecuteSellAction();
    }

    /// <summary>
    /// 交換ボタンが押されたときの処理
    /// </summary>
    public void OnExchangeButtonPressed()
    {
        if (EconomicAction.I == null || !EconomicAction.I.CanExchange())
        {
            Debug.LogWarning("[BattleUIManager] 交換アクションは使用できません");
            return;
        }

        // 交換自体が進行中の場合はキャンセルのみ（新しいポップアップは開かない）
        if (BattleManager.I != null && BattleManager.I.IsExchangeProcessActive())
        {
            Debug.Log("[BattleUIManager] 交換ポップアップ表示中 → キャンセル");
            BattleManager.I.CancelCurrentEconomicAction();
            return;
        }

        // 他の経済アクションが進行中ならキャンセルしてから開始
        BattleManager.I?.CancelCurrentEconomicAction();

        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す3.mp3");

        Debug.Log("[BattleUIManager] 交換アクション実行");
        BattleManager.I?.ExecuteExchangeAction();
    }

    /// <summary>
    /// 買いアクションの確認ポップアップを表示
    /// </summary>
    private void ShowBuyConfirmPopup()
    {
        if (confirmPopupPrefab == null)
        {
            Debug.LogError("[BattleUIManager] 確認ポップアップの Prefab が設定されていません");
            return;
        }

        var canvas = popupCanvas != null ? popupCanvas : uiCanvas;
        if (canvas == null)
        {
            Debug.LogError("[BattleUIManager] ポップアップ用の Canvas が設定されていません");
            return;
        }

        // ポップアップを生成
        var popup = Instantiate(confirmPopupPrefab, canvas.transform);
        popup.name = "BuyConfirmPopup";
        currentBuyPopup = popup; // 参照を保持

        // ポップアップのコンポーネントを取得
        var confirmPopup = popup.GetComponent<BuyConfirmPopup>();
        if (confirmPopup == null)
        {
            Debug.LogError("[BattleUIManager] BuyConfirmPopup コンポーネントが見つかりません");
            Destroy(popup);
            currentBuyPopup = null;
            return;
        }

        // ポップアップ状態を設定
        isBuyPopupOpen = true;

        // コールバックを設定
        confirmPopup.Setup(
            onConfirm: () => {
                Debug.Log("[BattleUIManager] 買いアクション承諾");
                isBuyPopupOpen = false;
                currentBuyPopup = null;
                BattleManager.I?.ExecuteBuyAction();
                Destroy(popup);
            },
            onCancel: () => {
                Debug.Log("[BattleUIManager] 買いアクションキャンセル");
                isBuyPopupOpen = false;
                currentBuyPopup = null;
                Destroy(popup);
            }
        );

        Debug.Log("[BattleUIManager] 買いアクション確認ポップアップ表示完了");
    }

    /// <summary>
    /// 購入確認ポップアップを強制クローズする（他の経済アクション開始時に使用）
    /// </summary>
    public void CancelBuyPopup()
    {
        if (!isBuyPopupOpen || currentBuyPopup == null) return;
        Debug.Log("[BattleUIManager] 買いポップアップを強制クローズ");
        isBuyPopupOpen = false;
        Destroy(currentBuyPopup);
        currentBuyPopup = null;
    }

    /// <summary>
    /// プレイヤーのカード表示エリアの中心位置を取得
    /// </summary>
    public Vector3 GetPlayerCardDisplayCenter()
    {
        if (playerCardDisplayPanel != null)
        {
            return playerCardDisplayPanel.position;
        }
        return Vector3.zero;
    }

    /// <summary>
    /// 敵のカード表示エリアの中心位置を取得
    /// </summary>
    public Vector3 GetEnemyCardDisplayCenter()
    {
        if (enemyCardDisplayPanel != null)
        {
            return enemyCardDisplayPanel.position;
        }
        return Vector3.zero;
    }

    /// <summary>
    /// プレイヤーのカード表示エリアの Transform を取得
    /// </summary>
    public Transform GetPlayerCardDisplayPanel()
    {
        return playerCardDisplayPanel;
    }

    /// <summary>
    /// 敵のカード表示エリアの Transform を取得
    /// </summary>
    public Transform GetEnemyCardDisplayPanel()
    {
        return enemyCardDisplayPanel;
    }

    /// <summary>
    /// SellConfirmPopup の Prefab を取得（BattleManager から使用）
    /// </summary>
    public GameObject GetSellConfirmPopupPrefab() => sellConfirmPopupPrefab;

    /// <summary>
    /// ExchangePopup の Prefab を取得（BattleManager から使用）
    /// </summary>
    public GameObject GetExchangePopupPrefab() => exchangePopupPrefab;

    /// <summary>
    /// カードシートの Prefab を取得
    /// </summary>
    public GameObject GetCardSheetPrefab() => cardSheetPrefab;

    /// <summary>
    /// ポップアップ用の Canvas を取得（BattleManager から使用）
    /// </summary>
    public Canvas GetPopupCanvas() => popupCanvas != null ? popupCanvas : uiCanvas;

    // ===== MagicPanel =====

    public void UpdateMagicPanel()
    {
        if (magicPanelUI == null || MagicPoolManager.I == null) return;
        magicPanelUI.Refresh(MagicPoolManager.I.GetPoolEntries());
    }

    /// <summary>
    /// プレイヤー魔法の <see cref="CardData.cardUI"/> が MagicPanel スロットの CardUI か。
    /// 手札に同種カードが残っていても、スロットに載っている参照と一致する場合のみ true（プールからの発動）。
    /// </summary>
    public bool IsPlayerMagicCardUiOnMagicPanel(CardData card)
    {
        if (card == null || card.cardType != CardType.Magic || magicPanelUI == null) return false;
        CardUI poolSlotUi = magicPanelUI.GetCardUI(card);
        return poolSlotUi != null && card.cardUI != null && ReferenceEquals(card.cardUI, poolSlotUi);
    }

    /// <summary>
    /// 手札の魔法カードが MagicPanel のスロットへ直線移動する演出（プール登録は呼び出し側）
    /// </summary>
    public async Task PlayMagicFlyHandToPanelAsync(CardData card, RectTransform handCardRt, int slotIndex)
    {
        if (card == null || handCardRt == null || magicPanelUI == null || card.cardImage == null)
        {
            await Task.CompletedTask;
            return;
        }

        if (!magicPanelUI.TryGetSlotTargetRect(slotIndex, out RectTransform slotRt) || slotRt == null)
        {
            await Task.CompletedTask;
            return;
        }

        Canvas canvas = uiCanvas != null ? uiCanvas : handCardRt.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            await Task.CompletedTask;
            return;
        }

        RectTransform canvasRt = canvas.transform as RectTransform;

        var fly = new GameObject("MagicHandToPanelFly");
        var flyRt = fly.AddComponent<RectTransform>();
        flyRt.SetParent(canvasRt, false);
        flyRt.SetAsLastSibling();
        var img = fly.AddComponent<Image>();
        img.sprite = card.cardImage;
        img.preserveAspect = true;
        img.raycastTarget = false;
        flyRt.sizeDelta = new Vector2(handCardRt.rect.width, handCardRt.rect.height);

        Vector3 startWorld = handCardRt.TransformPoint(handCardRt.rect.center);
        Vector3 endWorld = slotRt.TransformPoint(slotRt.rect.center);
        fly.transform.position = startWorld;

        LeanTween.move(fly, endWorld, magicHandToPanelDuration).setEase(LeanTweenType.easeOutCubic);

        int ms = Mathf.Max(1, Mathf.RoundToInt(magicHandToPanelDuration * 1000f));
        await Task.Delay(ms);

        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す42.mp3");

        if (fly != null) Destroy(fly);
    }

    public void RefreshMagicCardInteractivity(List<CardData> hand)
    {
        if (magicPanelUI == null) return;
        bool interactable = BattleManager.I != null
            && BattleManager.I.CurrentState == GameState.AttackPhase
            && !isHandInputBlocked;
        magicPanelUI.SetAllInteractable(interactable);
    }

    // ===== 大魔法（ArchMagic）詠唱中央オーバーレイ =====
    private GameObject _archMagicCastOverlay;
    private CanvasGroup _archMagicCastOverlayCanvasGroup;
    private Image _archMagicCastDimImage;
    private Image _archMagicCastOverlayImage;
    private TMPro.TMP_Text _archMagicCastOverlayRemainingText;

    /// <summary>詠唱中：全画面ディム + 中央に大魔法アイコン + 残りターンをフェードイン表示する。</summary>
    public async Task FadeInArchMagicCastOverlayAsync(Sprite magicSprite, int remainingTurns, int fadeMs, CancellationToken ct)
    {
        EnsureArchMagicCastOverlay();
        if (_archMagicCastOverlay == null) return;

        if (_archMagicCastOverlayImage != null) _archMagicCastOverlayImage.sprite = magicSprite;
        UpdateArchMagicCastOverlayRemaining(remainingTurns);

        _archMagicCastOverlay.SetActive(true);
        if (_archMagicCastOverlayCanvasGroup != null)
        {
            _archMagicCastOverlayCanvasGroup.alpha = 0f;
            _archMagicCastOverlayCanvasGroup.blocksRaycasts = false;
        }

        int steps = Mathf.Max(1, fadeMs / 16);
        float stepDelta = 1f / steps;
        int stepMs = Mathf.Max(1, fadeMs / steps);
        for (int i = 1; i <= steps; i++)
        {
            if (ct.IsCancellationRequested) break;
            if (_archMagicCastOverlayCanvasGroup != null)
                _archMagicCastOverlayCanvasGroup.alpha = Mathf.Clamp01(stepDelta * i);
            await Task.Delay(stepMs, ct);
        }
        if (_archMagicCastOverlayCanvasGroup != null)
        {
            _archMagicCastOverlayCanvasGroup.alpha = 1f;
            _archMagicCastOverlayCanvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>残りターン数のみ差し替える（ダウンカウント表現用）。</summary>
    public void UpdateArchMagicCastOverlayRemaining(int remainingTurns)
    {
        if (_archMagicCastOverlayRemainingText == null) return;
        _archMagicCastOverlayRemainingText.richText = true;
        int pct = Mathf.Clamp(archMagicCountdownNumberSizePercent, 100, 260);
        _archMagicCastOverlayRemainingText.text =
            $"残り <size={pct}%>{remainingTurns}</size> ターン";
    }

    /// <summary>詠唱中央オーバーレイを消す（即時 or フェード）。</summary>
    public async Task FadeOutArchMagicCastOverlayAsync(int fadeMs, CancellationToken ct)
    {
        if (_archMagicCastOverlay == null || _archMagicCastOverlayCanvasGroup == null)
        {
            HideArchMagicCastOverlayImmediate();
            return;
        }

        _archMagicCastOverlayCanvasGroup.blocksRaycasts = false;

        int steps = Mathf.Max(1, fadeMs / 16);
        float stepDelta = 1f / steps;
        int stepMs = Mathf.Max(1, fadeMs / steps);
        float a = _archMagicCastOverlayCanvasGroup.alpha;
        for (int i = 1; i <= steps; i++)
        {
            if (ct.IsCancellationRequested) break;
            _archMagicCastOverlayCanvasGroup.alpha = Mathf.Clamp01(a - stepDelta * i);
            await Task.Delay(stepMs, ct);
        }
        HideArchMagicCastOverlayImmediate();
    }

    public void HideArchMagicCastOverlayImmediate()
    {
        if (_archMagicCastOverlay == null) return;
        _archMagicCastOverlay.SetActive(false);
        if (_archMagicCastOverlayCanvasGroup != null)
        {
            _archMagicCastOverlayCanvasGroup.alpha = 0f;
            _archMagicCastOverlayCanvasGroup.blocksRaycasts = false;
        }
    }

    private void ClearArchMagicCastOverlayInternalRefs()
    {
        _archMagicCastOverlay = null;
        _archMagicCastOverlayCanvasGroup = null;
        _archMagicCastDimImage = null;
        _archMagicCastOverlayImage = null;
        _archMagicCastOverlayRemainingText = null;
    }

    private void EnsureArchMagicCastOverlay()
    {
        if (_archMagicCastOverlay != null)
        {
            if (_archMagicCastDimImage != null)
                return;
            Destroy(_archMagicCastOverlay);
            ClearArchMagicCastOverlayInternalRefs();
        }

        var canvas = popupCanvas != null ? popupCanvas : uiCanvas;
        if (canvas == null) return;

        var root = new GameObject("ArchMagicCastOverlay", typeof(RectTransform), typeof(CanvasGroup));
        var rt = root.GetComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();

        var cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // 背面：半透明ブラックで画面全体を暗くする
        var dimGo = new GameObject("ArchMagicDim", typeof(RectTransform));
        var dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.SetParent(rt, false);
        dimRt.SetAsFirstSibling();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        var dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.5f);
        dimImg.raycastTarget = true;

        // 前面：アイコン + 残りターン
        var contentGo = new GameObject("ArchMagicCastContent", typeof(RectTransform));
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.SetParent(rt, false);
        contentRt.anchorMin = new Vector2(0.5f, 0.5f);
        contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.pivot = new Vector2(0.5f, 0.5f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(420f, 560f);

        var imgGo = new GameObject("Icon", typeof(RectTransform));
        var imgRt = imgGo.GetComponent<RectTransform>();
        imgRt.SetParent(contentRt, false);
        imgRt.anchorMin = new Vector2(0.5f, 0.5f);
        imgRt.anchorMax = new Vector2(0.5f, 0.5f);
        imgRt.pivot = new Vector2(0.5f, 0.5f);
        imgRt.anchoredPosition = new Vector2(0f, 50f);
        imgRt.sizeDelta = new Vector2(360f, 360f);
        var iconImg = imgGo.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        var panelGo = new GameObject("RemainingBackdrop", typeof(RectTransform));
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.SetParent(contentRt, false);
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = new Vector2(0f, -210f);
        panelRt.sizeDelta = new Vector2(720f, 108f);
        var backdrop = panelGo.AddComponent<Image>();
        {
            var w = Texture2D.whiteTexture;
            backdrop.sprite = Sprite.Create(w, new Rect(0, 0, w.width, w.height), new Vector2(0.5f, 0.5f), 100f);
        }
        backdrop.type = Image.Type.Simple;
        backdrop.color = new Color(1f, 1f, 1f, Mathf.Clamp01(archMagicCountdownBackdropAlpha));
        backdrop.raycastTarget = false;

        var textGo = new GameObject("Remaining", typeof(RectTransform));
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(panelRt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(14f, 10f);
        textRt.offsetMax = new Vector2(-14f, -10f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontSize = 58f;
        tmp.richText = true;
        if (archMagicCastCountdownFont != null)
            tmp.font = archMagicCastCountdownFont;
        else if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontStyle = TMPro.FontStyles.Normal;
        tmp.color = Color.white;
        tmp.outlineColor = new Color(0.85f, 0.12f, 0.12f, 1f);
        tmp.outlineWidth = 0.22f;
        tmp.text = "";
        tmp.raycastTarget = false;

        _archMagicCastOverlay = root;
        _archMagicCastOverlayCanvasGroup = cg;
        _archMagicCastDimImage = dimImg;
        _archMagicCastOverlayImage = iconImg;
        _archMagicCastOverlayRemainingText = tmp;
        root.SetActive(false);
    }
}
