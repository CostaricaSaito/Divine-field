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

        Debug.Log($"[CardSelectionManager] カード選択試行: {card.cardName} (isRecovery: {card.isRecovery}, isAttack: {IsAttackCard(card)})");

        // 競合チェック
        if (!CheckCardConflicts(card))
        {
            return false;
        }

        // カード選択を追加
        selectedCards.Add(card);
        Debug.Log($"[CardSelectionManager] カード選択追加: {card.cardName} (selectedCards数: {selectedCards.Count})");

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
    /// 選択されたカード数を取得（メソッド版）
    /// </summary>
    public int GetSelectedCardCount()
    {
        return selectedCards.Count;
    }

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

        // 回復カードと攻撃カードの競合チェック
        if (newCard.isRecovery)
        {
            if (HasAttackCards())
            {
                Debug.Log("[CardSelectionManager] 回復カードを選択するため、既存のカードをキャンセルします");
                ClearAllSelections();
            }
        }
        else if (IsAttackCard(newCard))
        {
            if (HasRecoveryCard())
            {
                Debug.Log("[CardSelectionManager] 攻撃カードを選択するため、回復カードをキャンセルします");
                CancelRecoveryCards();
            }
        }

        Debug.Log($"[CardSelectionManager] CheckCardConflicts: {newCard.cardName} -> 競合なし");
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
                Debug.Log($"[CardSelectionManager] HasAttackCards: true");
                return true;
            }
        }
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
                Debug.Log($"[CardSelectionManager] HasRecoveryCard: true");
                return true;
            }
        }
        Debug.Log($"[CardSelectionManager] HasRecoveryCard: false");
        return false;
    }

    /// <summary>
    /// 回復カードをキャンセル
    /// </summary>
    private void CancelRecoveryCards()
    {
        for (int i = selectedCards.Count - 1; i >= 0; i--)
        {
            if (selectedCards[i].isRecovery)
            {
                selectedCards.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 攻撃カードかどうかを判定
    /// </summary>
    private bool IsAttackCard(CardData card)
    {
        return card.cardType == CardType.Attack || card.isPrimaryAttack || card.isAdditionalAttack;
    }

    /// <summary>
    /// 防御カードかどうかを判定
    /// </summary>
    private bool IsDefenseCard(CardData card)
    {
        return card.cardType == CardType.Defense || card.isPrimaryDefense || card.isCounterAttack;
    }
}