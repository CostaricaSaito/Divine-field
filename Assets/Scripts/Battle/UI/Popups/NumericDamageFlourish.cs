using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Coffee.UIEffects;
using TMPro;
using UnityEngine;

/// <summary>
/// Runtime normal-damage callout: "30 ダメージ", italic, one piece at a time.
/// Face stays red; bloom comes from TMP glow + UIEffect RgbShift. Each piece bounces in from below.
/// </summary>
public sealed class NumericDamageFlourish : MonoBehaviour
{
    private const string Word = "\u30c0\u30e1\u30fc\u30b8";
    private const float DigitFontSize = 160f;
    private const float WordSizeRatio = 0.35f;
    private const float EntranceScale = 1.28f;
    private const float RiseDistance = 74f;
    private const float OvershootDistance = 22f;
    private const float DigitStagger = 0.1f;
    private const float RiseDuration = 0.14f;
    private const float OvershootDuration = 0.07f;
    private const float SettleDuration = 0.11f;
    private const float HoldAfterComplete = 0.5f;
    private const float ExitDuration = 0.2f;
    private const float ExitRiseDistance = 58f;
    private const float DigitGap = 0f;
    private const float WordGap = 10f;
    private const float ChromaticIntensity = 0.22f;
    private const float SettledOutlineWidth = 0.11f;
    private const float EntranceOutlineWidth = 0.14f;

    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowOuterId = Shader.PropertyToID("_GlowOuter");
    private static readonly int GlowInnerId = Shader.PropertyToID("_GlowInner");
    private static readonly int GlowOffsetId = Shader.PropertyToID("_GlowOffset");
    private static readonly int GlowPowerId = Shader.PropertyToID("_GlowPower");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int OutlineSoftnessId = Shader.PropertyToID("_OutlineSoftness");
    private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
    private static readonly int FaceDilateId = Shader.PropertyToID("_FaceDilate");
    private static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");
    private static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");
    private static readonly int UnderlayDilateId = Shader.PropertyToID("_UnderlayDilate");
    private static readonly int UnderlayOffsetYId = Shader.PropertyToID("_UnderlayOffsetY");

    private readonly List<Glyph> _glyphs = new List<Glyph>();
    private readonly List<Material> _ownedMaterials = new List<Material>();
    private RectTransform _contentRoot;
    private CanvasGroup _group;
    private CancellationTokenSource _cts;
    private Color _fill = new Color(0.92f, 0.12f, 0.18f, 1f);
    private Color _outline = Color.white;
    private int _digitCount;

    /// <summary>Seconds from spawn until this object destroys itself. 0 if setup failed.</summary>
    public float Play(
        int amount,
        Color fill,
        Color outline,
        GameObject fontSourcePrefab,
        Material glowTemplate)
    {
        _fill = fill.a > 0.001f ? fill : new Color(0.92f, 0.12f, 0.18f, 1f);
        _outline = outline.a > 0.001f ? outline : Color.white;

        _group = GetComponent<CanvasGroup>();
        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        StretchToParentPanel();
        EnsureContentRoot();

        TMP_FontAsset font = ResolveFont(fontSourcePrefab);
        if (font == null || !Build(amount, font, glowTemplate))
        {
            Debug.LogWarning("[NumericDamageFlourish] Font or glyph setup failed");
            Destroy(gameObject);
            return 0f;
        }

        _cts = new CancellationTokenSource();
        float life = ComputeLifetime(_digitCount);
        _ = RunAsync(_cts.Token);
        return life;
    }

