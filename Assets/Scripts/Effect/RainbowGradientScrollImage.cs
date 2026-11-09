using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Diagonal rainbow fill on a UI <see cref="Image"/> (bottom-left to top-right), hue scrolls over time.
/// </summary>
[DisallowMultipleComponent]
public sealed class RainbowGradientScrollImage : MonoBehaviour
{
    private const int TexWidth = 256;
    private const int TexHeight = 128;

    [SerializeField] private float scrollSpeed = 0.42f;
    [SerializeField] [Range(0.4f, 1f)] private float saturation = 0.72f;
    [SerializeField] [Range(0.5f, 1f)] private float brightness = 1f;
    [SerializeField] [Range(0.08f, 0.45f)] private float cornerRadiusNormalized = 0.22f;

    private Image _image;
    private Texture2D _texture;
    private Sprite _sprite;
    private Color32[] _pixels;
    private byte[] _maskAlpha;
    private bool _active;

    public void Begin(Image image)
    {
        if (image == null) return;
        _image = image;
        EnsureResources();
        _image.sprite = _sprite;
        _image.type = Image.Type.Simple;
        _image.preserveAspect = false;
        _image.color = Color.white;
        _active = true;
        ApplyFrame(0f);
    }

    private void Awake()
    {
        if (_image == null)
            _image = GetComponent<Image>();
    }

    private void Update()
    {
        if (!_active || _texture == null) return;
        float phase = Time.unscaledTime * scrollSpeed;
        ApplyFrame(phase);
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private void EnsureResources()
    {
        if (_texture != null) return;

        _texture = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        _pixels = new Color32[TexWidth * TexHeight];
        _maskAlpha = BuildRoundedRectMask(TexWidth, TexHeight, cornerRadiusNormalized);
        _sprite = Sprite.Create(
            _texture,
            new Rect(0f, 0f, TexWidth, TexHeight),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private void ApplyFrame(float phase)
    {
        if (_texture == null || _pixels == null || _maskAlpha == null) return;

        int w = TexWidth;
        int h = TexHeight;
        float invW = w <= 1 ? 0f : 1f / (w - 1);
        float invH = h <= 1 ? 0f : 1f / (h - 1);

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            float fy = y * invH;
            for (int x = 0; x < w; x++)
            {
                byte a = _maskAlpha[row + x];
                if (a == 0)
                {
                    _pixels[row + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float fx = x * invW;
                float t = (fx + fy) * 0.5f;
                float hue = Mathf.Repeat(t + phase, 1f);
                Color rgb = Color.HSVToRGB(hue, saturation, brightness);
                _pixels[row + x] = new Color32(
                    (byte)(rgb.r * 255f),
                    (byte)(rgb.g * 255f),
                    (byte)(rgb.b * 255f),
                    a);
            }
        }

        _texture.SetPixels32(_pixels);
        _texture.Apply(false);
    }

    private static byte[] BuildRoundedRectMask(int width, int height, float radiusNorm)
    {
        var mask = new byte[width * height];
        float halfW = (width - 1) * 0.5f;
        float halfH = (height - 1) * 0.5f;
        float radius = Mathf.Min(halfW, halfH) * Mathf.Clamp01(radiusNorm);
        float radiusSq = radius * radius;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x - halfW);
                float dy = Mathf.Abs(y - halfH);
                float ix = Mathf.Max(dx - (halfW - radius), 0f);
                float iy = Mathf.Max(dy - (halfH - radius), 0f);
                bool inside = ix * ix + iy * iy <= radiusSq
                              && dx <= halfW && dy <= halfH;
                mask[y * width + x] = inside ? (byte)255 : (byte)0;
            }
        }

        return mask;
    }

    private void ReleaseResources()
    {
        _active = false;
        if (_sprite != null)
        {
            Destroy(_sprite);
            _sprite = null;
        }

        if (_texture != null)
        {
            Destroy(_texture);
            _texture = null;
        }

        _pixels = null;
        _maskAlpha = null;
    }
}
