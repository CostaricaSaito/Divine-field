using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class CardSheetTemplateView : MonoBehaviour
{
    [Header("Slots (assign in Inspector)")]
    [SerializeField] private Image artworkSlot;   // 左のカード絵
    [SerializeField] private TMP_Text nameText;      // カード名
    [SerializeField] private Image attrBase;      // 属性バッジの台(任意)
    [SerializeField] private Image attrIcon;      // 属性アイコン
    [SerializeField] private TMP_Text statText;      // "ATK 15 DEF 6"
    [SerializeField] private TMP_Text effectText;    // 特殊・説明
    [SerializeField] private Image priceIcon;     // 紙幣アイコン(任意)
    [SerializeField] private TMP_Text priceText;     // "¥10" を出したい場合

    [Header("Element icons & colors")]
    [SerializeField] private List<ElementEntry> elementTable = new();

    [Header("Price")]
    [Tooltip("CardData.price が無い時は mpCost を代用")]
    [SerializeField] private bool fallbackToMpCost = true;

    [Serializable]
    public struct ElementEntry
    {
        public ElementType type;
        public Sprite icon;   // 属性アイコン
        public Color tint;   // バッジ台の色
    }

    Dictionary<ElementType, ElementEntry> _emap;

    void Awake()
    {
        _emap = new Dictionary<ElementType, ElementEntry>();
        foreach (var e in elementTable) _emap[e.type] = e;
    }

    public void Set(CardData c)
    {
        if (!c) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);

        // 画像
        if (artworkSlot)
        {
            artworkSlot.enabled = c.cardImage;
            artworkSlot.sprite = c.cardImage;
            artworkSlot.preserveAspect = true;
        }

        // 名前
        if (nameText) nameText.text = c.cardName ?? "";

        // 属性バッジ
        if (_emap != null && _emap.TryGetValue(c.element, out var em) && c.element != ElementType.None)
        {
            if (attrBase) { attrBase.enabled = true; attrBase.color = em.tint; }
            if (attrIcon) { attrIcon.enabled = true; attrIcon.sprite = em.icon; }
        }
        else
        {
            if (attrBase) attrBase.enabled = false;
            if (attrIcon) attrIcon.enabled = false;
        }

        // 攻守
        if (statText)
        {
            string atk = c.attackPower > 0 ? $"ATK {c.attackPower}" : "";
            string def = c.defensePower > 0 ? $"DEF {c.defensePower}" : "";
            statText.text = (atk + (atk != "" && def != "" ? "   " : "") + def).Trim();
        }

        // 特殊/説明
        if (effectText)
        {
            string tag = "";
            if (c.isCounterAttack) tag = "反撃";
            else if (c.isAdditionalAttack) tag = "追加攻撃";
            else if (c.isPrimaryDefense) tag = "防御";
            if (c.canApplyStatusEffect && c.statusEffectChance > 0)
                tag += (string.IsNullOrEmpty(tag) ? "" : " / ") + $"状態異常 {c.statusEffectChance}%";
            string desc = string.IsNullOrWhiteSpace(c.description) ? "" : c.description;
            effectText.text = string.IsNullOrEmpty(tag) ? desc : (string.IsNullOrEmpty(desc) ? tag : tag + "\n" + desc);
        }

        // 金額
        int price = 0;
        var f = typeof(CardData).GetField("price");
        price = (f != null) ? (int)f.GetValue(c) : (fallbackToMpCost ? c.mpCost : 0);
        if (priceIcon) priceIcon.enabled = (price > 0);
        if (priceText) priceText.text = (price > 0) ? $"¥{price}" : "";
    }
}