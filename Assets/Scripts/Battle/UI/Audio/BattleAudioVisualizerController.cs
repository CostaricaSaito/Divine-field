using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Samples battle BGM waveform data and drives <see cref="BattleAudioVisualizerView"/>.
/// Attach to the BGM object (same as <see cref="BattleBgmController"/>).
/// </summary>
public sealed class BattleAudioVisualizerController : MonoBehaviour
{
    [SerializeField] private BattleBgmController bgmController;
    [SerializeField] private Image backgroundImageOverride;

    [Header("Sampling")]
    [SerializeField] private int outputSampleSize = 256;
    [SerializeField] private int displayPointCount = 128;
    [SerializeField] private float updateIntervalSeconds = 0.016f;
    [SerializeField] private float amplitudeGain = 1.8f;
    [SerializeField] private float smoothing = 0.12f;

    [Header("Appearance")]
    [SerializeField] private float waveformWidth = 960f;
    [SerializeField] private float waveformHeight = 200f;
    [Tooltip("Waveform center Y offset in pixels (positive = up).")]
    [SerializeField] private float waveformVerticalOffset;
    [SerializeField] private float lineThickness = 4f;
    [SerializeField] private float lineAlpha = 0.38f;
    [SerializeField] private float colorSaturation = 0.82f;
    [SerializeField] private float colorBrightness = 1f;
    [SerializeField] private float decaySpeed = 5f;

    [Header("Rainbow Scroll")]
    [Tooltip("Hue cycles per second. Colors scroll from left to right.")]
    [SerializeField] private float hueScrollSpeed = 0.12f;

    private BattleAudioVisualizerView _view;
    private RectTransform _canvasRoot;
    private Image _backgroundImage;
    private float[] _outputSamples;
    private float[] _displaySamples;
    private float[] _smoothedSamples;
    private float _updateTimer;
    private float _lastIntensity = 1f;
    private float _hueOffset;

    private void Awake()
    {
        if (bgmController == null)
            bgmController = GetComponent<BattleBgmController>();
        if (bgmController == null)
            bgmController = BattleBgmController.Instance;
    }

    private void Start()
    {
        EnsureViewBuilt();
    }

    private void Update()
    {
        if (_view == null)
        {
            EnsureViewBuilt();
            if (_view == null) return;
        }

        SyncAppearanceToView();
        AdvanceHueOffset();

        var source = ResolveBgmSource();
        if (source == null || !source.isPlaying)
        {
            _lastIntensity = 0f;
            _view.ApplyWaveform(null, 0f, _hueOffset, Time.unscaledDeltaTime, decaySpeed);
            return;
        }

        _updateTimer += Time.unscaledDeltaTime;
        if (_updateTimer >= updateIntervalSeconds)
        {
            _updateTimer = 0f;
            SampleWaveform(source);
        }

        _view.ApplyWaveform(_displaySamples, _lastIntensity, _hueOffset, Time.unscaledDeltaTime, decaySpeed);
    }

    private void AdvanceHueOffset()
    {
        if (hueScrollSpeed <= 0f) return;

        _hueOffset += Time.unscaledDeltaTime * hueScrollSpeed;
        if (_hueOffset >= 1f)
            _hueOffset -= Mathf.Floor(_hueOffset);
    }

    private void SyncAppearanceToView()
    {
        if (_view == null)
            return;

        if (_canvasRoot != null)
            _view.EnsureLayerOrder(_canvasRoot, ResolveVisualizerSiblingIndex());

        _view.SyncAppearance(
            waveformWidth,
            waveformHeight,
            lineThickness,
            lineAlpha,
            colorSaturation,
            colorBrightness,
            waveformVerticalOffset,
            displayPointCount);
        EnsureSampleBuffers();
    }

    private int ResolveVisualizerSiblingIndex()
    {
        if (_canvasRoot == null)
            return 0;

        var videoLayer = _canvasRoot.Find("BackGroundVideo") as RectTransform;
        if (videoLayer != null)
            return videoLayer.GetSiblingIndex() + 1;

        if (_backgroundImage != null)
            return _backgroundImage.rectTransform.GetSiblingIndex() + 1;

        return 0;
    }

    private AudioSource ResolveBgmSource()
    {
        if (bgmController == null)
            bgmController = BattleBgmController.Instance;
        return bgmController != null ? bgmController.BgmSource : null;
    }

    private void EnsureViewBuilt()
    {
        if (_view != null) return;

        var bgImage = backgroundImageOverride;
        if (bgImage == null && bgmController != null)
            bgImage = bgmController.BattleBackgroundImage;
        if (bgImage == null)
            bgImage = FindBackgroundImageFallback();

        if (bgImage == null)
        {
            Debug.LogWarning("[BattleAudioVisualizerController] Background Image not found; visualizer disabled.");
            enabled = false;
            return;
        }

        _backgroundImage = bgImage;
        _canvasRoot = bgImage.rectTransform.parent as RectTransform;
        if (_canvasRoot == null)
        {
            Debug.LogWarning("[BattleAudioVisualizerController] Canvas root not found; visualizer disabled.");
            enabled = false;
            return;
        }

        int siblingIndex = ResolveVisualizerSiblingIndex();
        var layerGo = new GameObject("AudioVisualizerLayer", typeof(RectTransform), typeof(BattleAudioVisualizerView));
        _view = layerGo.GetComponent<BattleAudioVisualizerView>();
        _view.Build(_canvasRoot, siblingIndex, displayPointCount);
        _view.EnsureLayerOrder(_canvasRoot, siblingIndex);
        SyncAppearanceToView();

        EnsureSampleBuffers();
    }

    private static Image FindBackgroundImageFallback()
    {
        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
        if (canvas == null) return null;

        var found = canvas.transform.Find("BackGroundImage");
        return found != null ? found.GetComponent<Image>() : null;
    }

    private void EnsureSampleBuffers()
    {
        outputSampleSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(outputSampleSize, 64, 1024));
        displayPointCount = Mathf.Clamp(displayPointCount, 16, 512);

        if (_outputSamples == null || _outputSamples.Length != outputSampleSize)
            _outputSamples = new float[outputSampleSize];

        if (_displaySamples == null || _displaySamples.Length != displayPointCount)
            _displaySamples = new float[displayPointCount];

        if (_smoothedSamples == null || _smoothedSamples.Length != displayPointCount)
            _smoothedSamples = new float[displayPointCount];
    }

    private void SampleWaveform(AudioSource source)
    {
        EnsureSampleBuffers();
        source.GetOutputData(_outputSamples, 0);

        for (int i = 0; i < displayPointCount; i++)
        {
            float sourceIndex = i / (float)(displayPointCount - 1) * (outputSampleSize - 1);
            int index0 = Mathf.FloorToInt(sourceIndex);
            int index1 = Mathf.Min(index0 + 1, outputSampleSize - 1);
            float blend = sourceIndex - index0;
            float sample = Mathf.Lerp(_outputSamples[index0], _outputSamples[index1], blend);
            float scaled = Mathf.Clamp(sample * amplitudeGain, -1f, 1f);

            if (smoothing <= 0.001f)
                _smoothedSamples[i] = scaled;
            else
                _smoothedSamples[i] = Mathf.Lerp(_smoothedSamples[i], scaled, smoothing);

            _displaySamples[i] = _smoothedSamples[i];
        }

        _lastIntensity = Mathf.Clamp01(source.volume);
    }
}
