using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// カード表示ゾーンのレイアウト管理を担当するクラス
/// 
/// 【役割】
/// - カードの位置計算
/// - カードの配置・再配置
/// - 表示順序の管理
/// 
/// 【責任範囲】
/// - カードの位置計算ロジック
/// - レイアウトの調整
/// - 表示順序の決定
/// 
/// 【他のクラスとの関係】
/// - BattleUIManager: カード配置の指示を受ける
/// - CardSheetDisplay: カードデータの取得
/// - CardRules: カードタイプの判定
/// </summary>
public class CardLayoutManager : MonoBehaviour
{
    [Header("レイアウト設定")]
    [SerializeField] private float cardSpacing = 10f;
    [SerializeField] private int maxVisibleCards = 3;
    
    private List<GameObject> activeCardSheets = new List<GameObject>();
    private List<CardData> selectedCards = new List<CardData>();
    
    /// <summary>
    /// カード表示シートのリストを設定
    /// </summary>
    public void SetActiveCardSheets(List<GameObject> cardSheets)
    {
        activeCardSheets = cardSheets;
    }
    
    /// <summary>
    /// 選択されたカードのリストを設定
    /// </summary>
    public void SetSelectedCards(List<CardData> cards)
    {
        selectedCards = cards;
    }
    
    /// <summary>
    /// カードの位置を設定
    /// </summary>
    public void SetupCardPosition(GameObject cardObj, Transform parent)
    {
        var rt = cardObj.transform as RectTransform;
        if (rt == null) return;
        
        // 基本設定
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        
        var cardDisplay = cardObj.GetComponent<CardSheetDisplay>();
        if (cardDisplay == null || cardDisplay.GetCardData() == null) return;
        
        var cardData = cardDisplay.GetCardData();
        int displayOrder = GetCardDisplayOrder(cardData);
        
        Debug.Log($"[CardLayoutManager] カード表示順序: {cardData.cardName} -> 順序: {displayOrder}");
        
        // 現在の総カード数を取得（同じタイプのカードのみ）
        int totalCards = GetCardCountByType(cardData);
        
        if (totalCards <= maxVisibleCards)
        {
            // 3枚以下の場合：等間隔配置（既存カードは動かさない）
            SetupNormalLayout(rt, displayOrder, totalCards);
            return;
        }
        else
        {
            // 4枚以上の場合：1枚目と最終枚を固定、中間を等間隔配置
            SetupAdvancedLayout(rt, displayOrder, totalCards);
        }
        
        // 4枚目以降が追加された場合のみ、カードの再配置を実行
        if (displayOrder >= maxVisibleCards) // 4枚目（index=3）以降
        {
            ReorderAllCards();
        }
        
        cardObj.transform.SetAsLastSibling();
    }
    
    /// <summary>
    /// 3枚以下の場合の等間隔配置
    /// </summary>
    private void SetupNormalLayout(RectTransform rt, int displayOrder, int totalCards)
    {
        float totalHeight = (rt.rect.height + cardSpacing) * (totalCards - 1);
        float startY = totalHeight / 2f; // 中央から上に半分
        float yOffset = startY - (displayOrder + 1) * (rt.rect.height + cardSpacing);
        rt.anchoredPosition = new Vector2(0, yOffset);
        rt.localScale = Vector3.one;
    }
    
    /// <summary>
    /// 4枚以上の場合の高度な配置
    /// </summary>
    private void SetupAdvancedLayout(RectTransform rt, int displayOrder, int totalCards)
    {
        float cardSpaceHeight = rt.rect.height + cardSpacing;
        
        // 1枚目の位置（上端）
        float firstCardY = cardSpaceHeight * (maxVisibleCards - 1) / 2f;
        // 最終枚の位置（下端）
        float lastCardY = firstCardY - (maxVisibleCards - 1) * cardSpaceHeight;
        
        if (displayOrder == 0)
        {
            // 1枚目：上端に固定
            rt.anchoredPosition = new Vector2(0, firstCardY);
            rt.localScale = Vector3.one;
        }
        else if (displayOrder == totalCards - 1)
        {
            // 最終枚：下端に固定
            rt.anchoredPosition = new Vector2(0, lastCardY);
            rt.localScale = Vector3.one;
        }
        else
        {
            // 中間カード：1枚目と最終枚の間に等間隔で配置
            int middleCardCount = totalCards - 2; // 中間カードの数
            float interval = (firstCardY - lastCardY) / (middleCardCount + 1);
            float yOffset = firstCardY - interval * (displayOrder + 1);
            
            rt.anchoredPosition = new Vector2(0, yOffset);
            rt.localScale = Vector3.one;
        }
    }
    
