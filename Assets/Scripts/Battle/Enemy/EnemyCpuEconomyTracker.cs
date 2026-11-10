/// <summary>
/// Local CPU-only economic cooldown (independent from player <see cref="EconomicAction"/>).
/// </summary>
public sealed class EnemyCpuEconomyTracker
{
    public const int ExchangeCooldownTurns = 5;

    private int _exchangeCooldown;

    public void Reset()
    {
        _exchangeCooldown = 0;
    }

    public void OnEnemyTurnStart()
    {
        if (_exchangeCooldown > 0)
            _exchangeCooldown--;
    }

    public bool CanExchange => _exchangeCooldown <= 0;

    public void SetExchangeCooldown()
    {
        _exchangeCooldown = ExchangeCooldownTurns;
    }
}
