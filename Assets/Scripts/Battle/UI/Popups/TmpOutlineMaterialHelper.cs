using TMPro;
using UnityEngine;

/// <summary>
/// TMP SDF outline via material instance (source: <see cref="TMP_FontAsset.material"/> only).
/// </summary>
public static class TmpOutlineMaterialHelper
{
    public const float DefaultBannerOutlineWidth = 0.24f;

    public static void ApplyOutlinedMaterialInstance(TMP_Text t, Color outlineColor, float outlineWidth)
    {
        if (t == null || t.font == null) return;

        var sharedMat = t.font.material;
        if (sharedMat == null) return;

        t.fontSharedMaterial = sharedMat;

        var mat = Object.Instantiate(sharedMat);
        t.fontMaterial = mat;

        if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
            mat.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
        if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);

        t.outlineWidth = outlineWidth;
        t.outlineColor = outlineColor;

        t.UpdateMeshPadding();
        t.ForceMeshUpdate(true);
    }

    public static float ResolveOutlineWidth(TMP_Text t, float defaultWidth = DefaultBannerOutlineWidth)
    {
        if (t == null) return defaultWidth;
        return t.outlineWidth >= 0.08f ? t.outlineWidth : defaultWidth;
    }

    /// <summary>
    /// Non-uniform scale X squashes outline in screen space; boost SDF width slightly when squeezed.
    /// </summary>
    public static float AdjustOutlineForHorizontalScale(float outlineWidth, float localScaleX)
    {
        if (localScaleX >= 0.999f) return outlineWidth;
        float inv = 1f / Mathf.Max(localScaleX, 0.35f);
        return outlineWidth * Mathf.Min(inv, 1.35f);
    }

    /// <summary>Layout before squeeze: create outline material. Call <see cref="RetuneOutlineAfterHorizontalLayout"/> after banner layout.</summary>
    public static void ApplyBannerOutlineBeforeLayout(
        TMP_Text t,
        Color outlineColor,
        float defaultWidth = DefaultBannerOutlineWidth)
    {
        if (t == null) return;
        float ow = ResolveOutlineWidth(t, defaultWidth);
        ApplyOutlinedMaterialInstance(t, outlineColor, ow);
    }

    /// <summary>After <see cref="MessagePopup.ApplyFixedBannerLayout"/>, widen outline in material if scale X &lt; 1.</summary>
    public static void RetuneOutlineAfterHorizontalLayout(TMP_Text t, Color outlineColor)
    {
        if (t == null) return;

        float baseOw = ResolveOutlineWidth(t);
        float sx = t.rectTransform != null ? t.rectTransform.localScale.x : 1f;
        float ow = AdjustOutlineForHorizontalScale(baseOw, sx);

        var mat = t.fontMaterial;
        if (mat != null)
        {
            if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
                mat.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
            if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, ow);
        }

        t.outlineWidth = ow;
        t.outlineColor = outlineColor;
        t.UpdateMeshPadding();
        t.ForceMeshUpdate(true);
    }
}
