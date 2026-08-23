using UnityEngine;

/// <summary>
/// Attach to scene BGM <see cref="AudioSource"/> objects (Main, Title, etc.).
/// Applies <see cref="GameSettings.BgmVolume"/> while preserving each source base volume.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class BgmVolumeBinder : MonoBehaviour
{
    [SerializeField] private bool captureBaseVolumeOnAwake = true;
    [SerializeField] private float baseVolume = 1f;

    AudioSource _source;

    void Awake()
    {
        _source = GetComponent<AudioSource>();
        if (captureBaseVolumeOnAwake)
            baseVolume = _source.volume;
        Apply();
        GameSettings.BgmVolumeChanged += Apply;
    }

    void OnDestroy()
    {
        GameSettings.BgmVolumeChanged -= Apply;
    }

    void Apply()
    {
        if (_source == null) return;
        _source.volume = GameSettings.ScaleBgmVolume(baseVolume);
    }

    public void SetBaseVolume(float volume)
    {
        baseVolume = volume;
        Apply();
    }
}
