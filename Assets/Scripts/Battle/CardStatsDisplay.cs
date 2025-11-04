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

        // 敵のターン（攻撃フェーズ）の場合
        if (battleManager.CurrentState == GameState.AttackSelect && battleManager.CurrentTurnOwner == PlayerType.Enemy)
        {
            // 敵が攻撃カードを選択している場合（currentAttackCardで判定）
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            if (currentAttackCard != null)
            {
                // 回復カードや特殊カード（即時効果）の場合は非表示
                if (CardRules.IsImmediateAction(currentAttackCard)) return true;

                // 攻撃力が0以下の場合は非表示
                if (currentAttackCard.attackPower <= 0) return true;

                // 表示する
                return false;
            }
            return true;
        }

        // プレイヤーのターン（防御フェーズ）の場合
        if (battleManager.CurrentState == GameState.DefenseSelect && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            // 敵が防御カードを選択している場合（selectedDefenseCardで判定、ただしこれはプレイヤーの防御カードなので、敵の防御カードは別の方法で取得）
            // 敵の防御カードは、DefenderがEnemyの場合のselectedDefenseCardで判定
            var selectedDefenseCard = battleManager.GetSelectedDefenseCard();
            if (selectedDefenseCard != null && battleManager.DefenderPublic == PlayerType.Enemy)
            {
                // 防御力が0以下の場合は非表示
                if (selectedDefenseCard.defensePower <= 0) return true;

                // 表示する
                return false;
            }
            return true;
        }

        // その他の状態では非表示
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
                int totalAttack = CalculateTotalAttackPower(currentSequenceCards);
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
                int totalAttack = CalculateTotalAttackPower(selectedAttackCards);
                return $"ATK {totalAttack}";
            }
            
            // 単一選択の場合
            if (selectedAttackCards != null && selectedAttackCards.Count == 1)
            {
                return $"ATK {selectedAttackCards[0].attackPower}";
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

        // 敵のターン（攻撃フェーズ）の場合
        if (battleManager.CurrentState == GameState.AttackSelect && battleManager.CurrentTurnOwner == PlayerType.Enemy)
        {
            // 敵が攻撃カードを選択している場合
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            if (currentAttackCard != null)
            {
                return $"ATK {currentAttackCard.attackPower}";
            }
        }

        // プレイヤーのターン（防御フェーズ）の場合
        if (battleManager.CurrentState == GameState.DefenseSelect && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            // 敵が防御カードを選択している場合
            var selectedDefenseCard = battleManager.GetSelectedDefenseCard();
            if (selectedDefenseCard != null && battleManager.DefenderPublic == PlayerType.Enemy)
            {
                return $"DEF {selectedDefenseCard.defensePower}";
            }
        }

        return "";
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
    /// 合計防御力を計算
    /// </summary>
    public int CalculateTotalDefensePower(List<CardData> defenseCards)
    {
        return CalculateTotalPower(defenseCards, false);
    }
}

