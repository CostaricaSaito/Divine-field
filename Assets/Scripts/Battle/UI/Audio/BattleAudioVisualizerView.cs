using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hosts a centered oscilloscope-style waveform between battle background and UI.
/// </summary>
public sealed class BattleAudioVisualizerView : MonoBehaviour
{
    private BattleAudioWaveformGraphic _graphic;
    private RectTransform _waveformRect;
    private float[] _displaySamples;
    private float _displayIntensity;
    private int _pointCount;

    public void Build(RectTransform canvasRoot, int siblingIndex, int pointCount)
    {
        if (canvasRoot == null) return;

        _pointCount = Mathf.Max(16, pointCount);

        var layer = GetComponent<RectTransform>();
        if (layer == null)
            layer = gameObject.AddComponent<RectTransform>();

        layer.SetParent(canvasRoot, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        layer.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, canvasRoot.childCount - 1));

        DestroyExistingWaveform();

        var waveformGo = new GameObject(
            "Waveform",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(BattleAudioWaveformGraphic));
        waveformGo.transform.SetParent(layer, false);

        _waveformRect = waveformGo.GetComponent<RectTransform>();
        _waveformRect.anchorMin = new Vector2(0.5f, 0.5f);
        _waveformRect.anchorMax = new Vector2(0.5f, 0.5f);
        _waveformRect.pivot = new Vector2(0.5f, 0.5f);
        _waveformRect.anchoredPosition = Vector2.zero;

        _graphic = waveformGo.GetComponent<BattleAudioWaveformGraphic>();
        _graphic.raycastTarget = false;

        EnsureDisplayBuffer(_pointCount);
    }

    public void EnsureLayerOrder(RectTransform canvasRoot, int siblingIndex)
    {
        if (canvasRoot == null)
            return;

        var layer = transform as RectTransform;
        if (layer == null)
            return;

        int clamped = Mathf.Clamp(siblingIndex, 0, canvasRoot.childCount - 1);
        if (layer.GetSiblingIndex() != clamped)
            layer.SetSiblingIndex(clamped);
    }

    public void SyncAppearance(
        float width,
        float height,
        float thickness,
        float alpha,
        float saturation,
        float brightness,
        float verticalOffset,
        int pointCount)
    {
        if (_waveformRect != null)
        {
            _waveformRect.sizeDelta = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
            _waveformRect.anchoredPosition = new Vector2(0f, verticalOffset);
        }

        if (_graphic != null)
            _graphic.ApplyAppearance(thickness, alpha, saturation, brightness);

        pointCount = Mathf.Max(16, pointCount);
        if (_pointCount != pointCount)
        {
            _pointCount = pointCount;
            EnsureDisplayBuffer(_pointCount);
        }
    }

    public void ApplyWaveform(float[] samples, float intensity, float hueOffset, float deltaTime, float decaySpeed)
    {
        if (_graphic == null || _displaySamples == null || _displaySamples.Length == 0)
            return;

        _displayIntensity = Mathf.MoveTowards(_displayIntensity, intensity, deltaTime * 8f);

        if (samples == null || intensity <= 0.001f)
        {
            DecayToZero(deltaTime, decaySpeed);
            _graphic.SetWaveform(_displaySamples, _displayIntensity, hueOffset);
            return;
        }

        int count = Mathf.Min(samples.Length, _displaySamples.Length);
        for (int i = 0; i < count; i++)
            _displaySamples[i] = samples[i];

        for (int i = count; i < _displaySamples.Length; i++)
            _displaySamples[i] = 0f;

        _graphic.SetWaveform(_displaySamples, _displayIntensity, hueOffset);
    }

    private void DecayToZero(float deltaTime, float decaySpeed)
    {
        if (_displaySamples == null) return;

        float step = deltaTime * decaySpeed;
        for (int i = 0; i < _displaySamples.Length; i++)
            _displaySamples[i] = Mathf.MoveTowards(_displaySamples[i], 0f, step);

        _displayIntensity = Mathf.MoveTowards(_displayIntensity, 0f, step);
    }

    private void EnsureDisplayBuffer(int pointCount)
    {
        if (_displaySamples == null || _displaySamples.Length != pointCount)
            _displaySamples = new float[pointCount];
    }

    private void DestroyExistingWaveform()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        _graphic = null;
        _waveformRect = null;
        _displaySamples = null;
        _displayIntensity = 0f;
        _pointCount = 0;
    }
}
