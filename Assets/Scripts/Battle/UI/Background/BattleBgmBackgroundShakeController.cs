using UnityEngine;

/// <summary>
/// Shakes background layers based on battle BGM amplitude (bass-heavy).
/// Overlay Canvas is unaffected by camera shake, so this targets RectTransforms directly.
/// </summary>
public sealed class BattleBgmBackgroundShakeController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private RectTransform[] shakeTargets;
    [SerializeField] private bool includeStaticBackground = true;
    [SerializeField] private bool includeBackgroundVideo = true;

    [Header("Shake")]
    [SerializeField] private float maxOffsetPixels = 12f;
    [SerializeField] private float maxRotationZ = 1.2f;
    [SerializeField] private float shakeSpeed = 1.6f;
    [SerializeField] private float bassSensitivity = 18f;
    [SerializeField] private int spectrumSize = 128;
    [SerializeField] private float responseSmoothing = 0.28f;

    private float[] _spectrum;
    private Vector2[] _baseAnchoredPositions;
    private float[] _baseRotationsZ;
    private float _shakeAmount;

    private void Awake()
    {
        EnsureTargets();
        CacheBaseTransforms();
    }

    private void LateUpdate()
    {
        if (shakeTargets == null || shakeTargets.Length == 0)
            return;

        var source = BattleBgmController.Instance != null ? BattleBgmController.Instance.BgmSource : null;
        float targetAmount = 0f;
        if (source != null && source.isPlaying)
            targetAmount = SampleBassAmount(source) * Mathf.Clamp01(source.volume);

        _shakeAmount = Mathf.Lerp(_shakeAmount, targetAmount, responseSmoothing);

        float time = Time.unscaledTime * shakeSpeed;
        float nx = Mathf.PerlinNoise(time, 0.13f) * 2f - 1f;
        float ny = Mathf.PerlinNoise(0.37f, time) * 2f - 1f;
        float nr = Mathf.PerlinNoise(time, time) * 2f - 1f;

        Vector2 offset = new Vector2(nx, ny) * maxOffsetPixels * _shakeAmount;
        float rotZ = nr * maxRotationZ * _shakeAmount;

        for (int i = 0; i < shakeTargets.Length; i++)
        {
            var target = shakeTargets[i];
            if (target == null || _baseAnchoredPositions == null || i >= _baseAnchoredPositions.Length)
                continue;

            target.anchoredPosition = _baseAnchoredPositions[i] + offset;
            target.localRotation = Quaternion.Euler(0f, 0f, _baseRotationsZ[i] + rotZ);
        }
    }

    private float SampleBassAmount(AudioSource source)
    {
        spectrumSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(spectrumSize, 64, 512));
        if (_spectrum == null || _spectrum.Length != spectrumSize)
            _spectrum = new float[spectrumSize];

        source.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);

        int bassBins = Mathf.Clamp(spectrumSize / 8, 4, 24);
        float sum = 0f;
        for (int i = 0; i < bassBins; i++)
            sum += _spectrum[i];

        float avg = sum / bassBins;
        return Mathf.Clamp01(avg * bassSensitivity);
    }

    private void EnsureTargets()
    {
        if (shakeTargets != null && shakeTargets.Length > 0)
            return;

        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
        if (canvas == null)
            return;

        RectTransform staticBg = null;
        RectTransform videoBg = null;

        if (includeStaticBackground)
        {
            var found = canvas.transform.Find("BackGroundImage");
            if (found != null)
                staticBg = found as RectTransform;
        }

        if (includeBackgroundVideo)
        {
            var found = canvas.transform.Find("BackGroundVideo");
            if (found != null)
                videoBg = found as RectTransform;
        }

        if (staticBg == null && videoBg == null)
            return;

        shakeTargets = videoBg != null && staticBg != null
            ? new[] { staticBg, videoBg }
            : new[] { staticBg != null ? staticBg : videoBg };
    }

    private void CacheBaseTransforms()
    {
        if (shakeTargets == null)
            return;

        _baseAnchoredPositions = new Vector2[shakeTargets.Length];
        _baseRotationsZ = new float[shakeTargets.Length];
        for (int i = 0; i < shakeTargets.Length; i++)
        {
            if (shakeTargets[i] == null)
                continue;
            _baseAnchoredPositions[i] = shakeTargets[i].anchoredPosition;
            _baseRotationsZ[i] = shakeTargets[i].localEulerAngles.z;
        }
    }
}
