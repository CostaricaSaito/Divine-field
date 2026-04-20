using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
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
            string displayText = GetPlayerDisplayText();
            atkdefText.text = displayText;
            ApplyAttackLabelTextStyle(atkdefText, ElementHelper.GetElementColor(GetPlayerCombinedElement()));
        }
        else
        {
            Debug.LogWarning("[CardStatsDisplay] ATKDEFtextが設定されていません");
        }
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
            string displayText = GetEnemyDisplayText();
            atkdefTextEnemy.text = displayText;
            ApplyAttackLabelTextStyle(atkdefTextEnemy, ElementHelper.GetElementColor(GetEnemyCombinedElement()));
        }
        else
        {
            Debug.LogWarning("[CardStatsDisplay] ATKDEFtextEnemyが設定されていません");
        }
    }

    /// <summary>
    /// 反射スライド後の TOTAL ATK 表示：攻撃側を消し反射側のみ表示する。
    /// </summary>
    private bool TryGetReflectionPlayerTotalHide(out bool hide)
    {
        hide = true;
        var bm = BattleManager.I;
        if (bm == null || !bm.IsReflectionAttackTotalDisplayActive()) return false;

        if (!bm.ReflectionAttackTotalOnPlayerSide)
        {
            // 通常防御を選んでいるときは反射用の「プレイヤー非表示」をかけない（DEF は ShouldHide 先頭で扱う）
            if (PlayerShouldShowDefenseTotalDuringReflectionChain(bm))
                return false;

            hide = true;
            return true;
        }

        var cards = bm.GetReflectionAttackCardsForTotalDisplay();
        int s = GetDisplayedAttackStrength(cards, bm.GetPlayerStatus());
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

        var incoming = bm.GetIncomingAttackSnapshotForDefenseUi();
        if (incoming == null || incoming.Count == 0)
            incoming = bm.GetAttackCardsForCombatPublic();
        if (incoming == null || incoming.Count == 0) return false;

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

        var incoming = bm.GetIncomingAttackSnapshotForDefenseUi();
        if (incoming == null || incoming.Count == 0)
            incoming = bm.GetAttackCardsForCombatPublic();
        if (incoming == null || incoming.Count == 0) return false;

        if (BlockingRules.AnyDefenseCardResolvesAsReflectionOrNullify(defCards, incoming))
            return false;

        return CalculateTotalDefensePower(defCards) > 0;
    }

    private bool TryGetReflectionEnemyTotalHide(out bool hide)
    {
        hide = true;
        var bm = BattleManager.I;
        if (bm == null || !bm.IsReflectionAttackTotalDisplayActive()) return false;

        if (bm.ReflectionAttackTotalOnPlayerSide)
        {
            hide = true;
            return true;
        }

        var cards = bm.GetReflectionAttackCardsForTotalDisplay();
        int s = GetDisplayedAttackStrength(cards, bm.GetEnemyStatus());
        hide = s <= 0;
        return true;
    }

    /// <summary>
    /// プレイヤーの表示を非表示にするかどうかを判定
    /// </summary>
    private bool ShouldHidePlayer()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return true;

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

        if (TryGetReflectionPlayerTotalHide(out bool refHide))
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
                    if (totalAttack <= 0) return true;
                    return false;
                }
                
                // 単一選択の場合
                var card = selectedAttackCards[0];
                if (CardRules.IsImmediateAction(card)) return true;
                var oneAtk = new List<CardData> { card };
                if (GetDisplayedAttackStrength(oneAtk, battleManager.GetPlayerStatus()) <= 0) return true;
                return false;
            }

            // CardSelectionManagerから取得した選択カードが空の場合は非表示にする
            // BattleManagerのselectedCardは参照しない（キャンセル時にクリアされない可能性があるため）
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

        if (TryGetReflectionEnemyTotalHide(out bool refHideEnemy))
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

        // プレイヤーのターン（敵が防御側）: selectedDefenseCard が設定されていれば表示
        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var state = battleManager.CurrentState;
            if (state == GameState.DefensePhase || state == GameState.DefenseConfirmPhase)
            {
                var selectedDefenseCard = battleManager.GetSelectedDefenseCard();
                if (selectedDefenseCard != null)
                {
                    var oneDef = new List<CardData> { selectedDefenseCard };
                    if (IsReflectionOrNullifyDefenseRoute(battleManager, oneDef)) return true;
                    if (selectedDefenseCard.defensePower <= 0) return true;
                    return false;
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
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 1)
                return $"DEF {CalculateTotalDefensePower(selectedDefenseCards)}";
            if (selectedDefenseCards != null && selectedDefenseCards.Count == 1)
                return $"DEF {selectedDefenseCards[0].defensePower}";
        }

        // CardSequenceManager／反射連鎖確定後の Prefab シーケンス（反射用 TOTAL ATK より優先）
        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Player)
        {
            if (currentSequenceType == "攻撃")
            {
                return FormatAttackPowerDisplayLabel(currentSequenceCards, battleManager.GetPlayerStatus());
            }
            else if (currentSequenceType == "防御")
            {
                if (IsReflectionOrNullifyDefenseRoute(battleManager, currentSequenceCards)) return "";
                int totalDefense = CalculateTotalDefensePower(currentSequenceCards);
                return $"DEF {totalDefense}";
            }
        }

        if (battleManager.IsReflectionAttackTotalDisplayActive() && battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return FormatAttackPowerDisplayLabel(rc, battleManager.GetPlayerStatus());
        }

        if (battleManager.CurrentState == GameState.AttackPhase
            && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            // 複数選択を優先してチェック（複数選択時は合計値を表示）
            var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
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
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 1)
            {
                int totalDefense = CalculateTotalDefensePower(selectedDefenseCards);
                return $"DEF {totalDefense}";
            }
            
            // 単一選択の場合
            if (selectedDefenseCards != null && selectedDefenseCards.Count == 1)
            {
                return $"DEF {selectedDefenseCards[0].defensePower}";
            }
            
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
                int totalDefense = CalculateTotalDefensePower(currentSequenceCards);
                return $"DEF {totalDefense}";
            }
        }

        if (battleManager.IsReflectionAttackTotalDisplayActive() && !battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return FormatAttackPowerDisplayLabel(rc, battleManager.GetEnemyStatus());
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

        // プレイヤーのターン（敵が防御側）: DEF を表示
        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var selectedDefenseCard = battleManager.GetSelectedDefenseCard();
            if (selectedDefenseCard != null)
            {
                var oneDef = new List<CardData> { selectedDefenseCard };
                if (IsReflectionOrNullifyDefenseRoute(battleManager, oneDef)) return "";
                return $"DEF {selectedDefenseCard.defensePower}";
            }
        }

        return "";
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
            var card = battleManager.GetSelectedDefenseCard();
            if (card != null) return card.element;
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
    /// 合計攻撃力を計算
    /// </summary>
    public int CalculateTotalAttackPower(List<CardData> attackCards)
    {
        return CalculateTotalPower(attackCards, true);
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
        return attacker == p ? e : (attacker == e ? p : null);
    }

    /// <summary>
    /// カード合計 ATK に対し、イフリートは「ATK base +n」（+n は赤字）、リヴァイアサンは「-n」（n は青字）。
    /// 計算値そのものは <see cref="GetDisplayedAttackStrength"/> と一致（衰弱などで最終が変わるときは → で補足）。
    /// </summary>
    private string FormatAttackPowerDisplayLabel(List<CardData> cards, PlayerStatus attacker)
    {
        if (cards == null || cards.Count == 0 || attacker == null) return "";

        int baseSum = CalculateTotalAttackPower(cards);
        if (baseSum <= 0) return "";

        int afterIfrit = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, baseSum);
        int ifritDelta = afterIfrit - baseSum;

        PlayerStatus defender = GetDefenderForAttackDisplay(attacker);
        int afterLevi = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(attacker, defender, cards, afterIfrit);
        int leviDelta = afterIfrit - afterLevi;

        int final = afterLevi;
        if (!CardRules.IsMagicOnlyAttackCombo(cards))
            final = attacker.ApplyOutgoingDamageModifiers(afterLevi);

        if (ifritDelta <= 0 && leviDelta <= 0)
            return $"ATK {final}";

        var sb = new StringBuilder(48);
        sb.Append("ATK ").Append(baseSum);
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
    /// </summary>
    private int GetDisplayedAttackStrength(List<CardData> cards, PlayerStatus attacker)
    {
        if (cards == null || cards.Count == 0) return 0;
        int raw = CalculateTotalAttackPower(cards);
        if (attacker != null && raw > 0)
            raw = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, raw);
        if (attacker != null && raw > 0)
        {
            var bm = BattleManager.I;
            PlayerStatus defender = null;
            if (bm != null)
            {
                var p = bm.GetPlayerStatus();
                var e = bm.GetEnemyStatus();
                defender = attacker == p ? e : (attacker == e ? p : null);
            }
            raw = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(attacker, defender, cards, raw);
        }
        if (attacker == null || raw <= 0) return raw;
        if (CardRules.IsMagicOnlyAttackCombo(cards)) return raw;
        return attacker.ApplyOutgoingDamageModifiers(raw);
    }

    /// <summary>
    /// 合計防御力を計算
    /// </summary>
    public int CalculateTotalDefensePower(List<CardData> defenseCards)
    {
        return CalculateTotalPower(defenseCards, false);
    }
}

