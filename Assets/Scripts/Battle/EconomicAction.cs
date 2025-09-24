using UnityEngine;

/// <summary>
/// 経済アクションの管理クラス
/// 5ターン制限の管理とアクション実行を担当
/// </summary>
public class EconomicAction : MonoBehaviour
{
    public static EconomicAction I;

    [Header("制限設定")]
    [SerializeField] private int cooldownTurns = 5; // クールダウンターン数

    // 制限管理
    private int buyCooldown = 0;
    private int sellCooldown = 0;
    private int exchangeCooldown = 0;

    private void Awake()
    {
        if (I == null)
        {
            I = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ターン開始時の処理（クールダウンを1ターン減らす）
    /// </summary>
    public void OnTurnStart()
    {
        if (buyCooldown > 0) buyCooldown--;
        if (sellCooldown > 0) sellCooldown--;
        if (exchangeCooldown > 0) exchangeCooldown--;

        Debug.Log($"[EconomicAction] クールダウン更新 - 買う: {buyCooldown}, 売る: {sellCooldown}, 両替: {exchangeCooldown}");
    }

    /// <summary>
    /// 「買う」アクションが使用可能かチェック
    /// </summary>
    public bool CanBuy()
    {
        return buyCooldown <= 0;
    }

    /// <summary>
    /// 「売る」アクションが使用可能かチェック
    /// </summary>
    public bool CanSell()
    {
        return sellCooldown <= 0;
    }

    /// <summary>
    /// 「両替」アクションが使用可能かチェック
    /// </summary>
    public bool CanExchange()
    {
        return exchangeCooldown <= 0;
    }

    /// <summary>
    /// 「買う」アクションのクールダウンを設定
    /// </summary>
    public void SetBuyCooldown()
    {
        buyCooldown = cooldownTurns;
        Debug.Log($"[EconomicAction] 買うアクションのクールダウン設定: {buyCooldown}ターン");
    }

    /// <summary>
    /// 「売る」アクションのクールダウンを設定
    /// </summary>
    public void SetSellCooldown()
    {
        sellCooldown = cooldownTurns;
        Debug.Log($"[EconomicAction] 売るアクションのクールダウン設定: {sellCooldown}ターン");
    }

    /// <summary>
    /// 「両替」アクションのクールダウンを設定
    /// </summary>
    public void SetExchangeCooldown()
    {
        exchangeCooldown = cooldownTurns;
        Debug.Log($"[EconomicAction] 両替アクションのクールダウン設定: {exchangeCooldown}ターン");
    }

    /// <summary>
    /// 買うボタンの残りクールダウンを取得
    /// </summary>
    public int GetBuyCooldown()
    {
        return buyCooldown;
    }

    /// <summary>
    /// 売るボタンの残りクールダウンを取得
    /// </summary>
    public int GetSellCooldown()
    {
        return sellCooldown;
    }

    /// <summary>
    /// 両替ボタンの残りクールダウンを取得
    /// </summary>
    public int GetExchangeCooldown()
    {
        return exchangeCooldown;
    }
}
