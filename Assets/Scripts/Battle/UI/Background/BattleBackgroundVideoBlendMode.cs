using System;
using UnityEngine;
using UnityEngine.Video;

public enum BattleBackgroundVideoBlendMode
{
    Alpha = 0,
    Additive = 1,
    Screen = 2,
    AdditiveGlow = 3,
}

[Serializable]
public sealed class BattleBackgroundVideoEntry
{
    [Header("Media (set Clip or Still Image)")]
    public VideoClip clip;
    [Tooltip("Optional still image entry. Used when Clip is not set.")]
    public Sprite stillImage;

    [Header("Video")]
    [Tooltip("VideoPlayer.isLooping for this clip. Ignored for still images.")]
    public bool loopClip = true;

    [Header("Timing")]
    [Min(0f)]
    [Tooltip("Seconds before switching to the next entry. 0 = video loop/end rules, or hold still image forever.")]
    public float durationSeconds = 30f;

    [Header("Composite")]
    public BattleBackgroundVideoBlendMode blendMode = BattleBackgroundVideoBlendMode.Additive;
    [Range(0f, 3f)]
    public float intensity = 1f;

    public bool IsVideo => clip != null;
    public bool IsStill => clip == null && stillImage != null;
    public bool IsValid => IsVideo || IsStill;
}
