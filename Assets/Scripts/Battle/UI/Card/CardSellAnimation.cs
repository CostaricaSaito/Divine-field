using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// カード売却時のアニメーション演出を管理するクラス
/// </summary>
public class CardSellAnimation : MonoBehaviour
{
    [Header("お金アイコンのPrefab")]
    [SerializeField] private GameObject moneyIcon2Prefab;
    
    [Header("アニメーション設定")]
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private float moneyIconSize = 200f;

    /// <summary>
    /// カード売却アニメーションを実行
    /// </summary>
    /// <param name="cardData">売却するカードデータ</param>
    /// <param name="sellAmount">売却金額</param>
    /// <param name="playerCardDisplayArea">プレイヤーのカード表示エリア</param>
    /// <param name="enemyCardDisplayArea">相手のカード表示エリア</param>
    /// <param name="cardSheetPrefab">カードシートのPrefab</param>
    public async Task PlaySellAnimation(CardData cardData, int sellAmount, 
        Transform playerCardDisplayArea, Transform enemyCardDisplayArea, GameObject cardSheetPrefab)
    {
        if (cardData == null || playerCardDisplayArea == null || enemyCardDisplayArea == null || cardSheetPrefab == null)
        {
            Debug.LogWarning("[CardSellAnimation] パラメータがnullです");
            return;
        }

        Debug.Log($"[CardSellAnimation] 売却アニメーション開始 - カード: {cardData.cardName}, 金額: {sellAmount}GP");

        // 1. 売却するカードをCardDisplayPanel（プレイヤーのカード表示エリア）の真ん中に表示
        GameObject cardObject = CreateCardSheet(cardData, playerCardDisplayArea, cardSheetPrefab);
        if (cardObject == null)
        {
            Debug.LogWarning("[CardSellAnimation] カードシートの生成に失敗しました");
            return;
        }

        // カードを表示エリアの中心に配置（RectTransformを使用）
        RectTransform cardRectInit = cardObject.GetComponent<RectTransform>();
        RectTransform playerPanelRectInit = playerCardDisplayArea.GetComponent<RectTransform>();
        if (cardRectInit != null && playerPanelRectInit != null)
        {
            // パネルの中心に配置（ローカル座標系）
            cardRectInit.anchoredPosition = Vector2.zero;
        }
        else
        {
            // フォールバック：ワールド座標を使用
            Vector3 playerCenter = playerCardDisplayArea.position;
            cardObject.transform.position = playerCenter;
        }

        // 2. 0.5秒インターバル
        Debug.Log("[CardSellAnimation] カード表示完了、0.5秒待機");
        await Task.Delay(500);

        // 3. お金アイコン（MoneyIcon2.prefab）を相手のカード表示エリアの真ん中に表示
        GameObject moneyIcon = CreateMoneyIcon(sellAmount, enemyCardDisplayArea);
        if (moneyIcon != null)
        {
            RectTransform moneyRectInit = moneyIcon.GetComponent<RectTransform>();
            RectTransform enemyPanelRectInit = enemyCardDisplayArea.GetComponent<RectTransform>();
            if (moneyRectInit != null && enemyPanelRectInit != null)
            {
                // パネルの中心に配置（ローカル座標系）
                moneyRectInit.anchoredPosition = Vector2.zero;
            }
            else
            {
                // フォールバック：ワールド座標を使用
                Vector3 enemyCenter = enemyCardDisplayArea.position;
                moneyIcon.transform.position = enemyCenter;
            }
            Debug.Log("[CardSellAnimation] お金アイコン表示完了");
        }
        else
        {
            Debug.LogWarning("[CardSellAnimation] お金アイコンの生成に失敗しました - Prefabが設定されていない可能性があります");
        }

        // 4. カードとお金アイコンを水平に移動（イージング）
        // カードは相手のカード表示エリアの真ん中に移動
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        RectTransform enemyPanelRect = enemyCardDisplayArea.GetComponent<RectTransform>();
        Vector2 cardStartAnchoredPos = Vector2.zero;
        Vector2 cardTargetAnchoredPos = Vector2.zero;
        
        if (cardRect != null && enemyPanelRect != null)
        {
            // 親を変更する前に、現在のワールド位置を取得
            Vector3 worldPos = cardRect.position;
            // カードを相手パネルの子に移動（worldPositionStays=falseでローカル座標をリセット）
            cardRect.SetParent(enemyPanelRect, false);
            // ワールド位置を新しい親のローカル座標に変換
            cardRect.position = worldPos;
            // 現在の位置を取得（相手パネル内での位置）
            cardStartAnchoredPos = cardRect.anchoredPosition;
            // 移動後の目標位置（相手パネルの中心）
            cardTargetAnchoredPos = Vector2.zero;
        }
        var cardAnimation = MoveCardToEnemy(cardObject, cardStartAnchoredPos, cardTargetAnchoredPos);
        
        // お金アイコンは自分のカード表示エリアの真ん中に移動
        Task moneyAnimation = Task.CompletedTask;
        if (moneyIcon != null)
        {
            RectTransform moneyRect = moneyIcon.GetComponent<RectTransform>();
            RectTransform playerPanelRect = playerCardDisplayArea.GetComponent<RectTransform>();
            Vector2 moneyStartAnchoredPos = Vector2.zero;
            Vector2 moneyTargetAnchoredPos = Vector2.zero;
            
            if (moneyRect != null && playerPanelRect != null)
            {
                // 親を変更する前に、現在のワールド位置を取得
                Vector3 worldPos = moneyRect.position;
                // お金アイコンをプレイヤーパネルの子に移動（worldPositionStays=falseでローカル座標をリセット）
                moneyRect.SetParent(playerPanelRect, false);
                // ワールド位置を新しい親のローカル座標に変換
                moneyRect.position = worldPos;
                // 現在の位置を取得（プレイヤーパネル内での位置）
                moneyStartAnchoredPos = moneyRect.anchoredPosition;
                // 移動後の目標位置（プレイヤーパネルの中心）
                moneyTargetAnchoredPos = Vector2.zero;
            }
            moneyAnimation = MoveMoneyToPlayer(moneyIcon, moneyStartAnchoredPos, moneyTargetAnchoredPos);
        }

        // 両方のアニメーション完了を待機
        await Task.WhenAll(cardAnimation, moneyAnimation);

        // 5. 移動終了と同時に効果音再生
        Debug.Log("[CardSellAnimation] アニメーション完了、効果音再生");
        SoundEffectPlayer.I?.Play("Assets/SE/レジスターで精算.mp3");

        // 6. 0.5秒インターバル
        Debug.Log("[CardSellAnimation] 0.5秒待機後に削除");
        await Task.Delay(500);

        // 7. カード表示エリアからお金アイコンと売却したカードを削除
        if (moneyIcon != null)
        {
            Destroy(moneyIcon);
            Debug.Log("[CardSellAnimation] お金アイコン削除完了");
        }

        if (cardObject != null)
        {
            Destroy(cardObject);
            Debug.Log("[CardSellAnimation] カードシート削除完了");
        }

        Debug.Log("[CardSellAnimation] 売却アニメーション完了");
    }

