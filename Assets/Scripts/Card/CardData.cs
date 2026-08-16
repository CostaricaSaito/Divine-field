using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum CardType
{
    Attack = 0,
    Defense = 1,
    Magic = 2,
    Recovery = 3,
    Special = 4,
    /// <summary>
    /// 大魔法。詠唱ターンを経て発動する単独使用カード。MagicPanel に行かず、他カードと併用不可。
    /// 反射・無効化を受けない。<see cref="ArchMagicRuleSO"/> と連携。
    /// </summary>
    ArchMagic = 5,
    /// <summary>
    /// Ultimate Skill only. Not dealt to hand; resolved only through the ultimate skill flow.
    /// </summary>
    Ultimate = 6,
    /// <summary>
    /// 天変地異。通常ドロー不可。トリガー経由でのみ発動。
    /// </summary>
    Disaster = 7,
}

public enum ElementType
{
    None,
    Fire,
    Water,
    Wind,
    Thunder,
    Steel,
    Ice,
    Dark,
    Light,
}

/// <summary>分解用。反射＋無効＋打ち払いは <see cref="CardData.reactiveInteraction"/> 1 列で保持。</summary>
public enum ReflectionKind
{
    None = 0,
    Physical = 1,
    Magic = 2,
    Full = 3,
}

public enum BlockingKind
{
    None = 0,
    Physical = 1,
    Magic = 2,
    Full = 3,
}

public enum ParryKind
{
    None = 0,
    Physical = 1,
    Magic = 2,
    Full = 3,
}

/// <summary>従: attackComboPickRule。派生は <see cref="CardData.attackComboPickRule"/>。</summary>
public enum AttackComboPickRule
{
    StandaloneAllowed = 0,
    ComboAttachmentOnly = 1,
}

public enum SelectionRole
{
    None = 0,
    Standalone = 1,
    Primary = 2,
    Addable = 3,
    Free = 4,
}

/// <summary>
/// 攻撃フェーズの手札組み合わせ。使用可否は <see cref="CardData.usableInAttackPhase"/> 等で別定義。
/// </summary>
public enum AttackPhaseUseRule
{
    /// <summary>メイン。他カードの後乗り可。メイン同士は 1 手札選択内で並ばない想定（Primary 衝突ルール）。</summary>
    Primary = 0,
    /// <summary>Standalone only. ArchMagic / Ultimate Skill / single immediate slot etc.</summary>
    Standalone = 1,
    /// <summary>単独でもメインに連結でも可。手札上は常に ATK+ 表記想定。従: Neutral/Addable 相当の「後乗り可」含む。</summary>
    Flexible = 2,
    /// <summary>メイン(Primary/Flexible)の後乗りのみ。先に攻撃1枚以上必要。</summary>
    AddOn = 3,
}

/// <summary>防御フェーズ手札衝突（従: defensePhaseRole 相当の値を維持）。</summary>
public enum DefensePhaseUseRule
{
    None = 0,
    Standalone = 1,
    Primary = 2,
    Addable = 3,
    Free = 4,
}

public enum ReactiveInteractionKind
{
    None = 0,
    Reflect_Physical = 1,
    Reflect_Magic = 2,
    Reflect_Full = 3,
    Block_Physical = 4,
    Block_Magic = 5,
    Block_Full = 6,
    Parry_Physical = 7,
    Parry_Magic = 8,
    Parry_Full = 9,
}

public static class ReactiveInteractionCodec
{
    public static ReflectionKind GetReflectionKind(ReactiveInteractionKind v)
    {
        if (v >= ReactiveInteractionKind.Reflect_Physical && v <= ReactiveInteractionKind.Reflect_Full)
            return (ReflectionKind)((int)v - (int)ReactiveInteractionKind.Reflect_Physical + 1);
        return ReflectionKind.None;
    }

    public static BlockingKind GetBlockingKind(ReactiveInteractionKind v)
    {
        if (v >= ReactiveInteractionKind.Block_Physical && v <= ReactiveInteractionKind.Block_Full)
            return (BlockingKind)((int)v - (int)ReactiveInteractionKind.Block_Physical + 1);
        return BlockingKind.None;
    }

