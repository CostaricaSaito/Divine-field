using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 属性システムのユーティリティ（色・アイコン・合算・マッチング判定）
/// </summary>
public static class ElementHelper
{
    private static readonly Dictionary<ElementType, Color> elementColors = new Dictionary<ElementType, Color>
    {
        { ElementType.None,    Color.black },
        { ElementType.Fire,    new Color(1.0f, 0.2f, 0.2f) },       // 赤
        { ElementType.Water,   new Color(0.2f, 0.4f, 1.0f) },       // 青
        { ElementType.Wind,    new Color(0.2f, 0.8f, 0.3f) },       // 緑
        { ElementType.Thunder, new Color(1.0f, 0.6f, 0.1f) },       // オレンジ
        { ElementType.Steel,   new Color(0.4f, 0.4f, 0.45f) },      // 濃灰
        { ElementType.Ice,     new Color(0.5f, 0.85f, 1.0f) },      // 水色
        { ElementType.Dark,    new Color(0.45f, 0.15f, 0.6f) },     // 濃紫
        { ElementType.Light,   new Color(1.0f, 0.85f, 0.3f) },      // 黄金
    };

    private static readonly Dictionary<ElementType, string> iconPaths = new Dictionary<ElementType, string>
    {
        { ElementType.Fire,    "Attributes/FireIcon" },
        { ElementType.Water,   "Attributes/WaterIcon" },
        { ElementType.Wind,    "Attributes/WindIcon" },
        { ElementType.Thunder, "Attributes/ThunderIcon" },
        { ElementType.Steel,   "Attributes/SteelIcon" },
        { ElementType.Ice,     "Attributes/IceIcon" },
        { ElementType.Dark,    "Attributes/DarkIcon" },
        { ElementType.Light,   "Attributes/LightIcon" },
    };

    /// <summary>
    /// 属性に対応する色を返す
    /// </summary>
    public static Color GetElementColor(ElementType element)
    {
        return elementColors.TryGetValue(element, out var c) ? c : Color.black;
    }

    /// <summary>
    /// Resources から属性アイコンの Sprite を読み込む
    /// </summary>
    public static Sprite LoadIcon(ElementType element)
    {
        if (element == ElementType.None) return null;
        if (!iconPaths.TryGetValue(element, out var path)) return null;
        return Resources.Load<Sprite>(path);
    }

    /// <summary>
    /// 複数カードの属性を合算して最終属性を返す
    /// - 全て同一属性 or 光との組み合わせ → その非光属性
    /// - 光のみ → 光
    /// - 非光属性が2種以上混在 → 無属性
    /// </summary>
    public static ElementType GetCombinedElement(List<CardData> cards)
    {
        if (cards == null || cards.Count == 0) return ElementType.None;

        // 属性付きカードと無属性カードを混在させた場合は無属性（例：ファイアボール＋クロスボウ）
        if (cards.Count >= 2)
        {
            bool hasNoneElementCard = false;
            bool hasNonNoneElementCard = false;
            foreach (var card in cards)
            {
                if (card == null) continue;
                if (card.element == ElementType.None)
                    hasNoneElementCard = true;
                else
                    hasNonNoneElementCard = true;
            }
            if (hasNoneElementCard && hasNonNoneElementCard)
                return ElementType.None;
        }

        ElementType nonLightElement = ElementType.None;
        bool hasNonLight = false;
        bool hasLight = false;
        bool mixed = false;

        foreach (var card in cards)
        {
            if (card == null) continue;
            ElementType e = card.element;

            if (e == ElementType.None) continue;

            if (e == ElementType.Light)
            {
                hasLight = true;
                continue;
            }

            if (!hasNonLight)
            {
                nonLightElement = e;
                hasNonLight = true;
            }
            else if (e != nonLightElement)
            {
                mixed = true;
            }
        }

        if (mixed) return ElementType.None;
        if (hasNonLight) return nonLightElement;
        if (hasLight) return ElementType.Light;
        return ElementType.None;
    }

    /// <summary>
    /// 攻撃属性に対して防御カードが有効かどうかを判定する（属性値同士）。
    /// 闇攻撃は光以外の任意属性で防げる想定だが、無属性防具は <see cref="CanDefendAgainst(ElementType, CardData)"/> 側で闇専用に許可する。
    /// </summary>
    public static bool CanDefendAgainst(ElementType attackElement, ElementType defenseCardElement)
    {
        if (attackElement == ElementType.None) return true;
        if (attackElement == ElementType.Dark) return true;
        if (defenseCardElement == ElementType.Light) return true;
        return defenseCardElement == attackElement;
    }

    /// <summary>
    /// 攻撃属性に対して防御カードが有効かを判定する（CardData版）
    /// 無属性の防御カードは、属性攻撃に対して原則無効（闇攻撃は例外で無属性防具も可）。
    /// </summary>
    public static bool CanDefendAgainst(ElementType attackElement, CardData defenseCard)
    {
        if (defenseCard == null) return false;
        if (attackElement == ElementType.None) return true;
        // 闇はどの属性の防具でも防げる（無属性の「通常防具」も含む）
        if (attackElement == ElementType.Dark) return true;
        if (defenseCard.element == ElementType.None) return false;
        return CanDefendAgainst(attackElement, defenseCard.element);
    }
}
