using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SummonSkillCutInController : MonoBehaviour
{
    public static SummonSkillCutInController I;

    [Header("UI参照")]
    public RectTransform imageRectTransform; // スライド用
    public CanvasGroup canvasGroup;         // フェードイン・アウト用
    public Image backgroundImage;
    public TMP_Text skillText;
    public Image whiteFlashImage;            // 白フラッシュ用 Image（全画面・白・最前面）

    [Header("演出設定")]
    public float slideDuration = 0.5f;     // スライドにかかる時間
    public float totalDuration = 1.0f;     // 演出全体の時間（1秒推奨）
    public AudioClip cutInSE;
    private AudioSource audioSource;

    private RectTransform rectTransform;


    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        canvasGroup.alpha = 0f;  // 最初は透明に
        whiteFlashImage.color = new Color(1, 1, 1, 0f); // 最初は透明
        // SetActive(false) は削除！
        audioSource = GetComponent<AudioSource>();
        rectTransform = GetComponent<RectTransform>(); // 自分自身

    }

    public void PlayCutIn(Sprite bg, string skillName)
    {
        StartCoroutine(ShowCutIn(bg, skillName));
    }

    private IEnumerator SlideInCutIn(float duration, Vector2 startPos, Vector2 endPos)
    {
        float t = 0f;
        while (t < duration)
        {
            float progress = Mathf.SmoothStep(0f, 1f, t / duration);
            imageRectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, progress);
            canvasGroup.alpha = progress;
            t += Time.deltaTime;
            yield return null;
        }

        imageRectTransform.anchoredPosition = endPos;
        canvasGroup.alpha = 1f;
    }

    private IEnumerator SlideDrift(Vector2 from, Vector2 to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            float progress = Mathf.SmoothStep(0f, 1f, t / duration);
            imageRectTransform.anchoredPosition = Vector2.Lerp(from, to, progress);
            t += Time.deltaTime;
            yield return null;
        }
        imageRectTransform.anchoredPosition = to;
    }

    private IEnumerator ShakeUI(RectTransform target, float duration, float magnitude)
    {
        //揺らすエフェクトくん
        Vector2 originalPos = target.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            target.anchoredPosition = originalPos + new Vector2(x, y);

            elapsed += Time.deltaTime;
            yield return null;
        }

        target.anchoredPosition = originalPos;
    }

    private IEnumerator ShowCutIn(Sprite bg, string skillName)
    {
        // 初期設定
        backgroundImage.sprite = bg;
        skillText.text = skillName;

        Vector2 startPos = new Vector2(800f, 500f);
        Vector2 centerPos = Vector2.zero;
        Vector2 driftPos = new Vector2(-10f, -15f); // 左に少しズレる位置（調整可能）

        imageRectTransform.anchoredPosition = startPos;
        canvasGroup.alpha = 0f;

        // SE再生
        if (cutInSE != null && audioSource != null)
            audioSource.PlayOneShot(cutInSE);

        // 同時に再生する（白フラッシュとカットイン）
        StartCoroutine(FlashWhite(0.4f));
        yield return StartCoroutine(SlideInCutIn(0.5f, startPos, centerPos));

        StartCoroutine(ShakeUI(rectTransform, 1f, 10f));

        // 「ズズッ…」と左に流れるように微調整
        yield return StartCoroutine(SlideDrift(centerPos, driftPos, 2f));

        // 表示を1秒キープ
        yield return new WaitForSeconds(0.1f);

        // 終了処理
        canvasGroup.alpha = 0f;

        // 白フラッシュ（抜け）を追加するならここでもう1回
        yield return StartCoroutine(FlashWhite(0.4f));


    }

    private IEnumerator FlashWhite(float duration)
    {
        float half = duration / 2f;
        float t = 0f;

        // フェードイン
        while (t < half)
        {
            whiteFlashImage.color = new Color(1, 1, 1, t / half);
            t += Time.deltaTime;
            yield return null;
        }

        // フェードアウト
        t = 0f;
        while (t < half)
        {
            whiteFlashImage.color = new Color(1, 1, 1, 1f - (t / half));
            t += Time.deltaTime;
            yield return null;
        }

        whiteFlashImage.color = new Color(1, 1, 1, 0f);
    }
}