    /// <summary>
    /// カードシートを生成
    /// </summary>
    private GameObject CreateCardSheet(CardData cardData, Transform parentTransform, GameObject cardSheetPrefab)
    {
        if (cardData == null || parentTransform == null || cardSheetPrefab == null)
        {
            Debug.LogWarning("[CardSellAnimation] カードシート生成に必要なパラメータがnullです");
            return null;
        }

        GameObject cardSheet = Instantiate(cardSheetPrefab, parentTransform);
        cardSheet.name = $"SellCard_{cardData.cardName}";
        
        var sheetDisplay = cardSheet.GetComponent<CardSheetDisplay>();
        if (sheetDisplay != null)
        {
            sheetDisplay.Setup(cardData);
        }
        else
        {
            Debug.LogWarning("[CardSellAnimation] CardSheetDisplayコンポーネントが見つかりません");
        }

        return cardSheet;
    }

    /// <summary>
    /// カードを相手の表示エリアに移動
    /// </summary>
    private async Task MoveCardToEnemy(GameObject cardObject, Vector2 startAnchoredPosition, Vector2 targetAnchoredPosition)
    {
        if (cardObject == null) return;

        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            // 親を変更した後、位置をリセットしてからアニメーション
            cardRect.anchoredPosition = startAnchoredPosition;
            
            // RectTransformのanchoredPositionをアニメーション
            LeanTween.value(cardObject, startAnchoredPosition, targetAnchoredPosition, animationDuration)
                .setEase(LeanTweenType.easeInOutQuad)
                .setOnUpdate((Vector2 pos) => {
                    if (cardRect != null) cardRect.anchoredPosition = pos;
                });
        }
        else
        {
            // フォールバック：通常のtransform.positionを使用
            var tween = LeanTween.move(cardObject, cardObject.transform.position, animationDuration)
                .setEase(LeanTweenType.easeInOutQuad);
        }

