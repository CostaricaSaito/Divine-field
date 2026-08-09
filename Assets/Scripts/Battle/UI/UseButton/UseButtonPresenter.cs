using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UseButton（使用ボタン）と「許す」表示の見た目・状態を制御するサブマネージャ。
///
/// 【主な責務】
/// - 使用／許す／祈り／MP不足／魔法使用不可 のラベル切替と配色
/// - 反射バウンス（虹色）・無効化（銀色）・大魔法詠唱（ピンク字＋紫→水色グラデ背景）スタイル
/// - 防御・反射連鎖等の自プレイヤー <see cref="SetUseButtonLabel"/>（相手用 <see cref="yurusuDisplay"/> は戦闘解決側のみ）
/// - 攻撃選択中の MP／大魔法コストチェックに基づくラベル＆操作可否
///
/// Inspector 参照はこのコンポーネントにバインドする（BattleUIManager 側からは廃止済）。
/// </summary>
public class UseButtonPresenter : MonoBehaviour
{
    private enum UseButtonMode { Use, Allow, Pray, MpShortage }

    [Header("UseButton / 許す表示")]
    [SerializeField] private Button useButton;
    [Tooltip("相手が防御0枚で解決中のみ。自プレイヤー防御は使用ボタンラベルのみで表現（このオブジェクトは使わない）")]
    [SerializeField] private GameObject yurusuDisplay;
    [SerializeField] private TMP_Text yurusuButtonLabelTMP;
    [SerializeField] private TMP_Text useButtonLabelTMP;
    [SerializeField] private Text useButtonLabelUGUI;
    [SerializeField] private Image useButtonImage;

    [Header("Use ボタン配色")]
    [SerializeField] private Color useButtonNormalColor = new Color(0.2f, 0.5f, 1f, 1f);
    [SerializeField] private Color useButtonDangerColor = new Color(0.9f, 0.2f, 0.25f, 1f);
    [SerializeField] private Color useButtonPrayColor = new Color(1f, 0.95f, 0.6f, 1f);

    private Color _defaultUseButtonLabelColor = Color.white;
    private Sprite _cachedUseButtonSprite;
    private bool _useButtonHasRainbowGeneratedSprite;
    private bool _useButtonHasBlockingSilverStyle;
    private bool _useButtonHasParryYellowStyle;
    private bool _useButtonHasArchMagicCastStyle;
    private Texture2D _rainbowUseButtonTexture;
    private Sprite _rainbowUseButtonSprite;
    private Texture2D _archMagicUseButtonTexture;
    private Sprite _archMagicUseButtonSprite;
    private float _defaultUseButtonLabelOutlineWidth;
    private Color _defaultUseButtonLabelOutlineColor = Color.black;
    private Material _useButtonLabelDefaultFontShared;
    /// <summary>打ち払い黄スタイル用に Instantiate したラベル材質。</summary>
    private Material _parryUseButtonLabelFontInstance;
    /// <summary>反射虹色スタイル用に Instantiate したラベル材質。</summary>
    private Material _reflectionUseButtonLabelFontInstance;
    /// <summary>使用／許す／防衛の黒字・白縁用ラベル材質。</summary>
    private Material _standardActionLabelFontInstance;
    /// <summary>YurusuButton 黒字・白縁用ラベル材質。</summary>
    private Material _yurusuLabelFontInstance;

    private const float BlackTextWhiteOutlineWidth = 0.22f;
    private const float BlackTextWhiteFaceDilate = 0.15f;
    private bool _yurusuLabelOutlineApplied;

    private void Awake()
    {
        if (useButton != null)
        {
            if (useButtonLabelTMP == null) useButtonLabelTMP = useButton.GetComponentInChildren<TMP_Text>(true);
            if (useButtonLabelUGUI == null) useButtonLabelUGUI = useButton.GetComponentInChildren<Text>(true);
            if (useButtonImage == null) useButtonImage = useButton.targetGraphic as Image;
            useButton.interactable = false;
        }

        if (yurusuDisplay != null)
        {
            yurusuDisplay.SetActive(false);
            if (yurusuButtonLabelTMP == null)
                yurusuButtonLabelTMP = yurusuDisplay.GetComponentInChildren<TMP_Text>(true);
        }

        if (useButtonImage != null)
            _cachedUseButtonSprite = useButtonImage.sprite;
    }

