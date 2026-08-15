using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerSummon layout: back RainbowFrame (93x93) + front icon (90x90).
/// The 3px ring is the frame visible around the opaque icon.
/// </summary>
public static class SummonRainbowFramePresenter
{
    public const string FrameChildName = "RainbowFrame";
    public const string IconFrontChildName = "SummonIconFront";
    public const float ButtonSize = 90f;
    public const float FrameSize = 93f;
    public const float RainbowIntensity = 1f;

    public static void EnsureLayers(Image summonRootImage)
    {
        if (summonRootImage == null) return;

        var root = summonRootImage.rectTransform;
        EnsureFrameLayer(root);
        var front = EnsureIconFrontLayer(root, summonRootImage);

        var button = summonRootImage.GetComponent<Button>();
        if (button != null && front != null)
            button.targetGraphic = front;

        summonRootImage.enabled = false;
        summonRootImage.raycastTarget = false;
    }

    public static void SyncIconSprite(Image summonRootImage, Sprite sprite)
    {
        if (summonRootImage == null) return;
        EnsureLayers(summonRootImage);

        var front = summonRootImage.transform.Find(IconFrontChildName)?.GetComponent<Image>();
        if (front != null)
            front.sprite = sprite;
    }

    public static void ApplyIconTint(Image summonRootImage, Color tint)
    {
        if (summonRootImage == null) return;
        EnsureLayers(summonRootImage);

        var front = summonRootImage.transform.Find(IconFrontChildName)?.GetComponent<Image>();
        if (front != null)
            front.color = tint;
    }

    public static void SetRainbowFrameActive(Image summonRootImage, bool active)
    {
        if (summonRootImage == null) return;
        EnsureLayers(summonRootImage);

        Transform frame = summonRootImage.transform.Find(FrameChildName);
        if (frame == null) return;

        frame.gameObject.SetActive(active);
        if (!active)
        {
            var rainbow = frame.GetComponent<RainbowOutline>();
            if (rainbow != null)
                Object.Destroy(rainbow);
            return;
        }

        var image = frame.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = RainbowOutline.GetFallbackWhiteSprite();
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
        }

        var animator = frame.GetComponent<RainbowOutline>();
        if (animator == null)
            animator = frame.gameObject.AddComponent<RainbowOutline>();
        animator.intensity = RainbowIntensity;
    }

    private static void EnsureFrameLayer(RectTransform root)
    {
        Transform existing = root.Find(FrameChildName);
        if (existing == null)
            existing = CreateLayerObject(root, FrameChildName, FrameSize);

        ConfigureCenteredSquare(existing as RectTransform, FrameSize);
        existing.SetSiblingIndex(0);

        ClearLegacyRainbowOverlay(root);
    }

    private static Image EnsureIconFrontLayer(RectTransform root, Image sourceImage)
    {
        Transform existing = root.Find(IconFrontChildName);
        if (existing == null)
        {
            existing = CreateLayerObject(root, IconFrontChildName, ButtonSize);
            var created = existing.GetComponent<Image>();
            created.sprite = sourceImage.sprite;
            created.color = sourceImage.color;
            created.preserveAspect = sourceImage.preserveAspect;
            created.raycastTarget = true;
        }

        ConfigureCenteredSquare(existing as RectTransform, ButtonSize);
        existing.SetSiblingIndex(1);

        return existing.GetComponent<Image>();
    }

    private static Transform CreateLayerObject(RectTransform root, string name, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        ConfigureCenteredSquare(rt, size);

        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return rt;
    }

    private static void ConfigureCenteredSquare(RectTransform rt, float size)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.one;
    }

    private static void ClearLegacyRainbowOverlay(RectTransform root)
    {
        Transform legacy = root.Find("RainbowOverlay");
        if (legacy == null) return;

        var legacyRainbow = legacy.GetComponent<RainbowOutline>();
        if (legacyRainbow != null)
            Object.Destroy(legacyRainbow);

        var legacyImage = legacy.GetComponent<Image>();
        if (legacyImage != null)
            legacyImage.color = new Color(1f, 1f, 1f, 0f);

        legacy.gameObject.SetActive(false);
    }
}
