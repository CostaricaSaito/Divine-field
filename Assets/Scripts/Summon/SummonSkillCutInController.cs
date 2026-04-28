using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SummonSkillCutInController : MonoBehaviour
{
    public static SummonSkillCutInController I;

    [Header("UIŽQÆ")]
    public RectTransform imageRectTransform; // ƒXƒ‰ƒCƒh—p
    public CanvasGroup canvasGroup;         // ƒtƒF[ƒhƒCƒ“EƒAƒEƒg—p
    public Image backgroundImage;
    public TMP_Text skillText;
    public Image whiteFlashImage;            // ”’ƒtƒ‰ƒbƒVƒ…—p Imagei‘S‰æ–ÊE”’EÅ‘O–Êj

    [Header("‰‰oÝ’è")]
    public float slideDuration = 0.5f;     // ƒXƒ‰ƒCƒh‚É‚©‚©‚éŽžŠÔ
    public float totalDuration = 1.0f;     // ‰‰o‘S‘Ì‚ÌŽžŠÔi1•b„§j
    public AudioClip cutInSE;
    private AudioSource audioSource;

    private RectTransform rectTransform;


    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        canvasGroup.alpha = 0f;  // Å‰‚Í“§–¾‚É
        whiteFlashImage.color = new Color(1, 1, 1, 0f); // Å‰‚Í“§–¾
        // SetActive(false) ‚ÍíœI
        audioSource = GetComponent<AudioSource>();
        rectTransform = GetComponent<RectTransform>(); // Ž©•ªŽ©g

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
        Vector2 originalPos = target.anchoredPosition;
        float signX = Random.value < 0.5f ? -1f : 1f;
        float signY = Random.value < 0.5f ? -1f : 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float envelope = Mathf.Sin(Mathf.PI * t);
            target.anchoredPosition = originalPos + new Vector2(
                signX * magnitude * envelope,
                signY * magnitude * envelope);

            yield return null;
        }

        target.anchoredPosition = originalPos;
    }

    private IEnumerator ShowCutIn(Sprite bg, string skillName)
    {
        // ‰ŠúÝ’è
        backgroundImage.sprite = bg;
        skillText.text = skillName;

        Vector2 startPos = new Vector2(800f, 500f);
        Vector2 centerPos = Vector2.zero;
        Vector2 driftPos = new Vector2(-10f, -15f); // ¶‚É­‚µƒYƒŒ‚éˆÊ’ui’²®‰Â”\j

        imageRectTransform.anchoredPosition = startPos;
        canvasGroup.alpha = 0f;

        // SEÄ¶
        if (cutInSE != null && audioSource != null)
            audioSource.PlayOneShot(cutInSE);

        // “¯Žž‚ÉÄ¶‚·‚éi”’ƒtƒ‰ƒbƒVƒ…‚ÆƒJƒbƒgƒCƒ“j
        StartCoroutine(FlashWhite(0.4f));
        yield return StartCoroutine(SlideInCutIn(0.5f, startPos, centerPos));

        StartCoroutine(ShakeUI(rectTransform, 1f, 10f));

        // uƒYƒYƒbcv‚Æ¶‚É—¬‚ê‚é‚æ‚¤‚É”÷’²®
        yield return StartCoroutine(SlideDrift(centerPos, driftPos, 2f));

        // •\Ž¦‚ð1•bƒL[ƒv
        yield return new WaitForSeconds(0.1f);

        // I—¹ˆ—
        canvasGroup.alpha = 0f;

        // ”’ƒtƒ‰ƒbƒVƒ…i”²‚¯j‚ð’Ç‰Á‚·‚é‚È‚ç‚±‚±‚Å‚à‚¤1‰ñ
        yield return StartCoroutine(FlashWhite(0.4f));


    }

    private IEnumerator FlashWhite(float duration)
    {
        float half = duration / 2f;
        float t = 0f;

        // ƒtƒF[ƒhƒCƒ“
        while (t < half)
        {
            whiteFlashImage.color = new Color(1, 1, 1, t / half);
            t += Time.deltaTime;
            yield return null;
        }

        // ƒtƒF[ƒhƒAƒEƒg
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