    private void Start()
    {
        CacheDefaultLabelAppearance();
    }

    private void OnDestroy()
    {
        if (_parryUseButtonLabelFontInstance != null)
        {
            Destroy(_parryUseButtonLabelFontInstance);
            _parryUseButtonLabelFontInstance = null;
        }
        if (_reflectionUseButtonLabelFontInstance != null)
        {
            Destroy(_reflectionUseButtonLabelFontInstance);
            _reflectionUseButtonLabelFontInstance = null;
        }
        ReleaseLabelFontInstance(ref _standardActionLabelFontInstance);
        ReleaseLabelFontInstance(ref _yurusuLabelFontInstance);
    }

    /// <summary>他の UI 要素でフォントを流用したいとき用。</summary>
    public TMP_FontAsset GetLabelFont() => useButtonLabelTMP != null ? useButtonLabelTMP.font : null;

    public void ShowYurusuDisplay()
    {
        if (yurusuDisplay == null) return;
        // TMP outline must run on an active hierarchy (inactive YurusuButton NREs in outlineWidth).
        yurusuDisplay.SetActive(true);
        EnsureYurusuLabelStyled();
    }

    public void HideYurusuDisplay()
    {
        if (yurusuDisplay == null) return;
        yurusuDisplay.SetActive(false);
    }

    public void SetUseButtonLabel(string text)
    {
        if (useButton == null) return;

        RestoreUseButtonFromReflectionRainbowIfNeeded();
        RestoreUseButtonFromBlockingSilverIfNeeded();
        RestoreUseButtonFromParryYellowIfNeeded();
        if (text != "詠唱開始" && text != "詠唱中")
            RestoreUseButtonFromArchMagicCastIfNeeded();

        if (useButtonLabelTMP != null) useButtonLabelTMP.text = text;
        if (useButtonLabelUGUI != null) useButtonLabelUGUI.text = text;

        // 大魔法：ピンク字・白縁・紫→水色グラデーション背景
        if (text == "詠唱開始" || text == "詠唱中")
        {
            ApplyArchMagicCastUseButtonStyle(text);
            return;
        }

        var mode = text == "許す" ? UseButtonMode.Allow
                 : text == "祈り" ? UseButtonMode.Pray
                 : text == "MPが足りない" || text == "魔法使用不可" ? UseButtonMode.MpShortage
                 : UseButtonMode.Use;

        if (mode == UseButtonMode.Use || mode == UseButtonMode.Allow)
            ApplyBlackTextWhiteOutlineToUseButtonLabel();
        else
        {
            StandardActionLabelReleaseFontInstanceIfNeeded();
            if (useButtonLabelTMP != null) useButtonLabelTMP.color = _defaultUseButtonLabelColor;
            if (useButtonLabelUGUI != null) useButtonLabelUGUI.color = _defaultUseButtonLabelColor;
        }

        ApplyUseButtonMode(mode);
    }

    public void SetUseButtonInteractable(bool interactable)
    {
        if (useButton != null) useButton.interactable = interactable;
    }

    /// <summary>ゲーム終了時に UseButton と許す表示を非アクティブ化。</summary>
    public void HideForGameEnd()
    {
        if (useButton != null)
            useButton.gameObject.SetActive(false);
        if (yurusuDisplay != null)
            yurusuDisplay.SetActive(false);
    }

    /// <summary>コンテキストに応じて UseButton / yurusuDisplay を一括更新する唯一の入口。</summary>
    public void Refresh()
    {
        if (useButton == null) return;
        var bm = BattleManager.I;
        if (bm == null) return;

        if (bm.IsPlayerDefenseInputActive())
            ApplyPlayerDefenseUseButton();
        else if (bm.CurrentState == GameState.AttackPhase && bm.CurrentTurnOwner == PlayerType.Player)
            ApplyPlayerAttackUseButton();
    }

    /// <summary>互換エイリアス。<see cref="Refresh"/> を呼ぶ。</summary>
    public void UpdateDefenseButtonLabel() => Refresh();

    /// <summary>互換エイリアス。<see cref="Refresh"/> を呼ぶ。</summary>
    public void RefreshUseButtonForMpAndSelection() => Refresh();

