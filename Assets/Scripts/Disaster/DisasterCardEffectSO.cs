using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 天変地異カードごとのメッセージと効果。Special 系とは別系統。
/// </summary>
public abstract class DisasterCardEffectSO : ScriptableObject
{
    [SerializeField] private DisasterKind kind;
    [TextArea(1, 2)]
    [SerializeField] private string notificationMessage;
    [FormerlySerializedAs("messagePopupKind")]
    [SerializeField] private ImportantPopupKind importantPopupKind = ImportantPopupKind.DisasterEruption;

    public DisasterKind Kind => kind;
    public string NotificationMessage => notificationMessage ?? string.Empty;
    public ImportantPopupKind ImportantPopupKind => importantPopupKind;

    internal void ConfigureForRuntime(DisasterKind runtimeKind, string message, ImportantPopupKind popupKind)
    {
        kind = runtimeKind;
        notificationMessage = message;
        importantPopupKind = popupKind;
    }

    public abstract Task ResolveAsync(DisasterResolveContext context, CancellationToken cancellationToken);
}

/// <summary>天変地異効果解決時に Orchestrator から渡すコンテキスト。</summary>
public sealed class DisasterResolveContext
{
    public BattleManager BattleManager;
    public BattleProcessor BattleProcessor;
    public CardSequenceManager Sequences;
    public PlayerStatus TriggerOwner;
    public PlayerStatus Opponent;
    public Side TriggerSide;
    public CardData DisplayCard;
    public CardData CombatCardTemplate;
}
