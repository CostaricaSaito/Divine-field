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

    [Tooltip("統計・オンライン連携用の不変ID（例: garuda）。空のときはこのアセットの Unity 名（ファイル名）を使います。")]
    [SerializeField]
    private string summonId;

    /// <summary>統計・ネットワークで用いる不変の召喚獣ID。</summary>
    public string StableSummonId =>
        string.IsNullOrWhiteSpace(summonId) ? name : summonId.Trim();

    [TextArea(2, 4)]
    public string description;

    [TextArea(2, 4)]
    public string passiveSkill;

    [TextArea(2, 4)]
    public string activeSkill;

    public Sprite characterSprite;
    public Sprite backgroundSprite;
    public Sprite foregroundSprite;

    [Tooltip("戦闘中の PlayerSummon / EnemySummon 用の正方形アイコン（Images/01_召喚獣アイコン用 など）。未設定時は Character Sprite を使用します。")]
    public Sprite summonIcon;
    public AudioClip summonSE;

    /// <summary>
    /// 戦闘ステータス行のアイコン。インスペクターで <see cref="summonIcon"/> を指定していればそれを、無ければ <see cref="characterSprite"/> を返す。
    /// </summary>
    public Sprite GetBattleStatusIconSprite()
    {
        return summonIcon != null ? summonIcon : characterSprite;
    }


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

    [Header("顕現スキル")]
    [Tooltip("窮地から発動時に表示・自動解決する顕現カード。未設定なら顕現ボタンは無効扱い。")]
    public CardData manifestationCard;

    [Header("加護（パッシブ・ランタイム）")]
    [SerializeField]
    [Tooltip("Auto: アセット名（Ifrit 等）で加護を決定。None で無効、Ifrit で明示。")]
    private SummonPassiveBlessingMode passiveBlessingMode = SummonPassiveBlessingMode.AutoByAssetName;

    /// <summary>
    /// <see cref="passiveBlessingMode"/> に応じた加護インスタンスを返す。
    /// </summary>
    public SummonPassiveBlessing GetEffectivePassiveBlessing()
    {
        return SummonPassiveBlessingFallback.ResolveMode(passiveBlessingMode, name);
    }

    /// <summary>
    /// ガルーダの開始時＋5nターン終了ドロー等のライフサイクル対象か（攻撃加護の有無とは独立）。
    /// </summary>
    public bool IsGarudaLifecycle()
    {
        if (passiveBlessingMode == SummonPassiveBlessingMode.Garuda) return true;
        return name == "Garuda";
    }

    /// <summary>
    /// ディアボロス「ダークプリパレーション」：開幕手札の1枚目を闇属性から抽選。メッセージは配布完了後・表向け前。
    /// 戦闘中の数値加護は <see cref="GetEffectivePassiveBlessing"/> とは別（ガルーダと同様ライフサイクル側）。
    /// </summary>
    public bool IsDiabolosDarkPreparation()
    {
        if (passiveBlessingMode == SummonPassiveBlessingMode.Diabolos) return true;
        return name == "Diabolos";
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
