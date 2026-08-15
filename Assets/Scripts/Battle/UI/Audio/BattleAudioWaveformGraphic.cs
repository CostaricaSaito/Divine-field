using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI oscilloscope line with horizontally scrolling rainbow vertex colors.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BattleAudioWaveformGraphic : MaskableGraphic
{
    [SerializeField] private float lineThickness = 4f;
    [SerializeField] private float saturation = 0.82f;
    [SerializeField] private float value = 1f;
    [SerializeField] private float alpha = 0.38f;

    private float[] _samples;
    private float _intensity;
    private float _hueOffset;

    public void ApplyAppearance(
        float thickness,
        float lineAlpha,
        float hsvSaturation,
        float hsvValue)
    {
        lineThickness = thickness;
        alpha = lineAlpha;
        saturation = hsvSaturation;
        value = hsvValue;
    }

    public void SetWaveform(float[] samples, float intensity, float hueOffset)
    {
        _samples = samples;
        _intensity = Mathf.Clamp01(intensity);
        _hueOffset = hueOffset - Mathf.Floor(hueOffset);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (_samples == null || _samples.Length < 2 || _intensity <= 0.001f)
            return;

        var rect = rectTransform.rect;
        float width = rect.width;
        float height = rect.height;
        if (width <= 1f || height <= 1f)
            return;

        float halfHeight = height * 0.5f;
        float thickness = lineThickness;
        int count = _samples.Length;
        float left = -width * 0.5f;

        Vector2 prev = SampleToLocal(0, count, width, halfHeight, left);
        Color prevColor = SampleColor(0, count);

        for (int i = 1; i < count; i++)
        {
            Vector2 current = SampleToLocal(i, count, width, halfHeight, left);
            Color currentColor = SampleColor(i, count);
            AddLineQuad(vh, prev, current, prevColor, currentColor, thickness);
            prev = current;
            prevColor = currentColor;
        }
    }

    private Vector2 SampleToLocal(int index, int count, float width, float halfHeight, float left)
    {
        float t = index / (float)(count - 1);
        float x = left + t * width;
        float y = Mathf.Clamp(_samples[index], -1f, 1f) * halfHeight * _intensity;
        return new Vector2(x, y);
    }

    private Color SampleColor(int index, int count)
    {
        float t = index / (float)Mathf.Max(1, count - 1);
        float hue = t + _hueOffset;
        hue -= Mathf.Floor(hue);
        var color = Color.HSVToRGB(hue, saturation, value);
        color.a = alpha * _intensity;
        return color;
    }

    private static void AddLineQuad(
        VertexHelper vh,
        Vector2 p0,
        Vector2 p1,
        Color c0,
        Color c1,
        float thickness)
    {
        Vector2 delta = p1 - p0;
        if (delta.sqrMagnitude < 0.0001f)
            return;

        Vector2 dir = delta.normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x) * thickness * 0.5f;

        int index = vh.currentVertCount;
        vh.AddVert(p0 - normal, c0, Vector2.zero);
        vh.AddVert(p0 + normal, c0, Vector2.zero);
        vh.AddVert(p1 + normal, c1, Vector2.zero);
        vh.AddVert(p1 - normal, c1, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}