    public static ParryKind GetParryKind(ReactiveInteractionKind v)
    {
        if (v >= ReactiveInteractionKind.Parry_Physical && v <= ReactiveInteractionKind.Parry_Full)
            return (ParryKind)((int)v - (int)ReactiveInteractionKind.Parry_Physical + 1);
        return ParryKind.None;
    }

    public static ReactiveInteractionKind FromLegacy(ReflectionKind r, BlockingKind b, ParryKind p)
    {
        if (r != ReflectionKind.None)
            return (ReactiveInteractionKind)((int)ReactiveInteractionKind.Reflect_Physical + (int)r - 1);
        if (b != BlockingKind.None)
            return (ReactiveInteractionKind)((int)ReactiveInteractionKind.Block_Physical + (int)b - 1);
        if (p != ParryKind.None)
            return (ReactiveInteractionKind)((int)ReactiveInteractionKind.Parry_Physical + (int)p - 1);
        return ReactiveInteractionKind.None;
    }
}

public static class AttackPhaseUseRuleCodec
{
    /// <summary>CardSelectionManager の衝突スイッチ用（SelectionRole への射影）。</summary>
    /// <remarks>
    /// <see cref="AttackPhaseUseRule.Primary"/> は <see cref="SelectionRole.Primary"/>。Standalone に落とすと
    /// Addable 追加時の <c>HasRoleSelected(Standalone)</c> 誤爆で Primary＋Flexible が併用不能になる。
    /// <see cref="AttackPhaseUseRule.Standalone"/>（大魔法等）だけ <see cref="SelectionRole.Standalone"/>。
    /// </remarks>
    public static SelectionRole ToSelectionRole(AttackPhaseUseRule rule)
    {
        return rule switch
        {
            AttackPhaseUseRule.Primary => SelectionRole.Primary,
            AttackPhaseUseRule.Flexible => SelectionRole.Addable,
            AttackPhaseUseRule.AddOn => SelectionRole.Addable,
            AttackPhaseUseRule.Standalone => SelectionRole.Standalone,
            _ => SelectionRole.None,
        };
    }

    /// <summary>旧 attackHandComboMode 整数 0..4（Neutral..AddOn）→ 新4値。既定 Flexible。</summary>
    public static AttackPhaseUseRule FromLegacyInt(int v1)
    {
        return (LegacyV1Hcm)Mathf.Clamp(v1, 0, 4) switch
        {
            LegacyV1Hcm.Neutral => AttackPhaseUseRule.Flexible,
            LegacyV1Hcm.PrimaryV1 => AttackPhaseUseRule.Primary,
            LegacyV1Hcm.Additional => AttackPhaseUseRule.Flexible,
            LegacyV1Hcm.Unrestricted => AttackPhaseUseRule.Flexible,
            LegacyV1Hcm.AddOnV1 => AttackPhaseUseRule.AddOn,
            _ => AttackPhaseUseRule.Flexible,
        };
    }

    private enum LegacyV1Hcm
    {
        Neutral = 0,
        PrimaryV1 = 1,
        Additional = 2,
        Unrestricted = 3,
        AddOnV1 = 4,
    }

    public static AttackPhaseUseRule MigrateFromLegacy(
        SelectionRole attackRole,
        AttackComboPickRule pick,
        bool combinationMagic)
    {
        if (pick == AttackComboPickRule.ComboAttachmentOnly) return AttackPhaseUseRule.AddOn;
        if (attackRole == SelectionRole.Addable) return AttackPhaseUseRule.Flexible;
        if (attackRole is SelectionRole.Standalone or SelectionRole.Primary) return AttackPhaseUseRule.Primary;
        if (attackRole == SelectionRole.Free) return AttackPhaseUseRule.Flexible;
        if (combinationMagic) return AttackPhaseUseRule.Flexible;
        return AttackPhaseUseRule.Flexible;
    }
}

public enum StatusEffectApplyTiming
{
    WithDamageThrough = 0,
    OnCardEffectResolve = 1,
}

[CreateAssetMenu(fileName = "NewCard", menuName = "DivineField/Card")]
public class CardData : ScriptableObject, ISerializationCallbackReceiver
{
    [Header("基本情報")]
    public string cardName;
    [Tooltip("シーケンス分岐は CardSequenceManager 参照。")]
    public CardType cardType;
    public Sprite cardImage;
    [TextArea(2, 4)] public string description;

