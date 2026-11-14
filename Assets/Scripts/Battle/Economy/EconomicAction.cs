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
    /// プレイヤーのターン開始時のみ呼ばれる
    /// </summary>
    public BattleEconomyCooldownReady OnTurnStart()
    {
        bool buyReady = TickCooldown(ref buyCooldown);
        bool sellReady = TickCooldown(ref sellCooldown);
        bool exchangeReady = TickCooldown(ref exchangeCooldown);

        Debug.Log($"[EconomicAction] クールダウン更新 - 買う: {buyCooldown}, 売る: {sellCooldown}, 両替: {exchangeCooldown}");
        return new BattleEconomyCooldownReady
        {
            Buy = buyReady,
            Sell = sellReady,
            Exchange = exchangeReady,
        };
    }

    private static bool TickCooldown(ref int cooldown)
    {
        if (cooldown <= 0) return false;
        cooldown--;
        return cooldown == 0;
    }

    private SummonTurnCounterState ResolveTurnCounters()
        => BattleManager.I != null ? BattleManager.I.SummonTurnCounters : null;

    /// <summary>買う／売るがターン条件で解放済みか。</summary>
    public bool IsBuySellUnlocked()
        => EconomicActionRules.IsBuySellUnlocked(ResolveTurnCounters());

    /// <summary>買う／売る解放までの残り表示ターン（解放済みなら 0）。</summary>
    public int GetBuySellTurnsUntilUnlock()
        => EconomicActionRules.GetBuySellTurnsUntilUnlock(ResolveTurnCounters());

    /// <summary>
    /// 「買う」アクションが使用可能かチェック
    /// </summary>
    public bool CanBuy()
    {
        if (!IsBuySellUnlocked()) return false;
        return buyCooldown <= 0;
    }

    /// <summary>
    /// 「売る」アクションが使用可能かチェック
    /// </summary>
    public bool CanSell()
    {
        if (!IsBuySellUnlocked()) return false;
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
