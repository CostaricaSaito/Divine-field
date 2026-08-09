using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>反射連鎖・介入・天変地異等、通常 DefensePhase 外のプレイヤー防御入力。</summary>
public enum AdHocDefenseKind
{
    ReflectionChain = 0,
    ParryRerun = 1,
    Intervention = 2,
    Disaster = 3,
    PostDeath = 4,
}

/// <summary>臨時防御セッション（同時に 1 件のみ）。</summary>
internal sealed class AdHocDefenseSession
{
    public AdHocDefenseKind Kind;
    public List<CardData> AttackSnapshot;
    public TaskCompletionSource<List<CardData>> SubmitTcs;

    public bool RequiresCombatResolvePhase;
    public bool RequiresTurnDefenderIsPlayer;
    public bool IgnoreTurnDefender;

    public bool IsPending => SubmitTcs != null && !SubmitTcs.Task.IsCompleted;

    /// <summary>選択クリア時に相手側 CardDisplay の攻撃表示を残す（介入・天変地異）。</summary>
    public bool KeepOpponentAttackPanelWhenClearingPlayerSelection =>
        Kind == AdHocDefenseKind.Intervention || Kind == AdHocDefenseKind.Disaster;

    public static AdHocDefenseSession Create(AdHocDefenseKind kind, List<CardData> attackSnapshot)
    {
        var session = new AdHocDefenseSession
        {
            Kind = kind,
            AttackSnapshot = attackSnapshot != null ? new List<CardData>(attackSnapshot) : null,
            SubmitTcs = new TaskCompletionSource<List<CardData>>(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        switch (kind)
        {
            case AdHocDefenseKind.Intervention:
                session.RequiresCombatResolvePhase = true;
                session.RequiresTurnDefenderIsPlayer = true;
                break;
            case AdHocDefenseKind.Disaster:
                session.IgnoreTurnDefender = true;
                break;
        }

        return session;
    }

    public BattleStep ToBattleStep()
    {
        return Kind switch
        {
            AdHocDefenseKind.ReflectionChain => BattleStep.ReflectionChainDefenseSelect,
            AdHocDefenseKind.Disaster => BattleStep.DisasterDefenseSelect,
            AdHocDefenseKind.Intervention => BattleStep.InterventionDefenseSelect,
            _ => BattleStep.DefenseSelect,
        };
    }
}
