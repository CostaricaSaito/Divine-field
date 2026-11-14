/// <summary>
/// Opponent buy / sell / exchange cooldowns.
/// CPU exchange uses this today. Online buy, sell, and exchange set the same counters.
/// </summary>
public sealed class EnemyCpuEconomyTracker
{
    public const int ExchangeCooldownTurns = 5;

    private int _buyCooldown;
    private int _sellCooldown;
    private int _exchangeCooldown;

    public void Reset()
    {
        _buyCooldown = 0;
        _sellCooldown = 0;
        _exchangeCooldown = 0;
    }

    /// <summary>Decrement on the opponent's turn start. True means that action just became ready.</summary>
    public BattleEconomyCooldownReady OnEnemyTurnStart()
    {
        return new BattleEconomyCooldownReady
        {
            Buy = Tick(ref _buyCooldown),
            Sell = Tick(ref _sellCooldown),
            Exchange = Tick(ref _exchangeCooldown),
        };
    }

    public bool CanBuy => _buyCooldown <= 0;
    public bool CanSell => _sellCooldown <= 0;
    public bool CanExchange => _exchangeCooldown <= 0;

    public void SetBuyCooldown() => _buyCooldown = ExchangeCooldownTurns;

    public void SetSellCooldown() => _sellCooldown = ExchangeCooldownTurns;

    public void SetExchangeCooldown() => _exchangeCooldown = ExchangeCooldownTurns;

    private static bool Tick(ref int cooldown)
    {
        if (cooldown <= 0) return false;
        cooldown--;
        return cooldown == 0;
    }
}
