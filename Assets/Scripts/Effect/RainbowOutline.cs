using UnityEngine;
using UnityEngine.UI;

public class RainbowOutline : MonoBehaviour
{
    public Image targetImage;
    public float speed = 1f;
    public float intensity = 0.3f; // “øF‚Ìå’£“x

    void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
    }

    void Update()
    {
        if (targetImage == null) return;

        float h = Mathf.Repeat(Time.time * speed, 1f);
        Color rainbow = Color.HSVToRGB(h, 1f, 1f);
        rainbow.a = intensity;

        targetImage.color = rainbow;
    }
}