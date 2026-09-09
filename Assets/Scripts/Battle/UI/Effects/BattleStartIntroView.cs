using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

/// <summary>
/// Runtime-built visuals of the battle-start intro: dim background, white flash,
/// diagonal black band and the two outline-only italic captions.
///
/// Caption materials are instantiated from the Font Asset material (never from
/// fontSharedMaterial) so shared TMP materials stay untouched.
/// </summary>
public sealed class BattleStartIntroView
{
    public const string CaptionFontPath = "Assets/TextMesh Pro/Fonts/yumindb SDF.asset";

    private const string JapaneseCaption = "運命をその手で掴み取れ";
    private const string EnglishCaption = "Seize the Destiny, Decide your fate";

    private const float BandAngleDegrees = 13.5f;
    private const float BandWidth = 2400f;
    private const float BandThinHeight = 12f;
    private const float BandOpenHeight = 260f;
    private const float BandOpenSeconds = 0.05f;
    private const float BandCloseSeconds = 0.5f;

    private const float FlashHoldSeconds = 0.04f;
    private const float FlashFadeSeconds = 0.22f;

    // "Transparency" here means see-through ratio (1 = invisible). Alpha = 1 - transparency.
    private const float DimStartTransparency = 0.2f;
    private const float DimMidTransparency = 0.4f;
    private const float DimFinalTransparency = 1f;
    private const float DimPhase1Seconds = 1f;
    private const float DimPhase2Seconds = 0.2f;

    private const float JapaneseFontSize = 84f;
    private const float EnglishFontSize = 44f;
    private const float JapaneseCaptionY = 50f;
    private const float EnglishCaptionY = -35f;
    private const float CaptionOutlineWidth = 0.34f;
    private const float CaptionStartScale = 1.7f;
    private const float CaptionEndScale = 1f;
    private const float JapaneseSpacingTo = 0f;
    private const float EnglishSpacingTo = 4f;
    public const float CaptionTravelSeconds = 2f;
    public const float CaptionHoldSeconds = 0.5f;
    private const float CaptionExitSeconds = 0.25f;
    private const float CaptionWidthMargin = 0.92f;

    /// <summary>Higher makes the first part of the slide-in snappier (80% of the way almost instantly).</summary>
    private const float CaptionTravelExponent = 14f;

    private static TMP_FontAsset _cachedCaptionFont;

    private GameObject _root;
    private Image _dimBackground;
    private Image _band;
    private Image _flash;
    private TMP_Text _japanese;
    private TMP_Text _english;
    private Material _japaneseMaterial;
    private Material _englishMaterial;
    private float _offscreenX;
    private float _exitX;

    public static async Task<TMP_FontAsset> LoadCaptionFontAsync(CancellationToken ct)
    {
        if (_cachedCaptionFont != null)
            return _cachedCaptionFont;

#if UNITY_EDITOR
        _cachedCaptionFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CaptionFontPath);
        if (_cachedCaptionFont != null)
            return _cachedCaptionFont;
#endif

        AsyncOperationHandle<TMP_FontAsset> handle;
        try
        {
            handle = Addressables.LoadAssetAsync<TMP_FontAsset>(CaptionFontPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BattleStartIntroView] Font load failed: {CaptionFontPath} - {ex.Message}");
            return null;
        }

