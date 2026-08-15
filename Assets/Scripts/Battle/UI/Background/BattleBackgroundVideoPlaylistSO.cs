using UnityEngine;

[CreateAssetMenu(fileName = "BattleBackgroundVideoPlaylist", menuName = "DivineField/Battle Background Video Playlist")]
public sealed class BattleBackgroundVideoPlaylistSO : ScriptableObject
{
    [SerializeField] private BattleBackgroundVideoEntry[] entries;
    [SerializeField] private bool loopPlaylist = true;
    [SerializeField] private float fadeOutSeconds = 0.5f;
    [SerializeField] private float fadeInSeconds = 0.5f;
    [SerializeField] private BattleBackgroundVideoBlendMode defaultBlendMode = BattleBackgroundVideoBlendMode.Additive;

    public BattleBackgroundVideoEntry[] Entries => entries;
    public bool LoopPlaylist => loopPlaylist;
    public float FadeOutSeconds => Mathf.Max(0f, fadeOutSeconds);
    public float FadeInSeconds => Mathf.Max(0f, fadeInSeconds);
    public BattleBackgroundVideoBlendMode DefaultBlendMode => defaultBlendMode;
}
