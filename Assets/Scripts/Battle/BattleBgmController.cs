using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

/// <summary>
/// 劣勢時に BGM を <see cref="DisadvantageBgmAddress"/> へ、回復時に通常 BGM へフェード切替する。
/// 任意で <see cref="battleBackgroundImage"/> のスプライトも同じタイミングでフェード切替する。
/// シーンの BGM オブジェクトに付与するか、<see cref="BattleManager"/> から自動で付与される。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BattleBgmController : MonoBehaviour
{
    public static BattleBgmController Instance { get; private set; }

    [Tooltip("Addressables キー（未設定時は定数 DisadvantageBgmAddress）")]
    [SerializeField] private string disadvantageBgmAddress = "Assets/Music/Extinguish.mp3";

    [Tooltip("シーン開始時のクリップが無い場合のフォールバック")]
    [SerializeField] private string normalBgmAddress = "Assets/Music/Crystal brilliance.mp3";

    [SerializeField] private float fadeOutSeconds = 0.45f;
    [SerializeField] private float fadeInSeconds = 0.45f;

    [Header("劣勢時の背景（任意）")]
    [Tooltip("未設定時はシーン内の BackGroundImage を検索")]
    [SerializeField] private Image battleBackgroundImage;

    [Tooltip("Addressables の Sprite キー（2D/UI スプライトとしてインポート）")]
    [SerializeField] private string disadvantageBackgroundAddress = "Assets/Images/02_背景/劣勢.jpg";

    private AudioSource _source;
    private AudioClip _baselineNormalClip;
    private AudioClip _disadvantageClip;
    private float _targetVolume = 0.27f;
    private bool? _lastDisadvantageWant;
    private Coroutine _fadeCoroutine;
    private CancellationTokenSource _fadeOutCts;

    private Sprite _baselineBackgroundSprite;
    private Sprite _disadvantageBackgroundSprite;
    private Color _backgroundBaseColor = Color.white;

    public const string DefaultDisadvantageBgmAddress = "Assets/Music/Extinguish.mp3";
    public const string DefaultNormalBgmAddress = "Assets/Music/Crystal brilliance.mp3";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        _source = GetComponent<AudioSource>();
        if (string.IsNullOrEmpty(disadvantageBgmAddress))
            disadvantageBgmAddress = DefaultDisadvantageBgmAddress;
        if (string.IsNullOrEmpty(normalBgmAddress))
            normalBgmAddress = DefaultNormalBgmAddress;

        if (_source != null)
        {
            _baselineNormalClip = _source.clip;
            _targetVolume = _source.volume;
        }

        if (battleBackgroundImage == null)
        {
            var go = GameObject.Find("BackGroundImage");
            if (go != null)
                battleBackgroundImage = go.GetComponent<Image>();
        }

        if (battleBackgroundImage != null)
        {
            _baselineBackgroundSprite = battleBackgroundImage.sprite;
            _backgroundBaseColor = battleBackgroundImage.color;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
            if (_disadvantageClip == null)
                yield return LoadClipToField(disadvantageBgmAddress, c => _disadvantageClip = c);
            _source.clip = _disadvantageClip;

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
                yield return LoadClipToField(normalBgmAddress, c => _baselineNormalClip = c);
            _source.clip = _baselineNormalClip;

            if (useBg)
                bg.sprite = _baselineBackgroundSprite;
        }

        if (_source.clip == null)
        {
            Debug.LogWarning("[BattleBgmController] 切替先の AudioClip が取得できませんでした");
            _source.volume = v0;
            if (useBg)
                ApplyBackgroundFullAlpha();
            _fadeCoroutine = null;
            yield break;
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

    // ===== 大魔法（ArchMagic）用 背景差し替え =====
    private Sprite _archMagicOverrideBaselineSprite;
    private bool _archMagicOverrideActive;

    /// <summary>大魔法詠唱中の背景差し替え。復帰用に現在のスプライトを保存する。</summary>
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

    /// <summary>大魔法背景を解除して元のスプライトへ復帰する。</summary>
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

    /// <summary>大魔法背景をアルファでフェードしながら <paramref name="durationMs"/> かけて差し替える。</summary>
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

    /// <summary>
    /// 大魔法用背景を保存済みのベーススプライトへ <paramref name="durationMs"/> かけてフェード復帰し、オーバーライドを解除する。
    /// </summary>
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

    /// <summary>ゲーム終了時：現在のバトル BGM の音量を下げゼロにして停止する（リザルト用に呼ぶ）。</summary>
    public void FadeOutBattleBgmAndStop(float durationSeconds)
    {
        _ = FadeOutBattleBgmAndStopAsync(durationSeconds);
    }

    /// <summary>ゲーム終了時 BGM フェードアウト（async/await）。</summary>
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

    private IEnumerator LoadSpriteToField(string address, System.Action<Sprite> assign)
    {
        if (string.IsNullOrEmpty(address))
            yield break;

        AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(address);
        yield return handle;
        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            assign?.Invoke(handle.Result);
        else
            Debug.LogWarning($"[BattleBgmController] 背景スプライト読み込み失敗: {address}");
    }

    private IEnumerator LoadClipToField(string address, System.Action<AudioClip> assign)
    {
        if (string.IsNullOrEmpty(address))
            yield break;

        AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(address);
        yield return handle;
        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            assign?.Invoke(handle.Result);
        else
            Debug.LogWarning($"[BattleBgmController] BGM 読み込み失敗: {address}");
    }
}
