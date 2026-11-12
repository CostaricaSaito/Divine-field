using System;
using System.Collections.Generic;
using Coffee.UIEffects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Brief vertical screen shake on damage and reflect bounce.
/// Small / medium / large damage scale amplitude. Large also applies RGB shift and a white flash.
/// </summary>
[DisallowMultipleComponent]
public sealed class DramaticScreenShakePresenter : MonoBehaviour
{
    public const int MediumDamageAmount = 15;
    public const int LargeDamageAmount = 30;

    private enum ShakeTier
    {
        Small,
        Medium,
        Large
    }

    private enum ApplyMode
    {
        RestoreToRest,
        KeepCurrentX,
        AdditiveY
    }

    private struct Entry
    {
        public RectTransform Target;
        public Vector2 Rest;
        public float Amplitude;
        public float Phase;
        public int Cycles;
        public ApplyMode Mode;
        public Graphic SourceGraphic;
        public UIEffect Effect;
        public bool AddedEffect;
        public bool RestoreEffect;
        public SamplingFilter PrevFilter;
        public float PrevIntensity;
        public float PrevWidth;
    }

    [Header("Timing")]
    [SerializeField] [Min(0.02f)] private float smallDuration = 0.12f;
    [SerializeField] [Min(0.02f)] private float mediumDuration = 0.15f;
    [SerializeField] [Min(0.02f)] private float largeDuration = 0.18f;
    [SerializeField] [Min(1)] private int smallMinCycles = 1;
    [SerializeField] [Min(1)] private int smallMaxCycles = 3;
    [SerializeField] [Min(1)] private int mediumMinCycles = 2;
    [SerializeField] [Min(1)] private int mediumMaxCycles = 4;
    [SerializeField] [Min(1)] private int largeMinCycles = 3;
    [SerializeField] [Min(1)] private int largeMaxCycles = 5;

    [Header("Small amplitude (pixels)")]
    [SerializeField] [Min(0f)] private float uiMinAmplitude = 18f;
    [SerializeField] [Min(0f)] private float uiMaxAmplitude = 42f;
    [SerializeField] [Min(0f)] private float backgroundMinAmplitude = 28f;
    [SerializeField] [Min(0f)] private float backgroundMaxAmplitude = 62f;

    [Header("Tier scale (small = 1)")]
    [SerializeField] [Min(1f)] private float mediumScale = 1.75f;
    [SerializeField] [Min(1f)] private float largeScale = 2.5f;

    [Header("Large hit extras")]
    [SerializeField] [Min(0f)] private float largeWhiteFlashMs = 50f;
    [SerializeField] [Range(0f, 1f)] private float mediumChromaticIntensity = 0.38f;
    [SerializeField] [Range(0.5f, 10f)] private float mediumChromaticWidth = 1.15f;
    [SerializeField] [Range(0f, 1f)] private float largeChromaticIntensity = 0.82f;
    [SerializeField] [Range(0.5f, 10f)] private float largeChromaticWidth = 2.2f;

    private readonly List<RectTransform> _collectBuffer = new List<RectTransform>(64);
    private readonly HashSet<RectTransform> _seen = new HashSet<RectTransform>();
    private readonly List<Entry> _entries = new List<Entry>(64);

    private bool _playing;
    private bool _canvasHooked;
    private float _elapsed;
    private float _waveT;
    private float _duration = 0.12f;
    private ShakeTier _tier;

    private UseButtonPresenter _useButtonPresenter;
    private EconomicUIHandler _economicHandler;
    private MagicPanelPresenter _magicPanelPresenter;
    private CardStatsDisplay _cardStatsDisplay;
    private BattleBackgroundVideoController _backgroundVideo;
    private BattleFogBackgroundPresenter _fogBackground;
    private BattleBgmBackgroundShakeController _bgmShake;

    private void Awake()
    {
        CacheCollaborators();
    }

    private void OnEnable()
    {
        HookCanvas();
    }

    private void OnDisable()
    {
        UnhookCanvas();
        RestoreAndClear();
    }

    public void PlayForDamage(int amount)
    {
        if (amount <= 0)
            return;
        PlayInternal(ResolveTier(amount), whiteFlash: amount >= LargeDamageAmount);
    }

