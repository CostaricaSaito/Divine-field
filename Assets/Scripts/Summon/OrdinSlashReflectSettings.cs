using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Odin passive (切り払い): slash cut-in video and 5% physical reflect proc.
/// Popup styling uses <see cref="DamagePopupKind.OrdinReflectionBounce"/>.
/// </summary>
[CreateAssetMenu(fileName = "OrdinSlashReflectSettings", menuName = "DivineField/Summon/Odin Slash Reflect Settings")]
public sealed class OrdinSlashReflectSettings : ScriptableObject
{
    [Header("Proc")]
    [Range(0, 100)]
    public int slashReflectChancePercent = 5;

    [Header("Cut-in")]
    public VideoClip slashVideoClip;

    [Tooltip("Addressables key when slashVideoClip is not assigned.")]
    public string slashVideoAddress = "Assets/Videos/\u65AC\u6483.mp4";

    public string reflectSoundEffectPath = "Assets/SE/\u30AA\u30FC\u30C7\u30A3\u30F3\u53CD\u5C04.wav";
}