        while (!handle.IsDone)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogWarning($"[BattleStartIntroView] Failed to load font: {CaptionFontPath}");
            return null;
        }

        _cachedCaptionFont = handle.Result;
        return _cachedCaptionFont;
    }

    public static BattleStartIntroView Build(Canvas canvas, TMP_FontAsset font)
    {
        if (canvas == null || font == null)
            return null;

        var view = new BattleStartIntroView();
        var canvasRect = canvas.transform as RectTransform;
        float canvasWidth = canvasRect != null && canvasRect.rect.width > 1f ? canvasRect.rect.width : 1080f;
        float captionWidth = canvasWidth * CaptionWidthMargin;
        view._offscreenX = canvasWidth * 1.6f;
        view._exitX = canvasWidth * 1.9f;

        view._root = new GameObject("BattleStartIntroOverlay");
        view._root.transform.SetParent(canvas.transform, false);
        var rootRect = view._root.AddComponent<RectTransform>();
        StretchFull(rootRect);
        rootRect.SetAsLastSibling();

        view._dimBackground = CreateDimBackground(rootRect);
        view._band = CreateBand(rootRect);
        view._japanese = CreateCaption(
            rootRect, "IntroCaptionJa", font, JapaneseCaption, JapaneseFontSize, JapaneseCaptionY,
            -view._offscreenX, captionWidth);
        view._english = CreateCaption(
            rootRect, "IntroCaptionEn", font, EnglishCaption, EnglishFontSize, EnglishCaptionY,
            view._offscreenX, captionWidth);
        view._flash = CreateFlash(rootRect);
        return view;
    }

    /// <summary>
    /// Ask TMP to bake any missing glyphs into the dynamic atlas before outline materials are applied.
    /// Dynamic additions live in the runtime atlas only; they are not written back to the Font Asset file.
    /// </summary>
    public async Task WarmupCaptionCharactersAsync(CancellationToken ct)
    {
        await WarmupCaptionAsync(_japanese, JapaneseCaption, ct);
        await WarmupCaptionAsync(_english, EnglishCaption, ct);
    }

    /// <summary>Transparent face + colored outline. Call after <see cref="WarmupCaptionCharactersAsync"/>.</summary>
    public void ApplyOutlinedTextMaterials()
    {
        _japaneseMaterial = ApplyOutlineOnly(_japanese, Color.white);
        _englishMaterial = ApplyOutlineOnly(_english, Color.red);
    }

    public void PlayDimBackground(CancellationToken ct)
    {
        _ = DimBackgroundAsync(ct);
    }

    public void PlayWhiteFlash(CancellationToken ct)
    {
        _ = FlashAsync(ct);
    }

    public async Task OpenBandAsync(CancellationToken ct)
    {
        if (_band != null)
        {
            _band.color = Color.black;
            _band.enabled = true;
        }

        await AnimateAsync(BandOpenSeconds, t => SetBandHeight(
            Mathf.Lerp(BandThinHeight, BandOpenHeight, EaseOutCubic(t))), ct);
    }

    public async Task CloseBandAsync(CancellationToken ct)
    {
        await AnimateAsync(BandCloseSeconds, t =>
        {
            float eased = EaseInCubic(t);
            SetBandHeight(Mathf.Lerp(BandOpenHeight, BandThinHeight * 0.25f, eased));
            if (_band != null)
                _band.color = new Color(0f, 0f, 0f, 1f - eased);
        }, ct);

        if (_band != null)
            _band.gameObject.SetActive(false);
    }

    public Task SlideInCaptionsAsync(CancellationToken ct)
    {
        return Task.WhenAll(
            SlideInCaptionAsync(_japanese, -_offscreenX, JapaneseSpacingTo, ct),
            SlideInCaptionAsync(_english, _offscreenX, EnglishSpacingTo, ct));
    }

    public Task ExitCaptionsAsync(CancellationToken ct)
    {
        return Task.WhenAll(
            ExitCaptionAsync(_japanese, _exitX, ct),
            ExitCaptionAsync(_english, -_exitX, ct));
    }

    public void Dispose()
    {
        RestoreCaptionMaterial(_japanese, ref _japaneseMaterial);
        RestoreCaptionMaterial(_english, ref _englishMaterial);

        if (_root != null)
            UnityEngine.Object.Destroy(_root);
        _root = null;
        _dimBackground = null;
        _band = null;
        _flash = null;
        _japanese = null;
        _english = null;
    }

    private async Task DimBackgroundAsync(CancellationToken ct)
    {
        if (_dimBackground == null)
            return;

        try
        {
            _dimBackground.enabled = true;
            SetDimBackgroundTransparency(DimStartTransparency);

            await AnimateAsync(DimPhase1Seconds, t =>
            {
                if (_dimBackground != null)
                    SetDimBackgroundTransparency(Mathf.Lerp(DimStartTransparency, DimMidTransparency, t));
            }, ct);

            await AnimateAsync(DimPhase2Seconds, t =>
            {
                if (_dimBackground != null)
                    SetDimBackgroundTransparency(Mathf.Lerp(DimMidTransparency, DimFinalTransparency, t));
            }, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_dimBackground != null)
            _dimBackground.enabled = false;
    }

    private void SetDimBackgroundTransparency(float transparency)
    {
        if (_dimBackground == null)
            return;
        _dimBackground.color = new Color(0f, 0f, 0f, TransparencyToAlpha(transparency));
    }

    private static float TransparencyToAlpha(float transparency) => 1f - Mathf.Clamp01(transparency);

    private async Task FlashAsync(CancellationToken ct)
    {
        if (_flash == null)
            return;

        try
        {
            _flash.color = Color.white;
            _flash.enabled = true;
            await AnimateAsync(FlashHoldSeconds, _ => { }, ct);
            await AnimateAsync(FlashFadeSeconds, t =>
            {
                if (_flash != null)
                    _flash.color = new Color(1f, 1f, 1f, 1f - EaseOutCubic(t));
            }, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_flash != null)
            _flash.gameObject.SetActive(false);
    }

    private async Task SlideInCaptionAsync(TMP_Text caption, float fromX, float spacingTo, CancellationToken ct)
    {
        if (caption == null)
            return;

        var rect = caption.rectTransform;
        float baseY = rect.anchoredPosition.y;

        await AnimateAsync(CaptionTravelSeconds, t =>
        {
            if (caption == null)
                return;
            float travel = EaseOutExponential(t, CaptionTravelExponent);
            float shape = EaseOutCubic(t);
            rect.anchoredPosition = new Vector2(Mathf.Lerp(fromX, 0f, travel), baseY);
            float scale = Mathf.Lerp(CaptionStartScale, CaptionEndScale, shape);
            rect.localScale = new Vector3(scale, scale, 1f);
            caption.characterSpacing = Mathf.Lerp(0f, spacingTo, t);
        }, ct);
    }

    private async Task ExitCaptionAsync(TMP_Text caption, float targetX, CancellationToken ct)
    {
        if (caption == null)
            return;

        var rect = caption.rectTransform;
        float fromX = rect.anchoredPosition.x;
        float baseY = rect.anchoredPosition.y;

        await AnimateAsync(CaptionExitSeconds, t =>
        {
            if (caption != null)
                rect.anchoredPosition = new Vector2(Mathf.Lerp(fromX, targetX, EaseInCubic(t)), baseY);
        }, ct);

        if (caption != null)
            caption.gameObject.SetActive(false);
    }

    private void SetBandHeight(float height)
    {
        if (_band == null)
            return;
        var rect = _band.rectTransform;
        rect.sizeDelta = new Vector2(BandWidth, height);
    }

    private static Image CreateDimBackground(RectTransform parent)
    {
        var go = new GameObject("IntroDimBackground");
        go.transform.SetParent(parent, false);
        StretchFull(go.AddComponent<RectTransform>());
        go.transform.SetAsFirstSibling();

        var image = go.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, TransparencyToAlpha(DimStartTransparency));
        image.raycastTarget = false;
        image.enabled = true;
        return image;
    }

    private static Image CreateBand(RectTransform parent)
    {
        var go = new GameObject("IntroDiagonalBand");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(BandWidth, BandThinHeight);
        rect.localRotation = Quaternion.Euler(0f, 0f, BandAngleDegrees);

        var image = go.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private static Image CreateFlash(RectTransform parent)
    {
        var go = new GameObject("IntroWhiteFlash");
        go.transform.SetParent(parent, false);
        StretchFull(go.AddComponent<RectTransform>());
        var image = go.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private static TMP_Text CreateCaption(
        RectTransform parent,
        string name,
        TMP_FontAsset font,
        string content,
        float fontSize,
        float y,
        float startX,
        float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 260f);
        rect.anchoredPosition = new Vector2(startX, y);
        rect.localRotation = Quaternion.identity;
        rect.localScale = new Vector3(CaptionStartScale, CaptionStartScale, 1f);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Italic | FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.color = Color.white;
        text.characterSpacing = 0f;
        return text;
    }

    private static async Task WarmupCaptionAsync(TMP_Text caption, string content, CancellationToken ct)
    {
        if (caption == null || caption.font == null)
            return;

        caption.text = content;
        caption.font.TryAddCharacters(content, out string missing);
        if (!string.IsNullOrEmpty(missing))
        {
            Debug.LogWarning(
                $"[BattleStartIntroView] Font '{caption.font.name}' is missing glyphs: {missing}. " +
                "Add them in Font Asset Creator and regenerate the atlas, or ensure the source TTF is included in the build.");
        }

        caption.ForceMeshUpdate(true, true);
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
    }

    private static Material ApplyOutlineOnly(TMP_Text caption, Color outlineColor)
    {
        if (caption == null || caption.font == null || !caption.gameObject.activeInHierarchy)
            return null;

        // Font Asset material only: fontSharedMaterial is already an instance once TMP touched it.
        var sharedMaterial = caption.font.material;
        if (sharedMaterial == null)
            return null;

        var instance = UnityEngine.Object.Instantiate(sharedMaterial);
        caption.fontSharedMaterial = sharedMaterial;
        caption.fontMaterial = instance;

        if (instance.HasProperty(ShaderUtilities.ID_FaceColor))
            instance.SetColor(ShaderUtilities.ID_FaceColor, new Color(1f, 1f, 1f, 0f));
        if (instance.HasProperty(ShaderUtilities.ID_OutlineColor))
            instance.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
        if (instance.HasProperty(ShaderUtilities.ID_OutlineWidth))
            instance.SetFloat(ShaderUtilities.ID_OutlineWidth, CaptionOutlineWidth);

        caption.UpdateMeshPadding();
        caption.ForceMeshUpdate();
        return instance;
    }

    private static void RestoreCaptionMaterial(TMP_Text caption, ref Material instance)
    {
        if (instance == null)
            return;

        if (caption != null && caption.font != null && caption.font.material != null)
            caption.fontSharedMaterial = caption.font.material;

        var toDestroy = instance;
        instance = null;
        UnityEngine.Object.Destroy(toDestroy);
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static async Task AnimateAsync(float durationSeconds, Action<float> onProgress, CancellationToken ct)
    {
        if (durationSeconds <= 0f)
        {
            onProgress(1f);
            return;
        }

        onProgress(0f);
        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            onProgress(Mathf.Clamp01(elapsed / durationSeconds));
        }

        onProgress(1f);
    }

    private static float EaseOutExponential(float t, float exponent)
    {
        float scale = 1f - Mathf.Pow(2f, -exponent);
        if (scale <= Mathf.Epsilon)
            return t;
        return (1f - Mathf.Pow(2f, -exponent * t)) / scale;
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    private static float EaseInCubic(float t) => t * t * t;
}