    private void ApplyPlayerDefenseUseButton()
    {
        var bm = BattleManager.I;
        if (bm == null || !bm.IsPlayerDefenseInputActive()) return;

        if (!bm.IsReactiveAdHocDefenseWaitActive()
            && (bm.IsUseButtonLocked || bm.IsPlayerDefenseCombatResolving))
        {
            SetUseButtonInteractable(false);
            return;
        }

        HideYurusuDisplay();

        var selectedDefenseCards = BattleUIManager.I != null
            ? BattleUIManager.I.GetSelectedDefenseCards()
            : new List<CardData>();

        List<CardData> incomingAttack = bm.GetIncomingAttackSnapshotForDefenseUi();

        bool showBounce = incomingAttack != null && incomingAttack.Count > 0
            && selectedDefenseCards.Count == 1
            && selectedDefenseCards[0] != null
            && ReflectionRules.RequiresReflectionExclusiveLock(selectedDefenseCards[0], incomingAttack);

        if (showBounce)
        {
            ApplyReflectionBounceUseButtonStyle();
            SetUseButtonInteractable(true);
            return;
        }

        bool showParry = incomingAttack != null && incomingAttack.Count > 0
            && selectedDefenseCards.Count == 1
            && selectedDefenseCards[0] != null
            && ParryRules.RequiresParryExclusiveLock(selectedDefenseCards[0], incomingAttack);

        if (showParry)
        {
            ApplyParryUseButtonStyle();
            SetUseButtonInteractable(true);
            return;
        }

        bool showBlockingNullify = incomingAttack != null && incomingAttack.Count > 0
            && selectedDefenseCards.Count == 1
            && selectedDefenseCards[0] != null
            && BlockingRules.RequiresBlockingExclusiveLock(selectedDefenseCards[0], incomingAttack);

        if (showBlockingNullify)
        {
            var ps = bm.GetPlayerStatus();
            if (ps != null && selectedDefenseCards[0].cardType == CardType.Magic
                && !BlockingRules.CanAffordMagicDefenseMp(selectedDefenseCards[0], ps))
            {
                SetUseButtonLabel("MPが足りない");
                SetUseButtonInteractable(false);
                return;
            }

            ApplyBlockingNullifyUseButtonStyle();
            SetUseButtonInteractable(true);
            return;
        }

        if (selectedDefenseCards.Count > 0)
            SetUseButtonLabel("使用");
        else
            SetUseButtonLabel("許す");
        SetUseButtonInteractable(true);
    }

    /// <summary>自プレイヤー攻撃フェーズの UseButton（祈り / 使用 / 詠唱開始 / MP不足等）。</summary>
    private void ApplyPlayerAttackUseButton()
    {
        var bm = BattleManager.I;
        if (bm == null) return;
        if (bm.CurrentState != GameState.AttackPhase || bm.CurrentTurnOwner != PlayerType.Player)
            return;
        if (bm.IsUseButtonLocked)
            return;

        var ps = bm.GetPlayerStatus();
        if (ps == null) return;

        var hand = bm.playerHand;
        if (hand != null && CardRules.GetAttackChoices(hand).Count == 0)
        {
            SetUseButtonLabel("祈り");
            SetUseButtonInteractable(true);
            return;
        }

        var selected = BattleUIManager.I != null
            ? BattleUIManager.I.GetSelectedCards()
            : null;
        if (selected == null || selected.Count == 0)
        {
            // 大魔法詠唱中：演出で選択がクリアされても「詠唱中」表示を維持（「使用」に戻さない）
            if (ps.IsCastingArchMagic)
            {
                SetUseButtonLabel("詠唱中");
                SetUseButtonInteractable(false);
                return;
            }
            SetUseButtonLabel("使用");
            SetUseButtonInteractable(false);
            return;
        }

        // 大魔法（ArchMagic）：単独使用・ラベルは「詠唱開始」・MP は archMagic の mpCost のみを確認
        var archMagic = ArchMagicRules.FindArchMagic(selected);
        if (archMagic != null)
        {
            if (archMagic.mpCost > ps.currentMP)
            {
                SetUseButtonLabel("MPが足りない");
                SetUseButtonInteractable(false);
                return;
            }
            SetUseButtonLabel("詠唱開始");
            SetUseButtonInteractable(true);
            return;
        }

        foreach (var c in selected)
        {
            if (c != null && c.cardType == CardType.Magic && ps.IsMagicUseForbidden())
            {
                SetUseButtonLabel("魔法使用不可");
                SetUseButtonInteractable(false);
                return;
            }
        }

        int magicTotal = ps.GetTotalEffectiveMagicMpForCards(selected);
        if (magicTotal > ps.currentMP)
        {
            SetUseButtonLabel("MPが足りない");
            SetUseButtonInteractable(false);
            return;
        }

        SetUseButtonLabel("使用");
        SetUseButtonInteractable(true);
    }

