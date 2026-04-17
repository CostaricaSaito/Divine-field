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
    private Dictionary<string, AudioClip> clipCache = new();

    private static bool bootstrapInitialized;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);

        if (seSource == null)
            seSource = gameObject.AddComponent<AudioSource>();
        seSource.playOnAwake = false;

        bootstrapInitialized = true;
    }

    /// <summary>
    /// どのシーンからでも <see cref="I"/> を用意する（Resources/Prefab/SEPlayer）。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (bootstrapInitialized)
            return;

        var prefab = Resources.Load<GameObject>("Prefab/SEPlayer");
        if (prefab != null)
            Instantiate(prefab);
        else
            Debug.LogWarning("[SoundEffectPlayer] Resources/Prefab/SEPlayer が見つかりません。");
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
        try
        {
            Addressables.LoadAssetAsync<AudioClip>(addressKey).Completed += handle =>
            {
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
        seSource.Stop();
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