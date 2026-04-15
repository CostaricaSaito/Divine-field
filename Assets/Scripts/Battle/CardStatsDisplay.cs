using System.Collections.Generic;
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
        currentSequenceCards.Clear();
        if (cards != null)
        {
            currentSequenceCards.AddRange(cards);
        }
        currentSequenceType = cardType ?? "";
    }

    /// <summary>
    /// 演出中のカードリストをクリア
    /// </summary>
    public void ClearSequenceCards()
    {
        currentSequenceCards.Clear();
        currentSequenceType = "";
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
            atkdefText.color = ElementHelper.GetElementColor(GetPlayerCombinedElement());
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
            atkdefTextEnemy.color = ElementHelper.GetElementColor(GetEnemyCombinedElement());
        }
        else
        {
            Debug.LogWarning("[CardStatsDisplay] ATKDEFtextEnemyが設定されていません");
        }
    }

    /// <summary>
    /// プレイヤーの表示を非表示にするかどうかを判定
    /// </summary>
    private bool ShouldHidePlayer()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return true;

        if (battleManager.IsEconomicActionInProgress()) return true;

        // 演出中のカードがある場合
        if (currentSequenceCards.Count > 0)
        {
            if (currentSequenceType == "攻撃")
            {
                int totalAttack = CalculateTotalAttackPower(currentSequenceCards);
                if (totalAttack <= 0) return true;
                return false;
            }
            else if (currentSequenceType == "防御")
            {
                int totalDefense = CalculateTotalDefensePower(currentSequenceCards);
                if (totalDefense <= 0) return true;
                return false;
            }
        }

        // 攻撃フェーズの場合
        if (battleManager.CurrentState == GameState.AttackSelect)
        {
            // 複数選択を優先してチェック
            var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
            if (selectedAttackCards != null && selectedAttackCards.Count > 0)
            {
                // 複数選択時は合計攻撃力をチェック
                if (selectedAttackCards.Count > 1)
                {
                    int totalAttack = CalculateTotalAttackPower(selectedAttackCards);
                    if (totalAttack <= 0) return true;
                    return false;
                }
                
                // 単一選択の場合
                var card = selectedAttackCards[0];
                if (CardRules.IsImmediateAction(card)) return true;
                if (card.attackPower <= 0) return true;
                return false;
            }

            // CardSelectionManagerから取得した選択カードが空の場合は非表示にする
            // BattleManagerのselectedCardは参照しない（キャンセル時にクリアされない可能性があるため）
            return true;
        }

        // 防御フェーズの場合
        if (battleManager.CurrentState == GameState.DefenseSelect)
        {
            // 複数選択を優先してチェック
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
            {
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

        // 敵のターン（攻撃側）: currentAttackCard が設定されていれば表示
        // RunEnemyTurnAsync は OnTurnStart から直接呼ばれるため
        // CurrentState が TurnStart のまま AttackSelect に遷移しないケースがある
        if (battleManager.CurrentTurnOwner == PlayerType.Enemy)
        {
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            if (currentAttackCard != null)
            {
                if (CardRules.IsImmediateAction(currentAttackCard)) return true;
                if (currentAttackCard.attackPower <= 0) return true;
                return false;
            }
            return true;
        }

        // プレイヤーのターン（敵が防御側）: selectedDefenseCard が設定されていれば表示
        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var state = battleManager.CurrentState;
            if (state == GameState.DefenseSelect || state == GameState.DefenseConfirm)
            {
                var selectedDefenseCard = battleManager.GetSelectedDefenseCard();
                if (selectedDefenseCard != null)
                {
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

        // 演出中のカードがある場合
        if (currentSequenceCards.Count > 0)
        {
            if (currentSequenceType == "攻撃")
            {
                int totalAttack = GetDisplayedAttackStrength(currentSequenceCards, battleManager.GetPlayerStatus());
                return $"ATK {totalAttack}";
            }
            else if (currentSequenceType == "防御")
            {
                int totalDefense = CalculateTotalDefensePower(currentSequenceCards);
                return $"DEF {totalDefense}";
            }
        }

        if (battleManager.CurrentState == GameState.AttackSelect)
        {
            // 複数選択を優先してチェック（複数選択時は合計値を表示）
            var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
            if (selectedAttackCards != null && selectedAttackCards.Count > 1)
            {
                int totalAttack = GetDisplayedAttackStrength(selectedAttackCards, battleManager.GetPlayerStatus());
                return $"ATK {totalAttack}";
            }
            
            // 単一選択の場合
            if (selectedAttackCards != null && selectedAttackCards.Count == 1)
            {
                var one = new List<CardData> { selectedAttackCards[0] };
                int atk = GetDisplayedAttackStrength(one, battleManager.GetPlayerStatus());
                return $"ATK {atk}";
            }
            
            // CardSelectionManagerから取得した選択カードが空の場合は、空文字列を返す（表示しない）
            // BattleManagerのselectedCardは参照しない（キャンセル時にクリアされない可能性があるため）
        }
        else if (battleManager.CurrentState == GameState.DefenseSelect)
        {
            // 複数選択を優先してチェック（複数選択時は合計値を表示）
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
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

        // 敵のターン（攻撃側）: ATK を表示
        if (battleManager.CurrentTurnOwner == PlayerType.Enemy)
        {
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            if (currentAttackCard != null)
            {
                var one = new List<CardData> { currentAttackCard };
                int atk = GetDisplayedAttackStrength(one, battleManager.GetEnemyStatus());
                return $"ATK {atk}";
            }
        }

        // プレイヤーのターン（敵が防御側）: DEF を表示
        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var selectedDefenseCard = battleManager.GetSelectedDefenseCard();
            if (selectedDefenseCard != null)
            {
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
        if (currentSequenceCards.Count > 0)
            return ElementHelper.GetCombinedElement(currentSequenceCards);

        var battleManager = BattleManager.I;
        if (battleManager == null) return ElementType.None;

        if (battleManager.CurrentState == GameState.AttackSelect)
        {
            var cards = BattleUIManager.I?.GetSelectedAttackCards();
            if (cards != null && cards.Count > 0) return ElementHelper.GetCombinedElement(cards);
        }
        else if (battleManager.CurrentState == GameState.DefenseSelect)
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
    /// TotalATK 表示用。衰弱時は与ダメ補正（魔法単体攻撃は除外）を反映。
    /// </summary>
    private int GetDisplayedAttackStrength(List<CardData> cards, PlayerStatus attacker)
    {
        if (cards == null || cards.Count == 0) return 0;
        int raw = CalculateTotalAttackPower(cards);
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

