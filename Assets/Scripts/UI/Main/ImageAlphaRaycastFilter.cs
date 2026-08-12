using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Excludes transparent pixels of a UI <see cref="Image"/> from raycasts.
/// Attach to irregular-shaped menu buttons so clicks on empty rect areas are ignored.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class ImageAlphaRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
{
    [SerializeField, Range(0f, 1f)]
    private float alphaThreshold = 0.1f;

    [SerializeField]
    [Tooltip("When the sprite texture is not Read/Write enabled, use import-time physics shape polygons.")]
    private bool useSpritePhysicsShapeFallback = true;

    private Image _image;
    private RectTransform _rectTransform;
    private readonly List<Vector2> _physicsShape = new List<Vector2>(64);

    void Awake()
    {
        CacheRefs();
    }

    void OnValidate()
    {
        CacheRefs();
        alphaThreshold = Mathf.Clamp01(alphaThreshold);
    }

    void CacheRefs()
    {
        if (_image == null)
            _image = GetComponent<Image>();
        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!isActiveAndEnabled || _image == null || !_image.raycastTarget)
            return true;

        var sprite = _image.sprite;
        if (sprite == null)
            return true;

        CacheRefs();
        if (_rectTransform == null)
            return true;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform, screenPoint, eventCamera, out var localPoint))
            return false;

        var rect = _image.GetPixelAdjustedRect();
        if (!rect.Contains(localPoint))
            return false;

        var texture = sprite.texture;
        if (texture != null && texture.isReadable)
            return SampleReadableAlpha(sprite, rect, localPoint);

        if (useSpritePhysicsShapeFallback)
            return HitSpritePhysicsShape(sprite, rect, localPoint);

#if UNITY_EDITOR
        Debug.LogWarning(
            $"[{nameof(ImageAlphaRaycastFilter)}] {name}: sprite texture is not readable and has no physics shape. " +
            "Enable Read/Write on the texture import settings, or keep Generate Physics Shape enabled.",
            this);
#endif
        return true;
    }

    bool SampleReadableAlpha(Sprite sprite, Rect rect, Vector2 localPoint)
    {
        if (!TryMapLocalPointToTexturePixel(sprite, rect, localPoint, out var texX, out var texY))
            return false;

        try
        {
            return sprite.texture.GetPixel(texX, texY).a >= alphaThreshold;
        }
        catch
        {
            return false;
        }
    }

    bool HitSpritePhysicsShape(Sprite sprite, Rect rect, Vector2 localPoint)
    {
        if (!TryMapLocalPointToSpriteSpace(sprite, rect, localPoint, out var spriteLocal))
            return false;

        var shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount <= 0)
            return false;

        for (var i = 0; i < shapeCount; i++)
        {
            _physicsShape.Clear();
            sprite.GetPhysicsShape(i, _physicsShape);
            if (_physicsShape.Count >= 3 && IsPointInPolygon(spriteLocal, _physicsShape))
                return true;
        }

        return false;
    }

    static bool TryMapLocalPointToSpriteSpace(Sprite sprite, Rect rect, Vector2 localPoint, out Vector2 spriteLocal)
    {
        spriteLocal = default;
        if (rect.width <= 0f || rect.height <= 0f)
            return false;

        var u = (localPoint.x - rect.xMin) / rect.width;
        var v = (localPoint.y - rect.yMin) / rect.height;

        var spriteRect = sprite.rect;
        var ppu = sprite.pixelsPerUnit;

        var texXUnits = (spriteRect.x + u * spriteRect.width) / ppu;
        var texYUnits = (spriteRect.y + v * spriteRect.height) / ppu;

        spriteLocal = new Vector2(
            texXUnits - sprite.pivot.x / ppu,
            texYUnits - sprite.pivot.y / ppu);
        return true;
    }

    static bool TryMapLocalPointToTexturePixel(
        Sprite sprite,
        Rect rect,
        Vector2 localPoint,
        out int texX,
        out int texY)
    {
        texX = texY = 0;
        if (!TryMapLocalPointToSpriteSpace(sprite, rect, localPoint, out var spriteLocal))
            return false;

        var ppu = sprite.pixelsPerUnit;
        var px = spriteLocal.x * ppu + sprite.pivot.x;
        var py = spriteLocal.y * ppu + sprite.pivot.y;

        texX = Mathf.FloorToInt(px);
        texY = Mathf.FloorToInt(py);

        var texture = sprite.texture;
        return texture != null
            && texX >= 0
            && texY >= 0
            && texX < texture.width
            && texY < texture.height;
    }

    static bool IsPointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            if (((pi.y > point.y) != (pj.y > point.y))
                && (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y + float.Epsilon) + pi.x))
                inside = !inside;
        }

        return inside;
    }
}
