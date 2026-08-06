using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    public Image cardImage;
    [Header("手札用ステータス表示（ATK/DEF・属性・特殊性）")]
    [SerializeField] private TMP_Text cardStatusText;
    [Tooltip("未設定時は子オブジェクト Card Hit Rate Text を検索")]
    [SerializeField] private TMP_Text cardHitRateText;
    public Button button;
    public Image highlightBorder;

    [Header("レア演出（任意・手札裏面のみ）")]
    [Tooltip("未設定時は実行時に CardImage と同レイアウトで生成します。")]
    [SerializeField] private Image rareBackRainbowOverlay;
    [Tooltip("RainbowOutline の不透明度。大きいほど虹がはっきり見えます。")]
    [Range(0.1f, 1f)] [SerializeField] private float rareRainbowIntensity = 0.65f;

    private const float AtkDefOutlineWidth = 0.2f;
    private const float HealOutlineWidth = 0.2f;
    private static readonly Color HealTextColor = Color.green;
    private static readonly Color HealOutlineColor = Color.white;
    private static readonly Color AtkDefNoneElementFillColor = Color.black;
    private static readonly Color AtkDefNoneElementOutlineColor = Color.white;
    private static readonly Color AtkDefOutlineColor = Color.black;

    private CardData cardData;
    private Sprite backSprite;
    private bool isFaceUp = false;
    private bool isHighlighted = false;
    private bool playerHandRareBackPresentation;

    private void Awake()
    {
        EnsureHitRateTextRef();
    }

    public void Setup(CardData data, Sprite back, bool playerHandRareBackPresentation = false)
    {
        cardData = data;
        backSprite = back;
        isFaceUp = false;
        this.playerHandRareBackPresentation = playerHandRareBackPresentation;

        ShowBack();
        if (button) button.interactable = false;

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        SetHighlight(false);
    }

    public void Reveal()
    {
        if (isFaceUp) return;
        isFaceUp = true;

        SetRareBackOverlayActive(false);

        if (cardData == null) return;

        if (cardImage) cardImage.sprite = cardData.cardImage;
        CardHandStatusText.Apply(cardStatusText, cardData, CardHandStatusText.GetIncomingSnapshotForReactiveHandLabelOrNull());
        ApplyHitRateDisplay(cardData);
        if (button) button.interactable = true;
    }

    private void ShowBack()
    {
        if (cardImage) cardImage.sprite = backSprite;
        if (cardStatusText)
        {
            cardStatusText.text = "";
            cardStatusText.richText = false;
        }

        ApplyHitRateDisplay(null);
        UpdateRareBackOverlayForCurrentState();
    }

    private void EnsureHitRateTextRef()
    {
        if (cardHitRateText != null) return;
        var texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == "Card Hit Rate Text")
            {
                cardHitRateText = texts[i];
                break;
            }
        }
    }

    private void ApplyHitRateDisplay(CardData c)
    {
        EnsureHitRateTextRef();
        if (cardHitRateText == null) return;

        if (c != null && HitRateRules.HasCustomHitRate(c))
        {
            cardHitRateText.text = HitRateRules.FormatHitRateLabel(c.hitRate);
            cardHitRateText.gameObject.SetActive(true);
        }
        else
        {
            cardHitRateText.text = "";
            cardHitRateText.gameObject.SetActive(false);
        }
    }

    private void UpdateRareBackOverlayForCurrentState()
    {
        bool show = !isFaceUp
                    && playerHandRareBackPresentation
                    && cardData != null
                    && cardData.isRare;
        SetRareBackOverlayActive(show);
    }

    private void SetRareBackOverlayActive(bool show)
    {
        var overlay = GetOrCreateRareOverlay();
        if (overlay == null) return;

        if (show)
        {
            if (cardImage != null)
            {
                overlay.sprite = cardImage.sprite;
                overlay.rectTransform.anchorMin = cardImage.rectTransform.anchorMin;
                overlay.rectTransform.anchorMax = cardImage.rectTransform.anchorMax;
                overlay.rectTransform.anchoredPosition = cardImage.rectTransform.anchoredPosition;
                overlay.rectTransform.sizeDelta = cardImage.rectTransform.sizeDelta;
                overlay.rectTransform.pivot = cardImage.rectTransform.pivot;
                overlay.rectTransform.localScale = cardImage.rectTransform.localScale;
            }

            overlay.gameObject.SetActive(true);
            var rainbow = overlay.GetComponent<RainbowOutline>();
            if (rainbow == null)
                rainbow = overlay.gameObject.AddComponent<RainbowOutline>();
            rainbow.intensity = rareRainbowIntensity;
        }
        else
        {
            var ro = overlay.GetComponent<RainbowOutline>();
            if (ro != null)
                Destroy(ro);
            overlay.color = new Color(1f, 1f, 1f, 0f);
            overlay.gameObject.SetActive(false);
        }
    }

    private Image GetOrCreateRareOverlay()
    {
        if (rareBackRainbowOverlay != null)
            return rareBackRainbowOverlay;

        Transform existing = transform.Find("RareBackRainbowOverlay");
        if (existing != null)
        {
            rareBackRainbowOverlay = existing.GetComponent<Image>();
            return rareBackRainbowOverlay;
        }

        if (cardImage == null)
            return null;

        var go = new GameObject("RareBackRainbowOverlay", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        var src = cardImage.rectTransform;
        rt.SetParent(src.parent, false);
        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.anchoredPosition = src.anchoredPosition;
        rt.sizeDelta = src.sizeDelta;
        rt.pivot = src.pivot;
        rt.localScale = src.localScale;
        rt.SetSiblingIndex(src.GetSiblingIndex() + 1);

        var img = go.GetComponent<Image>();
        img.sprite = src.GetComponent<Image>().sprite;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);

        rareBackRainbowOverlay = img;
        return rareBackRainbowOverlay;
    }

    private void OnClick()
    {
        if (!button || !button.interactable || cardData == null) return;

        if (BattleManager.I != null)
        {
            BattleManager.I.SetSelectedCard(this);
        }
        else
        {
            Debug.LogWarning("[CardUI] BattleManager インスタンスが見つかりません");
        }
    }

    public CardData GetCardData() => cardData;

    public void SetHighlight(bool highlight)
    {
        isHighlighted = highlight;
        if (highlightBorder != null)
        {
            highlightBorder.gameObject.SetActive(highlight);
        }
    }

    public bool IsHighlighted => isHighlighted;

    public bool IsFaceDown() => !isFaceUp;

    /// <summary>被攻撃スナップショットが変わったあと、表向き手札のステータス文字を再適用する（防御フェーズ等）。</summary>
    public void RefreshHandStatusText()
    {
        if (cardData == null || !isFaceUp || cardStatusText == null) return;
        CardHandStatusText.Apply(cardStatusText, cardData, CardHandStatusText.GetIncomingSnapshotForReactiveHandLabelOrNull());
        ApplyHitRateDisplay(cardData);
    }

    /// <summary>手札カード 1 枚の <see cref="cardStatusText"/> 文言・色。プレハブ差し替え用に静的でも利用可。</summary>
    public static class CardHandStatusText
    {
        /// <summary>
        /// 防御選択で手札操作可能（<see cref="BattleUIManager.IsHandInputBlocked"/> が false）なときだけ被攻撃スナップを返す。
        /// 演出待ち・SE 前・ポップアップ中は null（DEF/ATK 等の通常表記）。
        /// </summary>
        public static IReadOnlyList<CardData> GetIncomingSnapshotForReactiveHandLabelOrNull()
        {
            if (BattleUIManager.I != null && BattleUIManager.I.IsHandInputBlocked) return null;
            return BattleManager.I != null ? BattleManager.I.GetIncomingAttackSnapshotForDefenseUi() : null;
        }

        /// <param name="incomingForDefenseReactive">
        /// 反応系ラベル用。原則 <see cref="GetIncomingSnapshotForReactiveHandLabelOrNull"/>。手渡し null なら常に通常表記。
        /// </param>
        public static void Apply(
            TMP_Text text,
            CardData c,
            IReadOnlyList<CardData> incomingForDefenseReactive = null)
        {
            if (text == null) return;
            if (c == null)
            {
                text.text = "";
                return;
            }

            text.richText = false;
            if (IsRecoveryByCardData(c))
            {
                text.text = BuildRecoveryLabel(c);
                SetHealTextStyle(text);
                return;
            }

            if (TryGetDefenseReactiveHandLabel(c, incomingForDefenseReactive, out string spec))
            {
                text.text = spec;
                SetAtkDefTextStyle(text, c.element);
                return;
            }

            if (c.cardType == CardType.Attack
                && CardRules.IsUsableInDefensePhase(c)
                && c.defensePower > 0
                && incomingForDefenseReactive != null
                && c.reflectionKind == ReflectionKind.None
                && c.parryKind == ParryKind.None
                && c.blockingKind == BlockingKind.None)
            {
                text.text = $"DEF{c.defensePower}";
                SetAtkDefTextStyle(text, c.element);
                return;
            }

            switch (c.cardType)
            {
                case CardType.Attack:
                    text.text = BuildAttackTypeLabel(c);
                    SetAtkDefTextStyle(text, c.element);
                    return;
                case CardType.Defense:
                    text.text = $"DEF{c.defensePower}";
                    SetAtkDefTextStyle(text, c.element);
                    return;
                case CardType.Magic:
                case CardType.ArchMagic:
                    text.text = BuildMagicOrArchLabel(c);
                    SetAtkDefTextStyle(text, c.element);
                    return;
                case CardType.Recovery:
                    text.text = "SPECIAL";
                    SetAtkDefTextStyle(text, c.element);
                    return;
                default:
                    text.text = "SPECIAL";
                    SetAtkDefTextStyle(text, c.element);
                    return;
            }
        }

        public static bool IsRecoveryByCardData(CardData c)
        {
            if (c == null) return false;
            if (c.cardType == CardType.Recovery) return true;
            if (c.cardType == CardType.Magic || c.cardType == CardType.ArchMagic)
            {
                return c.healsHP || c.healsMP || c.healsGP || c.isRecovery
                       || c.cureAllStatusEffects;
            }
            return false;
        }

        private static string BuildRecoveryLabel(CardData c)
        {
            if (c.cureAllStatusEffects) return "CURE";

            int nHealKinds = (c.healsHP ? 1 : 0) + (c.healsMP ? 1 : 0) + (c.healsGP ? 1 : 0);
            if (nHealKinds >= 2) return $"HP+{c.recoveryAmount}";
            if (c.healsHP) return $"HP+{c.recoveryAmount}";
            if (c.healsMP) return $"MP+{c.recoveryAmount}";
            if (c.healsGP) return $"GP+{c.recoveryAmount}";
            return "SPECIAL";
        }

        /// <summary>被攻撃に合う反応系だけ REFLECT / PARRY / BLOCKING。防御 <see cref="CardType.Magic"/>（アイアンクラッド等）も含む。</summary>
        private static bool TryGetDefenseReactiveHandLabel(
            CardData c,
            IReadOnlyList<CardData> incoming,
            out string label)
        {
            label = null;
            if (c == null) return false;
            if (c.cardType != CardType.Defense
                && c.cardType != CardType.Attack
                && c.cardType != CardType.Magic)
            {
                return false;
            }
            if (c.cardType == CardType.Magic)
            {
                if (c.isRecovery || c.healsHP || c.healsMP || c.healsGP || c.cureAllStatusEffects) return false;
                if (c.attackPower > 0 && c.usableInAttackPhase) return false;
                if (c.reflectionKind == ReflectionKind.None
                    && c.parryKind == ParryKind.None
                    && c.blockingKind == BlockingKind.None)
                {
                    return false;
                }
            }
            if (incoming == null || incoming.Count == 0) return false;

            if (c.reflectionKind != ReflectionKind.None)
            {
                if (ReflectionRules.CanUsePhysicalReflectionAgainstAttack(c, incoming)
                    || ReflectionRules.CanUseMagicReflectionAgainstAttack(c, incoming))
                {
                    label = "REFLECT";
                    return true;
                }
                return false;
            }
            if (c.parryKind != ParryKind.None)
            {
                if (ParryRules.CanParryIncoming(c, incoming))
                {
                    label = "PARRY";
                    return true;
                }
                return false;
            }
            if (BlockingRules.IsPhysicalBlockingCard(c))
            {
                if (BlockingRules.CanUsePhysicalBlockingAgainstAttack(c, incoming))
                {
                    label = "BLOCKING";
                    return true;
                }
                return false;
            }
            return false;
        }

        private static string BuildAttackTypeLabel(CardData c)
        {
            if (HammadnessRules.IsHammadnessCard(c))
                return HammadnessRules.AtkQuestionMarkLabel;
            if (c.attackPower == 0 && c.defensePower == 0) return "SPECIAL";
            if (WantsAtkPlusNotation(c)) return $"ATK+{c.attackPower}";
            return $"ATK{c.attackPower}";
        }

        private static string BuildMagicOrArchLabel(CardData c)
        {
            if (c.attackPower == 0 && c.defensePower == 0) return "SPECIAL";
            if (c.attackPower == 0 && c.defensePower > 0) return "SPECIAL";
            if (c.attackPower > 0)
                return WantsAtkPlusNotation(c) ? $"ATK+{c.attackPower}" : $"ATK{c.attackPower}";
            return "SPECIAL";
        }

        private static bool WantsAtkPlusNotation(CardData c)
        {
            if (c == null) return false;
            if (c.attackPhaseUseRule == AttackPhaseUseRule.Flexible) return true;
            if (c.attackPhaseUseRule == AttackPhaseUseRule.AddOn)
                return c.attackPower > 0;
            return false;
        }

        private static void SetAtkDefTextStyle(TMP_Text tmp, ElementType element)
        {
            if (element == ElementType.None)
            {
                tmp.color = AtkDefNoneElementFillColor;
                tmp.outlineColor = AtkDefNoneElementOutlineColor;
            }
            else
            {
                tmp.color = ElementHelper.GetElementColor(element);
                tmp.outlineColor = AtkDefOutlineColor;
            }
            tmp.outlineWidth = AtkDefOutlineWidth;
        }

        private static void SetHealTextStyle(TMP_Text tmp)
        {
            tmp.color = HealTextColor;
            tmp.outlineColor = HealOutlineColor;
            tmp.outlineWidth = HealOutlineWidth;
        }
    }
}
