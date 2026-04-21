using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TotalATKDEF表示を管理するクラス
/// BattleManagerからTotalATKDEF表示関連の処理を移設
/// </summary>
public class CardStatsDisplay : MonoBehaviour
{
    [Header("TotalATKDEF表示（プレイヤー）")]
    [SerializeField] private GameObject totalATKDEFButton;
    [SerializeField] private TMP_Text atkdefText;
    [Tooltip("未指定時は totalATKDEFButton 上の Image を使用。自分攻撃モードで赤系に着色。")]
    [SerializeField] private Image playerTotalAtkDefBackground;

    [Header("TotalATKDEF表示（敵）")]
    [SerializeField] private GameObject totalATKDEFButtonEnemy;
    [SerializeField] private TMP_Text atkdefTextEnemy;

    // 演出中のカードリスト
    private List<CardData> currentSequenceCards = new List<CardData>();
    private string currentSequenceType = "";
    /// <summary>TotalATKDEF のどちら側にシーケンスを反映するか（反射連鎖の敵防御などで敵側に DEF を出す）。</summary>
    private Side sequenceOwnerSide = Side.Player;

    /// <summary>イフリート等の加算表示（TMP リッチテキスト）。</summary>
    private const string IfritBonusColorHex = "#E53935";
    /// <summary>リヴァイアサン等の抑制分表示（TMP リッチテキスト）。</summary>
    private const string LeviathanSuppressColorHex = "#1E88E5";
    /// <summary>ゴッドレイジ適用後の「ATK 本体」数字（リッチテキスト）。</summary>
    private const string GodRageAtkBaseGreenHex = "#33DD55";

    /// <summary>ATK を出さない状態異常付与攻撃（濃霧等）選択時。TOTAL 枠を出してターゲット切替を可能にする。</summary>
    private const string StatusAilmentGrantAttackLabel = "状態異常付与";

    /// <summary>ゴッドレイジ：2倍後のリッチ表示を <see cref="GetPlayerDisplayText"/> より優先する。</summary>
    private bool _godRagePlayerAtkDisplayLocked;
    private string _godRagePlayerAtkDisplayRichText;

    /// <summary>マジカルエクスプロージョン：演出直前（カード合計のみ・ME 加算前相当）。</summary>
    private bool _magicalExplosionPreRampLocked;
    private int _magicalExplosionPreRampAtkDisplayValue;

    /// <summary>マジカルエクスプロージョン：ランプ後のリッチ表示ロック。</summary>
    private bool _magicalExplosionPlayerAtkDisplayLocked;
    private string _magicalExplosionPlayerAtkDisplayRichText;

    /// <summary>
    /// 攻撃＋ME の Prefab シーケンス中は TOTAL に MP×2 の予測を含めない（カード表記の合計のみ。ME 演出前に解除）。
    /// </summary>
    private bool _suppressMagicalExplosionPredictionDuringSequenceReveal;

    /// <summary>
    /// 初期化時にボタンを非表示にする
    /// </summary>
    private void Awake()
    {
        // 初期状態ではボタンを非表示にする
        if (totalATKDEFButton != null)
        {
            totalATKDEFButton.SetActive(false);
        }
        if (totalATKDEFButtonEnemy != null)
        {
            totalATKDEFButtonEnemy.SetActive(false);
        }
    }

