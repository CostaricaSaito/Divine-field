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

        // ===== 魔法カードの事前ガード =====
        if (card.cardType == CardType.Magic)
        {
            bool isFromPool = card.cardUI == null;

            var playerStatus = BattleManager.I?.GetPlayerStatus();
            if (playerStatus != null && playerStatus.IsMagicUseForbidden())
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("魔法が使用できません", new Color(0.95f, 0.22f, 0.2f));
                return false;
            }

            // MP 合算は使用ボタン側で判定（眼精疲労の倍率・複数魔法対応）。ここでは単体MP不足で弾かない。

            // MagicPool 容量チェック（手札からの使用のみ）
            if (!isFromPool && MagicPoolManager.I != null && !MagicPoolManager.I.CanAddToPool(card))
            {
                Debug.Log($"[CardSelectionManager] MagicPool 満杯のため {card.cardName} は選択不可");
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("魔法容量不足！", new Color(1f, 0.5f, 0f));
                return false;
            }
        }

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

    // ---- SelectionRole ベースの競合チェック ----

    private SelectionRole GetRoleForCurrentPhase(CardData card)
    {
        if (BattleManager.I == null) return card.attackPhaseRole;

        var state = BattleManager.I.CurrentState;
        if (state == GameState.DefenseSelect || state == GameState.DefenseConfirm)
            return card.defensePhaseRole;

        return card.attackPhaseRole;
    }

    private bool HasRoleSelected(SelectionRole role)
    {
        foreach (var c in selectedCards)
        {
            if (GetRoleForCurrentPhase(c) == role) return true;
        }
        return false;
    }

    private bool HasMagicCards()
    {
        return selectedCards.Exists(c => c.cardType == CardType.Magic);
    }

    private void ClearAllWithUI()
    {
        ClearAllSelections();
        BattleUIManager.I?.HideAllCardDetails();
    }

    /// <summary>
    /// カード競合チェック（SelectionRole ベース）。
    /// <see cref="SelectionRole.None"/> のときは <c>switch</c> に入らず既選択をクリアしない（複数枚のまま追加可能）。
    /// </summary>
    private void CheckCardConflicts(CardData newCard)
    {
        if (selectedCards.Contains(newCard)) return;

        SelectionRole role = GetRoleForCurrentPhase(newCard);

        switch (role)
        {
            case SelectionRole.None:
                break;

            case SelectionRole.Standalone:
                if (selectedCards.Count > 0)
                    ClearAllWithUI();
                break;

            case SelectionRole.Primary:
                if (selectedCards.Count > 0)
                    ClearAllWithUI();
                break;

            case SelectionRole.Addable:
                if (HasRoleSelected(SelectionRole.Standalone))
                {
                    ClearAllWithUI();
                }
                else if (newCard.cardType == CardType.Magic && HasMagicCards())
                {
                    ClearAllWithUI();
                }
                break;

            case SelectionRole.Free:
                break;
        }
    }

    private bool IsAttackCard(CardData card)
    {
        if (card.cardType == CardType.Magic && !card.isRecovery) return true;
        return card.cardType == CardType.Attack || card.isPrimaryAttack || card.isAdditionalAttack || card.isRecovery;
    }

    private bool IsDefenseCard(CardData card)
    {
        return card.cardType == CardType.Defense || card.isPrimaryDefense || card.isCounterAttack;
    }
}