        await Task.Delay((int)(animationDuration * 1000));
    }

    /// <summary>
    /// お金アイコンをプレイヤーの表示エリアに移動
    /// </summary>
    private async Task MoveMoneyToPlayer(GameObject moneyObject, Vector2 startAnchoredPosition, Vector2 targetAnchoredPosition)
    {
        if (moneyObject == null) return;

        RectTransform moneyRect = moneyObject.GetComponent<RectTransform>();
        if (moneyRect != null)
        {
            // 親を変更した後、位置をリセットしてからアニメーション
            moneyRect.anchoredPosition = startAnchoredPosition;
            
            // RectTransformのanchoredPositionをアニメーション
            LeanTween.value(moneyObject, startAnchoredPosition, targetAnchoredPosition, animationDuration)
                .setEase(LeanTweenType.easeInOutQuad)
                .setOnUpdate((Vector2 pos) => {
                    if (moneyRect != null) moneyRect.anchoredPosition = pos;
                });
        }
        else
        {
            // フォールバック：通常のtransform.positionを使用
            var tween = LeanTween.move(moneyObject, moneyObject.transform.position, animationDuration)
                .setEase(LeanTweenType.easeInOutQuad);
        }

        await Task.Delay((int)(animationDuration * 1000));
    }

    /// <summary>
    /// お金アイコンを生成
    /// </summary>
    private GameObject CreateMoneyIcon(int amount, Transform parentTransform)
    {
        Debug.Log($"[CardSellAnimation] お金アイコン生成開始 - 金額: {amount}GP");
        
        if (moneyIcon2Prefab == null)
        {
            Debug.LogWarning("[CardSellAnimation] お金アイコンのPrefab（MoneyIcon2）が設定されていません");
            return null;
        }

        Debug.Log("[CardSellAnimation] Prefabからお金アイコンを生成中...");
        
        // お金アイコンを生成（指定された親の子として）
        GameObject moneyIcon = Instantiate(moneyIcon2Prefab, parentTransform);
        moneyIcon.name = "MoneyIcon2";
        
        Debug.Log($"[CardSellAnimation] お金アイコン生成完了: {moneyIcon.name}");

        // サイズを設定
        var rectTransform = moneyIcon.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(moneyIconSize, moneyIconSize);
            Debug.Log($"[CardSellAnimation] サイズ設定完了: {moneyIconSize}x{moneyIconSize}");
        }
        else
        {
            Debug.LogWarning("[CardSellAnimation] RectTransformが見つかりません");
        }

        // GP金額を表示するテキストを設定
        var textComponent = moneyIcon.GetComponentInChildren<TMP_Text>();
        if (textComponent != null)
        {
            textComponent.text = $"{amount}GP";
            Debug.Log($"[CardSellAnimation] テキスト設定完了: {amount}GP");
        }
        else
        {
            Debug.LogWarning("[CardSellAnimation] TMP_Textが見つかりません");
        }

        Debug.Log($"[CardSellAnimation] お金アイコン生成完了 - 金額: {amount}GP");
        return moneyIcon;
    }
}

