using UnityEngine;

public class SEPlayer : MonoBehaviour
{
    public static SEPlayer I;
    private AudioSource audioSource;

    // 自動生成済みかどうかのフラグ
    private static bool initialized = false;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;

        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        initialized = true; // 最後に！
    }

    /// <summary>
    /// どのSceneを再生しても、自動でSEPlayerを生成
    /// Resources/SEPlayer.prefab を読み込み
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (initialized) return;

        var prefab = Resources.Load<GameObject>("Prefab/SEPlayer"); // Resources/SEPlayer.prefab
        if (prefab != null)
        {
            GameObject.Instantiate(prefab);
        }
        else
        {
            Debug.LogWarning("SEPlayer prefab が Resources フォルダに見つかりませんでした。");
        }
    }

    public void Play(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning("再生しようとしたAudioClipがnullです");
        }
    }

    /// <summary>
    /// SEを強制的に切り替えて再生（上書き用）
    /// </summary>
    public void PlayReplace(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    /// <summary>
    /// 再生を止める
    /// </summary>
    public void Stop()
    {
        audioSource.Stop();
    }
}