    private void OnDestroy()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        for (int i = 0; i < _ownedMaterials.Count; i++)
        {
            if (_ownedMaterials[i] != null)
                Destroy(_ownedMaterials[i]);
        }
        _ownedMaterials.Clear();
    }

    private static float ComputeLifetime(int digitCount)
    {
        int digits = Mathf.Max(1, digitCount);
        float wordStart = (digits - 1) * DigitStagger + RiseDuration;
        float motion = RiseDuration + OvershootDuration + SettleDuration;
        // Small buffer: each await lands on the next frame, so the real fade ends slightly later.
        return wordStart + motion + HoldAfterComplete + ExitDuration + 0.06f;
    }

    private static TMP_FontAsset ResolveFont(GameObject fontSourcePrefab)
    {
        if (fontSourcePrefab != null)
        {
            var sample = fontSourcePrefab.GetComponentInChildren<TMP_Text>(true);
            if (sample != null && sample.font != null)
                return sample.font;
        }

        return TMP_Settings.defaultFontAsset;
    }

    private bool Build(int amount, TMP_FontAsset font, Material glowTemplate)
    {
        string digits = Mathf.Max(0, amount).ToString(CultureInfo.InvariantCulture);
        _digitCount = digits.Length;
        font.TryAddCharacters(digits + Word, out _);

        float lineHeight = DigitFontSize * 2.15f;
        float cursor = 0f;
        var slots = new List<float>(_digitCount + 1);

        for (int i = 0; i < digits.Length; i++)
        {
            var glyph = CreateGlyph(digits[i].ToString(), DigitFontSize, false, font, glowTemplate, lineHeight);
            if (glyph == null) return false;
            _glyphs.Add(glyph);
            slots.Add(glyph.SlotWidth);
        }

        var word = CreateGlyph(Word, DigitFontSize * WordSizeRatio, true, font, glowTemplate, lineHeight);
        if (word == null) return false;
        _glyphs.Add(word);
        slots.Add(word.SlotWidth);

        float total = 0f;
        for (int i = 0; i < slots.Count; i++)
        {
            total += slots[i];
            if (i < slots.Count - 1)
                total += _glyphs[i + 1].IsWord ? WordGap : DigitGap;
        }

        cursor = -total * 0.5f;
        for (int i = 0; i < _glyphs.Count; i++)
        {
            float slot = slots[i];
            float center = cursor + slot * 0.5f;
            var glyph = _glyphs[i];
            float x = center - glyph.InkCenterLocalX;
            glyph.RestX = x;
            glyph.Rect.anchoredPosition = new Vector2(x, -RiseDistance);
            glyph.Rect.localScale = new Vector3(EntranceScale, EntranceScale, 1f);
            ApplyLook(glyph, 0f, 0f);
            cursor += slot + (i < _glyphs.Count - 1 && _glyphs[i + 1].IsWord ? WordGap : DigitGap);
        }

        FitContentToPanel();
        return _glyphs.Count > 0;
    }

    private void StretchToParentPanel()
    {
        var root = transform as RectTransform;
        if (root == null) return;

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.localScale = Vector3.one;
    }

    private void EnsureContentRoot()
    {
        if (_contentRoot != null) return;

        var go = new GameObject("Content", typeof(RectTransform));
        _contentRoot = go.GetComponent<RectTransform>();
        _contentRoot.SetParent(transform, false);
        _contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _contentRoot.pivot = new Vector2(0.5f, 0.5f);
        _contentRoot.anchoredPosition = Vector2.zero;
        _contentRoot.localScale = Vector3.one;
    }

    private void FitContentToPanel()
    {
        var panel = transform.parent as RectTransform;
        if (panel == null || _contentRoot == null || _glyphs.Count == 0) return;

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        for (int i = 0; i < _glyphs.Count; i++)
        {
            var g = _glyphs[i];
            float half = g.SlotWidth * 0.5f;
            minX = Mathf.Min(minX, g.RestX - half);
            maxX = Mathf.Max(maxX, g.RestX + half);
        }

        const float bloomPad = 20f;
        float contentW = (maxX - minX) + bloomPad * 2f;
        float contentH = DigitFontSize * 1.42f + bloomPad;

        float maxW = panel.rect.width * 0.98f;
        float maxH = panel.rect.height * 0.52f;
        float scale = Mathf.Min(
            maxW / Mathf.Max(48f, contentW),
            maxH / Mathf.Max(48f, contentH));
        _contentRoot.localScale = new Vector3(scale, scale, 1f);
    }

    private Glyph CreateGlyph(
        string content,
        float fontSize,
        bool isWord,
        TMP_FontAsset font,
        Material glowTemplate,
        float lineHeight)
    {
        var sizeDelta = new Vector2(isWord ? 1100f : 480f, lineHeight);
        float spacing = isWord ? -14f : -9f;

        var go = new GameObject(isWord ? "DamageWord" : "DamageDigit");
        go.layer = gameObject.layer;
        go.transform.SetParent(_contentRoot != null ? _contentRoot : transform, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;

        var bloomMat = CreateBloomMaterial(font, glowTemplate);
        var inkMat = CreateGlowMaterial(font, glowTemplate);
        if (bloomMat == null || inkMat == null) return null;

        var bloom = CreateGlyphTextLayer(
            go.transform, "Bloom", font, content, fontSize, spacing, sizeDelta, bloomMat, font.material);
        var ink = CreateGlyphTextLayer(
            go.transform, "Ink", font, content, fontSize, spacing, sizeDelta, inkMat, font.material);

        bloom.go.transform.SetAsFirstSibling();
        ink.go.transform.SetAsLastSibling();

        ApplyBloomLook(bloomMat, 1f);
        ApplyLook(inkMat, 1f);
        bloom.text.color = new Color(1f, 0.94f, 0.94f, 0f);
        ink.text.color = new Color(_fill.r, _fill.g, _fill.b, 0f);

        ink.text.UpdateMeshPadding();
        bloom.text.UpdateMeshPadding();
        Canvas.ForceUpdateCanvases();
        ink.text.ForceMeshUpdate(true);
        bloom.text.ForceMeshUpdate(true);

        ApplyChromaticAberration(ink.text);

        float slot = fontSize * (isWord ? 0.48f : 0.62f) * Mathf.Max(1, content.Length);
        float inkCenter = 0f;
        var info = ink.text.textInfo;
        if (info != null && info.characterCount > 0)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                if (ch.origin < min) min = ch.origin;
                if (ch.xAdvance > max) max = ch.xAdvance;
            }

            if (max > min)
            {
                float measured = (max - min) * 1.04f;
                float minSlot = fontSize * 0.28f * Mathf.Max(1, content.Length);
                if (measured >= minSlot)
                    slot = measured;
                inkCenter = (min + max) * 0.5f;
            }
        }
        else if (ink.text.preferredWidth > 1f)
        {
            slot = ink.text.preferredWidth;
        }

        return new Glyph
        {
            Rect = rect,
            Text = ink.text,
            BloomText = bloom.text,
            Material = inkMat,
            BloomMaterial = bloomMat,
            IsWord = isWord,
            SlotWidth = Mathf.Max(8f, slot),
            InkCenterLocalX = inkCenter,
        };
    }

    private static (GameObject go, TextMeshProUGUI text) CreateGlyphTextLayer(
        Transform parent,
        string layerName,
        TMP_FontAsset font,
        string content,
        float fontSize,
        float characterSpacing,
        Vector2 sizeDelta,
        Material instanceMat,
        Material sharedMat)
    {
        var go = new GameObject(layerName);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold | FontStyles.Italic;
        text.alignment = TextAlignmentOptions.Baseline;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = false;
        text.richText = false;
        text.raycastTarget = false;
        text.characterSpacing = characterSpacing;
        text.extraPadding = true;
        text.fontSharedMaterial = sharedMat;
        text.fontMaterial = instanceMat;
        return (go, text);
    }

    private static void ApplyChromaticAberration(TMP_Text text)
    {
        if (text == null) return;

        var fx = text.gameObject.GetComponent<UIEffect>();
        if (fx == null)
            fx = text.gameObject.AddComponent<UIEffect>();

        fx.samplingFilter = SamplingFilter.RgbShift;
        fx.samplingIntensity = ChromaticIntensity;
        fx.samplingWidth = 1f;
        fx.samplingScale = 1f;
        fx.toneFilter = ToneFilter.None;
        fx.colorFilter = ColorFilter.None;
    }

    private Material CreateGlowMaterial(TMP_FontAsset font, Material glowTemplate)
    {
        Material source = glowTemplate != null ? glowTemplate : font.material;
        if (source == null) return null;

        var mat = Instantiate(source);
        mat.name = "DamageFlourish Ink";
        mat.EnableKeyword("GLOW_ON");
        mat.DisableKeyword("UNDERLAY_ON");
        mat.DisableKeyword("UNDERLAY_INNER");
        _ownedMaterials.Add(mat);
        return mat;
    }

    private Material CreateBloomMaterial(TMP_FontAsset font, Material glowTemplate)
    {
        Material source = glowTemplate != null ? glowTemplate : font.material;
        if (source == null) return null;

        var mat = Instantiate(source);
        mat.name = "DamageFlourish Bloom";
        mat.EnableKeyword("GLOW_ON");
        mat.EnableKeyword("UNDERLAY_ON");
        mat.DisableKeyword("UNDERLAY_INNER");
        _ownedMaterials.Add(mat);
        return mat;
    }

    private void ApplyLook(Glyph glyph, float settle, float alpha)
    {
        float bloomFade = 1f - Mathf.SmoothStep(0.48f, 0.92f, settle);
        float inkMix = Mathf.SmoothStep(0.28f, 0.88f, settle);

        if (glyph.BloomText != null)
        {
            bool showBloom = bloomFade > 0.02f && alpha > 0.001f;
            if (glyph.BloomText.gameObject.activeSelf != showBloom)
                glyph.BloomText.gameObject.SetActive(showBloom);

            if (showBloom)
            {
                var bloomFace = new Color(1f, 0.99f, 0.99f, Mathf.Clamp01(alpha * bloomFade * 1.15f));
                glyph.BloomText.color = bloomFace;
                ApplyBloomLook(glyph.BloomMaterial, bloomFade);
            }
        }

        if (glyph.Text != null)
        {
            var face = _fill;
            face.a = alpha * inkMix;
            glyph.Text.color = face;
        }

        ApplyLook(glyph.Material, settle);
    }

    private void ApplyBloomLook(Material mat, float hot)
    {
        if (mat == null) return;

        hot = Mathf.Clamp01(hot);
        float outer = Mathf.Lerp(0.08f, 1.18f, hot);
        float power = Mathf.Lerp(0.92f, 0.28f, hot);
        float outline = Mathf.Lerp(SettledOutlineWidth, 0.12f, hot);
        float softness = Mathf.Lerp(0.02f, 0.22f, hot);

        mat.EnableKeyword("GLOW_ON");
        mat.EnableKeyword("UNDERLAY_ON");
        if (mat.HasProperty(FaceColorId))
            mat.SetColor(FaceColorId, Color.white);
        if (mat.HasProperty(FaceDilateId))
            mat.SetFloat(FaceDilateId, Mathf.Lerp(0f, 0.12f, hot));
        if (mat.HasProperty(OutlineColorId))
            mat.SetColor(OutlineColorId, Color.white);
        if (mat.HasProperty(OutlineWidthId))
            mat.SetFloat(OutlineWidthId, outline);
        if (mat.HasProperty(OutlineSoftnessId))
            mat.SetFloat(OutlineSoftnessId, softness);
        if (mat.HasProperty(GlowColorId))
            mat.SetColor(GlowColorId, new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, hot)));
        if (mat.HasProperty(GlowOuterId))
            mat.SetFloat(GlowOuterId, outer);
        if (mat.HasProperty(GlowInnerId))
            mat.SetFloat(GlowInnerId, Mathf.Lerp(0.06f, 0.28f, hot));
        if (mat.HasProperty(GlowOffsetId))
            mat.SetFloat(GlowOffsetId, 0f);
        if (mat.HasProperty(GlowPowerId))
            mat.SetFloat(GlowPowerId, power);
        if (mat.HasProperty(UnderlayColorId))
            mat.SetColor(UnderlayColorId, new Color(1f, 1f, 1f, Mathf.Lerp(0f, 1f, hot)));
        if (mat.HasProperty(UnderlaySoftnessId))
            mat.SetFloat(UnderlaySoftnessId, Mathf.Lerp(0f, 0.62f, hot));
        if (mat.HasProperty(UnderlayDilateId))
            mat.SetFloat(UnderlayDilateId, Mathf.Lerp(0f, 0.92f, hot));
        if (mat.HasProperty(UnderlayOffsetYId))
            mat.SetFloat(UnderlayOffsetYId, 0f);
    }

    private void ApplyLook(Material mat, float settle)
    {
        if (mat == null) return;

        float outer = Mathf.Lerp(0.34f, 0.26f, settle);
        float power = Mathf.Lerp(0.88f, 0.68f, settle);
        float outline = Mathf.Lerp(EntranceOutlineWidth, SettledOutlineWidth, settle);
        float softness = Mathf.Lerp(0.04f, 0.01f, settle);
        Color glow = Color.Lerp(
            new Color(1f, 0.94f, 0.94f, 0.75f),
            new Color(1f, 0.88f, 0.88f, 0.48f),
            settle);

        mat.EnableKeyword("GLOW_ON");
        mat.DisableKeyword("UNDERLAY_ON");
        if (mat.HasProperty(FaceColorId))
            mat.SetColor(FaceColorId, Color.white);
        if (mat.HasProperty(FaceDilateId))
            mat.SetFloat(FaceDilateId, 0f);
        if (mat.HasProperty(OutlineColorId))
            mat.SetColor(OutlineColorId, _outline);
        if (mat.HasProperty(OutlineWidthId))
            mat.SetFloat(OutlineWidthId, outline);
        if (mat.HasProperty(OutlineSoftnessId))
            mat.SetFloat(OutlineSoftnessId, softness);
        if (mat.HasProperty(GlowColorId))
            mat.SetColor(GlowColorId, glow);
        if (mat.HasProperty(GlowOuterId))
            mat.SetFloat(GlowOuterId, outer);
        if (mat.HasProperty(GlowInnerId))
            mat.SetFloat(GlowInnerId, 0.04f);
        if (mat.HasProperty(GlowOffsetId))
            mat.SetFloat(GlowOffsetId, 0f);
        if (mat.HasProperty(GlowPowerId))
            mat.SetFloat(GlowPowerId, power);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            var tasks = new Task[_glyphs.Count];
            for (int i = 0; i < _glyphs.Count; i++)
            {
                float delay = _glyphs[i].IsWord
                    ? (_digitCount - 1) * DigitStagger + RiseDuration
                    : i * DigitStagger;
                tasks[i] = AnimateGlyphAsync(_glyphs[i], delay, ct);
            }

            await Task.WhenAll(tasks);
            ct.ThrowIfCancellationRequested();
            await WaitAsync(HoldAfterComplete, ct);
            await ExitRiseFadeAsync(ct);
            if (this != null)
                Destroy(gameObject);
        }
        catch (OperationCanceledException)
        {
        }
        catch (AggregateException ex)
        {
            foreach (var inner in ex.InnerExceptions)
            {
                if (inner is OperationCanceledException)
                    continue;
                Debug.LogException(inner);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            if (this != null)
                Destroy(gameObject);
        }
    }

    private async Task AnimateGlyphAsync(Glyph glyph, float delay, CancellationToken ct)
    {
        if (delay > 0f)
            await WaitAsync(delay, ct);

        await MoveAsync(glyph, RiseDuration, -RiseDistance, 0f, EntranceScale, 1f, true, true, ct);
        await MoveAsync(glyph, OvershootDuration, 0f, OvershootDistance, 1f, 1f, false, true, ct);
        await MoveAsync(glyph, SettleDuration, OvershootDistance, 0f, 1f, 1f, false, false, ct);

        glyph.Rect.anchoredPosition = new Vector2(glyph.RestX, 0f);
        glyph.Rect.localScale = Vector3.one;
        ApplyLook(glyph, 1f, 1f);
    }

    private async Task MoveAsync(
        Glyph glyph,
        float duration,
        float fromY,
        float toY,
        float fromScale,
        float toScale,
        bool driveLook,
        bool easeOut,
        CancellationToken ct)
    {
        if (duration <= 0f)
        {
            glyph.Rect.anchoredPosition = new Vector2(glyph.RestX, toY);
            glyph.Rect.localScale = new Vector3(toScale, toScale, 1f);
            if (driveLook)
                ApplyLook(glyph, 1f, 1f);
            return;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            ct.ThrowIfCancellationRequested();
            float linear = Mathf.Clamp01(elapsed / duration);
            float u = easeOut ? EaseOutCubic(linear) : Mathf.SmoothStep(0f, 1f, linear);
            // Position eases into the slot. Size and the white glow stay hot until the glyph lands.
            float lockT = driveLook
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, linear))
                : u;
            float y = Mathf.Lerp(fromY, toY, u);
            float s = Mathf.Lerp(fromScale, toScale, lockT);
            glyph.Rect.anchoredPosition = new Vector2(glyph.RestX, y);
            glyph.Rect.localScale = new Vector3(s, s, 1f);
            if (driveLook)
            {
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(linear / 0.12f));
                ApplyLook(glyph, lockT, alpha);
            }

            await Task.Yield();
            elapsed += Time.unscaledDeltaTime;
        }

        glyph.Rect.anchoredPosition = new Vector2(glyph.RestX, toY);
        glyph.Rect.localScale = new Vector3(toScale, toScale, 1f);
        if (driveLook)
            ApplyLook(glyph, 1f, 1f);
    }

    private async Task ExitRiseFadeAsync(CancellationToken ct)
    {
        Vector2 startPos = _contentRoot != null ? _contentRoot.anchoredPosition : Vector2.zero;
        Vector2 endPos = startPos + Vector2.up * ExitRiseDistance;

        float elapsed = 0f;
        while (elapsed < ExitDuration)
        {
            ct.ThrowIfCancellationRequested();
            float linear = Mathf.Clamp01(elapsed / ExitDuration);
            float u = EaseOutCubic(linear);
            if (_contentRoot != null)
                _contentRoot.anchoredPosition = Vector2.Lerp(startPos, endPos, u);
            if (_group != null)
                _group.alpha = 1f - u;
            await Task.Yield();
            elapsed += Time.unscaledDeltaTime;
        }

        if (_contentRoot != null)
            _contentRoot.anchoredPosition = endPos;
        if (_group != null)
            _group.alpha = 0f;
    }

    private static async Task WaitAsync(float seconds, CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            elapsed += Time.unscaledDeltaTime;
        }
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    private sealed class Glyph
    {
        public RectTransform Rect;
        public TMP_Text Text;
        public TMP_Text BloomText;
        public Material Material;
        public Material BloomMaterial;
        public bool IsWord;
        public float RestX;
        public float SlotWidth;
        public float InkCenterLocalX;
    }
}
