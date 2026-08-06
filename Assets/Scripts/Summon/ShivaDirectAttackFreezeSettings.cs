using UnityEngine;

/// <summary>
/// Shiva passive (Ice Boundary): freeze proc chance and duration.
/// Message styling is in <see cref="MessagePopupSettings"/>.
/// </summary>
[CreateAssetMenu(fileName = "ShivaDirectAttackFreezeSettings", menuName = "DivineField/Summon/Shiva Direct Attack Freeze Settings")]
public sealed class ShivaDirectAttackFreezeSettings : ScriptableObject
{
    [Header("Freeze proc")]
    [Range(0, 100)]
    public int freezeChancePercent = 5;

    [Min(1)]
    public int freezeDurationTurns = 1;
}
