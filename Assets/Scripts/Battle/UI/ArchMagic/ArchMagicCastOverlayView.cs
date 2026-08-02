using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 大魔法詠唱カウントダウン UI（ArchMagicCastOverlay.prefab）の参照と簡易更新。
/// </summary>
public class ArchMagicCastOverlayView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image dimImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image remainingBackdrop;
    [SerializeField] private TMP_Text remainingText;
    [SerializeField] private TMP_Text barrierText;

    public CanvasGroup CanvasGroup => canvasGroup;
    public Image IconImage => iconImage;
    public TMP_Text RemainingText => remainingText;
    public TMP_Text BarrierText => barrierText;
    public Image RemainingBackdrop => remainingBackdrop;

    private void Awake() => CacheRefs();

    public void CacheRefs()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (dimImage == null)
            dimImage = FindComponent<Image>("ArchMagicDim");

        if (iconImage == null)
            iconImage = FindComponent<Image>("Icon");

        if (remainingBackdrop == null)
            remainingBackdrop = FindComponent<Image>("RemainingBackdrop");

        if (remainingText == null)
            remainingText = FindComponent<TMP_Text>("Remaining");

        if (barrierText == null)
            barrierText = FindComponent<TMP_Text>("BarrierRemaining");
    }

    public void ApplyCountdownFont(TMP_FontAsset font)
    {
        if (font == null) return;
        if (remainingText != null) remainingText.font = font;
        if (barrierText != null) barrierText.font = font;
    }

    public void SetBackdropAlpha(float alpha)
    {
        if (remainingBackdrop == null) return;
        var c = remainingBackdrop.color;
        c.a = Mathf.Clamp01(alpha);
        remainingBackdrop.color = c;
    }

    public void ApplyDefaultTextStyles()
    {
        if (remainingText != null)
        {
            remainingText.alignment = TextAlignmentOptions.Center;
            remainingText.fontSize = 58f;
            remainingText.color = Color.white;
            remainingText.outlineColor = new Color(0.85f, 0.12f, 0.12f, 1f);
            remainingText.outlineWidth = 0.22f;
        }

        if (barrierText != null)
        {
            barrierText.alignment = TextAlignmentOptions.Center;
            barrierText.fontSize = 42f;
            barrierText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            barrierText.outlineColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            barrierText.outlineWidth = 0.18f;
        }
    }

    public void SetIconSprite(Sprite sprite)
    {
        if (iconImage != null)
            iconImage.sprite = sprite;
    }

    public void SetRemainingRichText(string richText)
    {
        if (remainingText == null) return;
        remainingText.richText = true;
        remainingText.text = richText ?? string.Empty;
    }

    public void SetBarrierText(string text, bool visible)
    {
        if (barrierText == null) return;
        barrierText.gameObject.SetActive(visible);
        if (visible)
            barrierText.text = text ?? string.Empty;
    }

    private T FindComponent<T>(string childName) where T : Component
    {
        var t = FindChild(transform, childName);
        return t != null ? t.GetComponent<T>() : null;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null) return null;
        var direct = root.Find(childName);
        if (direct != null) return direct;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChild(root.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }
}