    private void ApplyUseButtonMode(UseButtonMode mode)
    {
        if (useButton == null) return;
        var img = useButtonImage ?? (useButton.targetGraphic as Image);
        if (img == null) return;

        img.color = mode == UseButtonMode.Allow || mode == UseButtonMode.MpShortage ? useButtonDangerColor
                 : mode == UseButtonMode.Pray ? useButtonPrayColor
                 : useButtonNormalColor;
    }

    private void ApplyReflectionBounceUseButtonStyle()
    {
        if (useButton == null) return;

        StandardActionLabelReleaseFontInstanceIfNeeded();
        RestoreUseButtonFromBlockingSilverIfNeeded();
        RestoreUseButtonFromParryYellowIfNeeded();
        RestoreUseButtonFromArchMagicCastIfNeeded();

        EnsureRainbowUseButtonSprite();

        var img = useButtonImage ?? (useButton.targetGraphic as Image);
        if (img != null && _rainbowUseButtonSprite != null)
        {
            img.sprite = _rainbowUseButtonSprite;
            img.color = Color.white;
            _useButtonHasRainbowGeneratedSprite = true;
        }

        if (useButtonLabelTMP != null)
        {
            EnsureUseButtonDefaultFontSharedCached();
            ReflectionReleaseLabelFontInstanceIfNeeded();

            useButtonLabelTMP.text = "弾き返す";
            useButtonLabelTMP.color = Color.white;
            const float ow = 0.28f;
            if (_useButtonLabelDefaultFontShared != null)
            {
                var mat = Instantiate(_useButtonLabelDefaultFontShared);
                useButtonLabelTMP.fontSharedMaterial = _useButtonLabelDefaultFontShared;
                useButtonLabelTMP.fontMaterial = mat;
                _reflectionUseButtonLabelFontInstance = mat;
                if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
                    mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.red);
                if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
                    mat.SetFloat(ShaderUtilities.ID_OutlineWidth, ow);
            }
            useButtonLabelTMP.outlineWidth = ow;
            useButtonLabelTMP.outlineColor = Color.red;
        }

