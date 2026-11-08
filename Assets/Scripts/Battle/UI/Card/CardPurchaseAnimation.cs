using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// カード購入時のアニメーション演出を管理するクラス
/// </summary>
public class CardPurchaseAnimation : MonoBehaviour
{
    [Header("お金アイコンのPrefab")]
    [SerializeField] private GameObject moneyIconPrefab;
    
    [Header("アニメーション設定")]
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private float moneyIconSize = 200f;

    /// <summary>
    /// カード購入アニメーションを実行
    /// </summary>
    /// <param name="cardData">購入するカードデータ</param>
    /// <param name="paymentAmount">支払い金額</param>
    /// <param name="enemyCardDisplayArea">相手のカード表示エリア</param>
    /// <param name="playerCardDisplayArea">プレイヤーのカード表示エリア</param>
    public async Task PlayPurchaseAnimation(CardData cardData, int paymentAmount, 
        Transform enemyCardDisplayArea, Transform playerCardDisplayArea)
    {
        if (cardData == null || enemyCardDisplayArea == null || playerCardDisplayArea == null)
        {
            Debug.LogWarning("[CardPurchaseAnimation] パラメータがnullです");
            return;
        }

        Debug.Log($"[CardPurchaseAnimation] 購入アニメーション開始 - カード: {cardData.cardName}, 金額: {paymentAmount}GP");

        GameObject cardObject = ResolveCardSheetObject(enemyCardDisplayArea, cardData);
        if (cardObject == null)
        {
            Debug.LogWarning("[CardPurchaseAnimation] カードオブジェクトが見つかりません");
            return;
        }

        // 目標位置を計算（各表示エリアの中心位置）
        Vector3 playerCenter = playerCardDisplayArea.position;
        Vector3 enemyCenter = enemyCardDisplayArea.position;
        
        // カードの現在位置を取得
        Vector3 cardStartPosition = cardObject.transform.position;

        // 1. 購入するカードを表示し、インターバル
        Debug.Log("[CardPurchaseAnimation] カード表示完了、0.5秒待機");
        await Task.Delay(500);

        // 2. お金アイコンを表示し、0.2秒インターバル
        GameObject moneyIcon = CreateMoneyIcon(paymentAmount, playerCardDisplayArea);
        if (moneyIcon != null)
        {
            moneyIcon.transform.position = playerCenter;
            Debug.Log("[CardPurchaseAnimation] お金アイコン表示完了、0.2秒待機");
            await Task.Delay(200);
        }
        else
        {
            Debug.LogWarning("[CardPurchaseAnimation] お金アイコンの生成に失敗しました - Prefabが設定されていない可能性があります");
        }

        // 3. アニメーションを同時実行
        // カードは水平に移動（X座標のみ変更）
        Vector3 cardTargetPosition = new Vector3(playerCenter.x, cardStartPosition.y, cardStartPosition.z);
        var cardAnimation = MoveCardToPlayer(cardObject, cardTargetPosition);
        
        // お金アイコンは垂直に移動（Y座標のみ変更）
        Task moneyAnimation = Task.CompletedTask;
        if (moneyIcon != null)
        {
            Vector3 moneyTargetPosition = new Vector3(enemyCenter.x, enemyCenter.y, enemyCenter.z);
            moneyAnimation = MoveMoneyToEnemy(moneyIcon, moneyTargetPosition);
        }

        // 両方のアニメーション完了を待機
        await Task.WhenAll(cardAnimation, moneyAnimation);

        // 4. アニメーション終了後、効果音再生
        Debug.Log("[CardPurchaseAnimation] アニメーション完了、効果音再生");
        SoundEffectPlayer.I?.Play("Assets/SE/レジスターで精算.mp3");

        // 5. 1秒インターバルして削除
        Debug.Log("[CardPurchaseAnimation] 1秒待機後に削除");
        await Task.Delay(1000);

        // お金アイコンとカードテンプレートシートを削除
        if (moneyIcon != null)
        {
            Destroy(moneyIcon);
            Debug.Log("[CardPurchaseAnimation] お金アイコン削除完了");
        }

        if (cardObject != null)
        {
            Destroy(cardObject);
            Debug.Log("[CardPurchaseAnimation] カードテンプレートシート削除完了");
        }

        Debug.Log("[CardPurchaseAnimation] 購入アニメーション完了");
    }

