using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds <c>Resources/Prefab/StatusEffectDetail</c> while an ailment icon is held.
/// </summary>
public sealed class StatusEffectDetailPopupView : MonoBehaviour
{
    private TMP_Text _nameText;
    private TMP_Text _descText;
    private TMP_Text _severityText;
    private Image _severityBg;
    private Image _iconImage;
    private bool _hierarchyBound;

    public void EnsureHierarchyBound()
    {
        if (_hierarchyBound) return;
        _hierarchyBound = true;

        _nameText = FindTmp("StatusEffectDetailNameText");
        _descText = FindTmp("StatusEffectDetailDescText");
        _severityText = FindTmp("StatusEffectSeverityText");
        _severityBg = FindImage("StatusEffectSeverityBG");
        _iconImage = FindImage("StatusEffectIcon");

        DisableRaycastsExceptRoot();
    }

    public void Refresh(StatusEffectType type, Sprite icon, StatusEffectDetailSettings detailSettings)
    {
        EnsureHierarchyBound();
        if (detailSettings == null)
            detailSettings = StatusEffectDetailSettings.GetRuntimeFallback();

        var popupStyle = StatusEffectPresentation.GetPopupStyle(type);
        if (_nameText != null)
        {
            _nameText.text = StatusEffectPresentation.GetDisplayName(type);
            _nameText.color = popupStyle.textColor;
            TmpOutlineMaterialHelper.ApplyBannerOutlineBeforeLayout(_nameText, popupStyle.outlineColor);
        }

        if (_descText != null)
            _descText.text = detailSettings.GetDescription(type);

        var category = StatusEffectRules.GetCategory(type);
        if (_severityText != null)
            _severityText.text = StatusEffectDetailSettings.GetSeverityLabel(category);

        if (_severityBg != null)
            _severityBg.color = detailSettings.GetSeverityBackground(category);

        if (_iconImage != null)
        {
            _iconImage.sprite = icon;
            _iconImage.enabled = icon != null;
            _iconImage.preserveAspect = true;
        }
    }

    private TMP_Text FindTmp(string objectName)
    {
        var t = transform.Find(objectName);
        if (t != null) return t.GetComponent<TMP_Text>();

        foreach (var tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.gameObject.name == objectName) return tmp;
        }

        return null;
    }

    private Image FindImage(string objectName)
    {
        var t = transform.Find(objectName);
        if (t != null) return t.GetComponent<Image>();

        foreach (var img in GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject.name == objectName) return img;
        }

        return null;
    }

    private void DisableRaycastsExceptRoot()
    {
        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.gameObject == gameObject) continue;
            graphic.raycastTarget = false;
        }
    }
}
