/// <summary>
/// Phase transition host surface for callers outside <see cref="BattleManager"/>.
/// </summary>
public interface IBattlePhaseHost
{
    GameState CurrentState { get; }

    void SetGameState(GameState newState);
}