    /// <summary>
    /// カードキャンセル時の位置調整
    /// </summary>
    public void HandleCardCancellation()
    {
        // 残りのカードの位置を再配置（4枚以上の場合のみ）
        if (selectedCards.Count >= 4)
        {
            ReorderAllCards();
        }
        else if (selectedCards.Count == 1)
        {
            // 1枚だけの場合は上端スロットに配置
            SetupSingleCardLayout();
        }
        // 2枚、3枚の場合は既存カードを再配置しない（要件通り）
    }
    
    /// <summary>
    /// 1枚だけの場合のレイアウト
    /// </summary>
    private void SetupSingleCardLayout()
    {
        var remainingCard = selectedCards[0];
        var cardObj = activeCardSheets.FirstOrDefault(obj => 
            obj?.GetComponent<CardSheetDisplay>()?.GetCardData() == remainingCard);
        
        if (cardObj != null)
        {
            var rt = cardObj.transform as RectTransform;
            if (rt != null)
            {
                // 3枚以下の場合の1枚目の位置計算と同じロジック
                float totalHeight = (rt.rect.height + cardSpacing) * (1 - 1); // 1枚の場合
                float startY = totalHeight / 2f; // 中央から上に半分
                float yOffset = startY - (0 + 1) * (rt.rect.height + cardSpacing); // displayOrder=0
                rt.anchoredPosition = new Vector2(0, yOffset);
                rt.localScale = Vector3.one;
            }
        }
    }
    
    /// <summary>
    /// 現在のカード数に応じて全カードを適切に再配置
    /// </summary>
    private void ReorderAllCards()
    {
        int totalCards = activeCardSheets.Count(card => card != null);
        
        if (totalCards <= 3)
        {
            // 3枚以下の場合は通常の等間隔配置
            ReorderToNormalLayout();
        }
        else
        {
            // 4枚以上の場合は中間カード再配置
            ReorderIntermediateCards();
        }
    }
    
    /// <summary>
    /// 3枚以下の場合の等間隔配置
    /// </summary>
    private void ReorderToNormalLayout()
    {
        // 選択されたカードの順序に基づいて配置
        for (int i = 0; i < selectedCards.Count; i++)
        {
            var card = selectedCards[i];
            var cardObj = activeCardSheets.FirstOrDefault(obj => 
                obj?.GetComponent<CardSheetDisplay>()?.GetCardData() == card);
            
            if (cardObj == null) continue;
            
            var rt = cardObj.transform as RectTransform;
            if (rt == null) continue;
            
            // 3枚以下の場合：等間隔配置（SetupCardPositionと一致させる）
            float totalHeight = (rt.rect.height + cardSpacing) * (selectedCards.Count - 1);
            float startY = totalHeight / 2f; // 中央から上に半分
            float yOffset = startY - (i + 1) * (rt.rect.height + cardSpacing);
            rt.anchoredPosition = new Vector2(0, yOffset);
            rt.localScale = Vector3.one;
            
            Debug.Log($"[CardLayoutManager] 通常配置: {card.cardName} (順序{i}) -> Y位置: {rt.anchoredPosition.y}");
        }
    }
    
    /// <summary>
    /// 4枚以上の場合：1枚目と最終枚を固定、中間カードを等間隔で再配置
    /// </summary>
    private void ReorderIntermediateCards()
    {
        // 現在表示されているカード数を取得
        int totalCards = activeCardSheets.Count(card => card != null);
        
        // 4枚未満の場合は再配置不要
        if (totalCards < 4) return;
        
        // 選択されたカードの順序に基づいて配置
        for (int i = 0; i < selectedCards.Count; i++)
        {
            var card = selectedCards[i];
            var cardObj = activeCardSheets.FirstOrDefault(obj => 
                obj?.GetComponent<CardSheetDisplay>()?.GetCardData() == card);
            
            if (cardObj == null) continue;
            
            var rt = cardObj.transform as RectTransform;
            if (rt == null) continue;
            
            float cardSpaceHeight = rt.rect.height + cardSpacing;
            
            // 1枚目の位置（上端）
            float firstCardY = cardSpaceHeight * (maxVisibleCards - 1) / 2f;
            // 最終枚の位置（下端）
            float lastCardY = firstCardY - (maxVisibleCards - 1) * cardSpaceHeight;
            
            if (i == 0)
            {
                // 1枚目：上端に固定
                rt.anchoredPosition = new Vector2(0, firstCardY);
                rt.localScale = Vector3.one;
            }
            else if (i == selectedCards.Count - 1)
            {
                // 最終枚：下端に固定
                rt.anchoredPosition = new Vector2(0, lastCardY);
                rt.localScale = Vector3.one;
            }
            else
            {
                // 中間カード：1枚目と最終枚の間に等間隔で配置
                int middleCardCount = selectedCards.Count - 2; // 中間カードの数
                float interval = (firstCardY - lastCardY) / (middleCardCount + 1);
                float yOffset = firstCardY - interval * i;
                
                rt.anchoredPosition = new Vector2(0, yOffset);
                rt.localScale = Vector3.one;
            }
            
            Debug.Log($"[CardLayoutManager] 中間カード再配置: {card.cardName} (順序{i}) -> Y位置: {rt.anchoredPosition.y}");
        }
    }
    