    private static GameObject ResolveCardSheetObject(Transform displayArea, CardData cardData)
    {
        if (cardData == null) return null;

        if (BattleUIManager.I != null
            && BattleUIManager.I.TryGetCardSheetDisplayForCardData(cardData, out var tracked))
            return tracked.gameObject;

        if (displayArea == null) return null;

        int targetId = cardData.GetInstanceID();
        var sheets = displayArea.GetComponentsInChildren<CardSheetDisplay>(true);
        for (int i = sheets.Length - 1; i >= 0; i--)
        {
            var sheet = sheets[i];
            if (sheet == null) continue;
            var data = sheet.GetCurrentCardData();
            if (data == null) continue;
            if (ReferenceEquals(data, cardData) || data.GetInstanceID() == targetId)
                return sheet.gameObject;
        }

        var cardUIs = displayArea.GetComponentsInChildren<CardUI>(true);
        for (int i = cardUIs.Length - 1; i >= 0; i--)
        {
            var ui = cardUIs[i];
            if (ui == null) continue;
            var data = ui.GetCardData();
            if (data == null) continue;
            if (ReferenceEquals(data, cardData) || data.GetInstanceID() == targetId)
                return ui.gameObject;
        }

        Debug.LogWarning($"[CardPurchaseAnimation] カードオブジェクトが見つかりませんでした: {cardData.cardName}");
        return null;
    }

    /// <summary>
    /// カードをプレイヤーの表示エリアに移動
    /// </summary>
    private async Task MoveCardToPlayer(GameObject cardObject, Vector3 targetPosition)
    {
        if (cardObject == null) return;

        var tween = LeanTween.move(cardObject, targetPosition, animationDuration)
            .setEase(LeanTweenType.easeInOutQuad);

        await Task.Delay((int)(animationDuration * 1000));
    }

    /// <summary>
    /// お金アイコンを相手の表示エリアに移動
    /// </summary>
    private async Task MoveMoneyToEnemy(GameObject moneyObject, Vector3 targetPosition)
    {
        if (moneyObject == null) return;

        var tween = LeanTween.move(moneyObject, targetPosition, animationDuration)
            .setEase(LeanTweenType.easeInOutQuad);

        await Task.Delay((int)(animationDuration * 1000));
    }

        /// <summary>
        /// お金アイコンを生成
        /// </summary>
        private GameObject CreateMoneyIcon(int amount, Transform parentTransform)
        {
            Debug.Log($"[CardPurchaseAnimation] お金アイコン生成開始 - 金額: {amount}GP");
            
            if (moneyIconPrefab == null)
            {
                Debug.LogWarning("[CardPurchaseAnimation] お金アイコンのPrefabが設定されていません");
                return null;
            }

            Debug.Log("[CardPurchaseAnimation] Prefabからお金アイコンを生成中...");
            
            // お金アイコンを生成（指定された親の子として）
            GameObject moneyIcon = Instantiate(moneyIconPrefab, parentTransform);
            moneyIcon.name = "MoneyIcon";
            
            Debug.Log($"[CardPurchaseAnimation] お金アイコン生成完了: {moneyIcon.name}");

            // サイズを設定
            var rectTransform = moneyIcon.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(moneyIconSize, moneyIconSize);
                Debug.Log($"[CardPurchaseAnimation] サイズ設定完了: {moneyIconSize}x{moneyIconSize}");
            }
            else
            {
                Debug.LogWarning("[CardPurchaseAnimation] RectTransformが見つかりません");
            }

            // GP金額を表示するテキストを設定
            var textComponent = moneyIcon.GetComponentInChildren<TMP_Text>();
            if (textComponent != null)
            {
                textComponent.text = $"{amount}GP";
                Debug.Log($"[CardPurchaseAnimation] テキスト設定完了: {amount}GP");
            }
            else
            {
                Debug.LogWarning("[CardPurchaseAnimation] TMP_Textが見つかりません");
            }

            Debug.Log($"[CardPurchaseAnimation] お金アイコン生成完了 - 金額: {amount}GP");
            return moneyIcon;
        }
}
