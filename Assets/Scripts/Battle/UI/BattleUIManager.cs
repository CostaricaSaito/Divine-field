using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum Side { Player, Enemy }

/// <summary>
/// バトル画面 UI の「窓口（Facade）」。基本ステータス表示（ターン数・HP/MP など）と、
/// 各サブマネージャ（UseButton / Economic / Effect / ArchMagic / RestraintHeavy / Magic / Popup / Card）への
/// 薄い委譲のみを担当する。新規 UI ロジックは原則いずれかのサブマネージャへ追加し、
/// ここには外部 API 互換のために必要な公開メソッドだけを薄く残す。
///
/// 【責務範囲】
/// - シングルトン <see cref="I"/> の管理
/// - ステータス表示（<see cref="BattleStatusUI"/>）とターン表示（<see cref="turnCountText"/>）の更新
/// - サブマネージャへの API 委譲（<see cref="BattleManager"/> 等、外部からのエントリポイントを維持）
/// - 濃霧ポップアップ直後の遅延ベール（<see cref="ScheduleFogVisionRevealAfterPopup"/>）
///   のみ例外的に保持（<see cref="BattleStatusUI.SetDeferFogVisionVisuals"/> を叩くため）
///
/// 【サブマネージャ一覧】
/// - <see cref="UseButtonPresenter"/>：使用ボタン／許す表示／反射・無効化・詠唱開始スタイル
/// - <see cref="EconomicUIHandler"/>：購入／売却／交換ボタンと確認ポップ
/// - <see cref="BattleEffectPresenter"/>：全画面フラッシュ／GAMESET／往生後演出
/// - <see cref="ArchMagicOverlayPresenter"/>：大魔法詠唱中央オーバーレイ
/// - <see cref="RestraintHeavyOverlayPresenter"/>：拘束「体が重い」オーバーレイ
/// - <see cref="MagicPanelPresenter"/>：魔法パネル更新／手札→パネル飛行アニメ
/// - <see cref="BattlePopupPresenter"/>：ダメージ／回復／ミス／反射／無効／状態異常／Important／Ojyou ポップ
/// - <see cref="BattleCardUIController"/>：カード詳細表示・選択・手札インタラクティビティ・反射スライド・介入表示
///
/// 【注意事項】
/// - サブ相互に直接参照を張らず、本クラスをハブにして <see cref="I"/> 経由で呼ぶこと（循環依存の予防）。
/// - 新規ロジックは極力サブ側へ。ここに増えた場合は責務の見直しサイン。
/// </summary>
public class BattleUIManager : MonoBehaviour
{
    public static BattleUIManager I;

    //==== フィールド =====
    [Header("UI 要素")]
    [SerializeField] private BattleStatusUI statusUI;

    [Header("サブマネージャ")]
    [SerializeField] private UseButtonPresenter useButtonPresenter;
    [SerializeField] private EconomicUIHandler economicHandler;
    [SerializeField] private BattleEffectPresenter effectPresenter;
    [SerializeField] private ArchMagicOverlayPresenter archMagicPresenter;
    [SerializeField] private ArchMagicBarrierPresenter archMagicBarrierPresenter;
    [SerializeField] private RestraintHeavyOverlayPresenter restraintHeavyPresenter;
    [SerializeField] private MagicPanelPresenter magicPanelPresenter;
    [SerializeField] private BattlePopupPresenter popupPresenter;
    [SerializeField] private BattleCardUIController cardController;

    [Header("ターン表示（Canvas の TurnCount / TurnCountText）")]
    [SerializeField] private GameObject turnCountRoot;
    [SerializeField] private Image turnCountBackground;
    [SerializeField] private TMP_Text turnCountText;
    private bool _turnCountTextStyled;

    private static readonly Color TurnBgColorPlayer = new Color(0x39 / 255f, 0x32 / 255f, 0xE2 / 255f);
    private static readonly Color TurnBgColorEnemy = new Color(0xE2 / 255f, 0x4C / 255f, 0x32 / 255f);

    [Header("メイン Canvas")]
    [SerializeField] private Canvas uiCanvas;

    private Coroutine _fogVisionAfterPopupCoroutine;

    //==== 初期化 =====
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        if (turnCountRoot == null && turnCountText != null)
            turnCountRoot = turnCountText.transform.parent != null ? turnCountText.transform.parent.gameObject : null;
        if (turnCountBackground == null && turnCountRoot != null)
            turnCountBackground = turnCountRoot.GetComponent<Image>();
        if (turnCountRoot != null)
            turnCountRoot.SetActive(false);