    [Header("数値パラメータ")]
    public int attackPower = 0;
    public int defensePower = 0;
    public int cardValue = 0;
    public ElementType element = ElementType.None;
    [Range(0, 100)] public int hitRate = 100;

    [Header("魔法カード専用")]
    public int mpCost = 0;
    [Tooltip("MagicPool 残数・再使用等。")]
    public int maxUses = 1;

    [Header("回復パラメータ")]
    public int recoveryAmount = 0;
    public bool healsHP = false;
    public bool healsMP = false;
    public bool healsGP = false;
    [Tooltip("Cure ALL Status Effect: 解決時に全状態異常を除去。")]
    [FormerlySerializedAs("clearsAllStatusAilmentsOnUse")]
    public bool cureAllStatusEffects = false;

    [Header("使用可能なフェーズ")]
    [Tooltip("攻撃フェーズ手札で選べるか。Magic では false のとき AttackPhaseUseRule に関わらずグレーアウト（防御専用魔法など）。")]
    public bool usableInAttackPhase = false;
    public bool usableInDefensePhase = false;

    [SerializeField, HideInInspector, FormerlySerializedAs("isPrimaryDefense")]
    private bool _legacyIsPrimaryDefense;
    [SerializeField, HideInInspector, FormerlySerializedAs("isCounterAttack")]
    private bool _legacyIsCounterAttack;
    [SerializeField, HideInInspector, FormerlySerializedAs("isRecovery")]
    private bool _legacyIsRecovery;
    [SerializeField, HideInInspector, FormerlySerializedAs("isSpecialEffect")]
    private bool _legacyIsSpecialEffect;
    [SerializeField, HideInInspector]
    private bool _legacyActionClassImported;

    [Header("特殊効果")]
    public bool canApplyStatusEffect = false;
    [Range(0, 100)]
    [Tooltip("WithDamageThrough / OnCardEffectResolve 等。")]
    public int statusEffectChance = 0;
    public StatusEffectType statusEffectToApply = StatusEffectType.None;
    [Min(1)]
    [Tooltip("statusEffectToApply=Freeze または RandomOneAilment で凍結が選ばれたときの持続ターン。")]
    public int freezeDuration = 2;
    [Tooltip("① ダメージ通過 / ② 解決時のみ 等。")]
    public StatusEffectApplyTiming statusEffectApplyTiming = StatusEffectApplyTiming.WithDamageThrough;

    [Header("演出")]
    [Tooltip("手札抽選レア度。SuperRare 以上で裏面虹・レアSE。")]
    public CardRarity rarity = CardRarity.Common;

    [Header("手札抽選")]
    [Tooltip("-1 = CardDrawTable のレア度デフォルト重み。0 以上 = 個別重み（0 は抽選除外）。")]
    public int customDrawWeight = CardDrawWeightPool.UseRarityDefaultWeight;

    [SerializeField, HideInInspector, FormerlySerializedAs("isRare")]
    private bool _legacyIsRare;

    [Header("攻撃 Phase Use Rule（手札併用）")]
    [Tooltip("Attack-phase combo rule. ArchMagic / Ultimate Skill cards should use Standalone.")]
    public AttackPhaseUseRule attackPhaseUseRule = AttackPhaseUseRule.Flexible;

    [Header("防御 Phase Use Rule（手札衝突）")]
    [Tooltip("防御フェーズ専用。旧 defensePhaseRole と同じ整数 (0-4) 互換。")]
    [FormerlySerializedAs("defensePhaseRole")]
    public DefensePhaseUseRule defensePhaseUseRule = DefensePhaseUseRule.None;

    [Header("防御特殊反応（反射/無効/打ち払いの1つ）")]
    public ReactiveInteractionKind reactiveInteraction = ReactiveInteractionKind.None;

    [Header("特殊攻撃ルール（任意）")]
    public SpecialAttackRuleSO specialAttackRule;

    [Header("Special カード")]
    [Tooltip("cardType=Special 時の即時効果。")]
    public SpecialCardEffectSO specialCardEffect;

