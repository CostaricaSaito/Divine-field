using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;

/// <summary>
/// サウンドエフェクトの再生を管理するクラス
/// 
/// 【役割】
/// - 音響効果の再生
/// - 音声ファイルのキャッシュ管理
/// - Addressableアセットの読み込み
/// 
/// 【責任範囲】
/// - 音声ファイルの非同期読み込み
/// - 音声の再生制御
/// - メモリ効率的なキャッシュ管理
/// 
/// 【他のクラスとの関係】
/// - BattleManager: バトル音響の再生要求
/// - BattleController: バトル音響の再生要求
/// - 各種UI: ボタン音等の再生要求
/// </summary>
public class SoundEffectPlayer : MonoBehaviour
{
    public static SoundEffectPlayer I { get; private set; }

    [SerializeField] private AudioSource seSource;
    [Tooltip("ループ用 SE（電子ルーレット等）。未設定のとき起動時に子オブジェクトに生成する。")]
    [SerializeField] private AudioSource loopSeSource;
    private Dictionary<string, AudioClip> clipCache = new();
    private int _playGeneration;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;

        // DontDestroyOnLoad requires a root GameObject (Battle scene nests this under SystemObject).
        if (seSource != null && seSource.gameObject != gameObject)
            seSource.transform.SetParent(transform, true);
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        if (seSource == null)
            seSource = gameObject.AddComponent<AudioSource>();
        seSource.playOnAwake = false;
        if (loopSeSource == null)
        {
            var loopGo = new GameObject("SE_Looping");
            loopGo.transform.SetParent(transform, false);
            loopSeSource = loopGo.AddComponent<AudioSource>();
            loopSeSource.playOnAwake = false;
            loopSeSource.loop = true;
            loopSeSource.spatialBlend = 0f;
        }
    }

    /// <summary>
    /// アドレスキーを指定してSEを再生する
    /// 
    /// 【処理内容】
    /// 1. キャッシュから音声ファイルを検索
    /// 2. キャッシュにない場合は非同期読み込み
    /// 3. 読み込み完了後に再生
    /// 4. エラー時は警告ログを出力
    /// </summary>
    /// <param name="addressKey">音声ファイルのアドレスキー</param>
    public void Play(string addressKey)
    {
        if (string.IsNullOrEmpty(addressKey))
        {
            Debug.LogWarning("[SoundEffectPlayer] アドレスキーが空です");
            return;
        }

        // キャッシュから検索
        if (clipCache.TryGetValue(addressKey, out AudioClip cachedClip))
        {
            seSource.PlayOneShot(cachedClip);
            return;
        }

        // 非同期読み込み
        int generation = _playGeneration;
        try
        {
            Addressables.LoadAssetAsync<AudioClip>(addressKey).Completed += handle =>
            {
                if (generation != _playGeneration) return;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    AudioClip clip = handle.Result;
                    if (clip != null)
                    {
                        clipCache[addressKey] = clip;
                        seSource.PlayOneShot(clip);
                    }
                    else
                    {
                        Debug.LogWarning($"[SoundEffectPlayer] 音声ファイルがnullです: {addressKey}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[SoundEffectPlayer] SE読み込み失敗: {addressKey} - {handle.OperationException?.Message}");
                }
            };
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SoundEffectPlayer] SE読み込み例外: {addressKey} - {ex.Message}");
        }
    }

    /// <summary>Inspector などで参照した <see cref="AudioClip"/> をそのまま再生（ワンショット）。</summary>
    public void Play(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[SoundEffectPlayer] AudioClip が null です");
            return;
        }
        seSource.PlayOneShot(clip);
    }

    /// <summary>ループ用 SE。事前に <see cref="StartLoopingAsync"/> するか、キャッシュ済みキー用。</summary>
    public void StartLooping(string addressKey)
    {
        if (string.IsNullOrEmpty(addressKey) || loopSeSource == null) return;
        if (clipCache.TryGetValue(addressKey, out AudioClip clip) && clip != null)
        {
            loopSeSource.Stop();
            loopSeSource.clip = clip;
            loopSeSource.loop = true;
            loopSeSource.volume = 1f;
            loopSeSource.Play();
            return;
        }
        int generation = _playGeneration;
        Addressables.LoadAssetAsync<AudioClip>(addressKey).Completed += handle =>
        {
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null) return;
            clipCache[addressKey] = handle.Result;
            if (loopSeSource == null) return;
            if (generation != _playGeneration) return;
            loopSeSource.Stop();
            loopSeSource.clip = handle.Result;
            loopSeSource.loop = true;
            loopSeSource.volume = 1f;
            loopSeSource.Play();
        };
    }

    /// <summary>非同期でクリップ取得後、ループ再生を始める（カウント開始と同期させる）。</summary>
    public async Task StartLoopingAsync(string addressKey)
    {
        if (string.IsNullOrEmpty(addressKey) || loopSeSource == null) return;
        int generation = _playGeneration;
        if (!clipCache.TryGetValue(addressKey, out var clip) || clip == null)
        {
            var h = Addressables.LoadAssetAsync<AudioClip>(addressKey);
            var tcs = new TaskCompletionSource<AudioClip>();
            h.Completed += op =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
                {
                    clipCache[addressKey] = op.Result;
                    tcs.TrySetResult(op.Result);
                }
                else
                    tcs.TrySetResult(null);
            };
            clip = await tcs.Task;
        }
        if (clip == null || generation != _playGeneration || loopSeSource == null) return;
        loopSeSource.Stop();
        loopSeSource.clip = clip;
        loopSeSource.loop = true;
        loopSeSource.volume = 1f;
        loopSeSource.Play();
    }

    public void StopLooping()
    {
        if (loopSeSource == null) return;
        loopSeSource.Stop();
        loopSeSource.clip = null;
    }

    /// <summary>ループや上書き向け：再生を止めてから <paramref name="clip"/> を再生。</summary>
    public void PlayReplace(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[SoundEffectPlayer] AudioClip が null です");
            return;
        }
        seSource.Stop();
        seSource.clip = clip;
        seSource.Play();
    }

    /// <summary>ループ再生などを停止。</summary>
    public void Stop()
    {
        if (seSource != null)
            seSource.Stop();
    }

    /// <summary>
    /// 再生中の SE（ワンショット・ループ）をすべて停止する。
    /// 進行中の Addressable 読み込み完了後の再生も無効化する。
    /// </summary>
    public void StopAll()
    {
        _playGeneration++;

        if (seSource != null)
        {
            seSource.Stop();
            seSource.clip = null;
        }

        StopLooping();
    }

    /// <summary>
    /// 指定されたキーの音声ファイルをキャッシュから削除する
    /// </summary>
    /// <param name="addressKey">削除するアドレスキー</param>
    public void UnloadClip(string addressKey)
    {
        if (clipCache.ContainsKey(addressKey))
        {
            clipCache.Remove(addressKey);
            Debug.Log($"[SoundEffectPlayer] キャッシュから削除: {addressKey}");
        }
    }

    /// <summary>
    /// 全ての音声ファイルをキャッシュから削除する
    /// </summary>
    public void ClearCache()
    {
        clipCache.Clear();
        Debug.Log("[SoundEffectPlayer] キャッシュをクリアしました");
    }

    /// <summary>
    /// 現在キャッシュされている音声ファイル数を取得する
    /// </summary>
    /// <returns>キャッシュされている音声ファイル数</returns>
    public int GetCacheCount()
    {
        return clipCache.Count;
    }
}