    public void Play()
    {
        PlayInternal(ShakeTier.Large, whiteFlash: false);
    }

    public void PlayIfBigDamage(int amount)
    {
        PlayForDamage(amount);
    }

    private static ShakeTier ResolveTier(int amount)
    {
        if (amount >= LargeDamageAmount)
            return ShakeTier.Large;
        if (amount >= MediumDamageAmount)
            return ShakeTier.Medium;
        return ShakeTier.Small;
    }

    private void PlayInternal(ShakeTier tier, bool whiteFlash)
    {
        RestoreAndClear();
        CacheCollaborators();
        _tier = tier;
        _duration = DurationFor(tier);
        CollectTargets();
        if (_entries.Count == 0)
            return;

        if (tier == ShakeTier.Medium || tier == ShakeTier.Large)
        {
            try
            {
                BeginChromatic();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        if (tier == ShakeTier.Large && whiteFlash && largeWhiteFlashMs > 0f)
            BattleUIManager.I?.PlayFullscreenWhiteFlashMs(largeWhiteFlashMs);

        _elapsed = 0f;
        _waveT = 0f;
        _playing = true;
        HookCanvas();
    }

    private float DurationFor(ShakeTier tier)
    {
        switch (tier)
        {
            case ShakeTier.Medium: return mediumDuration;
            case ShakeTier.Large: return largeDuration;
            default: return smallDuration;
        }
    }

    private float ScaleFor(ShakeTier tier)
    {
        switch (tier)
        {
            case ShakeTier.Medium: return mediumScale;
            case ShakeTier.Large: return largeScale;
            default: return 1f;
        }
    }

    private void GetCycleRange(ShakeTier tier, out int min, out int max)
    {
        switch (tier)
        {
            case ShakeTier.Medium:
                min = mediumMinCycles;
                max = mediumMaxCycles;
                break;
            case ShakeTier.Large:
                min = largeMinCycles;
                max = largeMaxCycles;
                break;
            default:
                min = smallMinCycles;
                max = smallMaxCycles;
                break;
        }

        if (min > max)
        {
            int swap = min;
            min = max;
            max = swap;
        }
    }

    private void LateUpdate()
    {
        if (!_playing)
            return;

        _elapsed += Time.unscaledDeltaTime;
        _waveT = _duration > 0.0001f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
        ApplyWave(_waveT, ApplyMode.AdditiveY);
        UpdateChromatic(_waveT);

        if (_waveT >= 1f)
            RestoreAndClear();
    }

    private void OnWillRenderCanvases()
    {
        if (!_playing)
            return;
        ApplyWave(_waveT, ApplyMode.RestoreToRest);
        ApplyWave(_waveT, ApplyMode.KeepCurrentX);
    }

    private void HookCanvas()
    {
        if (_canvasHooked)
            return;
        Canvas.willRenderCanvases += OnWillRenderCanvases;
        _canvasHooked = true;
    }

    private void UnhookCanvas()
    {
        if (!_canvasHooked)
            return;
        Canvas.willRenderCanvases -= OnWillRenderCanvases;
        _canvasHooked = false;
    }

    private void CacheCollaborators()
    {
        if (_useButtonPresenter == null)
            _useButtonPresenter = FindObjectOfType<UseButtonPresenter>();
        if (_economicHandler == null)
            _economicHandler = FindObjectOfType<EconomicUIHandler>();
        if (_magicPanelPresenter == null)
            _magicPanelPresenter = FindObjectOfType<MagicPanelPresenter>();
        if (_cardStatsDisplay == null)
            _cardStatsDisplay = FindObjectOfType<CardStatsDisplay>();
        if (_backgroundVideo == null)
            _backgroundVideo = BattleBackgroundVideoController.Instance != null
                ? BattleBackgroundVideoController.Instance
                : FindObjectOfType<BattleBackgroundVideoController>();
        if (_fogBackground == null)
            _fogBackground = BattleFogBackgroundPresenter.Instance != null
                ? BattleFogBackgroundPresenter.Instance
                : FindObjectOfType<BattleFogBackgroundPresenter>();
        if (_bgmShake == null)
            _bgmShake = FindObjectOfType<BattleBgmBackgroundShakeController>();
    }

    private void CollectTargets()
    {
        _collectBuffer.Clear();
        _seen.Clear();
        _entries.Clear();

        float scale = ScaleFor(_tier);
        float uiMin = uiMinAmplitude * scale;
        float uiMax = uiMaxAmplitude * scale;
        float bgMin = backgroundMinAmplitude * scale;
        float bgMax = backgroundMaxAmplitude * scale;

        _useButtonPresenter?.CollectShakeTargets(_collectBuffer);
        _economicHandler?.CollectShakeTargets(_collectBuffer);
        _magicPanelPresenter?.CollectShakeTargets(_collectBuffer);
        _cardStatsDisplay?.CollectShakeTargets(_collectBuffer);
        CollectHandCards(_collectBuffer);
        CommitCollected(_collectBuffer, uiMin, uiMax, ApplyMode.RestoreToRest);

        RectTransform fogRt = null;
        bool fogActive = _fogBackground != null
            && _fogBackground.TryGetActiveShakeRect(out fogRt);
        if (fogActive)
            AddEntry(fogRt, bgMin, bgMax, ApplyMode.KeepCurrentX);

        _collectBuffer.Clear();
        _backgroundVideo?.CollectShakeTargets(_collectBuffer, includeStatic: !fogActive, includeVideo: true);
        bool bgmLive = _bgmShake != null && _bgmShake.isActiveAndEnabled;
        for (int i = 0; i < _collectBuffer.Count; i++)
        {
            var rt = _collectBuffer[i];
            ApplyMode mode = bgmLive && _bgmShake.Manages(rt)
                ? ApplyMode.AdditiveY
                : ApplyMode.RestoreToRest;
            AddEntry(rt, bgMin, bgMax, mode);
        }
    }

    private void CollectHandCards(List<RectTransform> dest)
    {
        var bm = BattleManager.I;
        if (bm == null) return;
        AddHand(dest, bm.playerHand);
        AddHand(dest, bm.cpuHand);
    }

    private static void AddHand(List<RectTransform> dest, List<CardData> hand)
    {
        if (hand == null) return;
        for (int i = 0; i < hand.Count; i++)
        {
            var ui = hand[i] != null ? hand[i].cardUI : null;
            if (ui == null || !ui.gameObject.activeInHierarchy)
                continue;
            var rt = ui.transform as RectTransform;
            if (rt != null)
                dest.Add(rt);
        }
    }

    private void CommitCollected(List<RectTransform> source, float minAmp, float maxAmp, ApplyMode mode)
    {
        for (int i = 0; i < source.Count; i++)
            AddEntry(source[i], minAmp, maxAmp, mode);
    }

    private void AddEntry(RectTransform rt, float minAmp, float maxAmp, ApplyMode mode)
    {
        if (rt == null || !rt.gameObject.activeInHierarchy)
            return;
        if (!_seen.Add(rt))
            return;

        float lo = Mathf.Min(minAmp, maxAmp);
        float hi = Mathf.Max(minAmp, maxAmp);
        GetCycleRange(_tier, out int cycleLo, out int cycleHi);
        _entries.Add(new Entry
        {
            Target = rt,
            Rest = rt.anchoredPosition,
            Amplitude = UnityEngine.Random.Range(lo, hi),
            Phase = UnityEngine.Random.value < 0.5f ? 0f : Mathf.PI,
            Cycles = UnityEngine.Random.Range(cycleLo, cycleHi + 1),
            Mode = mode,
            SourceGraphic = ResolveGraphic(rt)
        });
    }

    private static Graphic ResolveGraphic(RectTransform rt)
    {
        if (rt == null)
            return null;

        var card = rt.GetComponent<CardUI>();
        if (CanApplyChromatic(card != null ? card.cardImage : null))
            return card.cardImage;

        var image = rt.GetComponent<Image>();
        if (CanApplyChromatic(image))
            return image;

        var graphics = rt.GetComponentsInChildren<Graphic>(false);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (CanApplyChromatic(graphics[i]))
                return graphics[i];
        }

        return null;
    }

    private static bool CanApplyChromatic(Graphic graphic)
    {
        if (graphic == null || !graphic.enabled || !graphic.gameObject.activeInHierarchy)
            return false;
        if (graphic is TMP_Text || graphic is Text)
            return false;
        return graphic is Image;
    }

    private void ApplyWave(float t, ApplyMode mode)
    {
        float twoPi = Mathf.PI * 2f;
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.Target == null || e.Mode != mode)
                continue;

            int cycles = Mathf.Max(1, e.Cycles);
            float y = e.Amplitude * Mathf.Sin(twoPi * cycles * t + e.Phase);
            ApplyOffset(in e, y);
        }
    }

    private static void ApplyOffset(in Entry e, float y)
    {
        switch (e.Mode)
        {
            case ApplyMode.KeepCurrentX:
            {
                var p = e.Target.anchoredPosition;
                e.Target.anchoredPosition = new Vector2(p.x, e.Rest.y + y);
                break;
            }
            case ApplyMode.AdditiveY:
            {
                var p = e.Target.anchoredPosition;
                e.Target.anchoredPosition = new Vector2(p.x, p.y + y);
                break;
            }
            default:
                e.Target.anchoredPosition = e.Rest + new Vector2(0f, y);
                break;
        }
    }

    private void BeginChromatic()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (!CanApplyChromatic(e.SourceGraphic))
            {
                _entries[i] = e;
                continue;
            }

            var fx = e.SourceGraphic.GetComponent<UIEffect>();
            if (fx != null && fx.transitionFilter != TransitionFilter.None)
            {
                _entries[i] = e;
                continue;
            }

            if (fx == null)
            {
                fx = e.SourceGraphic.gameObject.AddComponent<UIEffect>();
                e.AddedEffect = true;
            }
            else
            {
                e.RestoreEffect = true;
                e.PrevFilter = fx.samplingFilter;
                e.PrevIntensity = fx.samplingIntensity;
                e.PrevWidth = fx.samplingWidth;
            }

            fx.toneFilter = ToneFilter.None;
            fx.colorFilter = ColorFilter.None;
            fx.samplingFilter = SamplingFilter.RgbShift;
            fx.samplingIntensity = ChromaticIntensityFor(_tier);
            fx.samplingWidth = ChromaticWidthFor(_tier);
            e.Effect = fx;
            _entries[i] = e;
        }
    }

    private float ChromaticIntensityFor(ShakeTier tier)
    {
        return tier == ShakeTier.Large ? largeChromaticIntensity : mediumChromaticIntensity;
    }

    private float ChromaticWidthFor(ShakeTier tier)
    {
        return tier == ShakeTier.Large ? largeChromaticWidth : mediumChromaticWidth;
    }

    private void UpdateChromatic(float t)
    {
        if (_tier != ShakeTier.Medium && _tier != ShakeTier.Large)
            return;

        float envelope = 1f - t;
        float peak = ChromaticIntensityFor(_tier);
        float floor = _tier == ShakeTier.Large ? 0.55f : 0.4f;
        float intensity = peak * Mathf.Lerp(floor, 1f, envelope);
        for (int i = 0; i < _entries.Count; i++)
        {
            var fx = _entries[i].Effect;
            if (fx != null)
                fx.samplingIntensity = intensity;
        }
    }

    private void RestoreAndClear()
    {
        if (_playing)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                RestoreChromatic(in e);
                if (e.Target == null)
                    continue;
                if (e.Mode == ApplyMode.AdditiveY)
                    continue;
                if (e.Mode == ApplyMode.KeepCurrentX)
                {
                    var p = e.Target.anchoredPosition;
                    e.Target.anchoredPosition = new Vector2(p.x, e.Rest.y);
                }
                else
                {
                    e.Target.anchoredPosition = e.Rest;
                }
            }
        }

        _playing = false;
        _elapsed = 0f;
        _waveT = 0f;
        _entries.Clear();
    }

    private static void RestoreChromatic(in Entry e)
    {
        if (e.Effect == null)
            return;

        if (e.AddedEffect)
        {
            Destroy(e.Effect);
            return;
        }

        if (!e.RestoreEffect)
            return;

        e.Effect.samplingFilter = e.PrevFilter;
        e.Effect.samplingIntensity = e.PrevIntensity;
        e.Effect.samplingWidth = e.PrevWidth;
    }
}
