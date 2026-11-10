using UnityEngine;

[CreateAssetMenu(fileName = "BattleBgmPlaylist", menuName = "DivineField/Battle BGM Playlist")]
public sealed class BattleBgmPlaylistSO : ScriptableObject
{
    [SerializeField] private AudioClip[] tracks;
    [SerializeField] private GameObject bgmTitlePrefab;

    public AudioClip[] Tracks => tracks;
    public GameObject BgmTitlePrefab => bgmTitlePrefab;

    public static string FormatTrackTitle(AudioClip clip)
    {
        if (clip == null) return string.Empty;
        return clip.name.Replace('_', ' ').ToUpperInvariant();
    }
}