        if (archMagicBarrierPresenter == null)
            archMagicBarrierPresenter = GetComponent<ArchMagicBarrierPresenter>();
        if (archMagicBarrierPresenter == null)
            archMagicBarrierPresenter = gameObject.AddComponent<ArchMagicBarrierPresenter>();

        if (archMagicBarrierPresenter != null && statusUI != null)
            archMagicBarrierPresenter.BindNameAnchors(statusUI.playerNameText, statusUI.enemyNameText);
    }

    /// <summary>
    /// <see cref="SummonTurnCounterState"/> に基づき「ターンN」を表示。
    /// 初回呼び出しで TurnCount を表示し、手番所有者に応じて背景色を切り替える。
    /// </summary>
    public void RefreshTurnCountDisplay(SummonTurnCounterState counters, PlayerType turnOwner)
    {
        if (turnCountText == null || counters == null) return;

        if (turnCountRoot != null && !turnCountRoot.activeSelf)
            turnCountRoot.SetActive(true);

        EnsureTurnCountTextStyle();
        turnCountText.text = $"ターン{counters.CurrentBattleTurnDisplay}";

        if (turnCountBackground != null)
        {
            var bg = turnOwner == PlayerType.Player ? TurnBgColorPlayer : TurnBgColorEnemy;
            var alpha = turnCountBackground.color.a;
            turnCountBackground.color = new Color(bg.r, bg.g, bg.b, alpha);
        }
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
    /// 相手（CPU）が DefenseSelect を防御0枚で完了し、戦闘解決（CombatSequence 相当）に入った区間でのみ
    /// 専用の「許す」装飾を出す。自プレイヤー防御中の表現は <see cref="SetUseButtonLabel"/>。
    /// 非表示は <see cref="HideYurusuButton"/>（解決の await 完了後など）。
    /// </summary>
    public void ShowYurusuDisplay() => useButtonPresenter?.ShowYurusuDisplay();

    public void HideYurusuButton() => useButtonPresenter?.HideYurusuDisplay();

    //==== パブリックAPI：ステータス表示 =====
    public void UpdateStatus(PlayerStatus player, PlayerStatus enemy, bool snapHpmgpNumbers = false)
    {
        // 手札の枚数を取得（通常は現在の手札枚数を参照）
        int playerHandCount = BattleManager.I?.playerHand?.Count ?? 0;
        int enemyHandCount = BattleManager.I?.cpuHand?.Count ?? 0;

        statusUI?.UpdateStatus(player, enemy, playerHandCount, enemyHandCount, snapHpmgpNumbers);
        BattleManager.I?.RefreshSummonSkillButtonInteractables();
        HitRateRules.MonitorAndRefreshHitRateDisplaysIfNeeded(player, enemy);
    }

    public void RefreshActiveCardSheetHitRateDisplaysForOwner(PlayerStatus owner) =>
        cardController?.RefreshActiveCardSheetHitRateDisplaysForOwner(owner);

    public void RefreshMagicPanelHitRateDisplays() =>
        magicPanelPresenter?.RefreshHitRateDisplays();

    //==== パブリックAPI：カード詳細表示（BattleCardUIController へ委譲） =====
    public void ShowCardDetail(CardData card, Side side) => cardController?.ShowCardDetail(card, side);

    /// <summary>現在表示中の CardSheet から <paramref name="card"/> と同一アセット参照のシートを検索（最後に生成されたもの）。</summary>
    public bool TryGetCardSheetDisplayForCardData(CardData card, out CardSheetDisplay display)
    {
        if (cardController != null)
            return cardController.TryGetCardSheetDisplayForCardData(card, out display);
        display = null;
        return false;
    }

    public void HideAllCardDetails() => cardController?.HideAllCardDetails();

    /// <summary>プレイヤー／敵の CardDisplay 直子を即破棄（宝玉の再掲前など。通常は <see cref="HideAllCardDetails"/>）。</summary>
    public void ClearAllCardDisplaysAndSelectionImmediate() =>
        cardController?.ClearAllCardDisplaysAndSelectionImmediate();

    /// <summary>指定側の CardDisplayPanel を即破棄（天変地異の混沌→Disaster 差し替え等）。</summary>
    public void ClearCardDisplayPanelImmediate(Side side) =>
        cardController?.ClearCardDisplayPanelImmediate(side);

    /// <summary>プレイヤー側のカード表示のみクリア（敵側は残す）</summary>
    public void HidePlayerCardDetails() => cardController?.HidePlayerCardDetails();

    /// <summary>敵側のカード表示のみクリア（プレイヤー側は残す）</summary>
    public void HideEnemyCardDetails() => cardController?.HideEnemyCardDetails();

    /// <summary>選択フローなしで CardDisplayPanel 相当にシートを出す（双剣2本目用・1枚）。</summary>
    public void ShowCardSheetVisualOnly(CardData card, Side side) => cardController?.ShowCardSheetVisualOnly(card, side);

    /// <summary>攻撃使用カード列を一括表示（双剣2本目の再掲示）。レイアウト用に選択は載せない。</summary>
    public void ShowCardSheetsVisualOnlyBatch(IReadOnlyList<CardData> cards, Side side) =>
        cardController?.ShowCardSheetsVisualOnlyBatch(cards, side);

    //==== パブリックAPI：カード選択管理 =====
    public List<CardData> GetSelectedCards()
        => cardController != null ? cardController.GetSelectedCards() : new List<CardData>();

    public List<CardData> GetSelectedAttackCards()
        => cardController != null ? cardController.GetSelectedAttackCards() : new List<CardData>();

    public List<CardData> GetSelectedDefenseCards()
        => cardController != null ? cardController.GetSelectedDefenseCards() : new List<CardData>();

    //==== パブリックAPI：ボタン管理 =====
    public void SetUseButtonLabel(string text) => useButtonPresenter?.SetUseButtonLabel(text);

    public void SetUseButtonInteractable(bool interactable) => useButtonPresenter?.SetUseButtonInteractable(interactable);

    /// <summary>手札カードのクリック受付のみを切り替える（見た目は変更しない）。</summary>
    public void SetHandClickable(bool clickable) => cardController?.SetHandClickable(clickable);

    /// <summary>手札をすべてグレーアウト（操作不可の見た目）にする／解除する。</summary>
    public void SetHandGrayedOut(List<CardData> hand, bool grayedOut)
        => cardController?.SetHandInteractivity(hand, !grayedOut);

    /// <summary>
    /// 相手の防御カードを表示（手札選択を介さない）。
    /// マジックパネル上の魔法防御（アイアンクラッド等）は <see cref="ShowCardDetail"/> だと
    /// 容量チェック等で失敗するため、こちらを使う。
    /// </summary>
    public void ShowEnemyDefenseCardPresentation(CardData card)
        => ShowCardSheetVisualOnly(card, Side.Enemy);

    /// <summary>相手の複数枚防御を順次表示（0.5秒/枚）。</summary>
    public async Task ShowEnemyDefenseCardsPresentationSequenceAsync(IReadOnlyList<CardData> cards)
    {
        if (cardController == null || cards == null || cards.Count == 0) return;

        for (int i = 0; i < cards.Count; i++)
        {
            var partial = new List<CardData>(i + 1);
            for (int j = 0; j <= i; j++)
            {
                if (cards[j] != null) partial.Add(cards[j]);
            }
            if (partial.Count == 0) continue;

            cardController.HideEnemyCardDetails();
            cardController.ShowCardSheetsVisualOnlyBatch(partial, Side.Enemy);
            BattleManager.I?.SetStatsDisplaySequenceCards(partial, "防御", Side.Enemy);
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            if (i < cards.Count - 1)
                await Task.Delay(500);
        }
    }

    /// <summary>プレイヤーの複数枚防御を順次表示（0.5秒/枚）。PostDeath 道連れ等、戦闘解決を伴わない掲出用。</summary>
    public async Task ShowPlayerDefenseCardsPresentationSequenceAsync(IReadOnlyList<CardData> cards)
    {
        if (cardController == null || cards == null || cards.Count == 0) return;

        for (int i = 0; i < cards.Count; i++)
        {
            var partial = new List<CardData>(i + 1);
            for (int j = 0; j <= i; j++)
            {
                if (cards[j] != null) partial.Add(cards[j]);
            }
            if (partial.Count == 0) continue;

            cardController.HidePlayerCardDetails();
            cardController.ShowCardSheetsVisualOnlyBatch(partial, Side.Player);
            BattleManager.I?.SetStatsDisplaySequenceCards(partial, "防御", Side.Player);
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            if (i < cards.Count - 1)
                await Task.Delay(500);
        }
    }

    //==== パブリックAPI：手札管理（BattleCardUIController へ委譲） =====
    public void SetHandInteractivity(List<CardData> hand, bool interactable)
        => cardController?.SetHandInteractivity(hand, interactable);

    public void SetCardInteractable(CardData card, bool interactable)
        => cardController?.SetCardInteractable(card, interactable);

    public void UpdateHandInteractivity(List<CardData> hand, List<CardData> allowedCards = null)
        => cardController?.UpdateHandInteractivity(hand, allowedCards);

    public void SetPrayModeUI(List<CardData> hand) => cardController?.SetPrayModeUI(hand);

    public void RefreshAttackInteractivity(List<CardData> hand, List<CardData> attackableCards)
        => cardController?.RefreshAttackInteractivity(hand, attackableCards);

    public void RefreshDefenseInteractivity(List<CardData> hand, List<CardData> defenseCards)
        => cardController?.RefreshDefenseInteractivity(hand, defenseCards);

    /// <summary>
    /// 防御側が拘束中のとき、カード表示パネル上の「体が重い」枠を表示（2枚目スロット相当またはオーバーライドRect）。
    /// </summary>
    public void SyncRestraintHeavyOverlay() => restraintHeavyPresenter?.Sync();

    /// <summary>拘束「体が重い」枠を全て非表示化する（介入・カード全クリア等）。</summary>
    public void HideRestraintHeavyOverlays() => restraintHeavyPresenter?.HideAll();

    /// <summary>
    /// Intro 時点でのカード表示（グレーアウトなし）
    /// </summary>
    public void SetIntroModeUI(List<CardData> hand)
        => cardController?.SetIntroModeUI(hand);

    //==== パブリックAPI：ポップアップ（BattlePopupPresenter へ委譲） =====
    /// <returns>表示したポップアップが Destroy されるまでの秒数（<see cref="DamagePopup.fadeDuration"/>）。生成失敗時は 0。</returns>
    public float ShowDamagePopup(int amount, PlayerStatus target)
        => popupPresenter != null ? popupPresenter.ShowDamagePopup(amount, target) : 0f;

    /// <summary>状態異常一括解除時、次のポップを出すまでの間隔（秒）。</summary>
    public const float StatusAilmentBulkClearStaggerSeconds = BattlePopupPresenter.StatusAilmentBulkClearStaggerSeconds;

    /// <summary>
    /// 一括解除済みの異常タイプを、付与時と同じ配色の <see cref="DamagePopup"/> で 0.2 秒ずつ重ね表示し、
    /// 最後のポップの寿命＋ポストインターバルまで待つ。
    /// </summary>
    public Task PlayStatusAilmentBulkClearPresentationAsync(
        IReadOnlyList<StatusEffectType> clearedTypesOrdered,
        PlayerStatus target,
        CancellationToken cancellationToken = default)
        => popupPresenter != null
            ? popupPresenter.PlayStatusAilmentBulkClearPresentationAsync(clearedTypesOrdered, target, cancellationToken)
            : Task.CompletedTask;

    /// <summary>
    /// 物理／魔法反射「弾き返す」ポップアップ。戻り値は <see cref="DamagePopup.fadeDuration"/>（秒）。
    /// </summary>
    public float ShowReflectionBouncePopup(PlayerStatus target, bool magicReflection = false)
        => popupPresenter != null ? popupPresenter.ShowReflectionBouncePopup(target, magicReflection) : 0f;

    /// <summary>無効化「護身」ポップアップ。戻り値は <see cref="DamagePopup.fadeDuration"/>（秒）。</summary>
    public float ShowBlockingNullifyPopup(PlayerStatus target)
        => popupPresenter != null ? popupPresenter.ShowBlockingNullifyPopup(target) : 0f;

    /// <summary>打ち払い「打ち払う」ポップアップ（黄・白字・黒縁・白フラッシュ・SE）。</summary>
    public float ShowParryIntroPopup(PlayerStatus target)
        => popupPresenter != null ? popupPresenter.ShowParryIntroPopup(target) : 0f;

    /// <summary>打ち払い後、攻撃が防御側に戻ったときのポップアップ。</summary>
    public float ShowParryReturnToSelfPopup(PlayerStatus target)
        => popupPresenter != null ? popupPresenter.ShowParryReturnToSelfPopup(target) : 0f;

    /// <summary>
    /// 反射で表示中の攻撃カードシートを、パネル間で横スライド（線形・既定500ms）する。
    /// </summary>
    public Task SlideReflectionAttackSheetsAsync(
        List<CardData> attackCards,
        bool slideTowardPlayer,
        float durationSec,
        CancellationToken cancellationToken = default)
        => cardController != null
            ? cardController.SlideReflectionAttackSheetsAsync(attackCards, slideTowardPlayer, durationSec, cancellationToken)
            : Task.CompletedTask;

    /// <summary>
    /// 闇属性：通常の超過ダメージ適用後の「残りHP分」表示（紫背景）。SE は呼び出し側で鳴らす。
    /// </summary>
    public float ShowDarkFollowupDamagePopup(int amount, PlayerStatus target)
        => popupPresenter != null ? popupPresenter.ShowDarkFollowupDamagePopup(amount, target) : 0f;

    /// <summary>
    /// 状態異常が付与されたとき（ダメージポップと同じプレハブ）。表示成功時に SE を再生。
    /// </summary>
    /// <returns><see cref="DamagePopup.fadeDuration"/>。Presenter 未設定時は 0。</returns>
    public float ShowStatusAilmentGrantPopup(StatusEffectType type, PlayerStatus target)
        => popupPresenter != null ? popupPresenter.ShowStatusAilmentGrantPopup(type, target) : 0f;

    /// <summary>手札リロードのメッセージポップ（ピンク背景・<c>Assets/SE/リロード.mp3</c>）。</summary>
    public float ShowHandReloadPopup(PlayerStatus target)
        => popupPresenter != null ? popupPresenter.ShowHandReloadPopup(target) : 0f;

    public float ShowHandDiscardRestartPopup(PlayerStatus target)
        => popupPresenter != null ? popupPresenter.ShowHandDiscardRestartPopup(target) : 0f;

    /// <summary>
    /// 濃霧ポップアップ寿命（fade）と <see cref="DamagePopup.PostPopupIntervalMs"/> 経過後に濃霧画面演出を有効化する。
    /// Popup Presenter からファサード経由で呼ばれる。
    /// </summary>
    public void ScheduleFogVisionRevealAfterPopup(float waitSeconds)
    {
        if (statusUI == null) return;
        statusUI.SetDeferFogVisionVisuals(true);
        if (_fogVisionAfterPopupCoroutine != null)
            StopCoroutine(_fogVisionAfterPopupCoroutine);
        _fogVisionAfterPopupCoroutine = StartCoroutine(CoRevealFogVisionVisualsAfterStatusPopup(waitSeconds));
    }

    private IEnumerator CoRevealFogVisionVisualsAfterStatusPopup(float waitSeconds)
    {
        yield return new WaitForSeconds(waitSeconds);
        _fogVisionAfterPopupCoroutine = null;
        if (statusUI != null)
            statusUI.SetDeferFogVisionVisuals(false);
        if (BattleManager.I != null)
            UpdateStatus(BattleManager.I.GetPlayerStatus(), BattleManager.I.GetEnemyStatus());
    }

    /// <summary>回復ポップアップを表示。</summary>
    public float ShowHealPopup(int amount, string statType, PlayerStatus target)
        => popupPresenter != null ? popupPresenter.ShowHealPopup(amount, statType, target) : 0f;

    public void ShowMissPopup(PlayerStatus target)
        => popupPresenter?.ShowMissPopup(target);

    /// <summary>命中時（100% 未満のみ呼び出す想定）。SE は呼び出し側。</summary>
    public float ShowCombatHitConfirmedPopup(PlayerStatus target)
        => popupPresenter != null ? popupPresenter.ShowCombatHitConfirmedPopup(target) : DamagePopup.DefaultFadeDurationIfUnknown;

    /// <summary>ステータス付近に任意メッセージのポップアップ。</summary>
    public float ShowMessagePopupForTarget(PlayerStatus target, string message, Color color)
        => popupPresenter != null ? popupPresenter.ShowMessagePopupForTarget(target, message, color) : 0f;

    public float ShowMessagePopupForTarget(PlayerStatus target, string message, Color color, Color outlineColor)
        => popupPresenter != null ? popupPresenter.ShowMessagePopupForTarget(target, message, color, outlineColor) : 0f;

    public float ShowStyledMessagePopup(PlayerStatus target, MessagePopupKind kind)
        => popupPresenter != null ? popupPresenter.ShowStyledMessagePopup(target, kind) : 0f;

    public float ShowDisasterMessagePopup(PlayerStatus target, MessagePopupKind kind, string messageOverride)
        => popupPresenter != null
            ? popupPresenter.ShowDisasterMessagePopup(target, kind, messageOverride)
            : 0f;

    public MessagePopup SpawnMessagePopupForTarget(PlayerStatus target, MessagePopupKind kind)
        => popupPresenter != null ? popupPresenter.SpawnMessagePopupForTarget(target, kind) : null;

    public Task<float> ShowBarriarDamagePopupAsync(
        int valueBefore,
        int valueAfter,
        bool barrierBroken,
        PlayerStatus target,
        CancellationToken cancellationToken = default)
        => popupPresenter != null
            ? popupPresenter.ShowBarriarDamagePopupAsync(valueBefore, valueAfter, barrierBroken, target, cancellationToken)
            : Task.FromResult(0f);

    /// <summary>対象のカードパネル中央にポップアップを生成し <see cref="DamagePopup"/> を返す（病系シーケンス用）。</summary>
    public DamagePopup SpawnDamagePopupForTarget(PlayerStatus target)
        => popupPresenter != null ? popupPresenter.SpawnDamagePopupForTarget(target) : null;

    /// <summary>プレイヤーの CardDisplayPanel 中央に情報ポップアップを表示。</summary>
    public DamagePopup ShowInfoPopupOnCardPanel(string message, Color color)
        => popupPresenter != null ? popupPresenter.ShowInfoPopupOnCardPanel(message, color) : null;

    public float ShowDisasterImportantPopup(
        ImportantPopupKind kind,
        string messageOverride,
        Side cardPanelSide)
        => popupPresenter != null
            ? popupPresenter.ShowDisasterImportantPopup(kind, messageOverride, cardPanelSide)
            : 0f;

    public float ShowStyledImportantPopup(
        ImportantPopupKind kind,
        string messageOverride,
        Side cardPanelSide)
        => popupPresenter != null
            ? popupPresenter.ShowStyledImportantPopup(kind, messageOverride, cardPanelSide)
            : 0f;

    public ImportantPopup SpawnImportantPopup(
        ImportantPopupKind kind,
        string messageOverride,
        Side cardPanelSide)
        => popupPresenter != null
            ? popupPresenter.SpawnImportantPopup(kind, messageOverride, cardPanelSide)
            : null;

    /// <summary>重要メッセージ用。</summary>
    public ImportantPopup ShowImportantPopup(string message, Color color, Side cardPanelSide)
        => popupPresenter != null ? popupPresenter.ShowImportantPopup(message, color, cardPanelSide) : null;

    /// <summary>「往生」ポップアップを表示する（ゲーム終了時）。</summary>
    public OjyouPopup ShowOjyouPopup(Side side)
        => popupPresenter != null ? popupPresenter.ShowOjyouPopup(side) : null;

    /// <summary>
    /// ゲーム終了時にバトル用 UI（カード表示パネル・TotalATKDEF・UseButton・許す・経済アクション）を一括で隠す／非アクティブ化する。
    /// 手札のタップも <see cref="SetHandClickable"/> で封鎖する。
    /// </summary>
    public void HideBattleUIForGameEnd()
    {
        cardController?.DisableCardDisplayPanels();

        useButtonPresenter?.HideForGameEnd();

        economicHandler?.DisableAllButtons();

        SetHandClickable(false);

        Debug.Log("[BattleUIManager] ゲーム終了：バトル UI を非表示化しました");
    }

    public void ClearAllSelections() => cardController?.ClearAllSelections();

    /// <summary>
    /// 表示中のカードシート（CardDisplay / EnemyDisplay のいずれか）を CardData で特定して破棄。反射「弾き返す」ポップアップ消滅後など。
    /// </summary>
    public void DestroyCardSheetForCardData(CardData card)
        => cardController?.DestroyCardSheetForCardData(card);

    /// <summary>
    /// 指定パネル上の該当 CardData のシートだけを破棄。
    /// </summary>
    public void DestroyCardSheetsForCardDataOnPanel(CardData card, Side side)
        => cardController?.DestroyCardSheetsForCardDataOnPanel(card, side);

    /// <summary>
    /// 同一パネルに同じ CardData のシートが複数あるとき、最後に追加された1枚だけ破棄（反射バウンスの重複除去）。
    /// </summary>
    public void DestroyMostRecentCardSheetOnPanelForCardData(CardData card, Side side)
        => cardController?.DestroyMostRecentCardSheetOnPanelForCardData(card, side);

    /// <summary>
    /// 防御フェーズのボタンラベルを更新
    /// </summary>
    public void RefreshUseButton() => useButtonPresenter?.Refresh();

    /// <summary>互換エイリアス。<see cref="RefreshUseButton"/> を呼ぶ。</summary>
    public void UpdateDefenseButtonLabel() => RefreshUseButton();

    /// <summary>互換エイリアス。<see cref="RefreshUseButton"/> を呼ぶ。</summary>
    public void RefreshUseButtonForMpAndSelection() => RefreshUseButton();

    /// <summary>反射の弾き返しと同じ全画面白フラッシュ（ミリ秒）。劣勢時レアドロー等からも利用。</summary>
    public void PlayFullscreenWhiteFlashMs(float durationMs) => effectPresenter?.PlayFullscreenWhiteFlashMs(durationMs);

    /// <summary>全画面を指定色で一瞬表示（ミリ秒）。</summary>
    public void PlayFullscreenColorFlashMs(Color flashColor, float durationMs)
        => effectPresenter?.PlayFullscreenColorFlashMs(flashColor, durationMs);

    /// <summary>
    /// 往生アニメ終了直後：反射「弾き返し」と同じ全画面白フラッシュ → 中央に GAMESET 大表示＋ゴング SE。一定時間後に画像を消す。
    /// </summary>
    public Task ShowPostOjyouFlashAndGameSetAsync(CancellationToken ct = default)
        => effectPresenter != null ? effectPresenter.ShowPostOjyouFlashAndGameSetAsync(ct) : Task.CompletedTask;

    /// <summary>介入発動時のメッセージ（病系処理より前）。</summary>
    public void ShowInterventionIntroPopup(PlayerStatus attackerStatus)
        => popupPresenter?.ShowInterventionIntroPopup(attackerStatus);

    /// <summary>介入攻撃カードを表示パネル先頭に出す（選択マネージャには載せない）。</summary>
    public void ShowInterventionAttackSheet(CardData card, Side side)
        => cardController?.ShowInterventionAttackSheet(card, side);

    //==== 経済アクション（EconomicUIHandler へ委譲） =====

    public void UpdateEconomicActionButtons() => economicHandler?.UpdateButtons();

    /// <summary>顕現ポップアップ等：経済ボタンを一時的に無効化（解除後は <see cref="UpdateEconomicActionButtons"/>）。</summary>
    public void DisableEconomicActionButtonsTemporarily() => economicHandler?.DisableAllButtons();

    public void OnBuyButtonPressed() => economicHandler?.OnBuyButtonPressed();

    public void OnSellButtonPressed() => economicHandler?.OnSellButtonPressed();

    public void OnExchangeButtonPressed() => economicHandler?.OnExchangeButtonPressed();

    public void CancelBuyPopup() => economicHandler?.CancelBuyPopup();

    /// <summary>プレイヤーのカード表示エリアの中心位置を取得</summary>
    public Vector3 GetPlayerCardDisplayCenter()
        => cardController != null ? cardController.GetPlayerCardDisplayCenter() : Vector3.zero;

    /// <summary>敵のカード表示エリアの中心位置を取得</summary>
    public Vector3 GetEnemyCardDisplayCenter()
        => cardController != null ? cardController.GetEnemyCardDisplayCenter() : Vector3.zero;

    /// <summary>プレイヤーのカード表示エリアの Transform を取得</summary>
    public Transform GetPlayerCardDisplayPanel()
        => cardController != null ? cardController.PlayerCardDisplayPanel : null;

    /// <summary>敵のカード表示エリアの Transform を取得</summary>
    public Transform GetEnemyCardDisplayPanel()
        => cardController != null ? cardController.EnemyCardDisplayPanel : null;

    /// <summary>
    /// SellConfirmPopup の Prefab を取得（BattleManager から使用）
    /// </summary>
    public GameObject GetSellConfirmPopupPrefab() => economicHandler != null ? economicHandler.GetSellConfirmPopupPrefab() : null;

    /// <summary>
    /// ExchangePopup の Prefab を取得（BattleManager から使用）
    /// </summary>
    public GameObject GetExchangePopupPrefab() => economicHandler != null ? economicHandler.GetExchangePopupPrefab() : null;

    public GameObject GetExchangeConfirmPopupPrefab()
        => economicHandler != null ? economicHandler.GetExchangeConfirmPopupPrefab() : null;

    /// <summary>
    /// カードシートの Prefab を取得
    /// </summary>
    public GameObject GetCardSheetPrefab() => cardController != null ? cardController.CardSheetPrefab : null;

    /// <summary>
    /// ポップアップ用の Canvas を取得（BattleManager から使用）
    /// </summary>
    public Canvas GetPopupCanvas()
    {
        if (economicHandler != null)
        {
            var c = economicHandler.GetResolvedPopupCanvas();
            if (c != null) return c;
        }
        return uiCanvas;
    }

    /// <summary>
    /// メイン UI Canvas を取得（サブマネージャからフォールバック用に参照）。
    /// </summary>
    public Canvas GetMainUICanvas() => uiCanvas;

    /// <summary>
    /// UseButton のラベルフォントを取得（RestraintHeavy 等のサブマネージャから流用する用）。
    /// </summary>
    public TMP_FontAsset GetUseButtonLabelFont()
        => useButtonPresenter != null ? useButtonPresenter.GetLabelFont() : null;

    /// <summary>
    /// CardLayoutManager を取得（RestraintHeavy 等のサブマネージャからスロット高さ計算に使用）。
    /// </summary>
    public CardLayoutManager GetCardLayoutManager() => cardController != null ? cardController.LayoutManager : null;

    /// <summary>手札入力ブロック中か（ポップアップや反射解決中）。サブマネージャ参照用。</summary>
    public bool IsHandInputBlocked => cardController != null && cardController.IsHandInputBlocked;

    // ===== MagicPanel：MagicPanelPresenter へ委譲 =====

    public void UpdateMagicPanel() => magicPanelPresenter?.UpdatePanel();

    /// <summary>相手 MagicPool 変更時（BattleManager から登録）。相手用パネルがあれば再描画。</summary>
    public void OnEnemyMagicPoolChanged() => magicPanelPresenter?.UpdateEnemyPanel();

    /// <summary>
    /// プレイヤー魔法の <see cref="CardData.cardUI"/> が MagicPanel スロットの CardUI か。
    /// </summary>
    public bool IsPlayerMagicCardUiOnMagicPanel(CardData card)
        => magicPanelPresenter != null && magicPanelPresenter.IsPlayerMagicCardUiOnMagicPanel(card);

    /// <summary>
    /// 敵魔法の <see cref="CardData.cardUI"/> が相手側 MagicPanel スロットの CardUI か。
    /// </summary>
    public bool IsEnemyMagicCardUiOnMagicPanel(CardData card)
        => magicPanelPresenter != null && magicPanelPresenter.IsEnemyMagicCardUiOnMagicPanel(card);

    /// <summary>
    /// 手札の魔法カードが MagicPanel のスロットへ直線移動する演出（プール登録は呼び出し側）
    /// </summary>
    public Task PlayMagicFlyHandToPanelAsync(CardData card, RectTransform handCardRt, int slotIndex)
        => magicPanelPresenter != null
            ? magicPanelPresenter.PlayFlyHandToPanelAsync(card, handCardRt, slotIndex)
            : Task.CompletedTask;

    public void RefreshMagicCardInteractivity(List<CardData> hand)
        => magicPanelPresenter?.RefreshMagicCardInteractivity(hand);

    // ===== 大魔法（ArchMagic）詠唱中央オーバーレイ：ArchMagicOverlayPresenter へ委譲 =====

    /// <summary>詠唱中：全画面ディム + 中央に大魔法アイコン + 残りターンをフェードイン表示する。</summary>
    public Task FadeInArchMagicCastOverlayAsync(Sprite magicSprite, int remainingTurns, int barrierRemaining, CancellationToken ct)
        => archMagicPresenter != null
            ? archMagicPresenter.FadeInAsync(magicSprite, remainingTurns, barrierRemaining, ct)
            : Task.CompletedTask;

    /// <summary>残りターン数と残バリアを差し替える（ダウンカウント表現用）。</summary>
    public void UpdateArchMagicCastOverlayRemaining(int remainingTurns, int barrierRemaining = -1)
        => archMagicPresenter?.UpdateRemaining(remainingTurns, barrierRemaining);

    /// <summary>詠唱中央オーバーレイを消す（フェード）。</summary>
    public Task FadeOutArchMagicCastOverlayAsync(CancellationToken ct)
        => archMagicPresenter != null
            ? archMagicPresenter.FadeOutAsync(ct)
            : Task.CompletedTask;

    public void HideArchMagicCastOverlayImmediate()
        => archMagicPresenter?.HideImmediate();

    /// <summary>詠唱中の残りターンを常時表示（ターン間も維持）。</summary>
    public void ShowArchMagicCastOverlayPersistent(Sprite magicSprite, int remainingTurns, int barrierRemaining = -1)
        => archMagicPresenter?.ShowPersistent(magicSprite, remainingTurns, barrierRemaining);

    // ===== 大魔法 HP バリア（Barriar.prefab）：ArchMagicBarrierPresenter へ委譲 =====

    public void ShowArchMagicBarrier(Side side, int remaining)
        => archMagicBarrierPresenter?.Show(side, remaining);

    public void UpdateArchMagicBarrier(Side side, int remaining)
        => archMagicBarrierPresenter?.UpdateRemaining(side, remaining);

    public void HideArchMagicBarrier(Side side)
        => archMagicBarrierPresenter?.Hide(side);

    public void HideAllArchMagicBarriers()
        => archMagicBarrierPresenter?.HideAll();

    public void UpdateArchMagicBarrierForStatus(PlayerStatus status, int remaining)
    {
        if (status == null) return;
        var bm = BattleManager.I;
        if (bm == null) return;
        Side side = ReferenceEquals(status, bm.GetPlayerStatus()) ? Side.Player : Side.Enemy;
        if (!status.IsCastingArchMagic || remaining <= 0)
        {
            HideArchMagicBarrier(side);
            return;
        }
        ShowArchMagicBarrier(side, remaining);
    }

}
