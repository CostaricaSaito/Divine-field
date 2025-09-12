using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    public TMP_Text text;
    public float floatSpeed = 30f;
    public float fadeDuration = 0.6f;
    private float timer = 0f;
    private CanvasGroup canvasGroup;

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Setup(string message, Color color)
    {
        text.text = message;
        text.color = color;
    }

    void Update()
    {
        timer += Time.deltaTime;

        // 浮かぶ
        transform.Translate(Vector3.up * floatSpeed * Time.deltaTime);

        // フェードアウト
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
        }

        if (timer >= fadeDuration)
        {
            Destroy(gameObject);
        }
    }
}