using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager I;

    [Header("Fade UI")]
    public Image fadeImage;
    public float fadeDuration = 0.5f;

    [Header("Fade Canvas (Screen Space)")]
    [Tooltip("If empty, searches child FadeCanvas. Set when UI camera differs from MainCamera.")]
    public Canvas fadeCanvas;

    [Tooltip("Uses Camera.main when null.")]
    public Camera fadeCameraOverride;

    Canvas _fadeCanvasResolved;

    void Awake()
    {
        Debug.Log("SceneTransitionManager Awake start");

        if (I != null && I != this)
        {
            Debug.Log("SceneTransitionManager already exists; destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("SceneTransitionManager registered");

        TryFindFadeImage();

        ResolveFadeCanvas();
        BindFadeCanvasCamera();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        BindFadeCanvasCamera();
        TryFindFadeImage();
    }

    void ResolveFadeCanvas()
    {
        _fadeCanvasResolved = fadeCanvas != null ? fadeCanvas : GetComponentInChildren<Canvas>(true);
    }

    /// <summary>
    /// Overlay mode can misalign with camera-space UI; switch to Camera mode and bind the UI camera.
    /// </summary>
    void BindFadeCanvasCamera()
    {
        if (_fadeCanvasResolved == null)
            ResolveFadeCanvas();
        if (_fadeCanvasResolved == null)
            return;

        var cam = fadeCameraOverride != null ? fadeCameraOverride : Camera.main;
        if (cam == null)
            return;

        _fadeCanvasResolved.renderMode = RenderMode.ScreenSpaceCamera;
        _fadeCanvasResolved.worldCamera = cam;
    }

    public void FadeToScene(string sceneName)
    {
        if (fadeImage == null)
            Debug.LogWarning("FadeToScene called but fadeImage is null.");

        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        Debug.Log("Fade out start");

        yield return StartCoroutine(Fade(1));

        Debug.Log("Loading scene -> " + sceneName);

        // Stop all playing SE right before the new scene loads (BGM untouched).
        SoundEffectPlayer.I?.StopAll();

        SceneManager.LoadScene(sceneName);

        yield return new WaitForSecondsRealtime(0.1f);

        if (fadeImage == null)
        {
            Transform fadeCanvasTf = GameObject.Find("FadeCanvas")?.transform;
            if (fadeCanvasTf != null)
                fadeImage = fadeCanvasTf.GetComponentInChildren<Image>();
        }

        if (fadeImage != null)
            fadeImage.color = new Color(0, 0, 0, 1f);

        TryFindFadeImage();

        Debug.Log("Fade in start");
        yield return new WaitForSecondsRealtime(0.1f);
        yield return StartCoroutine(Fade(0));
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("fadeImage is null; skipping fade.");
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

        var fadeCanvasGo = GameObject.Find("FadeCanvas");
        if (fadeCanvasGo != null)
        {
            fadeImage = fadeCanvasGo.GetComponentInChildren<Image>();
            if (fadeImage != null)
            {
                Debug.Log("fadeImage reacquired");
                return;
            }
        }

        Debug.LogWarning("fadeImage not found. Confirm FadeCanvas exists in the scene.");
    }
}
