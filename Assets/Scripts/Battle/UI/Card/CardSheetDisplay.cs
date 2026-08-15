using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CardSheetDisplay : MonoBehaviour
{
    [Header("UI参照")]
    [Tooltip("未設定時は Panel/BG または BG を自動検索。CardData.cardDisplayFrameSprite 指定時に枠差し替え。")]
    [SerializeField] private Image cardSheetBackground;

    public Image artworkSlot;
    public TMP_Text cardNameText;
    public TMP_Text atkDefText;
    public TMP_Text descText;
    public Image attributeIcon;
    public Image goldIcon;
    public TMP_Text goldValueText;

    [Header("魔法カード：Gold の代わりに消費MP（Prefab 上の CardSheet_MPcost / CardSheet_MPcostText を割り当て）")]
    public GameObject cardSheetMPcost;
    public TMP_Text cardSheetMPcostText;
    [SerializeField] private Color mpCostBoxDefaultColor = new Color(0.55f, 0.88f, 0.62f, 1f);
    [SerializeField] private Color mpCostTextDefaultColor = Color.black;
    [SerializeField] private Color mpCostEyeStrainBoxColor = new Color(1f, 0.92f, 0.35f, 1f);
    [SerializeField] private Color mpCostEyeStrainTextColor = Color.black;
    [SerializeField] private Color mpCostClusterBoxColor = new Color(0.88f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color mpCostClusterTextColor = Color.white;

    [Header("MagicPool remaining uses overlay (Magic Fountain presentation)")]
    [SerializeField] private TMP_Text cardSheetPoolUsesText;

    private CardData currentCardData;
    private PlayerStatus _ownerForDisplay;
    private HitRateApplicability.SheetContext _sheetContext = HitRateApplicability.SheetContext.Normal;
    private int _displayAttack;
    private int _displayDefense;
    private Image _orbTintOverlay;

    private Sprite _defaultCardSheetBackgroundSprite;

    private void Awake()
    {
        ResolveCardSheetBackgroundReference();
        if (cardSheetBackground != null)
            _defaultCardSheetBackgroundSprite = cardSheetBackground.sprite;
    }
    
    /// <param name="ownerForMpDisplay">魔法の消費MP表示・眼精疲労／群発頭痛の見た目に使う。null ならカード素の mpCost。</param>
    public void Setup(
        CardData cardData,
        PlayerStatus ownerForMpDisplay = null,
        HitRateApplicability.SheetContext sheetContext = HitRateApplicability.SheetContext.Normal)
    {
        currentCardData = cardData;
        _ownerForDisplay = ownerForMpDisplay;
        _sheetContext = sheetContext;
        ResolveCardSheetBackgroundReference();
        ApplyCardSheetBackground(cardData);

        SetupArtwork(cardData);
        if (cardNameText) cardNameText.text = cardData.cardName;
        _displayAttack = cardData.attackPower;
        _displayDefense = cardData.defensePower;
        RefreshHitRateLineOnAtkDefText();
        if (descText) descText.text = cardData.description;
        SetupElementDisplay(cardData);
        SetupGoldOrMpCostDisplay(cardData, ownerForMpDisplay);
        HidePoolRemainingUsesDisplay();
    }

    /// <summary>Show pooled magic remaining uses over ArtworkSlot (Magic Fountain).</summary>
    public void SetPoolRemainingUsesDisplay(int uses)
    {
        EnsurePoolUsesText();
        if (cardSheetPoolUsesText == null) return;

        cardSheetPoolUsesText.gameObject.SetActive(true);
        ApplyPoolUsesTextStyle();
        cardSheetPoolUsesText.text = uses.ToString();
    }

    public void HidePoolRemainingUsesDisplay()
    {
        if (cardSheetPoolUsesText != null)
            cardSheetPoolUsesText.gameObject.SetActive(false);
    }

    private void EnsurePoolUsesText()
    {
        if (cardSheetPoolUsesText != null) return;

        Transform parent = artworkSlot != null ? artworkSlot.transform : transform;
        var existing = parent.Find("CardSheet_PoolUses");
        if (existing != null)
        {
            cardSheetPoolUsesText = existing.GetComponent<TMP_Text>();
            ApplyPoolUsesTextStyle();
            return;
        }

        var go = new GameObject("CardSheet_PoolUses", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        cardSheetPoolUsesText = go.GetComponent<TextMeshProUGUI>();
        ApplyPoolUsesTextStyle();
    }

    private void ApplyPoolUsesTextStyle()
    {
        if (cardSheetPoolUsesText == null) return;

        TMP_FontAsset font = cardNameText != null ? cardNameText.font : cardSheetPoolUsesText.font;
        if (font == null) return;

        cardSheetPoolUsesText.font = font;
        cardSheetPoolUsesText.alignment = TextAlignmentOptions.Center;
        cardSheetPoolUsesText.fontSize = 100f;
        cardSheetPoolUsesText.fontStyle = FontStyles.Bold;
        cardSheetPoolUsesText.raycastTarget = false;
        cardSheetPoolUsesText.color = Color.white;

        Material sharedMat = font.material;
        if (sharedMat == null) return;

        const float outlineWidth = 0.3f;
        var mat = Instantiate(sharedMat);
        if (mat.HasProperty(ShaderUtilities.ID_FaceColor))
            mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);

        cardSheetPoolUsesText.fontSharedMaterial = sharedMat;
        cardSheetPoolUsesText.fontMaterial = mat;
        cardSheetPoolUsesText.outlineWidth = outlineWidth;
        cardSheetPoolUsesText.outlineColor = Color.black;
    }

    /// <summary>
    /// CardSheet prefab: BG is usually a root child named "BG" (not under Panel).
    /// </summary>
    private void ResolveCardSheetBackgroundReference()
    {
        if (cardSheetBackground != null) return;

        var t = transform.Find("Panel/BG");
        if (t == null) t = transform.Find("BG");
        if (t != null) cardSheetBackground = t.GetComponent<Image>();
    }

    private void ApplyCardSheetBackground(CardData cardData)
    {
        if (cardSheetBackground == null) return;

        if (cardData != null && cardData.cardDisplayFrameSprite != null)
            cardSheetBackground.sprite = cardData.cardDisplayFrameSprite;
        else if (_defaultCardSheetBackgroundSprite != null)
            cardSheetBackground.sprite = _defaultCardSheetBackgroundSprite;
    }

    public CardData GetCurrentCardData() => currentCardData;

    /// <summary>煙幕等で表示命中率が変わったとき、ATK 行の命中率部分だけ更新。</summary>
    public void RefreshHitRateDisplayIfOwner(PlayerStatus owner)
    {
        if (!ReferenceEquals(_ownerForDisplay, owner)) return;
        RefreshHitRateLineOnAtkDefText();
    }

    public void SetHitRateSheetContext(HitRateApplicability.SheetContext sheetContext)
    {
        _sheetContext = sheetContext;
        RefreshHitRateLineOnAtkDefText();
    }

    private void RefreshHitRateLineOnAtkDefText()
    {
        if (currentCardData == null || atkDefText == null) return;
        if (HammadnessRules.IsHammadnessCard(currentCardData) && _displayAttack <= 0)
        {
            atkDefText.text = HammadnessRules.AtkQuestionMarkLabel;
            return;
        }

        bool atkLineLikeAttack = currentCardData.cardType == CardType.Attack
            || currentCardData.cardType == CardType.Ultimate
            || currentCardData.cardType == CardType.Disaster;
        bool showHitRate = HitRateRules.ShouldDisplayHitRateLabelForSheet(
            currentCardData, _ownerForDisplay, _sheetContext);
        int hitRate = HitRateRules.GetDisplayedHitRatePercentForSheet(
            currentCardData, _ownerForDisplay, _sheetContext);
        atkDefText.text = FormatAtkDefLine(
            _displayAttack,
            _displayDefense,
            atkLineLikeAttack,
            hitRate,
            showHitRate);
    }

    /// <summary>マジカルエクスプロージョン演出など：ATK 行のみ差し替え。</summary>
    public void SetAtkDefenseNumbers(int attack, int defense)
    {
        _displayAttack = attack;
        _displayDefense = defense;
        RefreshHitRateLineOnAtkDefText();
    }

    /// <summary>
    /// 非 <see cref="CardType.Attack"/>: ATK0→DEFのみ、DEF0→ATKのみ、両方0→空欄。
    /// Attack: 基礎ATK0でも行を確保（マジカルエクスプロージョン等の数値差し替え用）。ATK0かつDEF1+のAttackは想定外だが、出た場合は ATK 0 も併記。
    /// 命中率が 100% 以外のとき末尾に「 / 50%」形式で付与（煙幕等の実効値を反映）。
    /// </summary>
    private static string FormatAtkDefLine(
        int attack,
        int defense,
        bool isAttackTypeCard,
        int hitRate = HitRateRules.DefaultHitRatePercent,
        bool showHitRateLabel = false)
    {
        string line;
        if (isAttackTypeCard)
        {
            if (attack == 0 && defense == 0)
                line = "ATK 0";
            else if (attack == 0)
                line = $"ATK 0 / DEF {defense}";
            else if (defense == 0)
                line = $"ATK {attack}";
            else
                line = $"ATK {attack} / DEF {defense}";
        }
        else if (attack == 0 && defense == 0)
        {
            line = "";
        }
        else if (attack == 0)
        {
            line = $"DEF {defense}";
        }
        else if (defense == 0)
        {
            line = $"ATK {attack}";
        }
        else
        {
            line = $"ATK {attack} / DEF {defense}";
        }

        if (!showHitRateLabel)
            return line;

        string hitLabel = HitRateRules.FormatHitRateLabel(hitRate);
        return string.IsNullOrEmpty(line) ? hitLabel : $"{line} / {hitLabel}";
    }

    /// <summary>
    /// 魔法以外：右下に GoldIcon + 価値（cardValue）。魔法：Gold を隠し消費MP（眼精疲労で2倍・群発で使用不可表示）。
    /// </summary>
    private void SetupGoldOrMpCostDisplay(CardData cardData, PlayerStatus owner)
    {
        bool isMagic = cardData != null && cardData.cardType == CardType.Magic;

        if (cardSheetMPcost != null)
            cardSheetMPcost.SetActive(isMagic);
        var mpBg = cardSheetMPcost != null ? cardSheetMPcost.GetComponent<Image>() : null;

        if (cardSheetMPcostText != null)
        {
            if (isMagic)
            {
                if (owner != null && owner.IsMagicUseForbidden())
                {
                    cardSheetMPcostText.text = "使用不可";
                    if (mpBg != null) mpBg.color = mpCostClusterBoxColor;
                    cardSheetMPcostText.color = mpCostClusterTextColor;
                }
                else if (owner != null && owner.HasEyeStrainEffect())
                {
                    int eff = owner.GetEffectiveMagicMpCost(cardData.mpCost);
                    cardSheetMPcostText.text = $"消費MP {eff}";
                    if (mpBg != null) mpBg.color = mpCostEyeStrainBoxColor;
                    cardSheetMPcostText.color = mpCostEyeStrainTextColor;
                }
                else
                {
                    cardSheetMPcostText.text = $"消費MP {cardData.mpCost}";
                    if (mpBg != null) mpBg.color = mpCostBoxDefaultColor;
                    cardSheetMPcostText.color = mpCostTextDefaultColor;
                }
            }
            else
                cardSheetMPcostText.text = "";
        }

        if (goldIcon != null)
            goldIcon.gameObject.SetActive(!isMagic);
        if (goldValueText != null)
        {
            goldValueText.gameObject.SetActive(!isMagic);
            if (!isMagic && cardData != null)
                goldValueText.text = $"¥ {cardData.cardValue}";
            else
                goldValueText.text = "";
        }
    }

    private void SetupElementDisplay(CardData cardData)
    {
        ElementType elem = cardData != null ? cardData.element : ElementType.None;

        if (attributeIcon)
        {
            Sprite icon = ElementHelper.LoadIcon(elem);
            if (icon != null)
            {
                attributeIcon.sprite = icon;
                attributeIcon.gameObject.SetActive(true);
            }
            else
            {
                attributeIcon.gameObject.SetActive(false);
            }
        }

        if (elem != ElementType.None)
        {
            Color elemColor = ElementHelper.GetElementColor(elem);
            if (cardNameText) cardNameText.color = elemColor;
            if (atkDefText) atkDefText.color = elemColor;
            if (descText) descText.color = elemColor;
        }
        else
        {
            if (cardNameText) cardNameText.color = Color.black;
            if (atkDefText) atkDefText.color = Color.black;
            if (descText) descText.color = Color.black;
        }
    }
    
    public CardData GetCardData()
    {
        return currentCardData;
    }
    
    /// <summary>
    /// カード画像を設定
    /// </summary>
    private void SetupArtwork(CardData cardData)
    {
        if (artworkSlot == null || cardData?.cardImage == null) return;
        
        // 画像を設定
        artworkSlot.sprite = cardData.cardImage;
        
        // 画像をArtWorkSlotにぴったりフィットさせる設定
        artworkSlot.type = Image.Type.Simple;
        artworkSlot.preserveAspect = false; // アスペクト比を無視して枠にぴったり合わせる
    }

    /// <summary>宝玉：CardSheet プレハブ全体の属性色トーン（フェードイン→フェードアウト。秒は呼び出し側で 0.5+0.5 想定）。</summary>
    public async Task PlayOrbElementTintFlashAsync(Color tintRgb, float fadeInSec, float fadeOutSec, CancellationToken ct)
    {
        // アート枠内ではなくルート（プレハブ全体）に被せる。旧オーバーレイは親が違えば作り直す
        var root = (RectTransform)transform;
        if (_orbTintOverlay != null && _orbTintOverlay.transform.parent != root)
        {
            Destroy(_orbTintOverlay.gameObject);
            _orbTintOverlay = null;
        }

        if (_orbTintOverlay == null)
        {
            var go = new GameObject("OrbTintOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();
            _orbTintOverlay = go.GetComponent<Image>();
            _orbTintOverlay.raycastTarget = false;
        }

        _orbTintOverlay.gameObject.SetActive(true);
        Color c0 = tintRgb;
        c0.a = 0f;
        Color c1 = new Color(tintRgb.r, tintRgb.g, tintRgb.b, 0.5f);
        _orbTintOverlay.color = c0;

        float u;
        u = 0f;
        while (u < 1.0001f)
        {
            ct.ThrowIfCancellationRequested();
            u += fadeInSec > 0.0001f ? Time.unscaledDeltaTime / fadeInSec : 1f;
            _orbTintOverlay.color = Color.Lerp(c0, c1, Mathf.Clamp01(u));
            await Task.Yield();
        }
        _orbTintOverlay.color = c1;
        u = 0f;
        while (u < 1.0001f)
        {
            ct.ThrowIfCancellationRequested();
            u += fadeOutSec > 0.0001f ? Time.unscaledDeltaTime / fadeOutSec : 1f;
            _orbTintOverlay.color = Color.Lerp(c1, c0, Mathf.Clamp01(u));
            await Task.Yield();
        }
        _orbTintOverlay.color = c0;
        _orbTintOverlay.gameObject.SetActive(false);
    }
}