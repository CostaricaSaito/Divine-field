/// <summary>
/// Which economic cooldowns reached 0 on this turn start.
/// Cooldowns that were already 0 are not reported.
/// </summary>
public struct BattleEconomyCooldownReady
{
    public bool Buy;
    public bool Sell;
    public bool Exchange;
}
