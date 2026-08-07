using System;
using UnityEngine;

/// <summary>
/// Float-up + fade timing shared by <see cref="MessagePopup"/> and <see cref="DamagePopup"/>.
/// Edit via <see cref="MessagePopupSettings"/> / <see cref="DamagePopupSettings"/> assets.
/// </summary>
[Serializable]
public struct PopupMotionTiming
{
    [Tooltip("Upward drift speed while visible.")]
    public float floatSpeed;

    [Tooltip("Seconds until fully faded out and destroyed.")]
    public float fadeDuration;

    [Tooltip("Extra wait after fade ends before the next battle sequence step (ms).")]
    public int postPopupIntervalMs;

    public bool IsFadeConfigured => fadeDuration > 0.001f;

    public static PopupMotionTiming MessageDefaults => new PopupMotionTiming
    {
        floatSpeed = 30f,
        fadeDuration = 1f,
        postPopupIntervalMs = 250,
    };

    public static PopupMotionTiming DamageDefaults => new PopupMotionTiming
    {
        floatSpeed = 30f,
        fadeDuration = 0.5f,
        postPopupIntervalMs = 250,
    };

    public PopupMotionTiming WithDefaults(PopupMotionTiming defaults)
    {
        var result = this;
        if (result.floatSpeed <= 0f) result.floatSpeed = defaults.floatSpeed;
        if (!result.IsFadeConfigured) result.fadeDuration = defaults.fadeDuration;
        if (result.postPopupIntervalMs <= 0) result.postPopupIntervalMs = defaults.postPopupIntervalMs;
        return result;
    }
}