    [Header("Disaster カード")]
    [Tooltip("cardType=Disaster 時の天変地異効果（表示用 CardData に紐付け）。")]
    public DisasterCardEffectSO disasterCardEffect;

    [Header("Post-Death カード")]
    [Tooltip("HP0 後の PostDeathEffectQueue で解決する効果（攻撃／防御フェーズでは使用不可）。")]
    public PostDeathCardEffectSO postDeathCardEffect;

    [Header("Near-Death カード")]
    [Tooltip("HP0 検出後「往生」の直前に解決する効果（不死鳥の尾羽根等）。")]
    public NearDeathCardEffectSO nearDeathCardEffect;

    [Header("手札パッシブ")]
    [Tooltip("いかなるフェーズでも手動選択・使用不可（道連れ・不死鳥等）。")]
    public bool passiveHandOnly;

    [Header("宝玉系（防御・任意）")]
    [Tooltip("第1段の実ダメ通過時に臨時効果。DEF0 だけのカード識別には使わない。")]
    public OrbCardRuleSO orbReactionRule;

    [Header("カードシート表示")]
    [Tooltip("指定時は CardSheet の BG にこのスプライトを使用（カード種別を問わない）。")]
    public Sprite cardDisplayFrameSprite;

    [Header("UI参照（非表示）")]
    [NonSerialized] public CardUI cardUI;

    // ---- 旧シリアル移行用 ----
    [SerializeField, HideInInspector, FormerlySerializedAs("reflectionKind")]
    private ReflectionKind _legacyReflection;
    [SerializeField, HideInInspector, FormerlySerializedAs("blockingKind")]
    private BlockingKind _legacyBlocking;
    [SerializeField, HideInInspector, FormerlySerializedAs("parryKind")]
    private ParryKind _legacyParry;
    [SerializeField, HideInInspector] private bool _legacyReactiveImported;

    [SerializeField, HideInInspector, FormerlySerializedAs("attackPhaseRole")]
    private SelectionRole _legacyAttackPhaseRole;
    [SerializeField, HideInInspector, FormerlySerializedAs("attackComboPickRule")]
    private AttackComboPickRule _legacyComboPick;
    [SerializeField, HideInInspector, FormerlySerializedAs("isCombinationMagic")]
    private bool _legacyIsCombinationMagic;
    [SerializeField, HideInInspector] private bool _legacyAttackComboImported;

    [SerializeField, HideInInspector, FormerlySerializedAs("isPrimaryAttack")]
    private bool _migIsPrimaryAttack;
    [SerializeField, HideInInspector, FormerlySerializedAs("isAdditionalAttack")]
    private bool _migIsAdditionalAttack;
    [SerializeField, HideInInspector] private bool _migPrimaryAdditionalImported;

    [SerializeField, HideInInspector, FormerlySerializedAs("attackHandComboMode")]
    private int _migV1Hcm = -1;

    /// <summary>従: attackPhaseRole（攻撃用）。衝突スイッチ向け射影。</summary>
    public SelectionRole attackPhaseRole => AttackPhaseUseRuleCodec.ToSelectionRole(attackPhaseUseRule);

    /// <summary>互換: 防御衝突は <see cref="defensePhaseUseRule"/>。SelectionRole と同じ底。</summary>
    public SelectionRole defensePhaseRole => (SelectionRole)defensePhaseUseRule;

    public AttackComboPickRule attackComboPickRule =>
        attackPhaseUseRule == AttackPhaseUseRule.AddOn
            ? AttackComboPickRule.ComboAttachmentOnly
            : AttackComboPickRule.StandaloneAllowed;

    public bool isCombinationMagic =>
        attackPhaseUseRule == AttackPhaseUseRule.Flexible
        || attackPhaseUseRule == AttackPhaseUseRule.AddOn;

    public ReflectionKind reflectionKind => ReactiveInteractionCodec.GetReflectionKind(reactiveInteraction);
    public BlockingKind blockingKind => ReactiveInteractionCodec.GetBlockingKind(reactiveInteraction);
    public ParryKind parryKind => ReactiveInteractionCodec.GetParryKind(reactiveInteraction);

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        if (!_legacyReactiveImported)
        {
            if (reactiveInteraction == ReactiveInteractionKind.None
                && (_legacyReflection != ReflectionKind.None
                    || _legacyBlocking != BlockingKind.None
                    || _legacyParry != ParryKind.None))
            {
                reactiveInteraction = ReactiveInteractionCodec.FromLegacy(_legacyReflection, _legacyBlocking, _legacyParry);
            }
            _legacyReflection = ReflectionKind.None;
            _legacyBlocking = BlockingKind.None;
            _legacyParry = ParryKind.None;
            _legacyReactiveImported = true;
        }

