using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Settings overlay: SE/BGM sliders and language dropdown (disabled until localization).</summary>
[DisallowMultipleComponent]
public sealed class SettingsView : MonoBehaviour
{
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider seVolumeSlider;
    [SerializeField] private TMP_Dropdown languageDropdown;
    [SerializeField] private TMP_Text bgmVolumeLabel;
    [SerializeField] private TMP_Text seVolumeLabel;
    [SerializeField] private TMP_Text versionLabel;
    [SerializeField] private TMP_Text userIdLabel;
    [SerializeField] private Button backButton;

    bool _suppressEvents;

    void Awake()
    {
        ConfigureLanguageDropdown();
        WireControls();
        LoadFromSettings();
        UpdateAppInfoLabels();
    }

    void OnDestroy()
    {
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmSliderChanged);
        if (seVolumeSlider != null)
            seVolumeSlider.onValueChanged.RemoveListener(OnSeSliderChanged);
        if (backButton != null)
            backButton.onClick.RemoveListener(Close);
    }

    void ConfigureLanguageDropdown()
    {
        if (languageDropdown == null) return;

        languageDropdown.SetValueWithoutNotify((int)GameSettings.Language);
        languageDropdown.RefreshShownValue();
        languageDropdown.interactable = false;
    }

    void WireControls()
    {
        ConfigureSlider(bgmVolumeSlider);
        ConfigureSlider(seVolumeSlider);

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.AddListener(OnBgmSliderChanged);
        if (seVolumeSlider != null)
            seVolumeSlider.onValueChanged.AddListener(OnSeSliderChanged);
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(Close);
            backButton.onClick.AddListener(Close);
        }
    }

    static void ConfigureSlider(Slider slider)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = GameSettings.VolumeStepCount;
        slider.wholeNumbers = true;
    }

    void LoadFromSettings()
    {
        _suppressEvents = true;
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.SetValueWithoutNotify(GameSettings.GetBgmVolumeStep());
        if (seVolumeSlider != null)
            seVolumeSlider.SetValueWithoutNotify(GameSettings.GetSeVolumeStep());
        UpdateVolumeLabels();
        _suppressEvents = false;
    }

    void UpdateAppInfoLabels()
    {
        if (versionLabel != null)
            versionLabel.text = AppInfo.VersionDisplay;
        if (userIdLabel != null)
            userIdLabel.text = PlayerProfileService.PlayerGuid;
    }

    void OnBgmSliderChanged(float stepValue)
    {
        if (_suppressEvents) return;
        GameSettings.SetBgmVolumeStep(Mathf.RoundToInt(stepValue));
        UpdateVolumeLabels();
    }

    void OnSeSliderChanged(float stepValue)
    {
        if (_suppressEvents) return;
        GameSettings.SetSeVolumeStep(Mathf.RoundToInt(stepValue));
        UpdateVolumeLabels();
    }

    void UpdateVolumeLabels()
    {
        if (bgmVolumeLabel != null)
            bgmVolumeLabel.text = GameSettings.FormatVolumePercent(GameSettings.BgmVolume);
        if (seVolumeLabel != null)
            seVolumeLabel.text = GameSettings.FormatVolumePercent(GameSettings.SeVolume);
    }

    public void Close()
    {
        Destroy(gameObject);
    }
}
