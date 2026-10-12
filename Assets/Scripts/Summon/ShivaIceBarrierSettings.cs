using UnityEngine;

/// <summary>
/// Shiva passive (Ice Barrier): chance for opponent draws to become the FROZEN hand token.
/// Message styling is in <see cref="MessagePopupSettings"/>.
/// </summary>
[CreateAssetMenu(fileName = "ShivaIceBarrierSettings", menuName = "DivineField/Summon/Shiva Ice Barrier Settings")]
public sealed class ShivaIceBarrierSettings : ScriptableObject
{
    [Header("Ice Barrier proc")]
    [Range(0, 100)]
    public int iceBarrierChancePercent = 5;
}
