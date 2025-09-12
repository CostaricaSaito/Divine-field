using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SummonRingViewer : MonoBehaviour
{
    [Header("召喚獣情報")]
    public SummonData[] summonDataList;

    [Header("UI参照")]
    public Image summonImage;
    public Image backgroundImage;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public TMP_Text passiveSkillText;
    public TMP_Text activeSkillText;
    public GameObject highlightFrame;
    public GameObject contractText;
    public Button selectButton;

    private int currentIndex = 0;

    void Start()
    {
        // 自動でSummonSelectionManagerからデータを取得
        summonDataList = SummonSelectionManager.I.GetAllSummonData();

        // 念のためデータがnullでないかチェック
        if (summonDataList == null || summonDataList.Length == 0)
        {
            Debug.LogError("SummonDataが見つかりません。SummonSelectionManagerの読み込みに問題があります。");
            return;
        }

        currentIndex = SummonSelectionManager.I.SelectedIndex; // 前回選択状態から再開
        UpdateDisplay();
    }

    public void Next()
    {
        currentIndex = (currentIndex + 1) % summonDataList.Length;
        UpdateDisplay();
    }

    public void Previous()
    {
        currentIndex = (currentIndex - 1 + summonDataList.Length) % summonDataList.Length;
        UpdateDisplay();
    }

    public void ForceRefresh()
    {
        UpdateDisplay();
    }


    void UpdateDisplay()
    {
        var data = summonDataList[currentIndex];

        summonImage.sprite = data.characterSprite;
        backgroundImage.sprite = data.backgroundSprite;

        nameText.text = data.summonName.Replace("\\n", "\n");
        descriptionText.text = data.description.Replace("\\n", "\n");
        passiveSkillText.text = data.passiveSkill.Replace("\\n", "\n");
        activeSkillText.text = data.activeSkill.Replace("\\n", "\n");

        ApplyTextStyle(nameText, data.nameStyle);
        ApplyTextStyle(descriptionText, data.descriptionStyle);
        ApplyTextStyle(passiveSkillText, data.passiveSkillStyle);
        ApplyTextStyle(activeSkillText, data.activeSkillStyle);

        // 選択音の再生（ここも移譲可能）
        if (data.summonSE != null)
            SEPlayer.I.PlayReplace(data.summonSE);

        int selectedIndex = SummonSelectionManager.I.SelectedIndex;

        bool isSelected = (currentIndex == selectedIndex);

        highlightFrame.SetActive(isSelected);
        contractText.SetActive(isSelected);
        selectButton.interactable = !isSelected;
    }

    void ApplyTextStyle(TMP_Text text, SummonTextStyle style)
    {
        text.fontMaterial = Instantiate(text.fontMaterial);

        text.color = style.fontColor;

        if (style.useGradient)
        {
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(style.topColor, style.topColor, style.bottomColor, style.bottomColor);
        }
        else
        {
            text.enableVertexGradient = false;
        }

        text.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, style.outlineColor);
        text.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, style.outlineThickness);
        text.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, style.faceDilate);
    }

    public int GetSelectedSummonIndex()
    {
        return currentIndex;
    }

 }