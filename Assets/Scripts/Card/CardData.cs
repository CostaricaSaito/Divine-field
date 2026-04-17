using UnityEngine;
using UnityEngine.UI;


public enum CardType
{
    Attack = 0,
    Defense = 1,
    Magic = 2,
    Recovery = 3,
    Special = 4,
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

/// <summary>
/// 同一フェーズで複数枚選ぶときの「衝突解決」用。実装は <see cref="CardSelectionManager"/> の競合チェックのみ。
/// <see cref="CardRules"/> のフェーズ可否とは独立。
/// </summary>
public enum SelectionRole
{
    /// <summary>
    /// 競合による全クリアは行わない（<c>switch</c> に該当ケースなし）。他カードと併選しやすい。
    /// </summary>
    None = 0,
    /// <summary>既に何か選ばれていれば全選択クリアのうえで単独。</summary>
    Standalone = 1,
    /// <summary><see cref="Standalone"/> と同様に、既選択があれば全クリア。</summary>
    Primary = 2,
    /// <summary>
    /// <see cref="Standalone"/> が既に選ばれていると全クリア。魔法が既に選ばれている状態で別魔法を足す場合もクリア。
    /// 攻撃フェーズでは <see cref="CardLayoutManager"/> が「メイン攻撃の直後」の並びに使う。
    /// </summary>
    Addable = 3,
    /// <summary>競合クリアなし（常に追加可能）。</summary>
    Free = 4,
}

public interface ISpecialCardEffect
{
    void Activate(PlayerStatus player, PlayerStatus enemy);
}

/// <summary>
/// 状態異常の付与タイミング。
/// ① <see cref="WithDamageThrough"/> … 戦闘で命中し、かつ最終ダメージが1以上のときのみ（攻撃・攻撃魔法のコンボ）。
/// ② <see cref="OnCardEffectResolve"/> … 即時解決カードは解決時に付与。戦闘コンボ内のカードは命中していれば最終ダメージ0でも付与（無効化・ミス時は別途処理）。
/// </summary>
public enum StatusEffectApplyTiming
{
    WithDamageThrough = 0,
    OnCardEffectResolve = 1,
}

[CreateAssetMenu(fileName = "NewCard", menuName = "DivineField/Card")]
public class CardData : ScriptableObject
{

    [Header("基本情報")]
    public string cardName;
    public CardType cardType;
    public Sprite cardImage;
    [TextArea(2, 4)] public string description;

    [Header("数値パラメータ")]
    public int attackPower = 0;
    public int defensePower = 0;
    public ElementType element = ElementType.None;
    [Range(0, 100)] public int hitRate = 100;

    [Header("魔法カード専用")]
    public int mpCost = 0;
    [Tooltip("MagicPool の残り回数計算に使用（TryUseMagicCard 等）。同種再使用時の回復量にも関与。")]
    public int maxUses = 1;
    [Tooltip("主に CardSequenceManager のログ・フロー表示用。命中・MP 等のルール分岐には未使用。")]
    public bool isCombinationMagic = false;

    [Header("回復パラメータ")]
    public int recoveryAmount = 0; 
    public bool healsHP = false;
    public bool healsMP = false;
    public bool healsGP = false;
    [Tooltip("回復解決時に対象から拘束を除去する（特定の治癒カード用）。")]
    public bool clearsRestraintOnUse = false;

    [Header("使用可能なフェーズ")]
    public bool usableInAttackPhase = false;
    public bool usableInDefensePhase = false;

    [Header("行動分類フラグ")]
    [Tooltip("フェーズ可否の上書きに使用。isPrimaryDefense と同時に true だと両フェーズ可だが CardRules.IsAttackCard/IsDefenseCard はどちらも false になる。")]
    public bool isPrimaryAttack = false;         // 例：剣、炎の拳
    public bool isAdditionalAttack = false;      // 例：連撃、火の粉
    [Tooltip("フェーズ可否の上書き・表示順（防御優先）。isPrimaryAttack と同時に true にしないことを推奨。")]
    public bool isPrimaryDefense = false;        // 例：盾
    public bool isCounterAttack = false;         // 例：反射剣、カウンター
    public bool isRecovery = false;              // 例：回復草
    public bool isSpecialEffect = false;         // 例：精霊のぬいぐるみ

    [Header("特殊効果（任意）")]
    public bool canApplyStatusEffect = false;
    [Range(0, 100)] public int statusEffectChance = 0;
    public StatusEffectType statusEffectToApply = StatusEffectType.None;
    [Tooltip("①ダメージが通ったときのみ / ②カード解決時（ダメージ不要）。攻撃力では分けない（Inspectorで明示）。")]
    public StatusEffectApplyTiming statusEffectApplyTiming = StatusEffectApplyTiming.WithDamageThrough;

    [Header("経済パラメータ")]
    public int cardValue = 0;

    [Header("演出")]
    [Tooltip("自分の手札に裏向きで入ったときのレアSE・虹オーバーレイに使用（PvPではローカル手札のみ）。")]
    public bool isRare = false;

    [Header("選択ロール")]
    [Tooltip("攻撃選択時の複数枚衝突解決と、CardLayoutManager の表示順（Addable＝メイン攻撃の直後）。None は既選択を消さずに追加しやすい。")]
    public SelectionRole attackPhaseRole = SelectionRole.None;
    [Tooltip("防御選択時の複数枚衝突解決。None は既選択を消さずに追加しやすい。")]
    public SelectionRole defensePhaseRole = SelectionRole.None;

    [Header("UI参照（非表示）")]
    [System.NonSerialized] public CardUI cardUI;

}

/// <summary>
/// 手札への配布・めくりなど「カードが手札コンテキストで鳴る」SE。レアは Addressable キーを切り替える。
/// </summary>
public static class CardDealAudio
{
    public const string NormalPath = "Assets/SE/普通カード.mp3";
    public const string RarePath = "Assets/SE/レアカード.mp3";

    public static void Play(CardData card)
    {
        string path = (card != null && card.isRare) ? RarePath : NormalPath;
        SoundEffectPlayer.I?.Play(path);
    }
}
