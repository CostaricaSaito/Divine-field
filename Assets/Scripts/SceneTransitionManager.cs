using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager I;

    [Header("フェード用UI")]
    public Image fadeImage;
    public float fadeDuration = 0.5f;

    void Awake()
    {
        Debug.Log("SceneTransitionManager Awake 開始");

        if (I != null && I != this)
        {
            Debug.Log("既にSceneTransitionManagerがあるため、自分は破棄されます");
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("SceneTransitionManager 登録完了");

        TryFindFadeImage(); // 初回取得
    }

    public void FadeToScene(string sceneName)
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("FadeToScene が呼ばれましたが fadeImage が null です。");
        }

        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        Debug.Log("フェードアウト開始");

        yield return StartCoroutine(Fade(1)); // フェードアウト

        Debug.Log("シーン切り替え中 → " + sceneName);

        var sePlayer = FindObjectOfType<AudioSource>();
        if (sePlayer != null && sePlayer.isPlaying)
        {
            sePlayer.Stop();
            Debug.Log("再生中のAudioSourceを停止しました");
        }

        SceneManager.LoadScene(sceneName);

        yield return new WaitForSecondsRealtime(0.1f); // または 0.2f

        if (fadeImage == null)
        {
            Transform fadeCanvas = GameObject.Find("FadeCanvas")?.transform;
            if (fadeCanvas != null)
                fadeImage = fadeCanvas.GetComponentInChildren<Image>();
        }

        if (fadeImage != null)
            fadeImage.color = new Color(0, 0, 0, 1f);

        TryFindFadeImage(); // 新シーンでもFadeCanvasを再検索（失っていた場合の保険）

        Debug.Log("フェードイン開始");
        yield return new WaitForSecondsRealtime(0.1f);
        yield return StartCoroutine(Fade(0)); // フェードイン
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("fadeImage が null のため、フェード処理スキップ");
            yield break;
        }

        fadeImage.raycastTarget = true;

        float startAlpha = fadeImage.color.a;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / fadeDuration);
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            fadeImage.color = new Color(0, 0, 0, alpha);
            yield return null;
        }

        fadeImage.raycastTarget = targetAlpha != 0;
    }

    private void TryFindFadeImage()
    {
        if (fadeImage != null) return;

        var fadeCanvas = GameObject.Find("FadeCanvas");
        if (fadeCanvas != null)
        {
            fadeImage = fadeCanvas.GetComponentInChildren<Image>();
            if (fadeImage != null)
            {
                Debug.Log("fadeImage を再取得しました");
                return;
            }
        }

        Debug.LogWarning("fadeImage が見つかりませんでした。FadeCanvasがシーンに存在するか確認してください。");
    }
}