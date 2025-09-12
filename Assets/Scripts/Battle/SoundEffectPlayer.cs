using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;

public class SoundEffectPlayer : MonoBehaviour
{
    public static SoundEffectPlayer I { get; private set; }

    [SerializeField] private AudioSource seSource;
    private Dictionary<string, AudioClip> clipCache = new();

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;

        if (seSource == null)
            seSource = gameObject.AddComponent<AudioSource>();
    }

    /// <summary>
    /// アドレスキーを指定してSEを鳴らす
    /// </summary>
    public void Play(string addressKey)
    {
        if (string.IsNullOrEmpty(addressKey)) return;

        if (clipCache.TryGetValue(addressKey, out AudioClip cachedClip))
        {
            seSource.PlayOneShot(cachedClip);
            return;
        }

        // 初回ロードは非同期で
        Addressables.LoadAssetAsync<AudioClip>(addressKey).Completed += handle =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                AudioClip clip = handle.Result;
                clipCache[addressKey] = clip;
                seSource.PlayOneShot(clip);
            }
            else
            {
                Debug.LogWarning($"SEロード失敗: {addressKey}");
            }
        };
    }
}