        if (_migV1Hcm >= 0)
        {
            attackPhaseUseRule = AttackPhaseUseRuleCodec.FromLegacyInt(_migV1Hcm);
            _migV1Hcm = -1;
        }

        if (!_legacyAttackComboImported)
        {
            bool legacyHad =
                _legacyComboPick != AttackComboPickRule.StandaloneAllowed
                || _legacyAttackPhaseRole != SelectionRole.None
                || _legacyIsCombinationMagic;
            if (legacyHad)
            {
                var fromOld = AttackPhaseUseRuleCodec.MigrateFromLegacy(
                    _legacyAttackPhaseRole, _legacyComboPick, _legacyIsCombinationMagic);
                attackPhaseUseRule = fromOld;
            }
            _legacyAttackPhaseRole = SelectionRole.None;
            _legacyComboPick = AttackComboPickRule.StandaloneAllowed;
            _legacyIsCombinationMagic = false;
            _legacyAttackComboImported = true;
        }

        if (!_migPrimaryAdditionalImported)
        {
            if (_migIsPrimaryAttack) attackPhaseUseRule = AttackPhaseUseRule.Primary;
            else if (_migIsAdditionalAttack) attackPhaseUseRule = AttackPhaseUseRule.Flexible;
            _migIsPrimaryAttack = false;
            _migIsAdditionalAttack = false;
            _migPrimaryAdditionalImported = true;
        }

        if (cardType == CardType.ArchMagic || cardType == CardType.Ultimate)
            attackPhaseUseRule = AttackPhaseUseRule.Standalone;

        if (!_legacyActionClassImported)
        {
            if (_legacyIsPrimaryDefense)
            {
                if (!usableInDefensePhase) usableInDefensePhase = true;
                if (defensePhaseUseRule == DefensePhaseUseRule.None)
                    defensePhaseUseRule = DefensePhaseUseRule.Primary;
            }

            if (_legacyIsCounterAttack)
            {
                usableInAttackPhase = true;
                usableInDefensePhase = true;
                if (defensePhaseUseRule == DefensePhaseUseRule.None)
                    defensePhaseUseRule = DefensePhaseUseRule.Standalone;
            }

            if (_legacyIsRecovery)
            {
                if (cardType == CardType.Attack)
                    cardType = CardType.Recovery;
                if (!usableInAttackPhase)
                    usableInAttackPhase = true;
            }

            if (_legacyIsSpecialEffect)
            {
                if (cardType == CardType.Attack)
                    cardType = CardType.Special;
            }

            _legacyIsPrimaryDefense = false;
            _legacyIsCounterAttack = false;
            _legacyIsRecovery = false;
            _legacyIsSpecialEffect = false;
            _legacyActionClassImported = true;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (cardType == CardType.ArchMagic || cardType == CardType.Ultimate)
            attackPhaseUseRule = AttackPhaseUseRule.Standalone;

        if (_legacyIsRare && rarity == CardRarity.Common)
            rarity = CardRarity.SuperRare;
    }
#endif
}

public static class CardDealAudio
{
    public const string NormalPath = "Assets/SE/普通カード.mp3";
    public const string RarePath = "Assets/SE/レアカード.mp3";

    public static void Play(CardData card) => Play(card, false);

    public static void Play(CardData card, bool isPlayerHandDeal)
    {
        string path = (card != null && card.HasPremiumHandPresentation()) ? RarePath : NormalPath;
        SoundEffectPlayer.I?.Play(path);
        if (!isPlayerHandDeal || card == null || !card.HasPremiumHandPresentation()) return;
        if (BattleManager.I == null) return;
        if (!DisadvantageRules.IsDisadvantaged(BattleManager.I.GetPlayerStatus())) return;
        BattleUIManager.I?.PlayFullscreenWhiteFlashMs(50f);
    }
}