        if (useButtonLabelUGUI != null)
        {
            useButtonLabelUGUI.text = "弾き返す";
            useButtonLabelUGUI.color = Color.white;
        }
    }

    /// <summary>無効化が有効なとき：灰色のボタン＋黒字の「防衛」。</summary>
    private void ApplyBlockingNullifyUseButtonStyle()
    {
        if (useButton == null) return;

        RestoreUseButtonFromReflectionRainbowIfNeeded();
        RestoreUseButtonFromParryYellowIfNeeded();
        RestoreUseButtonFromArchMagicCastIfNeeded();

        var img = useButtonImage ?? (useButton.targetGraphic as Image);
        if (img != null)
        {
            if (_cachedUseButtonSprite != null)
                img.sprite = _cachedUseButtonSprite;
            img.color = new Color(0.72f, 0.72f, 0.76f, 1f);
        }

        _useButtonHasBlockingSilverStyle = true;

        if (useButtonLabelTMP != null)
            useButtonLabelTMP.text = "防衛";
        if (useButtonLabelUGUI != null)
        {
            useButtonLabelUGUI.text = "防衛";
            useButtonLabelUGUI.color = Color.black;
        }

        ApplyBlackTextWhiteOutlineToUseButtonLabel();
    }

    private void RestoreUseButtonFromBlockingSilverIfNeeded()
    {
        if (!_useButtonHasBlockingSilverStyle) return;

        var img = useButtonImage ?? (useButton?.targetGraphic as Image);
        if (img != null)
        {
            img.sprite = _cachedUseButtonSprite;
            ApplyUseButtonMode(UseButtonMode.Use);
        }

        _useButtonHasBlockingSilverStyle = false;
    }

    /// <summary>打ち払い：黄色ボタン・白字・黒縁・「打ち払う」。</summary>
    private void ApplyParryUseButtonStyle()
    {
        if (useButton == null) return;

        StandardActionLabelReleaseFontInstanceIfNeeded();
        RestoreUseButtonFromReflectionRainbowIfNeeded();
        RestoreUseButtonFromBlockingSilverIfNeeded();
        RestoreUseButtonFromArchMagicCastIfNeeded();

        var img = useButtonImage ?? (useButton.targetGraphic as Image);
        if (img != null)
        {
            if (_cachedUseButtonSprite != null)
                img.sprite = _cachedUseButtonSprite;
            img.color = new Color(247f / 255f, 211f / 255f, 88f / 255f, 1f);
        }

        _useButtonHasParryYellowStyle = true;

        if (useButtonLabelTMP != null)
        {
            EnsureUseButtonDefaultFontSharedCached();
            ParryReleaseLabelFontInstanceIfNeeded();

            useButtonLabelTMP.text = "打ち払う";
            useButtonLabelTMP.color = Color.white;
            const float ow = 0.28f;
            if (_useButtonLabelDefaultFontShared != null)
            {
                var mat = Instantiate(_useButtonLabelDefaultFontShared);
                useButtonLabelTMP.fontSharedMaterial = _useButtonLabelDefaultFontShared;
                useButtonLabelTMP.fontMaterial = mat;
                _parryUseButtonLabelFontInstance = mat;
                if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
                    mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
                    mat.SetFloat(ShaderUtilities.ID_OutlineWidth, ow);
            }
            useButtonLabelTMP.outlineWidth = ow;
            useButtonLabelTMP.outlineColor = Color.black;
        }

        if (useButtonLabelUGUI != null)
        {
            useButtonLabelUGUI.text = "打ち払う";
            useButtonLabelUGUI.color = Color.white;
        }
    }

    private void EnsureUseButtonDefaultFontSharedCached()
    {
        CacheDefaultLabelAppearance();
    }

    private void CacheDefaultLabelAppearance()
    {
        if (useButtonLabelTMP == null) return;

        _defaultUseButtonLabelColor = useButtonLabelTMP.color;

        if (_useButtonLabelDefaultFontShared == null)
            _useButtonLabelDefaultFontShared = ResolveFontAssetMaterial(useButtonLabelTMP);

        if (useButtonLabelTMP.font == null || useButtonLabelTMP.fontMaterial == null)
            return;

        _defaultUseButtonLabelOutlineWidth = useButtonLabelTMP.outlineWidth;
        _defaultUseButtonLabelOutlineColor = useButtonLabelTMP.outlineColor;
    }

    private void EnsureYurusuLabelStyled()
    {
        if (_yurusuLabelOutlineApplied || yurusuButtonLabelTMP == null)
            return;

        ApplyBlackTextWhiteOutline(yurusuButtonLabelTMP, null, ref _yurusuLabelFontInstance);
        _yurusuLabelOutlineApplied = true;
    }

    /// <summary>
    /// Font Asset 本体の material のみ返す。tmp.fontSharedMaterial は instance 化後に汚染されるため使わない。
    /// </summary>
    private static Material ResolveFontAssetMaterial(TMP_Text tmp)
    {
        if (tmp?.font == null) return null;
        return tmp.font.material;
    }

    private void ParryReleaseLabelFontInstanceIfNeeded()
    {
        if (_parryUseButtonLabelFontInstance == null) return;
        var inst = _parryUseButtonLabelFontInstance;
        _parryUseButtonLabelFontInstance = null;
        Destroy(inst);
        if (useButtonLabelTMP != null && _useButtonLabelDefaultFontShared != null)
            useButtonLabelTMP.fontSharedMaterial = _useButtonLabelDefaultFontShared;
    }

    private void RestoreUseButtonFromParryYellowIfNeeded()
    {
        if (!_useButtonHasParryYellowStyle) return;

        var img = useButtonImage ?? (useButton?.targetGraphic as Image);
        if (img != null)
        {
            img.sprite = _cachedUseButtonSprite;
            img.color = Color.white;
        }

        _useButtonHasParryYellowStyle = false;

        if (useButtonLabelTMP != null)
        {
            ParryReleaseLabelFontInstanceIfNeeded();
            if (_useButtonLabelDefaultFontShared != null)
                useButtonLabelTMP.fontSharedMaterial = _useButtonLabelDefaultFontShared;

            SafeRestoreTmpOutline(useButtonLabelTMP, _defaultUseButtonLabelOutlineWidth, _defaultUseButtonLabelOutlineColor);
        }
    }

    private void EnsureRainbowUseButtonSprite()
    {
        if (_rainbowUseButtonSprite != null) return;

        const int w = 256;
        const int h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        for (int y = 0; y < h; y++)
        {
            float fy = h <= 1 ? 0.5f : y / (float)(h - 1);
            for (int x = 0; x < w; x++)
            {
                float fx = w <= 1 ? 0.5f : x / (float)(w - 1);
                float t = Mathf.Clamp01((fx + (1f - fy)) * 0.5f);
                float hue = Mathf.Repeat(t * 0.95f + 0.72f, 1f);
                tex.SetPixel(x, y, Color.HSVToRGB(hue, 0.68f, 1f));
            }
        }

        tex.Apply();
        _rainbowUseButtonTexture = tex;
        _rainbowUseButtonSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private void ReflectionReleaseLabelFontInstanceIfNeeded()
    {
        if (_reflectionUseButtonLabelFontInstance == null) return;
        var inst = _reflectionUseButtonLabelFontInstance;
        _reflectionUseButtonLabelFontInstance = null;
        Destroy(inst);
        if (useButtonLabelTMP != null && _useButtonLabelDefaultFontShared != null)
            useButtonLabelTMP.fontSharedMaterial = _useButtonLabelDefaultFontShared;
    }

    private void RestoreUseButtonFromReflectionRainbowIfNeeded()
    {
        if (!_useButtonHasRainbowGeneratedSprite) return;

        var img = useButtonImage ?? (useButton?.targetGraphic as Image);
        if (img != null)
        {
            img.sprite = _cachedUseButtonSprite;
            img.color = Color.white;
        }

        if (useButtonLabelTMP != null)
        {
            ReflectionReleaseLabelFontInstanceIfNeeded();
            if (_useButtonLabelDefaultFontShared != null)
                useButtonLabelTMP.fontSharedMaterial = _useButtonLabelDefaultFontShared;

            SafeRestoreTmpOutline(useButtonLabelTMP, _defaultUseButtonLabelOutlineWidth, _defaultUseButtonLabelOutlineColor);
        }

        if (_rainbowUseButtonTexture != null)
        {
            Destroy(_rainbowUseButtonTexture);
            _rainbowUseButtonTexture = null;
        }

        if (_rainbowUseButtonSprite != null)
        {
            Destroy(_rainbowUseButtonSprite);
            _rainbowUseButtonSprite = null;
        }

        _useButtonHasRainbowGeneratedSprite = false;
    }

    /// <summary>大魔法「詠唱開始／詠唱中」：ラベル #C400A8・白縁、ボタン背景は左 #9b55fc → 右 #09f9e4 のグラデーション。</summary>
    private void ApplyArchMagicCastUseButtonStyle(string label)
    {
        if (useButton == null) return;

        StandardActionLabelReleaseFontInstanceIfNeeded();
        RestoreUseButtonFromParryYellowIfNeeded();

        EnsureArchMagicGradientUseButtonSprite();

        var img = useButtonImage ?? (useButton.targetGraphic as Image);
        if (img != null && _archMagicUseButtonSprite != null)
        {
            img.sprite = _archMagicUseButtonSprite;
            img.color = Color.white;
            _useButtonHasArchMagicCastStyle = true;
        }

        var pink = new Color(0xC4 / 255f, 0x00 / 255f, 0xA8 / 255f, 1f);
        if (useButtonLabelTMP != null)
        {
            useButtonLabelTMP.text = label;
            useButtonLabelTMP.color = pink;
            useButtonLabelTMP.outlineColor = Color.white;
            useButtonLabelTMP.outlineWidth = 0.22f;
        }

        if (useButtonLabelUGUI != null)
        {
            useButtonLabelUGUI.text = label;
            useButtonLabelUGUI.color = pink;
        }
    }

    private void EnsureArchMagicGradientUseButtonSprite()
    {
        if (_archMagicUseButtonSprite != null) return;

        const int w = 256;
        const int h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var left = new Color(0x9b / 255f, 0x55 / 255f, 0xfc / 255f, 1f);
        var right = new Color(0x09 / 255f, 0xf9 / 255f, 0xe4 / 255f, 1f);

        for (int x = 0; x < w; x++)
        {
            float t = w <= 1 ? 0.5f : x / (float)(w - 1);
            Color c = Color.Lerp(left, right, t);
            for (int y = 0; y < h; y++)
                tex.SetPixel(x, y, c);
        }

        tex.Apply();
        _archMagicUseButtonTexture = tex;
        _archMagicUseButtonSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private void RestoreUseButtonFromArchMagicCastIfNeeded()
    {
        if (!_useButtonHasArchMagicCastStyle) return;

        var img = useButtonImage ?? (useButton?.targetGraphic as Image);
        if (img != null)
        {
            if (_cachedUseButtonSprite != null)
                img.sprite = _cachedUseButtonSprite;
            img.color = Color.white;
        }

        if (useButtonLabelTMP != null)
        {
            SafeRestoreTmpOutline(useButtonLabelTMP, _defaultUseButtonLabelOutlineWidth, _defaultUseButtonLabelOutlineColor);
        }

        if (_archMagicUseButtonTexture != null)
        {
            Destroy(_archMagicUseButtonTexture);
            _archMagicUseButtonTexture = null;
        }

        if (_archMagicUseButtonSprite != null)
        {
            Destroy(_archMagicUseButtonSprite);
            _archMagicUseButtonSprite = null;
        }

        _useButtonHasArchMagicCastStyle = false;
    }

    private static void SafeRestoreTmpOutline(TMP_Text tmp, float width, Color color)
    {
        if (tmp == null || !tmp.gameObject.activeInHierarchy) return;
        if (tmp.font == null || tmp.fontMaterial == null) return;
        tmp.outlineWidth = width;
        tmp.outlineColor = color;
    }

    private void ApplyBlackTextWhiteOutlineToUseButtonLabel()
    {
        ApplyBlackTextWhiteOutline(useButtonLabelTMP, useButtonLabelUGUI, ref _standardActionLabelFontInstance);
    }

    private void ApplyBlackTextWhiteOutline(TMP_Text tmp, Text ugui, ref Material fontInstance)
    {
        if (ugui != null)
            ugui.color = Color.black;
        if (tmp == null || tmp.font == null)
            return;
        if (!tmp.gameObject.activeInHierarchy)
            return;

        tmp.color = Color.black;
        EnsureUseButtonDefaultFontSharedCached();

        ReleaseLabelFontInstance(ref fontInstance);

        const float ow = BlackTextWhiteOutlineWidth;
        var sharedMat = _useButtonLabelDefaultFontShared ?? ResolveFontAssetMaterial(tmp);
        if (sharedMat == null)
            return;

        var mat = Instantiate(sharedMat);
        tmp.fontSharedMaterial = sharedMat;
        tmp.fontMaterial = mat;
        fontInstance = mat;
        if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.white);
        if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, ow);
        if (mat.HasProperty(ShaderUtilities.ID_FaceDilate))
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, BlackTextWhiteFaceDilate);

        // Outline is on the material instance only — tmp.outlineWidth NREs when TMP is not fully initialized.
        tmp.UpdateMeshPadding();
        tmp.ForceMeshUpdate();
    }

    private static void ReleaseLabelFontInstance(ref Material instance)
    {
        if (instance == null) return;
        var inst = instance;
        instance = null;
        Destroy(inst);
    }

    private void StandardActionLabelReleaseFontInstanceIfNeeded()
    {
        ReleaseLabelFontInstance(ref _standardActionLabelFontInstance);
        if (useButtonLabelTMP != null && _useButtonLabelDefaultFontShared != null)
            useButtonLabelTMP.fontSharedMaterial = _useButtonLabelDefaultFontShared;
    }
}
