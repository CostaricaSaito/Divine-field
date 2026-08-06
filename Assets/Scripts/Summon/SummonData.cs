using UnityEngine;
using TMPro;

[System.Serializable]
public class SummonTextStyle
{
    public Color fontColor = Color.white;
    public bool useGradient = false;
    public Color topColor = Color.white;
    public Color bottomColor = Color.white;
    public Color outlineColor = Color.black;
    [Range(0, 1)] public float outlineThickness = 0.0f;
    [Range(-1, 1)] public float faceDilate = 0.0f;
}

[CreateAssetMenu(fileName = "NewSummonData", menuName = "DivineField/SummonData")]
public class SummonData : ScriptableObject
{
    [TextArea(2, 4)]
    public string summonName;

    [Tooltip("NewSummon subtitle (e.g. Flame Beast).")]
    public string summonNameSubtitle;

    [Tooltip("NewSummon English name.")]
    public string summonNameEng;

    [Tooltip("Stable id for stats/network (e.g. garuda). Empty uses asset name.")]
    [SerializeField]
    private string summonId;

    /// <summary>Stable summon id for stats/network.</summary>
    public string StableSummonId =>
        string.IsNullOrWhiteSpace(summonId) ? name : summonId.Trim();

    [TextArea(2, 4)]
    public string description;

    [Header("Skills")]
    public string passiveSkillName;

    [TextArea(2, 4)]
    public string passiveSkillDescription;

    public string activeSkillName;

    [TextArea(2, 4)]
    public string activeSkillDescription;

    public Sprite characterSprite;
    public Sprite backgroundSprite;
    public Sprite foregroundSprite;

    [Tooltip("Battle status icon. Falls back to characterSprite.")]
    public Sprite summonIcon;
    public AudioClip summonSE;

    /// <summary>
    /// Battle status icon sprite. Uses summonIcon if set, otherwise characterSprite.
    /// </summary>
    public Sprite GetBattleStatusIconSprite()
    {
        return summonIcon != null ? summonIcon : characterSprite;
    }

    [Header("Text Style")]
    [Tooltip("Shared by NewSummon name/subtitle/skill names and battle popup skill name.")]
    public SummonTextStyle textStyle;

    [Header("Special skill")]
    public string specialSkillName;

    [TextArea(2, 4)]
    public string specialSkillDescription;

    [Tooltip("Description style for battle special skill popup.")]
    public SummonTextStyle popupSkillDescStyle;

    public Sprite specialSkillCutInSprite;
    public AudioClip specialSkillSE;

    [Header("Manifestation")]
    [Tooltip("Card resolved on manifestation. Empty disables manifestation button.")]
    public CardData manifestationCard;

    [Header("Passive blessing (runtime)")]
    [SerializeField]
    [Tooltip("Auto: resolve by asset name. None disables. Explicit modes available.")]
    private SummonPassiveBlessingMode passiveBlessingMode = SummonPassiveBlessingMode.AutoByAssetName;

    /// <summary>
    /// Returns blessing instance for the configured mode.
    /// </summary>
    public SummonPassiveBlessing GetEffectivePassiveBlessing()
    {
        return SummonPassiveBlessingFallback.ResolveMode(passiveBlessingMode, name);
    }

    /// <summary>
    /// Whether Garuda lifecycle rules apply.
    /// </summary>
    public bool IsGarudaLifecycle()
    {
        if (passiveBlessingMode == SummonPassiveBlessingMode.Garuda) return true;
        return name == "Garuda";
    }

    /// <summary>
    /// Whether Diabolos Dark Preparation opening applies.
    /// </summary>
    public bool IsDiabolosDarkPreparation()
    {
        if (passiveBlessingMode == SummonPassiveBlessingMode.Diabolos) return true;
        return name == "Diabolos";
    }

    /// <summary>
    /// Whether Indra turn-end hand destroy lifecycle applies.
    /// </summary>
    public bool IsIndraLifecycle()
    {
        if (passiveBlessingMode == SummonPassiveBlessingMode.Indra) return true;
        return name == "Indra" || StableSummonId == "indra";
    }

    /// <summary>
    /// Whether Shiva direct-attack freeze passive applies.
    /// </summary>
    public bool IsShivaDirectAttackFreeze()
    {
        if (passiveBlessingMode == SummonPassiveBlessingMode.Shiva) return true;
        return name == "Siva" || StableSummonId == "siva";
    }

    public void ApplyStyleTo(TMPro.TMP_Text text, SummonTextStyle style)
    {
        if (text == null || style == null) return;

        text.color = style.fontColor;

        text.enableVertexGradient = style.useGradient;
        if (style.useGradient)
        {
            text.colorGradient = new TMPro.VertexGradient(
                style.topColor, style.topColor,
                style.bottomColor, style.bottomColor
            );
        }

        var mat = text.fontMaterial;
        if (mat != null)
        {
            mat.SetFloat(TMPro.ShaderUtilities.ID_OutlineWidth, style.outlineThickness);
            mat.SetColor(TMPro.ShaderUtilities.ID_OutlineColor, style.outlineColor);
            mat.SetFloat(TMPro.ShaderUtilities.ID_FaceDilate, style.faceDilate);
        }
    }
}
