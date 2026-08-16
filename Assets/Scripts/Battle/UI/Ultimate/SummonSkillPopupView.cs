﻿using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds SummonSkillPopup prefab UI (SkillName, SkillDesc, UltimateSkillButton, CancelButton).
/// Does not modify RectTransform layout — prefab positions are preserved.
/// </summary>
public sealed class SummonSkillPopupView : MonoBehaviour
{
    private const string ConfirmButtonSeAddress = "Assets/SE/決定ボタンを押す3.mp3";
    private const string CancelButtonSeAddress = "Assets/SE/キャンセル4.mp3";
    private const float SkillDescOutlineWidth = 0.2f;

    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private TMP_Text skillDescText;
    [SerializeField] private Button ultimateSkillButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Image summonIcon;

    public void Bind(SummonData summon, Action onConfirm, Action onCancel)
    {
        ResolveRefsIfNeeded();

        if (summon != null)
        {
            if (skillNameText != null)
            {
                skillNameText.text = summon.ultimateSkillName ?? string.Empty;
                summon.ApplyStyleTo(skillNameText, summon.textStyle);
            }

            if (skillDescText != null)
            {
                skillDescText.text = summon.ultimateSkillDescription ?? string.Empty;
                ApplySkillDescCommonStyle(skillDescText);
            }

            if (summonIcon != null)
            {
                var icon = summon.GetBattleStatusIconSprite();
                if (icon != null)
                    summonIcon.sprite = icon;
            }
        }

        WireButton(ultimateSkillButton, ConfirmButtonSeAddress, onConfirm);
        WireButton(cancelButton, CancelButtonSeAddress, onCancel);
    }

    /// <summary>SkillDesc: black face + white outline (all summons).</summary>
    private static void ApplySkillDescCommonStyle(TMP_Text text)
    {
        if (text == null || text.font == null) return;

        text.color = Color.black;
        text.enableVertexGradient = false;

        var sharedMat = text.font.material;
        if (sharedMat == null) return;

        var mat = Instantiate(sharedMat);
        text.fontSharedMaterial = sharedMat;
        text.fontMaterial = mat;

        if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.white);
        if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, SkillDescOutlineWidth);
        if (mat.HasProperty(ShaderUtilities.ID_FaceDilate))
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
    }

    private void ResolveRefsIfNeeded()
    {
        if (skillNameText == null)
            skillNameText = FindComponent<TMP_Text>("SkillName");
        if (skillDescText == null)
            skillDescText = FindComponent<TMP_Text>("SkillDesc");
        if (ultimateSkillButton == null)
            ultimateSkillButton = FindComponent<Button>("UltimateSkillButton");
        if (cancelButton == null)
            cancelButton = FindComponent<Button>("CancelButton");
        if (summonIcon == null)
            summonIcon = FindComponent<Image>("SummonIcon");
    }

    private T FindComponent<T>(string objectName) where T : Component
    {
        if (string.IsNullOrEmpty(objectName)) return null;

        var transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t == null || t.name != objectName) continue;
            var component = t.GetComponent<T>();
            if (component != null)
                return component;
        }

        return null;
    }

    private static void WireButton(Button button, string sePath, Action onClick)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        if (onClick == null) return;

        button.onClick.AddListener(() =>
        {
            if (!string.IsNullOrEmpty(sePath))
                SoundEffectPlayer.I?.Play(sePath);
            onClick();
        });
    }
}