    /// <summary>
    /// カードの表示順序を取得
    /// </summary>
    private int GetCardDisplayOrder(CardData cardData)
    {
        // 防御カードの複数選択対応
        if (cardData.cardType == CardType.Defense || cardData.isPrimaryDefense)
        {
            // 防御カードは選択順に表示（0, 1, 2...）
            return CountExistingDefenseCards();
        }
        
        if (cardData.isPrimaryAttack) return 0;
        
        if (cardData.canBeUsedWithPrimaryAttack)
        {
            // 追加攻撃カードは既存の攻撃カード数を返す
            return CountExistingAttackCards();
        }
        
        // その他のカード（回復カード等）は既存のカード数を返す
        return CountExistingOtherCards();
    }
    
    /// <summary>
    /// 既存の防御カード数をカウント
    /// </summary>
    private int CountExistingDefenseCards()
    {
        int count = 0;
        foreach (var cardObj in activeCardSheets)
        {
            if (cardObj == null) continue;
            
            var cardDisplay = cardObj.GetComponent<CardSheetDisplay>();
            if (cardDisplay?.GetCardData() != null)
            {
                var existingCard = cardDisplay.GetCardData();
                if (existingCard.cardType == CardType.Defense || existingCard.isPrimaryDefense)
                {
                    count++;
                }
            }
        }
        Debug.Log($"[CardLayoutManager] 既存防御カード数: {count}");
        return count;
    }
    
    /// <summary>
    /// 既存の攻撃カード数をカウント
    /// </summary>
    private int CountExistingAttackCards()
    {
        int count = 0;
        foreach (var cardObj in activeCardSheets)
        {
            if (cardObj == null) continue;
            
            var cardDisplay = cardObj.GetComponent<CardSheetDisplay>();
            if (cardDisplay?.GetCardData() != null)
            {
                var existingCard = cardDisplay.GetCardData();
                if (CardRules.IsAttackCard(existingCard))
                {
                    count++;
                }
            }
        }
        return count;
    }
    
    /// <summary>
    /// 既存のその他のカード数をカウント
    /// </summary>
    private int CountExistingOtherCards()
    {
        int count = 0;
        foreach (var cardObj in activeCardSheets)
        {
            if (cardObj == null) continue;
            
            var cardDisplay = cardObj.GetComponent<CardSheetDisplay>();
            if (cardDisplay?.GetCardData() != null)
            {
                var existingCard = cardDisplay.GetCardData();
                if (!CardRules.IsAttackCard(existingCard) && 
                    !CardRules.IsDefenseCard(existingCard))
                {
                    count++;
                }
            }
        }
        return count;
    }
    
    /// <summary>
    /// 指定されたカードタイプの現在のカード数を取得
    /// </summary>
    private int GetCardCountByType(CardData cardData)
    {
        if (CardRules.IsAttackCard(cardData))
        {
            // 攻撃カードの場合：activeCardSheetsから攻撃カードのみをカウント
            return activeCardSheets.Count(card => 
                card != null && 
                card.GetComponent<CardSheetDisplay>()?.GetCardData() != null &&
                CardRules.IsAttackCard(card.GetComponent<CardSheetDisplay>().GetCardData()));
        }
        else if (CardRules.IsDefenseCard(cardData))
        {
            // 防御カードの場合：activeCardSheetsから防御カードのみをカウント
            return activeCardSheets.Count(card => 
                card != null && 
                card.GetComponent<CardSheetDisplay>()?.GetCardData() != null &&
                CardRules.IsDefenseCard(card.GetComponent<CardSheetDisplay>().GetCardData()));
        }
        else
        {
            // その他のカードタイプの場合は全体から取得
            return activeCardSheets.Count(card => card != null);
        }
    }
}
