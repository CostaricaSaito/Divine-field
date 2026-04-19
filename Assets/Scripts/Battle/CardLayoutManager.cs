using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// カード表示ゾーンのレイアウト管理を担当するクラス
/// 
/// 【方式】
/// VerticalLayoutGroupは使用せず、パネル内でカードを手動配置する。
/// - パネルに収まる場合：上から順に通常Spacingで配置
/// - 収まらない場合：1枚目を上端、最終枚を下端に固定し、中間カードを均等配置
/// - カードサイズは常に一定（縮小なし）
/// - パネルから絶対にはみ出さない
/// 
/// 【親Panelの設定】
/// - VerticalLayoutGroupは無効化すること
/// - 余白は本コンポーネントの Panel Padding（Inspector）で調整する
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
    [SerializeField] private float cardHeight = 120f;
    [Tooltip("CardDisplayPanel 内の余白（手動配置のため VerticalLayoutGroup の Padding に相当）")]
    [SerializeField] private float panelPaddingLeft = 8f;
    [SerializeField] private float panelPaddingRight = 8f;
    [SerializeField] private float panelPaddingTop = 8f;
    [SerializeField] private float panelPaddingBottom = 8f;
    
    private RectTransform panelRectTransform;
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
    
    /// <summary>反射スライド後など、レイアウト基準パネルを明示するときに使用。</summary>
    public void SetLayoutPanelRect(RectTransform panel)
    {
        if (panel != null)
            panelRectTransform = panel;
    }

    /// <summary>
    /// カードの位置を設定（カード追加時に全カードを再配置）
    /// </summary>
    public void SetupCardPosition(GameObject cardObj, Transform parent)
    {
        if (panelRectTransform == null && parent != null)
        {
            panelRectTransform = parent as RectTransform;
        }
        
        var cardDisplay = cardObj.GetComponent<CardSheetDisplay>();
        if (cardDisplay == null || cardDisplay.GetCardData() == null) return;
        
        var cardData = cardDisplay.GetCardData();
        int totalCards = GetCardCountByType(cardData);
        
        Debug.Log($"[CardLayoutManager] カード追加: {cardData.cardName}, 合計: {totalCards}");
        
        RepositionAllCards();
    }
    
    /// <summary>
    /// カードキャンセル時の位置調整
    /// </summary>
    public void HandleCardCancellation()
    {
        RepositionAllCards();
    }
    
    /// <summary>
    /// 全カードをパネル内に収まるよう再配置する
    /// </summary>
    private void RepositionAllCards()
    {
        activeCardSheets.RemoveAll(obj => obj == null);
        
        int totalCards = selectedCards.Count;
        if (totalCards == 0) return;
        
        Canvas.ForceUpdateCanvases();
        float panelHeight = panelRectTransform != null ? panelRectTransform.rect.height : 0;
        float availHeight = Mathf.Max(0f, panelHeight - panelPaddingTop - panelPaddingBottom);

        Debug.Log($"[CardLayoutManager] 再配置: カード数={totalCards}, パネル高さ={panelHeight}");
        
        for (int i = 0; i < selectedCards.Count; i++)
        {
            var card = selectedCards[i];
            var cardObj = activeCardSheets.FirstOrDefault(obj => 
                obj?.GetComponent<CardSheetDisplay>()?.GetCardData() == card);
            
            if (cardObj == null) continue;
            
            var rt = cardObj.transform as RectTransform;
            if (rt == null) continue;
            
            rt.anchorMin = new Vector2(0, 1f);
            rt.anchorMax = new Vector2(1, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            
            float y = CalculateCardY(i, totalCards, panelHeight, availHeight);
            rt.offsetMin = new Vector2(panelPaddingLeft, y - cardHeight);
            rt.offsetMax = new Vector2(-panelPaddingRight, y);
            rt.localScale = Vector3.one;
            
            cardObj.transform.SetSiblingIndex(i);
            
            Debug.Log($"[CardLayoutManager] {card.cardName} (順序{i}) -> Y={y}");
        }
    }
    
    /// <summary>
    /// カードのY座標を計算する。
    /// パネルに収まる場合は通常Spacing、収まらない場合は均等配置。
    /// </summary>
    private float CalculateCardY(int index, int totalCards, float panelHeight, float availHeight)
    {
        if (totalCards <= 0) return 0;
        if (totalCards == 1) return -panelPaddingTop;

        float normalTotal = totalCards * cardHeight + (totalCards - 1) * cardSpacing;
        float h = availHeight > 0f ? availHeight : Mathf.Max(0f, panelHeight - panelPaddingTop - panelPaddingBottom);

        if (panelHeight > 0 && normalTotal > h)
        {
            float denom = Mathf.Max(1, totalCards - 1);
            float interval = (h - cardHeight) / denom;
            return -panelPaddingTop - index * interval;
        }

        return -panelPaddingTop - index * (cardHeight + cardSpacing);
    }

    /// <summary>
    /// 防御パネル内で「2枚並んだときの2枚目」上端Y（上基準・負方向）。拘束オーバーレイ配置用。
    /// </summary>
    public float GetSecondSlotTopYForPanelHeight(float panelHeight)
    {
        float avail = Mathf.Max(0f, panelHeight - panelPaddingTop - panelPaddingBottom);
        return CalculateCardY(1, 2, panelHeight, avail);
    }

    public float LayoutCardHeight => cardHeight;
    
    /// <summary>
    /// カードの表示順序を取得
    /// </summary>
    private int GetCardDisplayOrder(CardData cardData)
    {
        if (cardData.cardType == CardType.Defense || cardData.isPrimaryDefense)
        {
            return CountExistingDefenseCards();
        }
        
        if (cardData.isPrimaryAttack) return 0;
        
        if (cardData.attackPhaseRole == SelectionRole.Addable)
        {
            return CountExistingAttackCards();
        }
        
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
            return activeCardSheets.Count(card => 
                card != null && 
                card.GetComponent<CardSheetDisplay>()?.GetCardData() != null &&
                CardRules.IsAttackCard(card.GetComponent<CardSheetDisplay>().GetCardData()));
        }
        else if (CardRules.IsDefenseCard(cardData))
        {
            return activeCardSheets.Count(card => 
                card != null && 
                card.GetComponent<CardSheetDisplay>()?.GetCardData() != null &&
                CardRules.IsDefenseCard(card.GetComponent<CardSheetDisplay>().GetCardData()));
        }
        else
        {
            return activeCardSheets.Count(card => card != null);
        }
    }
}
