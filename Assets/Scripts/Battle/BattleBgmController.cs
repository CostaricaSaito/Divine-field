using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

/// <summary>
/// Battle BGM: random track from <see cref="BattleBgmPlaylistSO"/>, disadvantage fade, arch-magic background.
/// Attach to the scene BGM object (with AudioSource).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BattleBgmController : MonoBehaviour
{
    public static BattleBgmController Instance { get; private set; }

    [Header("Playlist (required)")]
    [Tooltip("未設定時は Resources/BattleBgmPlaylist を読み込む")]
    [SerializeField] private BattleBgmPlaylistSO playlist;

    [Header("Disadvantage")]
    [SerializeField] private AudioClip disadvantageClip;
    [SerializeField] private float fadeOutSeconds = 0.45f;
    [SerializeField] private float fadeInSeconds = 0.45f;

    [Header("Disadvantage background (optional)")]
    [SerializeField] private Image battleBackgroundImage;
    [SerializeField] private Sprite disadvantageBackground;
    [Tooltip("disadvantageBackground 未設定時のみ Addressables から読み込む")]
    [SerializeField] private string disadvantageBackgroundAddress = "Assets/Images/02_背景/劣勢.jpg";

    private AudioSource _source;
    private AudioClip _baselineNormalClip;
    private float _targetVolume = 0.27f;
    private bool? _lastDisadvantageWant;
    private Coroutine _fadeCoroutine;
    private CancellationTokenSource _fadeOutCts;

    private Sprite _baselineBackgroundSprite;
    private Sprite _disadvantageBackgroundSprite;
    private Color _backgroundBaseColor = Color.white;

    private bool _battleSessionStarted;
    private CancellationTokenSource _titlePresentationCts;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        _source = GetComponent<AudioSource>();
        if (_source != null)
        {
            _targetVolume = _source.volume;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.clip = null;
            _source.Stop();
        }

        if (battleBackgroundImage != null)
        {
            _baselineBackgroundSprite = battleBackgroundImage.sprite;
            _backgroundBaseColor = battleBackgroundImage.color;
        }

        if (disadvantageBackground != null)
            _disadvantageBackgroundSprite = disadvantageBackground;
    }

    private void OnDestroy()
    {
        _titlePresentationCts?.Cancel();
        _titlePresentationCts?.Dispose();
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Battle bootstrap: pick random BGM from playlist, start playback, show title prefab.</summary>
    public void StartBattleSession()
    {
        if (_battleSessionStarted) return;
        _battleSessionStarted = true;

        if (_source == null) return;

        var resolvedPlaylist = ResolvePlaylist();
        var tracks = resolvedPlaylist?.Tracks;
        if (tracks == null || tracks.Length == 0)
        {
            Debug.LogError("[BattleBgmController] BattleBgmPlaylist has no tracks");
            return;
        }

        var clip = PickRandomTrackClip(tracks);
        if (clip == null)
        {
            Debug.LogError("[BattleBgmController] BattleBgmPlaylist has no valid AudioClip entries");
            return;
        }

        _baselineNormalClip = clip;
        _source.clip = clip;
        _source.loop = true;
        _source.volume = _targetVolume;
        _source.Play();

        if (resolvedPlaylist.BgmTitlePrefab != null)
            _ = ShowBgmTitleAsync(resolvedPlaylist.BgmTitlePrefab, BattleBgmPlaylistSO.FormatTrackTitle(clip));
    }

    private BattleBgmPlaylistSO ResolvePlaylist()
    {
        if (playlist != null) return playlist;
        return Resources.Load<BattleBgmPlaylistSO>("BattleBgmPlaylist");
    }

    private static AudioClip PickRandomTrackClip(AudioClip[] tracks)
    {
        if (tracks == null || tracks.Length == 0) return null;

        int validCount = 0;
        for (int i = 0; i < tracks.Length; i++)
        {
            if (tracks[i] != null) validCount++;
        }

        if (validCount == 0) return null;
        if (validCount == 1)
        {
            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i] != null) return tracks[i];
            }
        }

        int pick = PickRandomTrackIndex(validCount);
        for (int i = 0; i < tracks.Length; i++)
        {
            if (tracks[i] == null) continue;
            if (pick == 0) return tracks[i];
            pick--;
        }

        return null;
    }

    private static int PickRandomTrackIndex(int count)
    {
        if (count <= 1) return 0;
        return BattleRandom.IsDeterministic
            ? BattleRandom.Range(0, count)
            : UnityEngine.Random.Range(0, count);
    }

    private async Task ShowBgmTitleAsync(GameObject prefab, string trackTitle)
    {
        _titlePresentationCts?.Cancel();
        _titlePresentationCts?.Dispose();
        _titlePresentationCts = new CancellationTokenSource();
        var ct = _titlePresentationCts.Token;

        Transform parent = BattleUIManager.I != null
            ? BattleUIManager.I.GetPopupCanvas()?.transform
            : null;
        if (parent == null)
            parent = transform;

        var instance = Instantiate(prefab, parent, false);
        await Task.Yield();

        var view = instance.GetComponent<BattleBgmTitleView>();
        if (view == null)
            view = instance.AddComponent<BattleBgmTitleView>();

        try
        {
            await view.PlayAsync(trackTitle, ct);
        }
        catch (OperationCanceledException)
        {
            if (instance != null)
                Destroy(instance);
        }
    }

    /// <summary>プレイヤーのリソースに応じて BGM を同期（<see cref="BattleStatusUI.UpdateStatus"/> から呼ぶ）。</summary>
    public void SyncFromPlayer(PlayerStatus player)
    {
        if (_source == null || player == null) return;

        bool want = DisadvantageRules.IsDisadvantaged(player) && !player.hasUsedManifestationSkill;

        if (!_lastDisadvantageWant.HasValue)
        {
            _lastDisadvantageWant = want;
            if (want)
                SafeStartFade(want);
            return;
        }

        if (_lastDisadvantageWant.Value == want)
            return;

        _lastDisadvantageWant = want;
        SafeStartFade(want);
    }

    private void SafeStartFade(bool toDisadvantage)
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(CoFadeToMode(toDisadvantage));
    }

    private IEnumerator CoFadeToMode(bool toDisadvantage)
    {
        float v0 = _targetVolume;
        float outDur = Mathf.Max(0.05f, fadeOutSeconds);
        float t = 0f;
        Image bg = battleBackgroundImage;
        bool useBg = bg != null;

        while (t < outDur)
        {
            t += Time.unscaledDeltaTime;
            float u = t / outDur;
            _source.volume = Mathf.Lerp(v0, 0f, u);
            if (useBg)
                SetBackgroundAlpha(Mathf.Lerp(_backgroundBaseColor.a, 0f, u));
            yield return null;
        }

        _source.Stop();

        if (toDisadvantage)
        {
            if (disadvantageClip == null)
            {
                Debug.LogWarning("[BattleBgmController] disadvantageClip is not assigned");
                _source.volume = v0;
                if (useBg)
                    ApplyBackgroundFullAlpha();
                _fadeCoroutine = null;
                yield break;
            }

            _source.clip = disadvantageClip;

            if (useBg)
            {
                if (_disadvantageBackgroundSprite == null && !string.IsNullOrEmpty(disadvantageBackgroundAddress))
                    yield return LoadSpriteToField(disadvantageBackgroundAddress, s => _disadvantageBackgroundSprite = s);
                bg.sprite = _disadvantageBackgroundSprite != null ? _disadvantageBackgroundSprite : bg.sprite;
            }
        }
        else
        {
            if (_baselineNormalClip == null)
            {
                Debug.LogWarning("[BattleBgmController] baseline playlist clip missing; cannot restore normal BGM");
                _source.volume = v0;
                if (useBg)
                    ApplyBackgroundFullAlpha();
                _fadeCoroutine = null;
                yield break;
            }

            _source.clip = _baselineNormalClip;

            if (useBg)
                bg.sprite = _baselineBackgroundSprite;
        }

        _source.loop = true;
        _source.volume = 0f;
        _source.Play();

        float inDur = Mathf.Max(0.05f, fadeInSeconds);
        t = 0f;
        while (t < inDur)
        {
            t += Time.unscaledDeltaTime;
            float u = t / inDur;
            _source.volume = Mathf.Lerp(0f, v0, u);
            if (useBg)
                SetBackgroundAlpha(Mathf.Lerp(0f, _backgroundBaseColor.a, u));
            yield return null;
        }

        _source.volume = v0;
        if (useBg)
            ApplyBackgroundFullAlpha();

        _fadeCoroutine = null;
    }

    private void SetBackgroundAlpha(float a)
    {
        if (battleBackgroundImage == null) return;
        var c = _backgroundBaseColor;
        c.a = a;
        battleBackgroundImage.color = c;
    }

    private void ApplyBackgroundFullAlpha()
    {
        if (battleBackgroundImage == null) return;
        battleBackgroundImage.color = _backgroundBaseColor;
    }

    // ===== ArchMagic background override =====
    private Sprite _archMagicOverrideBaselineSprite;
    private bool _archMagicOverrideActive;

    public void SetArchMagicBackgroundOverride(Sprite overrideSprite)
    {
        if (battleBackgroundImage == null || overrideSprite == null) return;
        if (!_archMagicOverrideActive)
        {
            _archMagicOverrideBaselineSprite = battleBackgroundImage.sprite;
            _archMagicOverrideActive = true;
        }
        battleBackgroundImage.sprite = overrideSprite;
    }

    public void ClearArchMagicBackgroundOverride()
    {
        if (!_archMagicOverrideActive) return;
        if (battleBackgroundImage != null)
        {
            battleBackgroundImage.sprite = _archMagicOverrideBaselineSprite;
            battleBackgroundImage.color = _backgroundBaseColor;
        }
        _archMagicOverrideActive = false;
        _archMagicOverrideBaselineSprite = null;
    }

    public async Task CrossfadeToArchMagicBackgroundAsync(Sprite overrideSprite, int durationMs, CancellationToken ct)
    {
        if (battleBackgroundImage == null || overrideSprite == null) return;

        if (!_archMagicOverrideActive)
        {
            _archMagicOverrideBaselineSprite = battleBackgroundImage.sprite;
            _archMagicOverrideActive = true;
        }

        var img = battleBackgroundImage;
        Color baseCol = _backgroundBaseColor;
        int halfMs = Mathf.Max(1, durationMs / 2);
        const int steps = 26;

        for (int i = 1; i <= steps; i++)
        {
            if (ct.IsCancellationRequested) return;
            float u = i / (float)steps;
            var c = baseCol;
            c.a = baseCol.a * (1f - u);
            img.color = c;
            await Task.Delay(halfMs / steps, ct);
        }

        img.sprite = overrideSprite;

        for (int i = 1; i <= steps; i++)
        {
            if (ct.IsCancellationRequested) return;
            float u = i / (float)steps;
            var c = baseCol;
            c.a = baseCol.a * u;
            img.color = c;
            await Task.Delay(halfMs / steps, ct);
        }

        img.color = baseCol;
    }

    public async Task CrossfadeFromArchMagicBackgroundAsync(int durationMs, CancellationToken ct)
    {
        if (battleBackgroundImage == null || !_archMagicOverrideActive)
        {
            ClearArchMagicBackgroundOverride();
            return;
        }

        var img = battleBackgroundImage;
        Sprite targetSprite = _archMagicOverrideBaselineSprite;
        Color baseCol = _backgroundBaseColor;
        int halfMs = Mathf.Max(1, durationMs / 2);
        const int steps = 26;

        for (int i = 1; i <= steps; i++)
        {
            if (ct.IsCancellationRequested) return;
            float u = i / (float)steps;
            var c = baseCol;
            c.a = baseCol.a * (1f - u);
            img.color = c;
            await Task.Delay(halfMs / steps, ct);
        }

        img.sprite = targetSprite;

        for (int i = 1; i <= steps; i++)
        {
            if (ct.IsCancellationRequested) return;
            float u = i / (float)steps;
            var c = baseCol;
            c.a = baseCol.a * u;
            img.color = c;
            await Task.Delay(halfMs / steps, ct);
        }

        img.color = baseCol;
        _archMagicOverrideActive = false;
        _archMagicOverrideBaselineSprite = null;
    }

    public bool IsArchMagicBackgroundOverrideActive => _archMagicOverrideActive;

    public void FadeOutBattleBgmAndStop(float durationSeconds)
    {
        _ = FadeOutBattleBgmAndStopAsync(durationSeconds);
    }

    public async Task FadeOutBattleBgmAndStopAsync(float durationSeconds)
    {
        if (_source == null) return;

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        _fadeOutCts?.Cancel();
        _fadeOutCts?.Dispose();
        _fadeOutCts = new CancellationTokenSource();
        var ct = _fadeOutCts.Token;

        float v0 = _source.volume;
        float d = Mathf.Max(0.05f, durationSeconds);
        float t = 0f;

        try
        {
            while (t < d)
            {
                ct.ThrowIfCancellationRequested();
                t += Time.unscaledDeltaTime;
                if (_source != null)
                    _source.volume = Mathf.Lerp(v0, 0f, t / d);
                await Task.Yield();
            }

            if (_source != null)
            {
                _source.Stop();
                _source.volume = v0;
            }
        }
        catch (OperationCanceledException)
        {
            // superseded by another fade
        }
    }

    private IEnumerator LoadSpriteToField(string address, Action<Sprite> assign)
    {
        if (string.IsNullOrEmpty(address))
            yield break;

        AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(address);
        yield return handle;
        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            assign?.Invoke(handle.Result);
        else
            Debug.LogWarning($"[BattleBgmController] Failed to load disadvantage background: {address}");
    }
}
