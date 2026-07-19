using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// NewSummon scene viewer: page through catalog summons, refresh texts/styles,
/// play character/background transition animation, persist on confirm.
/// </summary>
public class NewSummonViewer : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private SummonCatalog catalog;

    [Header("Texts")]
    [SerializeField] private TMP_Text summonNameText;
    [SerializeField] private TMP_Text summonNameSubtitleText;
    [SerializeField] private TMP_Text summonNameEngText;
    [SerializeField] private TMP_Text passiveNameText;
    [SerializeField] private TMP_Text passiveDescText;
    [SerializeField] private TMP_Text activeNameText;
    [SerializeField] private TMP_Text activeDescText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Visuals")]
    [Tooltip("Prototype SummonImage (kept inactive). Clones are spawned for each page.")]
    [SerializeField] private Image summonImageTemplate;
    [SerializeField] private Image summonImageBlack;
    [SerializeField] private Image backgroundImage;

    [Header("Buttons")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmButtonLabel;

    [Header("Confirm")]
    [SerializeField] private string confirmSeAddress = "Assets/SE/普通カード.mp3";
    [SerializeField] private string confirmLabelIdle = "契約";
    [SerializeField] private string confirmLabelContracted = "現在契約中";

    [Header("Summon Image Animation")]
    [SerializeField] private float enterDuration = 0.2f;
    [SerializeField] private float shadowMoveDuration = 0.5f;
    [SerializeField] private float enterFromLeftOffset = 1200f;
    [SerializeField] private Vector2 shadowOffset = new Vector2(64f, -30f);
    [SerializeField] private LeanTweenType enterEase = LeanTweenType.easeOutCubic;
    [SerializeField] private LeanTweenType shadowEase = LeanTweenType.easeOutCubic;

    [Header("Background Fade")]
    [SerializeField] private float backgroundFadeDuration = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float backgroundTargetAlpha = 0.376f;
    [SerializeField] private LeanTweenType backgroundEase = LeanTweenType.easeOutQuad;

    private SummonData[] _summons;
    private int _currentIndex;
    private bool _stylesInstantiated;

    private Vector2 _summonRestAnchoredPos;
    private int _summonSiblingIndex;
    private Transform _summonParent;
    private RectTransform _activeSummonRt;
    private int _enterTweenId = -1;
    private int _shadowTweenId = -1;
    private int _backgroundTweenId = -1;
    private int _visualGeneration;

    public int CurrentIndex => _currentIndex;

    void Awake()
    {
        EnsureButton(ref confirmButton, "SetKey");
        EnsureButton(ref nextButton, "NextButton");
        EnsureButton(ref previousButton, "PreviousButton");
        EnsureConfirmLabel();

        if (nextButton != null) nextButton.onClick.AddListener(Next);
        if (previousButton != null) previousButton.onClick.AddListener(Previous);
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);

        CacheSummonTemplate();
    }

    void Start()
    {
        ResolveSummonList();
        if (_summons == null || _summons.Length == 0)
        {
            Debug.LogError("[NewSummonViewer] Summon list is empty. Check SummonCatalog.");
            return;
        }

        _currentIndex = 0;
        if (SummonSelectionManager.I != null)
            _currentIndex = Mathf.Clamp(SummonSelectionManager.I.SelectedIndex, 0, _summons.Length - 1);

        UpdateDisplay(playSe: false, playVisual: true);
    }

    void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(Next);
        if (previousButton != null) previousButton.onClick.RemoveListener(Previous);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
        _visualGeneration++;
        CancelVisualTweens();
        DestroyActiveSummonImage();
    }

    public void Next()
    {
        if (_summons == null || _summons.Length == 0) return;
        _currentIndex = (_currentIndex + 1) % _summons.Length;
        UpdateDisplay(playSe: true, playVisual: true);
    }

    public void Previous()
    {
        if (_summons == null || _summons.Length == 0) return;
        _currentIndex = (_currentIndex - 1 + _summons.Length) % _summons.Length;
        UpdateDisplay(playSe: true, playVisual: true);
    }

    public void Confirm()
    {
        if (_summons == null || _summons.Length == 0) return;

        if (SummonSelectionManager.I != null)
            SummonSelectionManager.I.SetSelectedIndex(_currentIndex);
        else
            Debug.LogWarning("[NewSummonViewer] SummonSelectionManager missing; cannot persist selection.");

        if (!string.IsNullOrEmpty(confirmSeAddress))
            SoundEffectPlayer.I?.Play(confirmSeAddress);

        UpdateDisplay(playSe: false, playVisual: false);
    }

    public int GetSelectedSummonIndex() => _currentIndex;

    void CacheSummonTemplate()
    {
        if (summonImageTemplate == null) return;

        var rt = summonImageTemplate.rectTransform;
        _summonRestAnchoredPos = rt.anchoredPosition;
        _summonSiblingIndex = rt.GetSiblingIndex();
        _summonParent = rt.parent;
        summonImageTemplate.gameObject.SetActive(false);

        if (summonImageBlack != null)
            summonImageBlack.gameObject.SetActive(false);
    }

    void ResolveSummonList()
    {
        if (catalog == null)
            catalog = Resources.Load<SummonCatalog>("Summons/SummonCatalog");

        if (catalog != null && catalog.Count > 0)
        {
            _summons = catalog.ToArray();
            return;
        }

        if (SummonSelectionManager.I != null)
        {
            _summons = SummonSelectionManager.I.GetAllSummonData();
            return;
        }

        _summons = Resources.LoadAll<SummonData>("Summons");
    }

    void UpdateDisplay(bool playSe, bool playVisual)
    {
        var data = _summons[_currentIndex];
        if (data == null) return;

        UpdateTexts(data);

        if (playVisual)
            PlayVisualTransition(data);
        else
            RefreshConfirmButton();

        if (playSe && data.summonSE != null && SoundEffectPlayer.I != null)
            SoundEffectPlayer.I.PlayReplace(data.summonSE);
    }

    void UpdateTexts(SummonData data)
    {
        SetText(summonNameText, data.summonName);
        SetText(summonNameSubtitleText, data.summonNameSubtitle);
        SetText(summonNameEngText, data.summonNameEng);
        SetText(passiveNameText, data.passiveSkillName);
        SetText(passiveDescText, data.passiveSkillDescription);
        SetText(activeNameText, data.activeSkillName);
        SetText(activeDescText, data.activeSkillDescription);
        SetText(descriptionText, data.description);

        EnsureStyleMaterials();
        ApplyStyle(summonNameText, data.textStyle);
        ApplyStyle(summonNameSubtitleText, data.textStyle);
        ApplyStyle(passiveNameText, data.textStyle);
        ApplyStyle(activeNameText, data.textStyle);

        RefreshConfirmButton();
    }

    void RefreshConfirmButton()
    {
        bool alreadySelected = SummonSelectionManager.I != null
            && SummonSelectionManager.I.SelectedIndex == _currentIndex;

        if (confirmButton != null)
            confirmButton.interactable = !alreadySelected;

        if (confirmButtonLabel != null)
            confirmButtonLabel.text = alreadySelected ? confirmLabelContracted : confirmLabelIdle;
    }

    void PlayVisualTransition(SummonData data)
    {
        _visualGeneration++;
        CancelVisualTweens();
        DestroyActiveSummonImage();

        int generation = _visualGeneration;

        if (summonImageBlack != null)
        {
            LeanTween.cancel(summonImageBlack.gameObject);
            summonImageBlack.gameObject.SetActive(false);
        }

        PlayBackgroundFade(data, generation);
        PlaySummonEnter(data, generation);
    }

    void PlaySummonEnter(SummonData data, int generation)
    {
        if (summonImageTemplate == null || _summonParent == null)
            return;

        var go = Instantiate(summonImageTemplate.gameObject, _summonParent);
        go.name = "SummonImage";
        go.SetActive(true);

        _activeSummonRt = go.GetComponent<RectTransform>();
        _activeSummonRt.SetSiblingIndex(_summonSiblingIndex);
        PlaceShadowBehindSummon();

        var image = go.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = data.characterSprite;
            image.color = Color.white;
        }

        Vector2 rest = _summonRestAnchoredPos;
        Vector2 start = rest + Vector2.left * enterFromLeftOffset;
        _activeSummonRt.anchoredPosition = start;

        var capturedRt = _activeSummonRt;
        _enterTweenId = LeanTween.value(go, start, rest, Mathf.Max(0.01f, enterDuration))
            .setEase(enterEase)
            .setOnUpdate((Vector2 p) =>
            {
                if (capturedRt != null)
                    capturedRt.anchoredPosition = p;
            })
            .setOnComplete(() =>
            {
                _enterTweenId = -1;
                if (generation != _visualGeneration) return;
                if (capturedRt != null)
                    capturedRt.anchoredPosition = rest;
                PlayShadowSlide(data, rest, generation);
            })
            .id;
    }

    void PlayShadowSlide(SummonData data, Vector2 from, int generation)
    {
        if (summonImageBlack == null) return;

        summonImageBlack.sprite = data.characterSprite;
        summonImageBlack.color = Color.black;
        summonImageBlack.rectTransform.anchoredPosition = from;
        summonImageBlack.gameObject.SetActive(true);
        PlaceShadowBehindSummon();

        Vector2 to = from + shadowOffset;
        var blackRt = summonImageBlack.rectTransform;
        _shadowTweenId = LeanTween.value(summonImageBlack.gameObject, from, to, Mathf.Max(0.01f, shadowMoveDuration))
            .setEase(shadowEase)
            .setOnUpdate((Vector2 p) =>
            {
                if (blackRt != null)
                    blackRt.anchoredPosition = p;
            })
            .setOnComplete(() =>
            {
                _shadowTweenId = -1;
                if (generation != _visualGeneration) return;
                if (blackRt != null)
                    blackRt.anchoredPosition = to;
            })
            .id;
    }

    void PlayBackgroundFade(SummonData data, int generation)
    {
        if (backgroundImage == null) return;

        backgroundImage.sprite = data.backgroundSprite;
        var c = backgroundImage.color;
        c.a = 0f;
        backgroundImage.color = c;

        float target = backgroundTargetAlpha;
        _backgroundTweenId = LeanTween.value(backgroundImage.gameObject, 0f, target, Mathf.Max(0.01f, backgroundFadeDuration))
            .setEase(backgroundEase)
            .setOnUpdate((float a) =>
            {
                if (generation != _visualGeneration || backgroundImage == null) return;
                var col = backgroundImage.color;
                col.a = a;
                backgroundImage.color = col;
            })
            .setOnComplete(() =>
            {
                _backgroundTweenId = -1;
                if (generation != _visualGeneration || backgroundImage == null) return;
                var col = backgroundImage.color;
                col.a = target;
                backgroundImage.color = col;
            })
            .id;
    }

    void CancelVisualTweens()
    {
        // Invalidate in-flight callbacks without advancing the play generation twice.
        // PlayVisualTransition bumps _visualGeneration when starting a new play.
        if (_enterTweenId >= 0)
        {
            LeanTween.cancel(_enterTweenId);
            _enterTweenId = -1;
        }

        if (_shadowTweenId >= 0)
        {
            LeanTween.cancel(_shadowTweenId);
            _shadowTweenId = -1;
        }

        if (_backgroundTweenId >= 0)
        {
            LeanTween.cancel(_backgroundTweenId);
            _backgroundTweenId = -1;
        }

        if (_activeSummonRt != null)
            LeanTween.cancel(_activeSummonRt.gameObject);
        if (summonImageBlack != null)
            LeanTween.cancel(summonImageBlack.gameObject);
        if (backgroundImage != null)
            LeanTween.cancel(backgroundImage.gameObject);
    }

    void DestroyActiveSummonImage()
    {
        if (_activeSummonRt == null) return;
        Destroy(_activeSummonRt.gameObject);
        _activeSummonRt = null;
    }

    /// <summary>
    /// UI hierarchy: lower sibling index is drawn below.
    /// Always places SummonImageBlack immediately under the active SummonImage.
    /// </summary>
    void PlaceShadowBehindSummon()
    {
        if (summonImageBlack == null || _activeSummonRt == null) return;

        Transform black = summonImageBlack.transform;
        Transform image = _activeSummonRt;

        // Keep summon at its intended slot first.
        image.SetSiblingIndex(_summonSiblingIndex);

        int imageIndex = image.GetSiblingIndex();
        black.SetSiblingIndex(imageIndex);

        // Moving black forward in the hierarchy can land it above the summon.
        // If that happened, move it one step back to sit strictly under the image.
        if (black.GetSiblingIndex() > image.GetSiblingIndex())
            black.SetSiblingIndex(image.GetSiblingIndex());
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text == null) return;
        text.text = string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\n", "\n");
    }

    void EnsureStyleMaterials()
    {
        if (_stylesInstantiated) return;
        InstantiateMaterial(summonNameText);
        InstantiateMaterial(summonNameSubtitleText);
        InstantiateMaterial(passiveNameText);
        InstantiateMaterial(activeNameText);
        _stylesInstantiated = true;
    }

    static void InstantiateMaterial(TMP_Text text)
    {
        if (text == null || text.fontMaterial == null) return;
        text.fontMaterial = Instantiate(text.fontMaterial);
    }

    static void ApplyStyle(TMP_Text text, SummonTextStyle style)
    {
        if (text == null || style == null) return;

        text.color = style.fontColor;
        text.enableVertexGradient = style.useGradient;
        if (style.useGradient)
        {
            text.colorGradient = new VertexGradient(
                style.topColor, style.topColor,
                style.bottomColor, style.bottomColor);
        }

        if (text.fontMaterial == null) return;
        text.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, style.outlineColor);
        text.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, style.outlineThickness);
        text.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, style.faceDilate);
    }

    static void EnsureButton(ref Button button, string objectName)
    {
        if (button != null) return;

        var go = GameObject.Find(objectName);
        if (go == null) return;

        button = go.GetComponent<Button>();
        if (button == null)
            button = go.AddComponent<Button>();

        if (button.targetGraphic == null)
        {
            var graphic = go.GetComponent<Graphic>();
            if (graphic != null)
                button.targetGraphic = graphic;
        }
    }

    void EnsureConfirmLabel()
    {
        if (confirmButtonLabel != null) return;

        if (confirmButton != null)
            confirmButtonLabel = confirmButton.GetComponentInChildren<TMP_Text>(true);

        if (confirmButtonLabel == null)
        {
            var go = GameObject.Find("SetKeyText");
            if (go != null)
                confirmButtonLabel = go.GetComponent<TMP_Text>();
        }
    }
}
