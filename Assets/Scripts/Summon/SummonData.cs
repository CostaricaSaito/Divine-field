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

    [TextArea(2, 4)]
    public string description;

    [TextArea(2, 4)]
    public string passiveSkill;

    [TextArea(2, 4)]
    public string activeSkill;

    public Sprite characterSprite;
    public Sprite backgroundSprite;
    public Sprite foregroundSprite;
    public Sprite summonIcon; // ← これ追加してね！
    public AudioClip summonSE;


    [Header("召喚獣選択画面でのテキストスタイル")]
    public SummonTextStyle nameStyle;
    public SummonTextStyle descriptionStyle;
    public SummonTextStyle passiveSkillStyle;
    public SummonTextStyle activeSkillStyle;

    [Header("スペシャルスキル")]
    public string specialSkillName;

    [TextArea(2, 4)]
    public string specialSkillDescription;

    public SummonTextStyle popupSkillNameStyle;
    public SummonTextStyle popupSkillDescStyle;

    public Sprite specialSkillCutInSprite;  // 全画面演出用イラスト
    public AudioClip specialSkillSE;        // 発動時のSE（任意）

    /// <summary>
    /// 顕現スキルの効果をここに書く（例：イフリート → 敵に30ダメージ）
    /// </summary>
    public void ActivateSpecialSkill(PlayerStatus self, PlayerStatus opponent)
    {
        if (summonName == "イフリート")
        {
            opponent.TakeDamage(30);
            Debug.Log("イフリートの顕現スキルが正常に発動！敵に30ダメージ！");
        }

        // 他の召喚獣はここに追加していく
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