    private void Start()
    {
        if (totalATKDEFButton != null)
        {
            var btn = totalATKDEFButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnPlayerTotalAtkDefButtonClicked);
        }
    }

    private void OnPlayerTotalAtkDefButtonClicked()
    {
        var bm = BattleManager.I;
        if (bm == null) return;
        if (bm.CurrentState != GameState.AttackPhase || bm.CurrentTurnOwner != PlayerType.Player)
            return;
        var ps = bm.GetPlayerStatus();
        if (ps != null && ps.HasConfusionEffect())
            return;
        bm.TogglePlayerSelfAttackTargetMode();
    }

    private static readonly Color ConfusionAtkDefBackgroundColor = new Color(1f, 0.92f, 0.45f);

    private void ApplyPlayerSelfAttackTargetBackground()
    {
        if (totalATKDEFButton == null || !totalATKDEFButton.activeSelf) return;
        var img = playerTotalAtkDefBackground != null
            ? playerTotalAtkDefBackground
            : totalATKDEFButton.GetComponent<Image>();
        if (img == null) return;
        var bm = BattleManager.I;
        var ps = bm != null ? bm.GetPlayerStatus() : null;
        bool confusedPlayer = ps != null && ps.HasConfusionEffect()
            && bm.CurrentState == GameState.AttackPhase
            && bm.CurrentTurnOwner == PlayerType.Player;
        var btn = totalATKDEFButton.GetComponent<Button>();
        if (btn != null)
            btn.interactable = !confusedPlayer;

        if (confusedPlayer)
        {
            img.color = ConfusionAtkDefBackgroundColor;
            return;
        }

        bool red = bm != null
            && bm.CurrentState == GameState.AttackPhase
            && bm.CurrentTurnOwner == PlayerType.Player
            && bm.IsPlayerSelfAttackTargetMode;
        img.color = red ? new Color(0.98f, 0.72f, 0.72f) : Color.white;
    }

    /// <summary>
    /// 演出中のカードリストを設定
    /// </summary>
    public void SetSequenceCards(List<CardData> cards, string cardType)
    {
        SetSequenceCards(cards, cardType, Side.Player);
    }

    /// <param name="ownerSide">プレイヤー手札由来の演出なら <see cref="Side.Player"/>、敵のカード表示に合わせるときは <see cref="Side.Enemy"/>。</param>
    public void SetSequenceCards(List<CardData> cards, string cardType, Side ownerSide)
    {
        currentSequenceCards.Clear();
        if (cards != null)
        {
            currentSequenceCards.AddRange(cards);
        }
        currentSequenceType = cardType ?? "";
        sequenceOwnerSide = ownerSide;
    }

    /// <summary>
    /// 演出中のカードリストをクリア
    /// </summary>
    public void ClearSequenceCards()
    {
        currentSequenceCards.Clear();
        currentSequenceType = "";
        sequenceOwnerSide = Side.Player;
        ClearGodRagePlayerAttackDisplayLock();
        ClearMagicalExplosionAttackDisplayLocks();
    }

    /// <summary>ゴッドレイジの ATK 表示ロックを解除（次の攻撃・演出用）。</summary>
    public void ClearGodRagePlayerAttackDisplayLock()
    {
        _godRagePlayerAtkDisplayLocked = false;
        _godRagePlayerAtkDisplayRichText = null;
    }

    /// <summary>マジカルエクスプロージョンの表示ロックを解除。</summary>
    public void ClearMagicalExplosionAttackDisplayLocks()
    {
        _magicalExplosionPreRampLocked = false;
        _magicalExplosionPreRampAtkDisplayValue = 0;
        _magicalExplosionPlayerAtkDisplayLocked = false;
        _magicalExplosionPlayerAtkDisplayRichText = null;
        _suppressMagicalExplosionPredictionDuringSequenceReveal = false;
    }

    /// <summary>ME ランプ完了後のリッチ表示のみ解除（続けてゴッドレイジ演出を行うとき）。</summary>
    public void ClearMagicalExplosionPlayerAtkDisplayLockOnly()
    {
        _magicalExplosionPlayerAtkDisplayLocked = false;
        _magicalExplosionPlayerAtkDisplayRichText = null;
    }

    /// <summary>ME の MP×2 予測を TOTAL に含めない期間（カード決定〜ME イントロ直前）。</summary>
    public void SetSuppressMagicalExplosionPredictionDuringSequenceReveal(bool value)
    {
        _suppressMagicalExplosionPredictionDuringSequenceReveal = value;
    }

    /// <summary>ME 演出直前：TOTAL を「ME 加算前」の強さで固定表示する。</summary>
    public void SetMagicalExplosionPreRampAttackDisplay(int displayedAtkStrength)
    {
        _magicalExplosionPreRampAtkDisplayValue = displayedAtkStrength;
        _magicalExplosionPreRampLocked = true;
    }

    /// <summary>
    /// TotalATKDEF表示を更新（プレイヤーと敵の両方）
    /// </summary>
    public void UpdateDisplay()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null)
        {
            Debug.LogWarning("[CardStatsDisplay] BattleManagerが設定されていません");
            return;
        }

        UpdatePlayerDisplay();
        UpdateEnemyDisplay();
    }

    /// <summary>
    /// プレイヤーのTotalATKDEF表示を更新
    /// </summary>
    private void UpdatePlayerDisplay()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null)
        {
            if (totalATKDEFButton != null)
            {
                totalATKDEFButton.SetActive(false);
            }
            return;
        }

        if (totalATKDEFButton == null)
        {
            Debug.LogWarning("[CardStatsDisplay] totalATKDEFButtonが設定されていません");
            return;
        }

        bool shouldHide = ShouldHidePlayer();
        totalATKDEFButton.SetActive(!shouldHide);

        if (shouldHide) return;

        if (atkdefText != null)
        {
            atkdefText.text = GetPlayerDisplayText();
            ApplyPlayerTotalAtkDefTextStyle();
        }
        else
        {
            Debug.LogWarning("[CardStatsDisplay] ATKDEFtextが設定されていません");
        }

        ApplyPlayerSelfAttackTargetBackground();
    }

    /// <summary>
    /// 敵のTotalATKDEF表示を更新
    /// </summary>
    private void UpdateEnemyDisplay()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return;

        if (totalATKDEFButtonEnemy == null)
        {
            Debug.LogWarning("[CardStatsDisplay] totalATKDEFButtonEnemyが設定されていません");
            return;
        }

        bool shouldHide = ShouldHideEnemy();
        totalATKDEFButtonEnemy.SetActive(!shouldHide);

        if (shouldHide) return;

        if (atkdefTextEnemy != null)
        {
            atkdefTextEnemy.text = GetEnemyDisplayText();
            ApplyEnemyTotalAtkDefTextStyle();
        }
        else
        {
            Debug.LogWarning("[CardStatsDisplay] ATKDEFtextEnemyが設定されていません");
        }

        ApplyEnemyConfusionBackground();
    }

    /// <summary>防御 UI 用：攻撃側スナップショット（介入・戦闘用のフォールバック込み）。</summary>
    private static List<CardData> GetIncomingAttackSnapshotForDefenseUi(BattleManager bm)
    {
        if (bm == null) return null;
        var incoming = bm.GetIncomingAttackSnapshotForDefenseUi();
        if (incoming == null || incoming.Count == 0)
            incoming = bm.GetAttackCardsForCombatPublic();
        if (incoming == null || incoming.Count == 0) return null;
        return incoming;
    }

    /// <summary>「DEF n」行。1枚・複数枚で表記を統一。</summary>
    private string FormatDefensePowerLabel(List<CardData> defenseCards)
    {
        if (defenseCards == null || defenseCards.Count == 0) return "";
        if (defenseCards.Count == 1) return $"DEF {defenseCards[0].defensePower}";
        return $"DEF {CalculateTotalDefensePower(defenseCards)}";
    }

    /// <summary>反射スライド後の TOTAL ATK 文言（プレイヤー／敵パネル共通）。</summary>
    private string FormatReflectionAttackTotalLabel(BattleManager bm, PlayerStatus fallbackAttacker)
    {
        var rc = bm.GetReflectionAttackCardsForTotalDisplay();
        if (rc == null || rc.Count == 0) return "";
        var rAtk = bm.GetReflectionAttackBlessingAttacker();
        var rDef = bm.GetReflectionAttackBlessingDefender();
        if (rAtk != null && rDef != null)
        {
            if (GodRageRules.IsGodRageDoublingCombo(rc))
                return FormatGodRageDoubledAttackPowerDisplayLabel(rc, rAtk, rDef);
            return FormatAttackPowerDisplayLabel(rc, rAtk, rDef);
        }
        return FormatAttackPowerDisplayLabel(rc, fallbackAttacker);
    }

    private void ApplyPlayerTotalAtkDefTextStyle()
    {
        if (atkdefText == null) return;
        ApplyAttackLabelTextStyle(atkdefText, ElementHelper.GetElementColor(GetPlayerCombinedElement()));
    }

    private void ApplyEnemyTotalAtkDefTextStyle()
    {
        if (atkdefTextEnemy == null) return;
        ApplyAttackLabelTextStyle(atkdefTextEnemy, ElementHelper.GetElementColor(GetEnemyCombinedElement()));
    }

    /// <summary>混乱中の敵攻撃側で TotalATKDEF を黄色表示（プレイヤー側と同系色）。</summary>
    private void ApplyEnemyConfusionBackground()
    {
        if (totalATKDEFButtonEnemy == null || !totalATKDEFButtonEnemy.activeSelf) return;
        var bm = BattleManager.I;
        if (bm == null) return;
        var es = bm.GetEnemyStatus();
        bool confusedEnemy = es != null && es.HasConfusionEffect()
            && bm.CurrentState == GameState.AttackPhase
            && bm.CurrentTurnOwner == PlayerType.Enemy
            && bm.GetCurrentAttackCard() != null;
        var img = totalATKDEFButtonEnemy.GetComponent<Image>();
        if (img == null) return;
        img.color = confusedEnemy ? ConfusionAtkDefBackgroundColor : Color.white;
    }

    /// <summary>
    /// 反射スライド後の TOTAL ATK 表示：攻撃側を消し反射側のみ表示する。
    /// <paramref name="evaluatingPlayerPanel"/> true = プレイヤー側パネルの表示可否（<see cref="ShouldHidePlayer"/> 用）。
    /// </summary>
    private bool TryGetReflectionTotalHideForPanel(bool evaluatingPlayerPanel, out bool hide)
    {
        hide = true;
        var bm = BattleManager.I;
        if (bm == null || !bm.IsReflectionAttackTotalDisplayActive()) return false;

        bool totalOnPlayer = bm.ReflectionAttackTotalOnPlayerSide;

        if (evaluatingPlayerPanel)
        {
            if (!totalOnPlayer)
            {
                // 通常防御を選んでいるときは反射用の「プレイヤー非表示」をかけない（DEF は ShouldHide 先頭で扱う）
                if (PlayerShouldShowDefenseTotalDuringReflectionChain(bm))
                    return false;

                hide = true;
                return true;
            }
        }
        else
        {
            if (totalOnPlayer)
            {
                hide = true;
                return true;
            }
        }

        var cards = bm.GetReflectionAttackCardsForTotalDisplay();
        var atkB = bm.GetReflectionAttackBlessingAttacker();
        var defB = bm.GetReflectionAttackBlessingDefender();
        int s = (atkB != null && defB != null)
            ? GetReflectionAttackNumericStrength(cards, atkB, defB)
            : GetDisplayedAttackStrength(
                cards,
                evaluatingPlayerPanel ? bm.GetPlayerStatus() : bm.GetEnemyStatus());
        hide = s <= 0;
        return true;
    }

    /// <summary>
    /// 反射（物理／魔法）または無効化として解決される防御なら、TotalATK/DEF に数値を出さない。
    /// 判定本体は <see cref="BlockingRules.AnyDefenseCardResolvesAsReflectionOrNullify"/>。
    /// </summary>
    private static bool IsReflectionOrNullifyDefenseRoute(BattleManager bm, List<CardData> defenseCards)
    {
        if (bm == null || defenseCards == null || defenseCards.Count == 0) return false;

        var incoming = GetIncomingAttackSnapshotForDefenseUi(bm);
        if (incoming == null) return false;

        return BlockingRules.AnyDefenseCardResolvesAsReflectionOrNullify(defenseCards, incoming);
    }

    /// <summary>
    /// 連鎖反射の防御入力中、通常防御のみ選ばれていれば TOTAL に DEF を出す。
    /// 反射 ATK オーバーレイの有無には依存しない（演出シーケンス残りと整合させるため）。
    /// 反射／無効化の排他のみのときは false。
    /// </summary>
    private bool PlayerShouldShowDefenseTotalDuringReflectionChain(BattleManager bm)
    {
        if (bm == null || !bm.IsReflectionChainDefensePending()) return false;

        var defCards = BattleUIManager.I?.GetSelectedDefenseCards();
        if (defCards == null || defCards.Count == 0) return false;

        var incoming = GetIncomingAttackSnapshotForDefenseUi(bm);
        if (incoming == null) return false;

        if (BlockingRules.AnyDefenseCardResolvesAsReflectionOrNullify(defCards, incoming))
            return false;

        return CalculateTotalDefensePower(defCards) > 0;
    }

    /// <summary>反射 TOTAL 用：ゴッドレイジ2倍を含め <see cref="BattleProcessor.CalculateTotalAttackPower"/> と同じ管の強さ。</summary>
    private int GetReflectionAttackNumericStrength(
        List<CardData> cards,
        PlayerStatus blessingAttacker,
        PlayerStatus blessingDefender)
    {
        if (cards == null || cards.Count == 0) return 0;
        return GetDisplayedAttackStrengthWithDefender(cards, blessingAttacker, blessingDefender);
    }

    /// <summary>
    /// プレイヤーの表示を非表示にするかどうかを判定
    /// </summary>
    private bool ShouldHidePlayer()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return true;

        if (battleManager.CurrentState == GameState.EndPhase)
            return true;

        if (battleManager.IsEconomicActionInProgress()) return true;

        // 連鎖反射で防御選択時は最優先で表示（古い攻撃シーケンス・反射非表示より先）
        if (PlayerShouldShowDefenseTotalDuringReflectionChain(battleManager))
            return false;

        // カード Prefab シーケンス（反射 TOTAL より先：確定後の DEF 再表示と整合）
        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Player)
        {
            if (currentSequenceType == "攻撃")
            {
                int totalAttack = GetDisplayedAttackStrength(currentSequenceCards, battleManager.GetPlayerStatus());
                if (totalAttack <= 0) return true;
                return false;
            }
            else if (currentSequenceType == "防御")
            {
                if (IsReflectionOrNullifyDefenseRoute(battleManager, currentSequenceCards)) return true;
                int totalDefense = CalculateTotalDefensePower(currentSequenceCards);
                if (totalDefense <= 0) return true;
                return false;
            }
        }

        if (TryGetReflectionTotalHideForPanel(true, out bool refHide))
            return refHide;

        // 攻撃フェーズのうち、プレイヤーが攻撃側のときだけ（敵ターンの AttackPhase では敵用の表示に任せる）
        if (battleManager.CurrentState == GameState.AttackPhase
            && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            // 複数選択を優先してチェック
            var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
            if (selectedAttackCards != null && selectedAttackCards.Count > 0)
            {
                // 複数選択時は合計攻撃力をチェック
                if (selectedAttackCards.Count > 1)
                {
                    int totalAttack = GetDisplayedAttackStrength(selectedAttackCards, battleManager.GetPlayerStatus());
                    if (totalAttack <= 0)
                    {
                        if (CardRules.IsStatusOnlyMagicAttackCombo(selectedAttackCards))
                            return false;
                        return true;
                    }
                    return false;
                }
                
                // 単一選択の場合
                var card = selectedAttackCards[0];
                if (CardRules.IsImmediateAction(card)) return true;
                var oneAtk = new List<CardData> { card };
                if (GetDisplayedAttackStrength(oneAtk, battleManager.GetPlayerStatus()) <= 0)
                {
                    if (CardRules.IsStatusOnlyMagicAttackCombo(oneAtk))
                        return false;
                    return true;
                }
                return false;
            }

            // 選択なし：自分攻撃ターゲット切替中だけ枠を出す。濃霧付与など選んでキャンセルした直後は空パネルにならないよう非表示にする。
            if (battleManager.IsPlayerSelfAttackTargetMode)
                return false;
            return true;
        }

        // 防御フェーズの場合
        if (battleManager.CurrentState == GameState.DefensePhase
            || battleManager.CurrentState == GameState.DefenseConfirmPhase)
        {
            // 複数選択を優先してチェック
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
            {
                if (IsReflectionOrNullifyDefenseRoute(battleManager, selectedDefenseCards)) return true;
                // 複数選択時は合計防御力をチェック
                if (selectedDefenseCards.Count > 1)
                {
                    int totalDefense = CalculateTotalDefensePower(selectedDefenseCards);
                    if (totalDefense <= 0) return true;
                    return false;
                }
                
                // 単一選択の場合
                var card = selectedDefenseCards[0];
                if (card.defensePower <= 0) return true;
                return false;
            }

            // CardSelectionManagerから取得した選択カードが空の場合は非表示にする
            // BattleManagerのselectedDefenseCardは参照しない（キャンセル時にクリアされない可能性があるため）
            return true;
        }

        // その他の状態では非表示
        return true;
    }

    /// <summary>
    /// 敵の表示を非表示にするかどうかを判定
    /// </summary>
    private bool ShouldHideEnemy()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return true;

        if (battleManager.CurrentState == GameState.EndPhase)
            return true;

        if (battleManager.IsEconomicActionInProgress()) return true;

        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Enemy)
        {
            if (currentSequenceType == "攻撃")
            {
                int totalAttack = GetDisplayedAttackStrength(currentSequenceCards, battleManager.GetEnemyStatus());
                if (totalAttack <= 0) return true;
                return false;
            }
            else if (currentSequenceType == "防御")
            {
                if (IsReflectionOrNullifyDefenseRoute(battleManager, currentSequenceCards)) return true;
                int totalDefense = CalculateTotalDefensePower(currentSequenceCards);
                if (totalDefense <= 0) return true;
                return false;
            }
        }

        if (TryGetReflectionTotalHideForPanel(false, out bool refHideEnemy))
            return refHideEnemy;

        // 敵のターン（攻撃側）: currentAttackCard が設定されていれば表示
        if (battleManager.CurrentTurnOwner == PlayerType.Enemy)
        {
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            if (currentAttackCard != null)
            {
                if (CardRules.IsImmediateAction(currentAttackCard)) return true;
                var one = new List<CardData> { currentAttackCard };
                if (GetDisplayedAttackStrength(one, battleManager.GetEnemyStatus()) <= 0) return true;
                return false;
            }
            return true;
        }

        // プレイヤーのターン（敵が防御側）: CardSelectionManager と BattleManager の両方から防御を解決
        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var state = battleManager.CurrentState;
            if (state == GameState.DefensePhase || state == GameState.DefenseConfirmPhase)
            {
                var defCards = ResolveEnemyDefenseCardsForDisplay(battleManager);
                if (defCards != null && defCards.Count > 0)
                {
                    if (IsReflectionOrNullifyDefenseRoute(battleManager, defCards)) return true;
                    if (defCards.Count > 1)
                        return CalculateTotalDefensePower(defCards) <= 0;
                    return defCards[0].defensePower <= 0;
                }
            }
            return true;
        }

        return true;
    }

    /// <summary>
    /// プレイヤーの表示テキストを取得
    /// </summary>
    private string GetPlayerDisplayText()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return "";

        // 連鎖反射の通常防御は、残った攻撃演出シーケンスより優先
        if (PlayerShouldShowDefenseTotalDuringReflectionChain(battleManager))
        {
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && IsReflectionOrNullifyDefenseRoute(battleManager, selectedDefenseCards))
                return "";
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
                return FormatDefensePowerLabel(selectedDefenseCards);
        }

        // CardSequenceManager／反射連鎖確定後の Prefab シーケンス（反射用 TOTAL ATK より優先）
        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Player)
        {
            if (currentSequenceType == "攻撃")
            {
                if (_magicalExplosionPlayerAtkDisplayLocked && !string.IsNullOrEmpty(_magicalExplosionPlayerAtkDisplayRichText))
                    return _magicalExplosionPlayerAtkDisplayRichText;
                if (_magicalExplosionPreRampLocked)
                    return $"ATK {_magicalExplosionPreRampAtkDisplayValue}";
                if (_godRagePlayerAtkDisplayLocked && !string.IsNullOrEmpty(_godRagePlayerAtkDisplayRichText))
                    return _godRagePlayerAtkDisplayRichText;
                return FormatAttackPowerDisplayLabel(currentSequenceCards, battleManager.GetPlayerStatus());
            }
            else if (currentSequenceType == "防御")
            {
                if (IsReflectionOrNullifyDefenseRoute(battleManager, currentSequenceCards)) return "";
                return FormatDefensePowerLabel(currentSequenceCards);
            }
        }

        if (battleManager.IsReflectionAttackTotalDisplayActive() && battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return FormatReflectionAttackTotalLabel(battleManager, battleManager.GetPlayerStatus());
        }

        if (battleManager.CurrentState == GameState.AttackPhase
            && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
            if (selectedAttackCards != null && selectedAttackCards.Count > 0
                && CardRules.IsStatusOnlyMagicAttackCombo(selectedAttackCards))
                return StatusAilmentGrantAttackLabel;

            // 複数選択を優先してチェック（複数選択時は合計値を表示）
            if (selectedAttackCards != null && selectedAttackCards.Count > 1)
            {
                return FormatAttackPowerDisplayLabel(selectedAttackCards, battleManager.GetPlayerStatus());
            }
            
            // 単一選択の場合
            if (selectedAttackCards != null && selectedAttackCards.Count == 1)
            {
                var one = new List<CardData> { selectedAttackCards[0] };
                return FormatAttackPowerDisplayLabel(one, battleManager.GetPlayerStatus());
            }
            
            // CardSelectionManagerから取得した選択カードが空の場合は、空文字列を返す（表示しない）
            // BattleManagerのselectedCardは参照しない（キャンセル時にクリアされない可能性があるため）
        }
        else if (battleManager.CurrentState == GameState.DefensePhase
            || battleManager.CurrentState == GameState.DefenseConfirmPhase)
        {
            // 複数選択を優先してチェック（複数選択時は合計値を表示）
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0
                && IsReflectionOrNullifyDefenseRoute(battleManager, selectedDefenseCards))
                return "";
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
                return FormatDefensePowerLabel(selectedDefenseCards);
            
            // CardSelectionManagerから取得した選択カードが空の場合は、空文字列を返す（表示しない）
            // BattleManagerのselectedDefenseCardは参照しない（キャンセル時にクリアされない可能性があるため）
        }

        return "";
    }

    /// <summary>
    /// 敵の表示テキストを取得
    /// </summary>
    private string GetEnemyDisplayText()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return "";

        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Enemy)
        {
            if (currentSequenceType == "攻撃")
            {
                return FormatAttackPowerDisplayLabel(currentSequenceCards, battleManager.GetEnemyStatus());
            }
            else if (currentSequenceType == "防御")
            {
                if (IsReflectionOrNullifyDefenseRoute(battleManager, currentSequenceCards)) return "";
                return FormatDefensePowerLabel(currentSequenceCards);
            }
        }

        if (battleManager.IsReflectionAttackTotalDisplayActive() && !battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return FormatReflectionAttackTotalLabel(battleManager, battleManager.GetEnemyStatus());
        }

        // 敵のターン（攻撃側）: ATK を表示
        if (battleManager.CurrentTurnOwner == PlayerType.Enemy)
        {
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            if (currentAttackCard != null)
            {
                var one = new List<CardData> { currentAttackCard };
                return FormatAttackPowerDisplayLabel(one, battleManager.GetEnemyStatus());
            }
        }

        // プレイヤーのターン（敵が防御側）: DEF を表示（UI 選択を優先し BattleManager 単一参照にフォールバック）
        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var defCards = ResolveEnemyDefenseCardsForDisplay(battleManager);
            if (defCards != null && defCards.Count > 0)
            {
                if (IsReflectionOrNullifyDefenseRoute(battleManager, defCards)) return "";
                return FormatDefensePowerLabel(defCards);
            }
        }

        return "";
    }

    /// <summary>
    /// 敵防御の表示用：CardSelectionManager の選択を優先。手札補充後など BattleManager だけ古い参照のときの取りこぼし防止。
    /// </summary>
    private static List<CardData> ResolveEnemyDefenseCardsForDisplay(BattleManager bm)
    {
        if (bm == null) return null;
        var ui = BattleUIManager.I?.GetSelectedDefenseCards();
        if (ui != null && ui.Count > 0)
            return ui;
        var single = bm.GetSelectedDefenseCard();
        if (single == null) return null;
        return new List<CardData> { single };
    }

    /// <summary>
    /// プレイヤー側の合算属性を取得
    /// </summary>
    private ElementType GetPlayerCombinedElement()
    {
        var battleManager = BattleManager.I;

        if (battleManager != null && PlayerShouldShowDefenseTotalDuringReflectionChain(battleManager))
        {
            var defCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (defCards != null && defCards.Count > 0)
                return ElementHelper.GetCombinedElement(defCards);
        }

        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Player)
            return ElementHelper.GetCombinedElement(currentSequenceCards);

        if (battleManager != null && battleManager.IsReflectionAttackTotalDisplayActive()
            && battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return ElementHelper.GetCombinedElement(rc);
        }

        if (battleManager == null) return ElementType.None;

        if (battleManager.CurrentState == GameState.AttackPhase
            && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            var cards = BattleUIManager.I?.GetSelectedAttackCards();
            if (cards != null && cards.Count > 0) return ElementHelper.GetCombinedElement(cards);
        }
        else if (battleManager.CurrentState == GameState.DefensePhase
            || battleManager.CurrentState == GameState.DefenseConfirmPhase)
        {
            var cards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (cards != null && cards.Count > 0) return ElementHelper.GetCombinedElement(cards);
        }
        return ElementType.None;
    }

    /// <summary>
    /// 敵側の合算属性を取得
    /// </summary>
    private ElementType GetEnemyCombinedElement()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return ElementType.None;

        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Enemy)
            return ElementHelper.GetCombinedElement(currentSequenceCards);

        if (battleManager.IsReflectionAttackTotalDisplayActive() && !battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return ElementHelper.GetCombinedElement(rc);
        }

        if (battleManager.CurrentTurnOwner == PlayerType.Enemy)
        {
            var card = battleManager.GetCurrentAttackCard();
            if (card != null) return card.element;
        }

        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var defCards = ResolveEnemyDefenseCardsForDisplay(battleManager);
            if (defCards != null && defCards.Count > 0)
                return ElementHelper.GetCombinedElement(defCards);
        }
        return ElementType.None;
    }

    /// <summary>
    /// カードリストの合計攻撃力・防御力を計算（統一メソッド）
    /// </summary>
    private int CalculateTotalPower(List<CardData> cards, bool isAttack)
    {
        int total = 0;
        foreach (var card in cards)
        {
            if (card != null)
            {
                total += isAttack ? card.attackPower : card.defensePower;
            }
        }
        return total;
    }

    /// <summary>
    /// 合計攻撃力を計算（マジカルエクスプロージョンは <paramref name="attackerForMeRule"/> があるとき反映）。
    /// </summary>
    private int CalculateTotalAttackPower(List<CardData> attackCards, PlayerStatus attackerForMeRule = null)
    {
        if (attackCards == null || attackCards.Count == 0) return 0;
        if (attackerForMeRule != null && MagicalExplosionRules.ContainsMagicalExplosion(attackCards))
        {
            if (_suppressMagicalExplosionPredictionDuringSequenceReveal)
                return MagicalExplosionRules.SumAttackPowerExcludingMagicalExplosion(attackCards);
            return MagicalExplosionRules.SumCardAttackPowerForMagicalExplosionCombo(attackCards, attackerForMeRule);
        }
        return CalculateTotalPower(attackCards, true);
    }

    /// <summary>ME 演出：カード表記 ATK 合計のみ（加護・衰弱の後）で「演出開始前」値。</summary>
    public int ComputeMagicalExplosionRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sumEx = MagicalExplosionRules.SumAttackPowerExcludingMagicalExplosion(cards);
        return ComputeAttackPowerFromCardSum(sumEx, cards, attacker, defenderForBlessings);
    }

    /// <summary>ME 演出完了後の TOTAL と一致する強さ。</summary>
    public int ComputeMagicalExplosionRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sum = MagicalExplosionRules.SumCardAttackPowerForMagicalExplosionCombo(cards, attacker);
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    /// <summary>
    /// TOTAL とマジカルエクスプロージョンの CardSheet ATK を同時にカウントアップ。完了後リッチ表示ロック。
    /// </summary>
    public async Task PlayMagicalExplosionAttackRampAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        CardData meCard,
        int meSheetAtkTarget,
        int fromTotal,
        int toTotal,
        float totalDurationSec,
        CancellationToken cancellationToken)
    {
        _magicalExplosionPreRampLocked = false;

        if (atkdefText == null || totalDurationSec <= 0f)
        {
            _magicalExplosionPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
            _magicalExplosionPlayerAtkDisplayLocked = true;
            UpdateDisplay();
            return;
        }

        if (fromTotal == toTotal)
        {
            _magicalExplosionPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
            _magicalExplosionPlayerAtkDisplayLocked = true;
            UpdateDisplay();
            return;
        }

        int lo = Mathf.Min(fromTotal, toTotal);
        int hi = Mathf.Max(fromTotal, toTotal);
        int span = hi - lo;
        float stepSec = span > 0 ? totalDurationSec / span : 0f;

        CardSheetDisplay meSheet = null;
        if (meCard != null && BattleUIManager.I != null
            && BattleUIManager.I.TryGetCardSheetDisplayForCardData(meCard, out var sh))
            meSheet = sh;

        int defPow = meCard != null ? meCard.defensePower : 0;

        atkdefText.richText = false;
        {
            var el = attackCards != null && attackCards.Count > 0
                ? ElementHelper.GetCombinedElement(attackCards)
                : ElementType.None;
            atkdefText.color = ElementHelper.GetElementColor(el);
        }

        float invSpan = (hi - lo) > 0 ? 1f / (hi - lo) : 0f;
        for (int v = lo; v <= hi; v++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            float t = (v - lo) * invSpan;
            int meAtkVal = Mathf.RoundToInt(Mathf.Lerp(0f, meSheetAtkTarget, t));
            if (v == hi)
                meAtkVal = meSheetAtkTarget;
            if (meSheet != null)
                meSheet.SetAtkDefenseNumbers(meAtkVal, defPow);

            atkdefText.text = $"ATK {v}";
            if (v < hi && stepSec > 0f)
                await Task.Delay(TimeSpan.FromSeconds(stepSec), cancellationToken);
        }

        if (meSheet != null && meSheetAtkTarget >= 0)
            meSheet.SetAtkDefenseNumbers(meSheetAtkTarget, defPow);

        _magicalExplosionPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
        _magicalExplosionPlayerAtkDisplayLocked = true;
        UpdateDisplay();
    }

    /// <summary>
    /// タグなし部分（「ATK 10」など）のベース色。無属性は黒、その他は <see cref="ElementHelper.GetElementColor"/>。
    /// +n / -n はリッチテキストの &lt;color&gt; で上書き。
    /// </summary>
    private static void ApplyAttackLabelTextStyle(TMP_Text tmp, Color elementTint)
    {
        if (tmp == null) return;
        tmp.richText = true;
        tmp.color = elementTint;
    }

    private static PlayerStatus GetDefenderForAttackDisplay(PlayerStatus attacker)
    {
        var bm = BattleManager.I;
        if (bm == null || attacker == null) return null;
        var p = bm.GetPlayerStatus();
        var e = bm.GetEnemyStatus();
        if (attacker.HasConfusionEffect())
        {
            if (bm.TryGetConfusionAttackTargetResolved(out bool targetsSelf))
                return targetsSelf ? attacker : (attacker == p ? e : p);
            if ((bm.CurrentState == GameState.AttackPhase || bm.CurrentState == GameState.CombatResolvePhase)
                && bm.CurrentTurnOwner == (attacker == p ? PlayerType.Player : PlayerType.Enemy))
                return attacker == p ? e : p;
        }

        if (bm.IsPlayerSelfAttackTargetMode
            && bm.CurrentState == GameState.AttackPhase
            && bm.CurrentTurnOwner == PlayerType.Player
            && attacker == p)
            return p;
        return attacker == p ? e : (attacker == e ? p : null);
    }

    /// <summary>
    /// カード合計 ATK に対し、イフリートは「ATK base +n」（+n は赤字）、リヴァイアサンは「-n」（n は青字）。
    /// 計算値そのものは <see cref="GetDisplayedAttackStrength"/> と一致（衰弱などで最終が変わるときは → で補足）。
    /// </summary>
    /// <param name="defenderForBlessingsOverride">
    /// 指定時は <see cref="GetDefenderForAttackDisplay"/> を使わず加護の防御側に使う（反射 TOTAL の「相手側視点」用）。
    /// </param>
    /// <param name="forMeOnlyPostRampExcludeGodRageDouble">
    /// true のときゴッドレイジ 2 倍を適用しない（ME ランプ直後の TOTAL は MP×2 までのみ）。
    /// </param>
    private string FormatAttackPowerDisplayLabel(
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defenderForBlessingsOverride = null,
        bool forMeOnlyPostRampExcludeGodRageDouble = false)
    {
        if (cards == null || cards.Count == 0 || attacker == null) return "";

        int rawCombo = CalculateTotalAttackPower(cards, attacker);
        if (rawCombo <= 0) return "";

        bool applyGodDouble = GodRageRules.IsGodRageDoublingCombo(cards) && !forMeOnlyPostRampExcludeGodRageDouble;
        if (_suppressMagicalExplosionPredictionDuringSequenceReveal && MagicalExplosionRules.ContainsMagicalExplosion(cards))
            applyGodDouble = false;
        int baseForBlessings = applyGodDouble ? rawCombo * 2 : rawCombo;

        int afterIfrit = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, baseForBlessings);
        int ifritDelta = afterIfrit - baseForBlessings;

        PlayerStatus defender = defenderForBlessingsOverride ?? GetDefenderForAttackDisplay(attacker);
        int afterLevi = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(attacker, defender, cards, afterIfrit);
        int leviDelta = afterIfrit - afterLevi;

        int final = afterLevi;
        if (!CardRules.IsMagicOnlyAttackCombo(cards))
            final = attacker.ApplyOutgoingDamageModifiers(afterLevi);

        if (ifritDelta <= 0 && leviDelta <= 0)
            return $"ATK {final}";

        var sb = new StringBuilder(48);
        sb.Append("ATK ").Append(baseForBlessings);
        if (ifritDelta > 0)
        {
            sb.Append(" <color=").Append(IfritBonusColorHex).Append(">+").Append(ifritDelta).Append("</color>");
        }
        if (leviDelta > 0)
        {
            sb.Append(" <color=").Append(LeviathanSuppressColorHex).Append("> -").Append(leviDelta).Append("</color>");
        }
        if (final != afterLevi)
            sb.Append(" → ").Append(final);

        return sb.ToString();
    }

    /// <summary>
    /// ゴッドレイジ適用後の表示：カード合計を 2 倍した値を起点にイフリート→リヴァ→衰弱。ATK 本体は緑、+n / -n は従来色。
    /// </summary>
    private string FormatGodRageDoubledAttackPowerDisplayLabel(List<CardData> cards, PlayerStatus attacker, PlayerStatus defender)
    {
        if (cards == null || cards.Count == 0 || attacker == null || defender == null) return "";

        int baseSum = CalculateTotalAttackPower(cards, attacker);
        if (baseSum <= 0) return "";

        int doubledBase = baseSum * 2;
        int afterIfrit = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, doubledBase);
        int ifritDelta = afterIfrit - doubledBase;

        int afterLevi = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(attacker, defender, cards, afterIfrit);
        int leviDelta = afterIfrit - afterLevi;

        int final = afterLevi;
        if (!CardRules.IsMagicOnlyAttackCombo(cards))
            final = attacker.ApplyOutgoingDamageModifiers(afterLevi);

        if (ifritDelta <= 0 && leviDelta <= 0)
            return $"<color={GodRageAtkBaseGreenHex}>ATK {final}</color>";

        var sb = new StringBuilder(96);
        sb.Append("<color=").Append(GodRageAtkBaseGreenHex).Append(">ATK ").Append(doubledBase).Append("</color>");
        if (ifritDelta > 0)
        {
            sb.Append(" <color=").Append(IfritBonusColorHex).Append(">+").Append(ifritDelta).Append("</color>");
        }
        if (leviDelta > 0)
        {
            sb.Append(" <color=").Append(LeviathanSuppressColorHex).Append("> -").Append(leviDelta).Append("</color>");
        }
        if (final != afterLevi)
            sb.Append(" → ").Append(final);

        return sb.ToString();
    }

    /// <summary>
    /// TotalATK 表示用。攻撃側加護 → 防御側の攻撃力抑制（リヴァイアサン等）→ 衰弱時は与ダメ補正（魔法単体攻撃は除外）。
    /// ゴッドレイジは <see cref="BattleProcessor.CalculateTotalAttackPower"/> と同様、加護前にカード合計を 2 倍する。
    /// </summary>
    private int GetDisplayedAttackStrength(List<CardData> cards, PlayerStatus attacker)
    {
        return GetDisplayedAttackStrengthWithDefender(cards, attacker, GetDefenderForAttackDisplay(attacker));
    }

    private int GetDisplayedAttackStrengthWithDefender(
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defenderForBlessings)
    {
        if (cards == null || cards.Count == 0) return 0;
        int sum = CalculateTotalAttackPower(cards, attacker);
        bool godDouble = GodRageRules.IsGodRageDoublingCombo(cards)
            && !(_suppressMagicalExplosionPredictionDuringSequenceReveal && MagicalExplosionRules.ContainsMagicalExplosion(cards));
        if (godDouble)
            sum *= 2;
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    /// <summary>カード攻撃力合計を起点に、イフリート→リヴァ→衰弱まで適用（ゴッドレイジ2倍は <paramref name="cardSum"/> に含める）。</summary>
    private int ComputeAttackPowerFromCardSum(
        int cardSum,
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defenderForBlessings)
    {
        if (cards == null || cards.Count == 0) return 0;
        int raw = cardSum;
        if (attacker != null && raw > 0)
            raw = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, raw);
        if (attacker != null && raw > 0 && defenderForBlessings != null)
            raw = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(attacker, defenderForBlessings, cards, raw);
        if (attacker == null || raw <= 0) return raw;
        if (CardRules.IsMagicOnlyAttackCombo(cards)) return raw;
        return attacker.ApplyOutgoingDamageModifiers(raw);
    }

    /// <summary>ゴッドレイジ演出用：2倍前（命中・イフリート・リヴァ等は通常どおり、2倍は未適用）。</summary>
    public int ComputeGodRageRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        int sum = CalculateTotalAttackPower(cards, attacker);
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    /// <summary>ゴッドレイジ演出用：カード合計2倍後にイフリート・リヴァ等を適用した最終値。</summary>
    public int ComputeGodRageRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        int sum = CalculateTotalAttackPower(cards, attacker);
        if (GodRageRules.IsGodRageDoublingCombo(cards))
            sum *= 2;
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    /// <summary>
    /// ゴッドレイジ：緑字・ロボットSE・ATK を整数カウントアップ。完了後は 2 倍後リッチ表示をロックし、攻撃シーケンス終了まで維持。
    /// </summary>
    public async Task PlayGodRageAttackRampAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        int from,
        int to,
        float totalDurationSec,
        CancellationToken cancellationToken)
    {
        ClearMagicalExplosionPlayerAtkDisplayLockOnly();

        if (atkdefText == null || totalDurationSec <= 0f)
        {
            ClearGodRagePlayerAttackDisplayLock();
            UpdateDisplay();
            return;
        }

        if (from == to)
        {
            _godRagePlayerAtkDisplayRichText = FormatGodRageDoubledAttackPowerDisplayLabel(attackCards, atk, def);
            _godRagePlayerAtkDisplayLocked = true;
            UpdateDisplay();
            return;
        }

        int lo = Mathf.Min(from, to);
        int hi = Mathf.Max(from, to);
        int span = hi - lo;
        float stepSec = span > 0 ? totalDurationSec / span : 0f;

        atkdefText.richText = false;
        atkdefText.color = new Color(0.2f, 0.85f, 0.35f);
        SoundEffectPlayer.I?.Play("Assets/SE/ロボット合体2.mp3");

        for (int v = lo; v <= hi; v++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            atkdefText.text = $"ATK {v}";
            if (v < hi && stepSec > 0f)
                await Task.Delay(TimeSpan.FromSeconds(stepSec), cancellationToken);
        }

        _godRagePlayerAtkDisplayRichText = FormatGodRageDoubledAttackPowerDisplayLabel(attackCards, atk, def);
        _godRagePlayerAtkDisplayLocked = true;
        UpdateDisplay();
    }

    /// <summary>
    /// 合計防御力を計算
    /// </summary>
    private int CalculateTotalDefensePower(List<CardData> defenseCards)
    {
        return CalculateTotalPower(defenseCards, false);
    }
}

