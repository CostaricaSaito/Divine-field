using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カード選択の管理を行うクラス
/// </summary>
public class CardSelectionManager : MonoBehaviour
{
    public static CardSelectionManager I;

    // 選択されたカードのリスト
    private readonly List<CardData> selectedCards = new List<CardData>();

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    /// <summary>
    /// カード選択を追加
    /// </summary>
    public bool AddCardSelection(CardData card)
    {
        if (card == null) return false;

        // 競合チェック（CheckCardConflictsは常にtrueを返すが、競合がある場合は既存選択をクリアする）
        CheckCardConflicts(card);

        // 同じカードが既に選択されている場合は追加しない
        if (selectedCards.Contains(card))
        {
            return false;
        }

        // カード選択を追加
        selectedCards.Add(card);
        return true;
    }

    /// <summary>
    /// カード選択をキャンセル
    /// </summary>
    public bool CancelCardSelection(CardData card)
    {
        bool removed = selectedCards.Remove(card);
        Debug.Log($"[CardSelectionManager] カード選択キャンセル: {card.cardName} (削除成功: {removed}, selectedCards数: {selectedCards.Count})");
        return removed;
    }

    /// <summary>
    /// 全選択をクリア
    /// </summary>
    public void ClearAllSelections()
    {
        Debug.Log("[CardSelectionManager] 全選択をクリア");
        selectedCards.Clear();
    }

    /// <summary>
    /// 選択されたカードのリストを取得
    /// </summary>
    public List<CardData> GetSelectedCards()
    {
        return selectedCards; // 直接返す（読み取り専用として使用）
    }

    /// <summary>
    /// 選択された攻撃カードのリストを取得
    /// </summary>
    public List<CardData> GetSelectedAttackCards()
    {
        var attackCards = new List<CardData>();
        foreach (var card in selectedCards)
        {
            if (IsAttackCard(card))
            {
                attackCards.Add(card);
            }
        }
        return attackCards;
    }

    /// <summary>
    /// 選択された防御カードのリストを取得
    /// </summary>
    public List<CardData> GetSelectedDefenseCards()
    {
        var defenseCards = new List<CardData>();
        foreach (var card in selectedCards)
        {
            if (IsDefenseCard(card))
            {
                defenseCards.Add(card);
            }
        }
        return defenseCards;
    }

    /// <summary>
    /// 選択されたカード数
    /// </summary>
    public int SelectedCardCount => selectedCards.Count;

    /// <summary>
    /// 選択されたカードがないかチェック
    /// </summary>
    public bool HasNoSelectedCards()
    {
        return selectedCards.Count == 0;
    }

    /// <summary>
    /// 指定されたカードが選択されているかチェック
    /// </summary>
    public bool IsCardSelected(CardData card)
    {
        return selectedCards.Contains(card);
    }

    /// <summary>
    /// カード競合チェック
    /// </summary>
    private bool CheckCardConflicts(CardData newCard)
    {
        Debug.Log($"[CardSelectionManager] CheckCardConflicts: {newCard.cardName} (isPrimaryAttack: {newCard.isPrimaryAttack}, cardType: {newCard.cardType})");
        Debug.Log($"[CardSelectionManager] 現在の選択カード数: {selectedCards.Count}");

        // 同じカードの重複選択をチェック
        if (selectedCards.Contains(newCard))
        {
            Debug.Log($"[CardSelectionManager] 同じカードの重複選択を拒否: {newCard.cardName}");
            return false;
        }

        // カードの競合チェック
        bool hasConflict = false;
        if (newCard.isRecovery && HasRecoveryCard())
        {
            Debug.Log("[CardSelectionManager] 回復カードを選択するため、既存の回復カードをキャンセルします");
            hasConflict = true;
        }
        else if (newCard.isRecovery && HasAttackCards())
        {
            Debug.Log("[CardSelectionManager] 回復カードを選択するため、既存のカードをキャンセルします");
            hasConflict = true;
        }
        else if (IsAttackCard(newCard) && HasRecoveryCard())
        {
            Debug.Log("[CardSelectionManager] 攻撃カードを選択するため、既存のカードをキャンセルします");
            hasConflict = true;
        }
        else if (newCard.isPrimaryAttack && HasPrimaryAttackCards())
        {
            Debug.Log("[CardSelectionManager] 通常攻撃カードを選択するため、既存の通常攻撃カードをキャンセルします");
            hasConflict = true;
        }

        // 競合がある場合は既存のカードをキャンセル
        if (hasConflict)
        {
            ClearAllSelections();
            // UI表示もクリアする
            BattleUIManager.I?.HideAllCardDetails();
            Debug.Log($"[CardSelectionManager] CheckCardConflicts: {newCard.cardName} -> 競合あり、既存カードをキャンセル");
        }
        else
        {
            Debug.Log($"[CardSelectionManager] CheckCardConflicts: {newCard.cardName} -> 競合なし");
        }

        return true;
    }

    /// <summary>
    /// 攻撃カードが選択されているかチェック
    /// </summary>
    private bool HasAttackCards()
    {
        foreach (var card in selectedCards)
        {
            if (IsAttackCard(card))
            {
                if (Debug.isDebugBuild)
                    Debug.Log($"[CardSelectionManager] HasAttackCards: true");
                return true;
            }
        }
        if (Debug.isDebugBuild)
            Debug.Log($"[CardSelectionManager] HasAttackCards: false");
        return false;
    }

    /// <summary>
    /// 回復カードが選択されているかチェック
    /// </summary>
    private bool HasRecoveryCard()
    {
        foreach (var card in selectedCards)
        {
            if (card.isRecovery)
            {
                if (Debug.isDebugBuild)
                    Debug.Log($"[CardSelectionManager] HasRecoveryCard: true");
                return true;
            }
        }
        if (Debug.isDebugBuild)
            Debug.Log($"[CardSelectionManager] HasRecoveryCard: false");
        return false;
    }

    /// <summary>
    /// 通常攻撃カードが選択されているかチェック
    /// </summary>
    private bool HasPrimaryAttackCards()
    {
        foreach (var card in selectedCards)
        {
            if (card.isPrimaryAttack)
            {
                if (Debug.isDebugBuild)
                    Debug.Log($"[CardSelectionManager] HasPrimaryAttackCards: true");
                return true;
            }
        }
        if (Debug.isDebugBuild)
            Debug.Log($"[CardSelectionManager] HasPrimaryAttackCards: false");
        return false;
    }

    /// <summary>
    /// 攻撃カードかどうかを判定（回復カードも含む）
    /// </summary>
    private bool IsAttackCard(CardData card)
    {
        return card.cardType == CardType.Attack || card.isPrimaryAttack || card.isAdditionalAttack || card.isRecovery;
    }

    /// <summary>
    /// 防御カードかどうかを判定
    /// </summary>
    private bool IsDefenseCard(CardData card)
    {
        return card.cardType == CardType.Defense || card.isPrimaryDefense || card.isCounterAttack;
    }
}