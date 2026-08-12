using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle scene top-left control: CPU exits immediately; online shows LeavingCaution first.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class ReturnToTopButton : MonoBehaviour
{
    [SerializeField] private string clickSeAddress = "Assets/SE/決定ボタンを押す3.mp3";

    private Button _button;
    private Image _image;

    void Awake()
    {
        _image = GetComponent<Image>();
        _button = GetComponent<Button>();
        if (_button == null)
        {
            _button = gameObject.AddComponent<Button>();
            _button.targetGraphic = _image;
        }

        _button.transition = Selectable.Transition.ColorTint;
        _button.targetGraphic = _image;

        var colors = _button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        _button.colors = colors;

        _button.onClick.AddListener(OnClicked);
    }

    void Update()
    {
        if (_button == null) return;

        bool disable = ShouldDisableButton();
        if (_button.interactable == disable)
            _button.interactable = !disable;
    }

    void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnClicked);
    }

    private bool ShouldDisableButton()
    {
        if (BattleManager.I == null) return false;

        if (BattleManager.I.IsGameEndTriggered || BattleManager.I.IsBattleExitInProgress)
            return true;

        var exit = BattleManager.I.BattleExit;
        return exit != null && exit.IsLeavingCautionOpen;
    }

    private void OnClicked()
    {
        if (BattleManager.I == null) return;
        if (ShouldDisableButton()) return;

        if (!string.IsNullOrEmpty(clickSeAddress))
            SoundEffectPlayer.I?.Play(clickSeAddress);

        BattleManager.I.RequestReturnToTop();
    }
}
