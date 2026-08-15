using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rainbow fill animation for UI Images (rare cards, summon rainbow frame).
/// </summary>
[RequireComponent(typeof(Image))]
public class RainbowOutline : MonoBehaviour
{
    private static Sprite _fallbackWhiteSprite;

    [SerializeField] private Image targetImage;
    [SerializeField] private float speed = 1f;
    [SerializeField] public float intensity = 0.3f;

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
    }

    public static Sprite GetFallbackWhiteSprite()
    {
        if (_fallbackWhiteSprite != null)
            return _fallbackWhiteSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);

        _fallbackWhiteSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            100f);
        return _fallbackWhiteSprite;
    }

    private void Update()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
        if (targetImage == null) return;

        float h = Mathf.Repeat(Time.unscaledTime * speed, 1f);
        Color rainbow = Color.HSVToRGB(h, 1f, 1f);
        rainbow.a = intensity;
        targetImage.color = rainbow;
    }
}
