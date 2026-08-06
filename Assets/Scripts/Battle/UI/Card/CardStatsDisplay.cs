using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TotalATKDEF表示を管理するクラス
/// BattleManagerからTotalATKDEF表示関連の処理を移設
/// </summary>
public class CardStatsDisplay : MonoBehaviour
{
    [Header("TotalATKDEF表示（プレイヤー）")]
    [SerializeField] private GameObject totalATKDEFButton;
    [SerializeField] private TMP_Text atkdefText;
    [Tooltip("未指定時は totalATKDEFButton 上の Image を使用。自分攻撃モードで赤系に着色。")]
    [SerializeField] private Image playerTotalAtkDefBackground;
    [Tooltip("TotalATKDEF の合算属性アイコン（表示テキスト・色と同じ合算ルール。無属性は非表示。CardSheet と同じ Resources/Attributes）。")]
    [SerializeField] private Image atkdefElement;

    [Header("TotalATKDEF表示（敵）")]
    [SerializeField] private GameObject totalATKDEFButtonEnemy;
    [SerializeField] private TMP_Text atkdefTextEnemy;
    [Tooltip("敵側 TotalATKDEF の合算属性アイコン。無属性は非表示。")]
    [SerializeField] private Image atkdefElementEnemy;

    // 演出中のカードリスト
    private List<CardData> currentSequenceCards = new List<CardData>();
    private string currentSequenceType = "";
    /// <summary>TotalATKDEF のどちら側にシーケンスを反映するか（反射連鎖の敵防御などで敵側に DEF を出す）。</summary>
    private Side sequenceOwnerSide = Side.Player;

    /// <summary>イフリート等の加算表示（TMP リッチテキスト）。</summary>
    private const string IfritBonusColorHex = "#E53935";
    /// <summary>リヴァイアサン等の抑制分表示（TMP リッチテキスト）。</summary>
    private const string LeviathanSuppressColorHex = "#1E88E5";
    /// <summary>ゴッドレイジ適用後の「ATK 本体」数字（リッチテキスト）。</summary>
    private const string GodRageAtkBaseGreenHex = "#33DD55";

    /// <summary>ゴッドレイジ：2倍後のリッチ表示を <see cref="GetPlayerDisplayText"/> より優先する。</summary>
    private bool _godRagePlayerAtkDisplayLocked;
    private string _godRagePlayerAtkDisplayRichText;

    /// <summary>マジカルエクスプロージョン：演出直前（カード合計のみ・ME 加算前相当）。</summary>
    private bool _magicalExplosionPreRampLocked;
    private int _magicalExplosionPreRampAtkDisplayValue;

    /// <summary>マジカルエクスプロージョン：ランプ後のリッチ表示ロック。</summary>
    private bool _magicalExplosionPlayerAtkDisplayLocked;
    private string _magicalExplosionPlayerAtkDisplayRichText;

    /// <summary>
    /// 攻撃＋ME の Prefab シーケンス中は TOTAL に MP×2 の予測を含めない（カード表記の合計のみ。ME 演出前に解除）。
    /// </summary>
    private bool _suppressMagicalExplosionPredictionDuringSequenceReveal;

    /// <summary>100万ドルバズーカ：ランプ後のリッチ表示ロック。</summary>
    private bool _millionDollarBazookaPlayerAtkDisplayLocked;
    private string _millionDollarBazookaPlayerAtkDisplayRichText;

    /// <summary>100万ドルバズーカ：演出直前（GP 加算前相当）。</summary>
    private bool _millionDollarBazookaPreRampLocked;
    private int _millionDollarBazookaPreRampAtkDisplayValue;

    /// <summary>攻撃＋100万ドルバズーカの Prefab シーケンス中は TOTAL に GP×倍数の予測を含めない。</summary>
    private bool _suppressMillionDollarBazookaPredictionDuringSequenceReveal;

    /// <summary>気狂いハンマー：演出直前（ランダム加算前相当）。</summary>
    private bool _hammadnessPreRampLocked;
    private int _hammadnessPreRampAtkDisplayValue;

    /// <summary>気狂いハンマー：ランプ後のリッチ表示ロック。</summary>
    private bool _hammadnessPlayerAtkDisplayLocked;
    private string _hammadnessPlayerAtkDisplayRichText;

    /// <summary>攻撃＋気狂いハンマーの Prefab シーケンス中は TOTAL にランダム ATK の予測を含めない。</summary>
    private bool _suppressHammadnessPredictionDuringSequenceReveal;

    /// <summary>Tribute Blood: pre-ramp TOTAL lock.</summary>
    private bool _tributeBloodPreRampLocked;
    private int _tributeBloodPreRampAtkDisplayValue;

    /// <summary>Tribute Blood: post-ramp rich display lock.</summary>
    private bool _tributeBloodPlayerAtkDisplayLocked;
    private string _tributeBloodPlayerAtkDisplayRichText;

    /// <summary>Attack + Tribute Blood sequence: suppress paid-HP damage prediction on TOTAL until intro.</summary>
    private bool _suppressTributeBloodPredictionDuringSequenceReveal;

    /// <summary>
    /// 魔導書：カード表示中は合算属性を魔導書適用前で表示し、フラッシュ後に強制属性へ切り替える。
    /// </summary>
    private bool _suppressSpellbookElementDuringSequenceReveal;

    private bool _attackDisplaySuppressGodRageDouble;
    private bool _attackDisplaySuppressMagicalSwordBonus;
    private bool _magicalSwordRampAtkDisplayLocked;
    private string _magicalSwordRampAtkDisplayRichText;

    /// <summary>
    /// 初期化時にボタンを非表示にする
    /// </summary>
    private void Awake()
    {
        // 初期状態ではボタンを非表示にする
        if (totalATKDEFButton != null)
        {
            totalATKDEFButton.SetActive(false);
        }
        if (totalATKDEFButtonEnemy != null)
        {
            totalATKDEFButtonEnemy.SetActive(false);
        }
        if (atkdefElement != null)
            atkdefElement.gameObject.SetActive(false);
        if (atkdefElementEnemy != null)
            atkdefElementEnemy.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (totalATKDEFButton != null)
        {
            var btn = totalATKDEFButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnPlayerTotalAtkDefButtonClicked);
        }
    }

    /// <summary>
    /// ゲーム終了時：両サイドの TotalATKDEF ボタンを SetActive(false) で完全に隠す。
    /// </summary>
    public void HideAllForGameEnd()
    {
        if (totalATKDEFButton != null)
            totalATKDEFButton.SetActive(false);
        if (totalATKDEFButtonEnemy != null)
            totalATKDEFButtonEnemy.SetActive(false);
        if (atkdefElement != null)
            atkdefElement.gameObject.SetActive(false);
        if (atkdefElementEnemy != null)
            atkdefElementEnemy.gameObject.SetActive(false);
    }

    private void OnPlayerTotalAtkDefButtonClicked()
    {
        var bm = BattleManager.I;
        if (bm == null) return;
        if (bm.CurrentState != GameState.AttackPhase || bm.CurrentTurnOwner != PlayerType.Player)
            return;
        var ps = bm.GetPlayerStatus();
        if (ps != null && ps.HasConfusionEffect())
            return;
        bm.TogglePlayerSelfAttackTargetMode();
    }

    private static readonly Color ConfusionAtkDefBackgroundColor = new Color(1f, 0.92f, 0.45f);

    private void ApplyPlayerSelfAttackTargetBackground()
    {
        if (totalATKDEFButton == null || !totalATKDEFButton.activeSelf) return;
        var img = playerTotalAtkDefBackground != null
            ? playerTotalAtkDefBackground
            : totalATKDEFButton.GetComponent<Image>();
        if (img == null) return;
        var bm = BattleManager.I;
        var ps = bm != null ? bm.GetPlayerStatus() : null;
        bool confusedPlayer = ps != null && ps.HasConfusionEffect()
            && bm.CurrentState == GameState.AttackPhase
            && bm.CurrentTurnOwner == PlayerType.Player;
        var btn = totalATKDEFButton.GetComponent<Button>();
        if (btn != null)
            btn.interactable = !confusedPlayer;

        if (confusedPlayer)
        {
            img.color = ConfusionAtkDefBackgroundColor;
            return;
        }

        if (bm != null
            && (bm.IsPostDeathSequenceActive || PostDeathCombatContext.Active != null))
        {
            img.color = Color.white;
            return;
        }

        bool red = bm != null
            && bm.CurrentState == GameState.AttackPhase
            && bm.CurrentTurnOwner == PlayerType.Player
            && bm.IsPlayerSelfAttackTargetMode;
        img.color = red ? new Color(0.92f, 0.42f, 0.42f) : Color.white;
    }

    /// <summary>
    /// 攻撃フェーズの選択について、TOTAL に出す合算攻撃力が 0 以下（数値 ATK なし）か。
    /// </summary>
    public bool IsPlayerAttackSelectionNumericAtkZero(IReadOnlyList<CardData> attackCards)
    {
        if (attackCards == null || attackCards.Count == 0 || BattleManager.I == null) return false;
        var list = new List<CardData>(attackCards.Count);
        foreach (var c in attackCards)
        {
            if (c != null) list.Add(c);
        }
        if (list.Count == 0) return false;
        if (list.Count == 1 && HammadnessRules.IsHammadnessCard(list[0]))
            return false;
        return GetDisplayedAttackStrength(list, BattleManager.I.GetPlayerStatus()) <= 0;
    }

    /// <param name="recoveryEffectCard">
    /// true のとき回復系：既定は自分、TOTAL 赤＝相手（攻撃・状態異常系とは文言の対応が逆）。
    /// </param>
    private static string FormatEffectTargetToggleLabel(BattleManager bm, bool recoveryEffectCard)
    {
        if (bm == null) return "対象：相手";
        bool red = bm.IsPlayerSelfAttackTargetMode;
        if (recoveryEffectCard)
            return red ? "対象：相手" : "対象：自分";
        return red ? "対象：自分" : "対象：相手";
    }

    /// <summary>
    /// 演出中のカードリストを設定
    /// </summary>
    public void SetSequenceCards(List<CardData> cards, string cardType)
    {
        SetSequenceCards(cards, cardType, Side.Player);
    }

    /// <param name="ownerSide">プレイヤー手札由来の演出なら <see cref="Side.Player"/>、敵のカード表示に合わせるときは <see cref="Side.Enemy"/>。</param>
    public void SetSequenceCards(List<CardData> cards, string cardType, Side ownerSide)
    {
        currentSequenceCards.Clear();
        if (cards != null)
        {
            currentSequenceCards.AddRange(cards);
        }
        currentSequenceType = cardType ?? "";
        sequenceOwnerSide = ownerSide;
    }

    private TMP_Text GetSequenceOwnerAtkDefText() =>
        sequenceOwnerSide == Side.Enemy ? atkdefTextEnemy : atkdefText;

    private Image GetSequenceOwnerAtkDefElementImage() =>
        sequenceOwnerSide == Side.Enemy ? atkdefElementEnemy : atkdefElement;

    /// <summary>攻撃シーケンス中のランプ後リッチ表示・pre-ramp 固定値。</summary>
    private bool TryGetSequenceAttackLockedDisplayText(out string text)
    {
        text = null;
        if (_magicalExplosionPlayerAtkDisplayLocked && !string.IsNullOrEmpty(_magicalExplosionPlayerAtkDisplayRichText))
        {
            text = _magicalExplosionPlayerAtkDisplayRichText;
            return true;
        }
        if (_magicalExplosionPreRampLocked)
        {
            text = $"ATK {_magicalExplosionPreRampAtkDisplayValue}";
            return true;
        }
        if (_millionDollarBazookaPlayerAtkDisplayLocked && !string.IsNullOrEmpty(_millionDollarBazookaPlayerAtkDisplayRichText))
        {
            text = _millionDollarBazookaPlayerAtkDisplayRichText;
            return true;
        }
        if (_millionDollarBazookaPreRampLocked)
        {
            text = $"ATK {_millionDollarBazookaPreRampAtkDisplayValue}";
            return true;
        }
        if (_tributeBloodPlayerAtkDisplayLocked && !string.IsNullOrEmpty(_tributeBloodPlayerAtkDisplayRichText))
        {
            text = _tributeBloodPlayerAtkDisplayRichText;
            return true;
        }
        if (_tributeBloodPreRampLocked)
        {
            text = $"ATK {_tributeBloodPreRampAtkDisplayValue}";
            return true;
        }
        if (_hammadnessPlayerAtkDisplayLocked && !string.IsNullOrEmpty(_hammadnessPlayerAtkDisplayRichText))
        {
            text = _hammadnessPlayerAtkDisplayRichText;
            return true;
        }
        if (_hammadnessPreRampLocked)
        {
            text = $"ATK {_hammadnessPreRampAtkDisplayValue}";
            return true;
        }
        if (_godRagePlayerAtkDisplayLocked && !string.IsNullOrEmpty(_godRagePlayerAtkDisplayRichText))
        {
            text = _godRagePlayerAtkDisplayRichText;
            return true;
        }
        if (_magicalSwordRampAtkDisplayLocked
            && !string.IsNullOrEmpty(_magicalSwordRampAtkDisplayRichText))
        {
            text = _magicalSwordRampAtkDisplayRichText;
            return true;
        }
        return false;
    }

    /// <summary>演出中のカードリストのみクリア（緑字 ATK ロックは維持。DefenseSelect 中の表示用）。</summary>
    public void ClearSequenceCards()
    {
        currentSequenceCards.Clear();
        currentSequenceType = "";
        sequenceOwnerSide = Side.Player;
    }

    /// <summary>攻撃 TOTAL のランプ後リッチ表示ロックをすべて解除（戦闘解決後など）。</summary>
    public void ClearAllAttackSequenceDisplayLocks()
    {
        ClearGodRageAttackDisplayLock();
        ClearMagicalSwordRampAttackDisplayLock();
        ClearAttackModifierRevealSuppressions();
        ClearMagicalExplosionAttackDisplayLocks();
        ClearMillionDollarBazookaAttackDisplayLocks();
        ClearHammadnessAttackDisplayLocks();
        ClearTributeBloodAttackDisplayLocks();
    }

    /// <summary>シーケンスと攻撃表示ロックを両方クリア（戦闘シーケンス終了時）。</summary>
    public void ClearSequenceCardsAndAttackDisplayLocks()
    {
        ClearSequenceCards();
        ClearAllAttackSequenceDisplayLocks();
    }

    /// <summary>ゴッドレイジの ATK 表示ロックを解除。</summary>
    private void ClearGodRageAttackDisplayLock()
    {
        _godRagePlayerAtkDisplayLocked = false;
        _godRagePlayerAtkDisplayRichText = null;
    }

    /// <summary>気狂いハンマーの表示ロックを解除。</summary>
    public void ClearHammadnessAttackDisplayLocks()
    {
        _hammadnessPreRampLocked = false;
        _hammadnessPreRampAtkDisplayValue = 0;
        _hammadnessPlayerAtkDisplayLocked = false;
        _hammadnessPlayerAtkDisplayRichText = null;
        _suppressHammadnessPredictionDuringSequenceReveal = false;
    }

    /// <summary>気狂いハンマー：ランプ完了後のリッチ表示のみ解除（続けてゴッドレイジ演出を行うとき）。</summary>
    public void ClearHammadnessPlayerAtkDisplayLockOnly()
    {
        _hammadnessPlayerAtkDisplayLocked = false;
        _hammadnessPlayerAtkDisplayRichText = null;
    }

    /// <summary>気狂いハンマー：ランダム ATK の予測を TOTAL に含めない期間。</summary>
    public void SetSuppressHammadnessPredictionDuringSequenceReveal(bool value)
    {
        _suppressHammadnessPredictionDuringSequenceReveal = value;
    }

    /// <summary>気狂いハンマー演出直前：TOTAL をランダム加算前の強さで固定表示する。</summary>
    public void SetHammadnessPreRampAttackDisplay(int displayedAtkStrength)
    {
        _hammadnessPreRampAtkDisplayValue = displayedAtkStrength;
        _hammadnessPreRampLocked = true;
    }

    /// <summary>マジカルエクスプロージョンの表示ロックを解除。</summary>
    public void ClearMagicalExplosionAttackDisplayLocks()
    {
        _magicalExplosionPreRampLocked = false;
        _magicalExplosionPreRampAtkDisplayValue = 0;
        _magicalExplosionPlayerAtkDisplayLocked = false;
        _magicalExplosionPlayerAtkDisplayRichText = null;
        _suppressMagicalExplosionPredictionDuringSequenceReveal = false;
        _suppressSpellbookElementDuringSequenceReveal = false;
    }

    /// <summary>ME ランプ完了後のリッチ表示のみ解除（続けてゴッドレイジ演出を行うとき）。</summary>
    public void ClearMagicalExplosionPlayerAtkDisplayLockOnly()
    {
        _magicalExplosionPlayerAtkDisplayLocked = false;
        _magicalExplosionPlayerAtkDisplayRichText = null;
    }

    /// <summary>ME の MP×2 予測を TOTAL に含めない期間（カード決定〜ME イントロ直前）。</summary>
    public void SetSuppressMagicalExplosionPredictionDuringSequenceReveal(bool value)
    {
        _suppressMagicalExplosionPredictionDuringSequenceReveal = value;
    }

    public void ClearMillionDollarBazookaAttackDisplayLocks()
    {
        _millionDollarBazookaPreRampLocked = false;
        _millionDollarBazookaPreRampAtkDisplayValue = 0;
        _millionDollarBazookaPlayerAtkDisplayLocked = false;
        _millionDollarBazookaPlayerAtkDisplayRichText = null;
        _suppressMillionDollarBazookaPredictionDuringSequenceReveal = false;
    }

    public void ClearMillionDollarBazookaPlayerAtkDisplayLockOnly()
    {
        _millionDollarBazookaPlayerAtkDisplayLocked = false;
        _millionDollarBazookaPlayerAtkDisplayRichText = null;
    }

    public void SetSuppressMillionDollarBazookaPredictionDuringSequenceReveal(bool value)
    {
        _suppressMillionDollarBazookaPredictionDuringSequenceReveal = value;
    }

    public void SetMillionDollarBazookaPreRampAttackDisplay(int displayedAtkStrength)
    {
        _millionDollarBazookaPreRampAtkDisplayValue = displayedAtkStrength;
        _millionDollarBazookaPreRampLocked = true;
    }

    /// <summary>Tribute Blood: suppress paid-HP damage on TOTAL during card reveal.</summary>
    public void SetSuppressTributeBloodPredictionDuringSequenceReveal(bool value)
    {
        _suppressTributeBloodPredictionDuringSequenceReveal = value;
    }

    public void SetTributeBloodPreRampAttackDisplay(int displayedAtkStrength)
    {
        _tributeBloodPreRampAtkDisplayValue = displayedAtkStrength;
        _tributeBloodPreRampLocked = true;
    }

    public void ClearTributeBloodAttackDisplayLocks()
    {
        _tributeBloodPreRampLocked = false;
        _tributeBloodPreRampAtkDisplayValue = 0;
        _tributeBloodPlayerAtkDisplayLocked = false;
        _tributeBloodPlayerAtkDisplayRichText = null;
        _suppressTributeBloodPredictionDuringSequenceReveal = false;
    }

    /// <summary>魔導書：表示シーケンス中のみ合算を魔導書適用前の属性で出す。</summary>
    public void SetSuppressSpellbookElementDuringSequenceReveal(bool value)
    {
        _suppressSpellbookElementDuringSequenceReveal = value;
    }

    /// <summary>カード掲出中：マジカルソード上乗せ／ゴッドレイジ 2 倍を TOTAL から一時除外する。</summary>
    public void SetAttackModifierRevealPhase(bool suppressMagicalSwordBonus, bool suppressGodRageDouble)
    {
        _attackDisplaySuppressMagicalSwordBonus = suppressMagicalSwordBonus;
        _attackDisplaySuppressGodRageDouble = suppressGodRageDouble;
    }

    /// <summary>ランプ前の掲出抑制フラグを解除する。</summary>
    public void ClearAttackModifierRevealSuppressions()
    {
        _attackDisplaySuppressGodRageDouble = false;
        _attackDisplaySuppressMagicalSwordBonus = false;
    }

    /// <summary>マジカルソード上乗せランプ後の緑字 ATK 表示ロックを解除。</summary>
    private void ClearMagicalSwordRampAttackDisplayLock()
    {
        _magicalSwordRampAtkDisplayLocked = false;
        _magicalSwordRampAtkDisplayRichText = null;
    }

    private void LockMagicalSwordRampAttackDisplay(int finalDisplayedAtk)
    {
        if (finalDisplayedAtk <= 0) return;
        _magicalSwordRampAtkDisplayRichText =
            $"<color={GodRageAtkBaseGreenHex}>ATK {finalDisplayedAtk}</color>";
        _magicalSwordRampAtkDisplayLocked = true;
    }

    /// <summary>ME 演出直前：TOTAL を「ME 加算前」の強さで固定表示する。</summary>
    public void SetMagicalExplosionPreRampAttackDisplay(int displayedAtkStrength)
    {
        _magicalExplosionPreRampAtkDisplayValue = displayedAtkStrength;
        _magicalExplosionPreRampLocked = true;
    }

    /// <summary>
    /// TotalATKDEF表示を更新（プレイヤーと敵の両方）
    /// </summary>
    public void UpdateDisplay()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null)
        {
            Debug.LogWarning("[CardStatsDisplay] BattleManagerが設定されていません");
            return;
        }

        UpdatePlayerDisplay();
        UpdateEnemyDisplay();
    }

    /// <summary>
    /// プレイヤーのTotalATKDEF表示を更新
    /// </summary>
    private void UpdatePlayerDisplay()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null)
        {
            if (totalATKDEFButton != null)
            {
                totalATKDEFButton.SetActive(false);
            }
            if (atkdefElement != null)
                atkdefElement.gameObject.SetActive(false);
            return;
        }

        if (totalATKDEFButton == null)
        {
            Debug.LogWarning("[CardStatsDisplay] totalATKDEFButtonが設定されていません");
            return;
        }

        bool shouldHide = ShouldHidePlayer();
        totalATKDEFButton.SetActive(!shouldHide);

        if (shouldHide)
        {
            if (atkdefElement != null)
                atkdefElement.gameObject.SetActive(false);
            return;
        }

        if (atkdefText != null)
        {
            atkdefText.text = GetPlayerDisplayText();
            ApplyPlayerTotalAtkDefTextStyle();
        }
        else
        {
            Debug.LogWarning("[CardStatsDisplay] ATKDEFtextが設定されていません");
        }

        ApplyPlayerSelfAttackTargetBackground();
        ApplyTotalAtkDefElementImage(atkdefElement, GetPlayerCombinedElement());
    }

    /// <summary>
    /// 敵のTotalATKDEF表示を更新
    /// </summary>
    private void UpdateEnemyDisplay()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return;

        if (totalATKDEFButtonEnemy == null)
        {
            Debug.LogWarning("[CardStatsDisplay] totalATKDEFButtonEnemyが設定されていません");
            return;
        }

        bool shouldHide = ShouldHideEnemy();
        totalATKDEFButtonEnemy.SetActive(!shouldHide);

        if (shouldHide)
        {
            if (atkdefElementEnemy != null)
                atkdefElementEnemy.gameObject.SetActive(false);
            return;
        }

        if (atkdefTextEnemy != null)
        {
            atkdefTextEnemy.text = GetEnemyDisplayText();
            ApplyEnemyTotalAtkDefTextStyle();
        }
        else
        {
            Debug.LogWarning("[CardStatsDisplay] ATKDEFtextEnemyが設定されていません");
        }

        ApplyEnemyConfusionBackground();
        ApplyTotalAtkDefElementImage(atkdefElementEnemy, GetEnemyCombinedElement());
    }

    /// <summary>防御 UI 用：攻撃側スナップショット（介入・戦闘用のフォールバック込み）。</summary>
    private static List<CardData> GetIncomingAttackSnapshotForDefenseUi(BattleManager bm)
    {
        if (bm == null) return null;
        var incoming = bm.GetIncomingAttackSnapshotForDefenseUi();
        if (incoming == null || incoming.Count == 0)
            incoming = bm.GetAttackCardsForCombatPublic();
        if (incoming == null || incoming.Count == 0) return null;
        return incoming;
    }

    /// <summary>「DEF n」行。1枚・複数枚で表記を統一。</summary>
    private string FormatDefensePowerLabel(List<CardData> defenseCards)
    {
        if (defenseCards == null || defenseCards.Count == 0) return "";
        if (defenseCards.Count == 1) return $"DEF {defenseCards[0].defensePower}";
        return $"DEF {CalculateTotalDefensePower(defenseCards)}";
    }

    /// <summary>反射スライド後の TOTAL ATK 文言（プレイヤー／敵パネル共通）。</summary>
    private string FormatReflectionAttackTotalLabel(BattleManager bm, PlayerStatus fallbackAttacker)
    {
        var rc = bm.GetReflectionAttackCardsForTotalDisplay();
        if (rc == null || rc.Count == 0) return "";
        if (bm.GetReflectionAttackDisplayStrengthOverride() is int ovr)
        {
            if (ovr <= 0) return "";
            return $"ATK {ovr}";
        }
        var rAtk = bm.GetReflectionAttackBlessingAttacker();
        var rDef = bm.GetReflectionAttackBlessingDefender();
        if (rAtk != null && rDef != null)
        {
            if (GodrageRules.IsGodrageDoublingCombo(rc))
                return FormatGodRageDoubledAttackPowerDisplayLabel(rc, rAtk, rDef);
            return FormatAttackPowerDisplayLabel(rc, rAtk, rDef);
        }
        return FormatAttackPowerDisplayLabel(rc, fallbackAttacker);
    }

    private void ApplyPlayerTotalAtkDefTextStyle()
    {
        if (atkdefText == null) return;
        if (IsShowingLockedIncomingAttackDisplay(forEnemyPanel: false))
        {
            atkdefText.richText = true;
            return;
        }
        var bm = BattleManager.I;
        if (bm != null && bm.CurrentState == GameState.AttackPhase && bm.CurrentTurnOwner == PlayerType.Player)
        {
            var sel = BattleUIManager.I?.GetSelectedAttackCards();
            if (sel != null && IsPlayerAttackSelectionNumericAtkZero(sel))
            {
                ApplyAttackLabelTextStyle(atkdefText, new Color(0.15f, 0.15f, 0.18f));
                return;
            }
        }
        ApplyAttackLabelTextStyle(atkdefText, ElementHelper.GetElementColor(GetPlayerCombinedElement()));
    }

    private void ApplyEnemyTotalAtkDefTextStyle()
    {
        if (atkdefTextEnemy == null) return;
        if (IsShowingLockedIncomingAttackDisplay(forEnemyPanel: true))
        {
            atkdefTextEnemy.richText = true;
            return;
        }
        ApplyAttackLabelTextStyle(atkdefTextEnemy, ElementHelper.GetElementColor(GetEnemyCombinedElement()));
    }

    /// <summary>ランプ後の緑字 ATK を DefenseSelect〜ダメージ解決完了まで維持しているか。</summary>
    private bool IsShowingLockedIncomingAttackDisplay(bool forEnemyPanel)
    {
        var bm = BattleManager.I;
        if (bm == null || !TryGetSequenceAttackLockedDisplayText(out _))
            return false;
        if (forEnemyPanel)
        {
            if (!IsPlayerDefendingAgainstEnemyAttack(bm)) return false;
            return bm.DefenderPublic == PlayerType.Player && bm.CurrentTurnOwner == PlayerType.Enemy;
        }
        if (!IsEnemyDefendingAgainstPlayerAttack(bm) && !IsPlayerOutgoingAttackTotalPending(bm))
            return false;
        return bm.DefenderPublic == PlayerType.Enemy && bm.CurrentTurnOwner == PlayerType.Player;
    }

    /// <summary>DefenseSelect〜戦闘解決中（ダメージ処理完了前）の攻撃 TOTAL 表示フェーズ。</summary>
    private static bool IsAttackTotalHeldThroughCombatPhase(BattleManager bm)
    {
        if (bm == null || bm.CurrentState == GameState.EndPhase) return false;
        if (IsPostDeathChainCombatTotalActive(bm)) return false;
        if (bm.CurrentState == GameState.DefensePhase || bm.CurrentState == GameState.DefenseConfirmPhase)
            return true;
        if (bm.IsPlayerDefenseCombatResolving) return true;
        return IsPlayerOutgoingAttackTotalPending(bm);
    }

    private static bool IsPostDeathChainCombatTotalActive(BattleManager bm) =>
        bm != null && (bm.IsPostDeathSequenceActive || bm.IsPostDeathChainAttackDisplayActive);

    private static bool IsPlayerDefendingAgainstEnemyAttack(BattleManager bm)
    {
        if (bm == null) return false;
        if (!IsAttackTotalHeldThroughCombatPhase(bm)) return false;
        return bm.DefenderPublic == PlayerType.Player && bm.CurrentTurnOwner == PlayerType.Enemy;
    }

    private static bool IsEnemyDefendingAgainstPlayerAttack(BattleManager bm)
    {
        if (bm == null) return false;
        if (!IsAttackTotalHeldThroughCombatPhase(bm)) return false;
        return bm.DefenderPublic == PlayerType.Enemy && bm.CurrentTurnOwner == PlayerType.Player;
    }

    /// <summary>
    /// プレイヤー攻撃：相手 DefenseSelect 完了後も <see cref="GetAttackCardsForCombatPublic"/> で TOTAL を維持する。
    /// （相手防御掲出が <see cref="BattleManager.SetStatsDisplaySequenceCards"/> で攻撃シーケンスを上書きするため）
    /// </summary>
    private static bool IsPlayerOutgoingAttackTotalPending(BattleManager bm)
    {
        if (bm == null || bm.AttackerPublic != PlayerType.Player) return false;
        if (bm.CurrentState == GameState.EndPhase || bm.CurrentState == GameState.CombatResolvePhase)
            return false;
        var cards = bm.GetAttackCardsForCombatPublic();
        if (cards == null || cards.Count == 0) return false;
        if (bm.CurrentState == GameState.DefenseConfirmPhase && bm.DefenderPublic == PlayerType.Enemy)
            return true;
        if (bm.CurrentState == GameState.AttackPhase && bm.CurrentTurnOwner == PlayerType.Player)
        {
            var sel = BattleUIManager.I?.GetSelectedAttackCards();
            if (sel != null && sel.Count > 0) return false;
            return true;
        }
        return false;
    }

    private string ResolvePlayerOutgoingAttackDisplayText()
    {
        var bm = BattleManager.I;
        if (bm == null) return null;
        var cards = bm.GetAttackCardsForCombatPublic();
        if (cards == null || cards.Count == 0) return null;
        if (TryGetSequenceAttackLockedDisplayText(out var locked))
            return locked;
        return FormatAttackPowerDisplayLabel(cards, bm.GetPlayerStatus());
    }

    private string ResolveIncomingAttackDisplayText(PlayerStatus attacker)
    {
        var bm = BattleManager.I;
        if (bm == null || attacker == null) return null;
        var incoming = GetIncomingAttackSnapshotForDefenseUi(bm);
        if (incoming == null || incoming.Count == 0) return null;
        if (TryGetSequenceAttackLockedDisplayText(out var locked))
            return locked;
        return FormatAttackPowerDisplayLabel(incoming, attacker);
    }

    /// <summary>CardSheet と同じ <see cref="ElementHelper.LoadIcon"/>。無属性またはスプライトが無い場合は Image を非表示にする。</summary>
    private static void ApplyTotalAtkDefElementImage(Image image, ElementType combinedElement)
    {
        if (image == null) return;
        if (combinedElement == ElementType.None)
        {
            image.gameObject.SetActive(false);
            return;
        }
        var sprite = ElementHelper.LoadIcon(combinedElement);
        if (sprite == null)
        {
            image.gameObject.SetActive(false);
            return;
        }
        image.sprite = sprite;
        image.gameObject.SetActive(true);
    }

    /// <summary>混乱中の敵攻撃側で TotalATKDEF を黄色表示（プレイヤー側と同系色）。</summary>
    private void ApplyEnemyConfusionBackground()
    {
        if (totalATKDEFButtonEnemy == null || !totalATKDEFButtonEnemy.activeSelf) return;
        var bm = BattleManager.I;
        if (bm == null) return;
        var es = bm.GetEnemyStatus();
        bool confusedEnemy = es != null && es.HasConfusionEffect()
            && bm.CurrentState == GameState.AttackPhase
            && bm.CurrentTurnOwner == PlayerType.Enemy
            && bm.GetCurrentAttackCard() != null;
        var img = totalATKDEFButtonEnemy.GetComponent<Image>();
        if (img == null) return;
        img.color = confusedEnemy ? ConfusionAtkDefBackgroundColor : Color.white;
    }

    /// <summary>
    /// 反射スライド後の TOTAL ATK 表示：攻撃側を消し反射側のみ表示する。
    /// <paramref name="evaluatingPlayerPanel"/> true = プレイヤー側パネルの表示可否（<see cref="ShouldHidePlayer"/> 用）。
    /// </summary>
    private bool TryGetReflectionTotalHideForPanel(bool evaluatingPlayerPanel, out bool hide)
    {
        hide = true;
        var bm = BattleManager.I;
        if (bm == null || !bm.IsReflectionAttackTotalDisplayActive()) return false;

        bool totalOnPlayer = bm.ReflectionAttackTotalOnPlayerSide;

        if (evaluatingPlayerPanel)
        {
            if (!totalOnPlayer)
            {
                // 通常防御を選んでいるときは反射用の「プレイヤー非表示」をかけない（DEF は ShouldHide 先頭で扱う）
                if (PlayerShouldShowDefenseTotalDuringReflectionChain(bm))
                    return false;

                hide = true;
                return true;
            }
        }
        else
        {
            if (totalOnPlayer)
            {
                hide = true;
                return true;
            }
        }

        var cards = bm.GetReflectionAttackCardsForTotalDisplay();
        var atkB = bm.GetReflectionAttackBlessingAttacker();
        var defB = bm.GetReflectionAttackBlessingDefender();
        int s = (atkB != null && defB != null)
            ? GetReflectionAttackNumericStrength(cards, atkB, defB)
            : GetDisplayedAttackStrength(
                cards,
                evaluatingPlayerPanel ? bm.GetPlayerStatus() : bm.GetEnemyStatus());
        hide = s <= 0;
        return true;
    }

    /// <summary>
    /// 反射（物理／魔法）または無効化として解決される防御なら、TotalATK/DEF に数値を出さない。
    /// 判定本体は <see cref="BlockingRules.AnyDefenseCardResolvesAsReflectionOrNullify"/>。
    /// </summary>
    private static bool IsReflectionOrNullifyDefenseRoute(BattleManager bm, List<CardData> defenseCards)
    {
        if (bm == null || defenseCards == null || defenseCards.Count == 0) return false;

        var incoming = GetIncomingAttackSnapshotForDefenseUi(bm);
        if (incoming == null) return false;

        return BlockingRules.AnyDefenseCardResolvesAsReflectionOrNullify(defenseCards, incoming);
    }

    /// <summary>
    /// 連鎖反射の防御入力中、通常防御のみ選ばれていれば TOTAL に DEF を出す。
    /// 反射 ATK オーバーレイの有無には依存しない（演出シーケンス残りと整合させるため）。
    /// 反射／無効化の排他のみのときは false。
    /// </summary>
    private bool PlayerShouldShowDefenseTotalDuringReflectionChain(BattleManager bm)
    {
        if (bm == null || !bm.IsReflectionChainDefensePending()) return false;

        var defCards = BattleUIManager.I?.GetSelectedDefenseCards();
        if (defCards == null || defCards.Count == 0) return false;

        var incoming = GetIncomingAttackSnapshotForDefenseUi(bm);
        if (incoming == null) return false;

        if (BlockingRules.AnyDefenseCardResolvesAsReflectionOrNullify(defCards, incoming))
            return false;

        return CalculateTotalDefensePower(defCards) > 0;
    }

    /// <summary>反射 TOTAL 用：ゴッドレイジ2倍を含め <see cref="BattleProcessor.CalculateTotalAttackPower"/> と同じ管の強さ。</summary>
    private int GetReflectionAttackNumericStrength(
        List<CardData> cards,
        PlayerStatus blessingAttacker,
        PlayerStatus blessingDefender)
    {
        if (cards == null || cards.Count == 0) return 0;
        var bm = BattleManager.I;
        if (bm != null && bm.GetReflectionAttackDisplayStrengthOverride() is int ovr)
            return ovr;
        return GetDisplayedAttackStrengthWithDefender(cards, blessingAttacker, blessingDefender);
    }

    /// <summary>獄炎宝玉表示は「防御」シーケンスだが、TOTAL は反撃 ATK（反射オーバーレイ）を使う。</summary>
    private bool IsHellfireOrbSequenceWithActiveReflectionForPanel(BattleManager bm, bool forPlayerPanel)
    {
        if (bm == null || !bm.IsReflectionAttackTotalDisplayActive()) return false;
        if (forPlayerPanel != bm.ReflectionAttackTotalOnPlayerSide) return false;
        if (currentSequenceCards == null || currentSequenceCards.Count != 1) return false;
        if (currentSequenceType != "防御") return false;
        var wantSide = forPlayerPanel ? Side.Player : Side.Enemy;
        if (sequenceOwnerSide != wantSide) return false;
        var c = currentSequenceCards[0];
        if (c == null || c.orbReactionRule is not OrbOfHellfireRuleSO) return false;
        return true;
    }

    private static bool TryGetPostDeathChainAttackForPanel(
        bool forPlayerPanel,
        out List<CardData> cards,
        out PlayerStatus attacker)
    {
        cards = null;
        attacker = null;
        var bm = BattleManager.I;
        if (bm == null || !bm.IsPostDeathChainAttackDisplayActive) return false;
        bool onPlayer = bm.GetPostDeathChainAttackDisplaySide() == Side.Player;
        if (onPlayer != forPlayerPanel) return false;
        var src = bm.GetPostDeathChainAttackDisplayCards();
        if (src == null || src.Count == 0) return false;
        cards = new List<CardData>(src);
        attacker = onPlayer ? bm.GetPlayerStatus() : bm.GetEnemyStatus();
        return attacker != null;
    }

    /// <summary>
    /// プレイヤーの表示を非表示にするかどうかを判定
    /// </summary>
    private bool ShouldHidePlayer()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return true;

        if (battleManager.CurrentState == GameState.EndPhase)
            return true;

        if (battleManager.IsEconomicActionInProgress()) return true;

        if (battleManager.IsHandReloadPopupOpen) return true;

        // 連鎖反射で防御選択時は最優先で表示（古い攻撃シーケンス・反射非表示より先）
        if (PlayerShouldShowDefenseTotalDuringReflectionChain(battleManager))
            return false;

        // カード Prefab シーケンス（反射 TOTAL より先：確定後の DEF 再表示と整合）
        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Player)
        {
            if (currentSequenceType == "攻撃")
            {
                int totalAttack = GetDisplayedAttackStrength(currentSequenceCards, battleManager.GetPlayerStatus());
                if (totalAttack <= 0) return true;
                return false;
            }
            else if (currentSequenceType == "防御")
            {
                if (IsHellfireOrbSequenceWithActiveReflectionForPanel(battleManager, true))
                    return false;
                if (IsReflectionOrNullifyDefenseRoute(battleManager, currentSequenceCards)) return true;
                int totalDefense = CalculateTotalDefensePower(currentSequenceCards);
                if (totalDefense <= 0) return true;
                return false;
            }
        }

        if (TryGetReflectionTotalHideForPanel(true, out bool refHide))
            return refHide;

        if (TryGetPostDeathChainAttackForPanel(true, out var pdAtkCards, out var pdAttacker)
            && GetDisplayedAttackStrength(pdAtkCards, pdAttacker) > 0
            && !(battleManager.IsPostDeathDefenseWaitActive() && battleManager.IsPostDeathPlayerDefender))
            return false;

        // 攻撃フェーズのうち、プレイヤーが攻撃側のときだけ（敵ターンの AttackPhase では敵用の表示に任せる）
        if (battleManager.CurrentState == GameState.AttackPhase
            && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            // 複数選択を優先してチェック
            var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
            if (selectedAttackCards != null && selectedAttackCards.Count > 0)
            {
                // 1 枚以上選ばれていれば表示（数値 ATK ゼロでも対象切替用 TOTAL を出す）
                return false;
            }

            // 選択なし：自分攻撃ターゲット切替中だけ枠を出す。濃霧付与など選んでキャンセルした直後は空パネルにならないよう非表示にする。
            if (battleManager.IsPlayerSelfAttackTargetMode)
                return false;
            return true;
        }

        if (IsPlayerOutgoingAttackTotalPending(battleManager))
        {
            var outgoing = ResolvePlayerOutgoingAttackDisplayText();
            if (!string.IsNullOrEmpty(outgoing)) return false;
        }

        if (IsEnemyDefendingAgainstPlayerAttack(battleManager))
        {
            var incoming = GetIncomingAttackSnapshotForDefenseUi(battleManager);
            if (incoming != null && incoming.Count > 0)
            {
                if (TryGetSequenceAttackLockedDisplayText(out _))
                    return false;
                if (GetDisplayedAttackStrength(incoming, battleManager.GetPlayerStatus()) > 0)
                    return false;
            }
        }

        // 防御フェーズの場合
        if (battleManager.CurrentState == GameState.DefensePhase
            || battleManager.CurrentState == GameState.DefenseConfirmPhase
            || (battleManager.IsPostDeathDefenseWaitActive() && battleManager.IsPostDeathPlayerDefender))
        {
            // 複数選択を優先してチェック
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
            {
                if (IsReflectionOrNullifyDefenseRoute(battleManager, selectedDefenseCards)) return true;
                // 複数選択時は合計防御力をチェック
                if (selectedDefenseCards.Count > 1)
                {
                    int totalDefense = CalculateTotalDefensePower(selectedDefenseCards);
                    if (totalDefense <= 0) return true;
                    return false;
                }
                
                // 単一選択の場合
                var card = selectedDefenseCards[0];
                if (card.defensePower <= 0) return true;
                return false;
            }

            // CardSelectionManagerから取得した選択カードが空の場合は非表示にする
            // BattleManagerのselectedDefenseCardは参照しない（キャンセル時にクリアされない可能性があるため）
            return true;
        }

        // その他の状態では非表示
        return true;
    }

    /// <summary>
    /// 敵の表示を非表示にするかどうかを判定
    /// </summary>
    private bool ShouldHideEnemy()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return true;

        if (battleManager.CurrentState == GameState.EndPhase)
            return true;

        if (battleManager.IsEconomicActionInProgress()) return true;

        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Enemy)
        {
            if (currentSequenceType == "攻撃")
            {
                int totalAttack = GetDisplayedAttackStrength(currentSequenceCards, battleManager.GetEnemyStatus());
                if (totalAttack <= 0) return true;
                return false;
            }
            else if (currentSequenceType == "防御")
            {
                if (IsHellfireOrbSequenceWithActiveReflectionForPanel(battleManager, false))
                    return false;
                if (IsReflectionOrNullifyDefenseRoute(battleManager, currentSequenceCards)) return true;
                int totalDefense = CalculateTotalDefensePower(currentSequenceCards);
                if (totalDefense <= 0) return true;
                return false;
            }
        }

        if (TryGetReflectionTotalHideForPanel(false, out bool refHideEnemy))
            return refHideEnemy;

        if (TryGetPostDeathChainAttackForPanel(false, out var pdEnemyAtkCards, out var pdEnemyAttacker)
            && GetDisplayedAttackStrength(pdEnemyAtkCards, pdEnemyAttacker) > 0)
            return false;

        if (IsPlayerDefendingAgainstEnemyAttack(battleManager))
        {
            var incoming = GetIncomingAttackSnapshotForDefenseUi(battleManager);
            if (incoming != null && incoming.Count > 0)
            {
                if (TryGetSequenceAttackLockedDisplayText(out _))
                    return false;
                if (GetDisplayedAttackStrength(incoming, battleManager.GetEnemyStatus()) > 0)
                    return false;
            }
        }

        // 敵のターン（攻撃側）: currentAttackCard が設定されていれば表示
        if (!IsPostDeathChainCombatTotalActive(battleManager)
            && battleManager.CurrentTurnOwner == PlayerType.Enemy
            && !battleManager.IsSuppressingEnemyStaleAttackerInTotalByOrb())
        {
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            if (currentAttackCard != null)
            {
                if (CardRules.IsImmediateAction(currentAttackCard)) return true;
                var one = new List<CardData> { currentAttackCard };
                if (GetDisplayedAttackStrength(one, battleManager.GetEnemyStatus()) <= 0) return true;
                return false;
            }
            return true;
        }

        // プレイヤーのターン（敵が防御側）: CardSelectionManager と BattleManager の両方から防御を解決
        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var state = battleManager.CurrentState;
            if (state == GameState.DefensePhase || state == GameState.DefenseConfirmPhase)
            {
                var defCards = ResolveEnemyDefenseCardsForDisplay(battleManager);
                if (defCards != null && defCards.Count > 0)
                {
                    if (IsReflectionOrNullifyDefenseRoute(battleManager, defCards)) return true;
                    if (defCards.Count > 1)
                        return CalculateTotalDefensePower(defCards) <= 0;
                    return defCards[0].defensePower <= 0;
                }
            }
            return true;
        }

        return true;
    }

    /// <summary>
    /// プレイヤーの表示テキストを取得
    /// </summary>
    private string GetPlayerDisplayText()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return "";

        // 連鎖反射の通常防御は、残った攻撃演出シーケンスより優先
        if (PlayerShouldShowDefenseTotalDuringReflectionChain(battleManager))
        {
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && IsReflectionOrNullifyDefenseRoute(battleManager, selectedDefenseCards))
                return "";
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
                return FormatDefensePowerLabel(selectedDefenseCards);
        }

        if (IsHellfireOrbSequenceWithActiveReflectionForPanel(battleManager, true))
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return FormatReflectionAttackTotalLabel(battleManager, battleManager.GetPlayerStatus());
        }

        if (battleManager.IsPostDeathDefenseWaitActive() && battleManager.IsPostDeathPlayerDefender)
        {
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
            {
                if (IsReflectionOrNullifyDefenseRoute(battleManager, selectedDefenseCards)) return "";
                return FormatDefensePowerLabel(selectedDefenseCards);
            }
        }

        if (TryGetPostDeathChainAttackForPanel(true, out var pdChainCards, out var pdChainAttacker)
            && !(battleManager.IsPostDeathDefenseWaitActive() && battleManager.IsPostDeathPlayerDefender))
            return FormatAttackPowerDisplayLabel(pdChainCards, pdChainAttacker);

        if (IsPlayerOutgoingAttackTotalPending(battleManager))
        {
            var outgoingText = ResolvePlayerOutgoingAttackDisplayText();
            if (!string.IsNullOrEmpty(outgoingText))
                return outgoingText;
        }

        if (IsEnemyDefendingAgainstPlayerAttack(battleManager))
        {
            var incomingText = ResolveIncomingAttackDisplayText(battleManager.GetPlayerStatus());
            if (!string.IsNullOrEmpty(incomingText))
                return incomingText;
        }

        // CardSequenceManager／反射連鎖確定後の Prefab シーケンス（反射用 TOTAL ATK より優先。獄炎宝玉は上で反撃 ATK）
        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Player)
        {
            if (currentSequenceType == "攻撃")
            {
                if (TryGetSequenceAttackLockedDisplayText(out var lockedAtk))
                    return lockedAtk;
                return FormatAttackPowerDisplayLabel(currentSequenceCards, battleManager.GetPlayerStatus());
            }
            else if (currentSequenceType == "防御")
            {
                if (IsReflectionOrNullifyDefenseRoute(battleManager, currentSequenceCards)) return "";
                return FormatDefensePowerLabel(currentSequenceCards);
            }
        }

        if (battleManager.IsReflectionAttackTotalDisplayActive() && battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return FormatReflectionAttackTotalLabel(battleManager, battleManager.GetPlayerStatus());
        }

        if (battleManager.CurrentState == GameState.AttackPhase
            && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            var selectedAttackCards = BattleUIManager.I?.GetSelectedAttackCards();
            if (selectedAttackCards != null && selectedAttackCards.Count > 0
                && IsPlayerAttackSelectionNumericAtkZero(selectedAttackCards))
            {
                bool recovery = selectedAttackCards.Count == 1
                    && CardRules.IsRecoveryCard(selectedAttackCards[0]);
                return FormatEffectTargetToggleLabel(battleManager, recovery);
            }

            // 複数選択を優先してチェック（複数選択時は合計値を表示）
            if (selectedAttackCards != null && selectedAttackCards.Count > 1)
            {
                return FormatAttackPowerDisplayLabel(selectedAttackCards, battleManager.GetPlayerStatus());
            }

            // 単一選択の場合
            if (selectedAttackCards != null && selectedAttackCards.Count == 1)
            {
                var one = new List<CardData> { selectedAttackCards[0] };
                return FormatAttackPowerDisplayLabel(one, battleManager.GetPlayerStatus());
            }
            
            // CardSelectionManagerから取得した選択カードが空の場合は、空文字列を返す（表示しない）
            // BattleManagerのselectedCardは参照しない（キャンセル時にクリアされない可能性があるため）
        }
        else if (battleManager.CurrentState == GameState.DefensePhase
            || battleManager.CurrentState == GameState.DefenseConfirmPhase)
        {
            // 複数選択を優先してチェック（複数選択時は合計値を表示）
            var selectedDefenseCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0
                && IsReflectionOrNullifyDefenseRoute(battleManager, selectedDefenseCards))
                return "";
            if (selectedDefenseCards != null && selectedDefenseCards.Count > 0)
                return FormatDefensePowerLabel(selectedDefenseCards);
            
            // CardSelectionManagerから取得した選択カードが空の場合は、空文字列を返す（表示しない）
            // BattleManagerのselectedDefenseCardは参照しない（キャンセル時にクリアされない可能性があるため）
        }

        return "";
    }

    /// <summary>
    /// 敵の表示テキストを取得
    /// </summary>
    private string GetEnemyDisplayText()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return "";

        if (IsHellfireOrbSequenceWithActiveReflectionForPanel(battleManager, false))
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return FormatReflectionAttackTotalLabel(battleManager, battleManager.GetEnemyStatus());
        }

        if (IsPlayerDefendingAgainstEnemyAttack(battleManager))
        {
            var incomingText = ResolveIncomingAttackDisplayText(battleManager.GetEnemyStatus());
            if (!string.IsNullOrEmpty(incomingText))
                return incomingText;
        }

        if (TryGetPostDeathChainAttackForPanel(false, out var pdEnemyChainCards, out var pdEnemyChainAttacker))
            return FormatAttackPowerDisplayLabel(pdEnemyChainCards, pdEnemyChainAttacker);

        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Enemy)
        {
            if (currentSequenceType == "攻撃")
            {
                if (TryGetSequenceAttackLockedDisplayText(out var lockedAtk))
                    return lockedAtk;
                return FormatAttackPowerDisplayLabel(currentSequenceCards, battleManager.GetEnemyStatus());
            }
            else if (currentSequenceType == "防御")
            {
                if (IsReflectionOrNullifyDefenseRoute(battleManager, currentSequenceCards)) return "";
                return FormatDefensePowerLabel(currentSequenceCards);
            }
        }

        if (battleManager.IsReflectionAttackTotalDisplayActive() && !battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return FormatReflectionAttackTotalLabel(battleManager, battleManager.GetEnemyStatus());
        }

        // 敵のターン（攻撃側）: ATK を表示（宝玉反撃中は元攻撃カード行を抑止）
        if (!IsPostDeathChainCombatTotalActive(battleManager)
            && battleManager.CurrentTurnOwner == PlayerType.Enemy
            && !battleManager.IsSuppressingEnemyStaleAttackerInTotalByOrb())
        {
            var currentAttackCard = battleManager.GetCurrentAttackCard();
            if (currentAttackCard != null)
            {
                var one = new List<CardData> { currentAttackCard };
                return FormatAttackPowerDisplayLabel(one, battleManager.GetEnemyStatus());
            }
        }

        // プレイヤーのターン（敵が防御側）: DEF を表示（UI 選択を優先し BattleManager 単一参照にフォールバック）
        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var defCards = ResolveEnemyDefenseCardsForDisplay(battleManager);
            if (defCards != null && defCards.Count > 0)
            {
                if (IsReflectionOrNullifyDefenseRoute(battleManager, defCards)) return "";
                return FormatDefensePowerLabel(defCards);
            }
        }

        return "";
    }

    /// <summary>
    /// 敵防御の表示用：CardSelectionManager の選択を優先。手札補充後など BattleManager だけ古い参照のときの取りこぼし防止。
    /// </summary>
    private static List<CardData> ResolveEnemyDefenseCardsForDisplay(BattleManager bm)
    {
        if (bm == null) return null;
        var combat = bm.GetEnemyDefenseCardsForCombat();
        if (combat != null && combat.Count > 0)
            return combat;
        var ui = BattleUIManager.I?.GetSelectedDefenseCards();
        if (ui != null && ui.Count > 0)
            return ui;
        var single = bm.GetSelectedDefenseCard();
        if (single == null) return null;
        return new List<CardData> { single };
    }

    /// <summary>
    /// プレイヤー側の合算属性を取得
    /// </summary>
    private ElementType GetPlayerCombinedElement()
    {
        var battleManager = BattleManager.I;

        if (TryGetPostDeathChainAttackForPanel(true, out _, out _))
        {
            var ctx = PostDeathCombatContext.Active;
            if (ctx != null) return ctx.AttackElement;
        }

        if (battleManager != null && PlayerShouldShowDefenseTotalDuringReflectionChain(battleManager))
        {
            var defCards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (defCards != null && defCards.Count > 0)
                return ElementHelper.GetCombinedElement(defCards);
        }

        if (IsEnemyDefendingAgainstPlayerAttack(battleManager))
        {
            var incoming = GetIncomingAttackSnapshotForDefenseUi(battleManager);
            if (incoming != null && incoming.Count > 0)
                return ElementHelper.GetCombinedElement(incoming);
        }

        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Player)
        {
            var postDeathCtx = PostDeathCombatContext.Active;
            if (postDeathCtx != null && postDeathCtx.MatchesIncoming(currentSequenceCards))
                return postDeathCtx.AttackElement;
            bool applySpellbookElement = !(_suppressSpellbookElementDuringSequenceReveal && currentSequenceType == "攻撃");
            return ElementHelper.GetCombinedElement(currentSequenceCards, applySpellbookElement);
        }

        if (battleManager != null && battleManager.IsReflectionAttackTotalDisplayActive()
            && battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return ElementHelper.GetCombinedElement(rc);
        }

        if (battleManager == null) return ElementType.None;

        if (battleManager.CurrentState == GameState.AttackPhase
            && battleManager.CurrentTurnOwner == PlayerType.Player)
        {
            var cards = BattleUIManager.I?.GetSelectedAttackCards();
            if (cards != null && cards.Count > 0) return ElementHelper.GetCombinedElement(cards);
        }
        else if (battleManager.CurrentState == GameState.DefensePhase
            || battleManager.CurrentState == GameState.DefenseConfirmPhase)
        {
            var cards = BattleUIManager.I?.GetSelectedDefenseCards();
            if (cards != null && cards.Count > 0) return ElementHelper.GetCombinedElement(cards);
        }
        return ElementType.None;
    }

    /// <summary>
    /// 敵側の合算属性を取得
    /// </summary>
    private ElementType GetEnemyCombinedElement()
    {
        var battleManager = BattleManager.I;
        if (battleManager == null) return ElementType.None;

        if (TryGetPostDeathChainAttackForPanel(false, out _, out _))
        {
            var ctx = PostDeathCombatContext.Active;
            if (ctx != null) return ctx.AttackElement;
        }

        if (IsPlayerDefendingAgainstEnemyAttack(battleManager))
        {
            var incoming = GetIncomingAttackSnapshotForDefenseUi(battleManager);
            if (incoming != null && incoming.Count > 0)
                return ElementHelper.GetCombinedElement(incoming);
        }

        if (currentSequenceCards.Count > 0 && sequenceOwnerSide == Side.Enemy)
        {
            var postDeathCtx = PostDeathCombatContext.Active;
            if (postDeathCtx != null && postDeathCtx.MatchesIncoming(currentSequenceCards))
                return postDeathCtx.AttackElement;
            bool applySpellbookElement = !(_suppressSpellbookElementDuringSequenceReveal && currentSequenceType == "攻撃");
            return ElementHelper.GetCombinedElement(currentSequenceCards, applySpellbookElement);
        }

        if (battleManager.IsReflectionAttackTotalDisplayActive() && !battleManager.ReflectionAttackTotalOnPlayerSide)
        {
            var rc = battleManager.GetReflectionAttackCardsForTotalDisplay();
            if (rc != null && rc.Count > 0)
                return ElementHelper.GetCombinedElement(rc);
        }

        if (battleManager.CurrentTurnOwner == PlayerType.Enemy
            && !battleManager.IsSuppressingEnemyStaleAttackerInTotalByOrb())
        {
            var card = battleManager.GetCurrentAttackCard();
            if (card != null) return card.element;
        }

        if (battleManager.CurrentTurnOwner == PlayerType.Player
            && battleManager.DefenderPublic == PlayerType.Enemy)
        {
            var defCards = ResolveEnemyDefenseCardsForDisplay(battleManager);
            if (defCards != null && defCards.Count > 0)
                return ElementHelper.GetCombinedElement(defCards);
        }
        return ElementType.None;
    }

    /// <summary>
    /// カードリストの合計攻撃力・防御力を計算（統一メソッド）
    /// </summary>
    private int CalculateTotalPower(List<CardData> cards, bool isAttack)
    {
        int total = 0;
        foreach (var card in cards)
        {
            if (card != null)
            {
                total += isAttack ? card.attackPower : card.defensePower;
            }
        }
        return total;
    }

    /// <summary>
    /// 合計攻撃力を計算（マジカルエクスプロージョンは <paramref name="attackerForMeRule"/> があるとき反映）。
    /// </summary>
    private int CalculateTotalAttackPower(List<CardData> attackCards, PlayerStatus attackerForMeRule = null)
    {
        if (attackCards == null || attackCards.Count == 0) return 0;
        var postDeathCtx = PostDeathCombatContext.Active;
        if (postDeathCtx != null && postDeathCtx.MatchesIncoming(attackCards))
            return postDeathCtx.FixedAttackPower;
        if (attackerForMeRule != null && MagicalExplosionRules.ContainsMagicalExplosion(attackCards))
        {
            if (_suppressMagicalExplosionPredictionDuringSequenceReveal)
                return MagicalExplosionRules.SumAttackPowerExcludingMagicalExplosion(attackCards);
            return MagicalExplosionRules.SumCardAttackPowerForMagicalExplosionCombo(attackCards, attackerForMeRule);
        }
        if (attackerForMeRule != null && MillionDollarBazookaRules.ContainsMillionDollarBazooka(attackCards))
        {
            if (_suppressMillionDollarBazookaPredictionDuringSequenceReveal)
                return MillionDollarBazookaRules.SumAttackPowerExcludingMillionDollarBazooka(attackCards)
                    + (_attackDisplaySuppressMagicalSwordBonus
                        ? 0
                        : MagicalSwordRules.GetActivePowerBonus(attackCards, attackerForMeRule));
            return MillionDollarBazookaRules.SumCardAttackPowerForMillionDollarBazookaCombo(attackCards, attackerForMeRule);
        }
        if (attackerForMeRule != null && TributeBloodRules.ContainsTributeBlood(attackCards))
        {
            if (_suppressTributeBloodPredictionDuringSequenceReveal)
                return TributeBloodRules.SumAttackPowerExcludingTributeBlood(attackCards)
                    + (_attackDisplaySuppressMagicalSwordBonus
                        ? 0
                        : MagicalSwordRules.GetActivePowerBonus(attackCards, attackerForMeRule));
            return TributeBloodRules.SumCardAttackPowerForTributeBloodCombo(attackCards, attackerForMeRule);
        }
        if (attackerForMeRule != null && HammadnessRules.ContainsHammadness(attackCards))
        {
            if (_suppressHammadnessPredictionDuringSequenceReveal)
                return HammadnessRules.SumAttackPowerExcludingHammadness(attackCards)
                    + (_attackDisplaySuppressMagicalSwordBonus
                        ? 0
                        : MagicalSwordRules.GetActivePowerBonus(attackCards, attackerForMeRule));
            return HammadnessRules.SumCardAttackPowerForHammadnessCombo(attackCards, attackerForMeRule);
        }
        int plain = CalculateTotalPower(attackCards, true);
        if (attackerForMeRule != null && !_attackDisplaySuppressMagicalSwordBonus)
            plain += MagicalSwordRules.GetActivePowerBonus(attackCards, attackerForMeRule);
        return plain;
    }

    /// <summary>ME 演出：カード表記 ATK 合計のみ（加護・衰弱の後）で「演出開始前」値。</summary>
    public int ComputeMagicalExplosionRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sumEx = MagicalExplosionRules.SumAttackPowerExcludingMagicalExplosion(cards);
        sumEx += MagicalSwordRules.GetActivePowerBonus(cards, attacker);
        return ComputeAttackPowerFromCardSum(sumEx, cards, attacker, defenderForBlessings);
    }

    /// <summary>ME 演出完了後の TOTAL と一致する強さ。</summary>
    public int ComputeMagicalExplosionRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sum = MagicalExplosionRules.SumCardAttackPowerForMagicalExplosionCombo(cards, attacker);
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    /// <summary>
    /// TOTAL とマジカルエクスプロージョンの CardSheet ATK を同時にカウントアップ。完了後リッチ表示ロック。
    /// </summary>
    public async Task PlayMagicalExplosionAttackRampAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        CardData meCard,
        int meSheetAtkTarget,
        int fromTotal,
        int toTotal,
        float totalDurationSec,
        CancellationToken cancellationToken)
    {
        _magicalExplosionPreRampLocked = false;
        var rampText = GetSequenceOwnerAtkDefText();
        var rampElement = GetSequenceOwnerAtkDefElementImage();

        if (rampText == null || totalDurationSec <= 0f)
        {
            _magicalExplosionPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
            _magicalExplosionPlayerAtkDisplayLocked = true;
            UpdateDisplay();
            return;
        }

        if (fromTotal == toTotal)
        {
            _magicalExplosionPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
            _magicalExplosionPlayerAtkDisplayLocked = true;
            UpdateDisplay();
            return;
        }

        int lo = Mathf.Min(fromTotal, toTotal);
        int hi = Mathf.Max(fromTotal, toTotal);
        int span = hi - lo;
        float stepSec = span > 0 ? totalDurationSec / span : 0f;

        CardSheetDisplay meSheet = null;
        if (meCard != null && BattleUIManager.I != null
            && BattleUIManager.I.TryGetCardSheetDisplayForCardData(meCard, out var sh))
            meSheet = sh;

        int defPow = meCard != null ? meCard.defensePower : 0;

        rampText.richText = false;
        {
            var el = attackCards != null && attackCards.Count > 0
                ? ElementHelper.GetCombinedElement(attackCards)
                : ElementType.None;
            rampText.color = ElementHelper.GetElementColor(el);
            ApplyTotalAtkDefElementImage(rampElement, el);
        }

        float invSpan = (hi - lo) > 0 ? 1f / (hi - lo) : 0f;
        for (int v = lo; v <= hi; v++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            float t = (v - lo) * invSpan;
            int meAtkVal = Mathf.RoundToInt(Mathf.Lerp(0f, meSheetAtkTarget, t));
            if (v == hi)
                meAtkVal = meSheetAtkTarget;
            if (meSheet != null)
                meSheet.SetAtkDefenseNumbers(meAtkVal, defPow);

            rampText.text = $"ATK {v}";
            if (v < hi && stepSec > 0f)
                await Task.Delay(TimeSpan.FromSeconds(stepSec), cancellationToken);
        }

        if (meSheet != null && meSheetAtkTarget >= 0)
            meSheet.SetAtkDefenseNumbers(meSheetAtkTarget, defPow);

        _magicalExplosionPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
        _magicalExplosionPlayerAtkDisplayLocked = true;
        UpdateDisplay();
    }

    public int ComputeMillionDollarBazookaRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sumEx = MillionDollarBazookaRules.SumAttackPowerExcludingMillionDollarBazooka(cards);
        sumEx += MagicalSwordRules.GetActivePowerBonus(cards, attacker);
        return ComputeAttackPowerFromCardSum(sumEx, cards, attacker, defenderForBlessings);
    }

    public int ComputeMillionDollarBazookaRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sum = MillionDollarBazookaRules.SumCardAttackPowerForMillionDollarBazookaCombo(cards, attacker);
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    /// <summary>
    /// 100万ドルバズーカ演出完了後の TOTAL リッチ表示をロック（ME ランプと同じ見た目）。
    /// </summary>
    public void LockMillionDollarBazookaPlayerAttackDisplay(List<CardData> attackCards, PlayerStatus atk)
    {
        _millionDollarBazookaPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
        _millionDollarBazookaPlayerAtkDisplayLocked = true;
    }

    /// <summary>Tribute Blood ramp: card sum only (before paid-HP bonus), with blessings.</summary>
    public int ComputeTributeBloodRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sumEx = TributeBloodRules.SumAttackPowerExcludingTributeBlood(cards);
        sumEx += MagicalSwordRules.GetActivePowerBonus(cards, attacker);
        return ComputeAttackPowerFromCardSum(sumEx, cards, attacker, defenderForBlessings);
    }

    /// <summary>Tribute Blood ramp: final strength after paid-HP bonus, with blessings.</summary>
    public int ComputeTributeBloodRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        if (cards == null || attacker == null) return 0;
        int sum = TributeBloodRules.SumCardAttackPowerForTributeBloodCombo(cards, attacker);
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    /// <summary>
    /// TOTAL and Tribute Blood CardSheet ATK count up together; HP drops 1 per ATK step (up to hpPaid total).
    /// </summary>
    public async Task PlayTributeBloodAttackRampAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        CardData tbCard,
        int tbSheetAtkTarget,
        int hpPaid,
        int fromTotal,
        int toTotal,
        float totalDurationSec,
        CancellationToken cancellationToken)
    {
        _tributeBloodPreRampLocked = false;
        var rampText = GetSequenceOwnerAtkDefText();
        var rampElement = GetSequenceOwnerAtkDefElementImage();

        if (rampText == null || totalDurationSec <= 0f)
        {
            _tributeBloodPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
            _tributeBloodPlayerAtkDisplayLocked = true;
            if (hpPaid > 0 && atk != null)
            {
                atk.ApplyRawHpDamage(hpPaid);
                BattleUIManager.I?.UpdateStatus(BattleManager.I?.GetPlayerStatus(), BattleManager.I?.GetEnemyStatus());
            }
            UpdateDisplay();
            return;
        }

        if (fromTotal == toTotal)
        {
            _tributeBloodPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
            _tributeBloodPlayerAtkDisplayLocked = true;
            if (hpPaid > 0 && atk != null)
            {
                atk.ApplyRawHpDamage(hpPaid);
                BattleUIManager.I?.UpdateStatus(BattleManager.I?.GetPlayerStatus(), BattleManager.I?.GetEnemyStatus());
            }
            UpdateDisplay();
            return;
        }

        int lo = Mathf.Min(fromTotal, toTotal);
        int hi = Mathf.Max(fromTotal, toTotal);
        int span = hi - lo;
        float stepSec = span > 0 ? totalDurationSec / span : 0f;

        CardSheetDisplay tbSheet = null;
        if (tbCard != null && BattleUIManager.I != null
            && BattleUIManager.I.TryGetCardSheetDisplayForCardData(tbCard, out var sh))
            tbSheet = sh;

        int defPow = tbCard != null ? tbCard.defensePower : 0;
        int hpReduced = 0;

        rampText.richText = false;
        {
            var el = attackCards != null && attackCards.Count > 0
                ? ElementHelper.GetCombinedElement(attackCards)
                : ElementType.None;
            rampText.color = ElementHelper.GetElementColor(el);
            ApplyTotalAtkDefElementImage(rampElement, el);
        }

        float invSpan = span > 0 ? 1f / span : 0f;
        for (int v = lo; v <= hi; v++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (v > lo && hpReduced < hpPaid && atk != null)
            {
                atk.ApplyRawHpDamage(1);
                hpReduced++;
                BattleUIManager.I?.UpdateStatus(BattleManager.I?.GetPlayerStatus(), BattleManager.I?.GetEnemyStatus());
            }

            float t = (v - lo) * invSpan;
            int tbAtkVal = Mathf.RoundToInt(Mathf.Lerp(0f, tbSheetAtkTarget, t));
            if (v == hi)
                tbAtkVal = tbSheetAtkTarget;
            if (tbSheet != null)
                tbSheet.SetAtkDefenseNumbers(tbAtkVal, defPow);

            rampText.text = $"ATK {v}";
            if (v < hi && stepSec > 0f)
                await Task.Delay(TimeSpan.FromSeconds(stepSec), cancellationToken);
        }

        if (hpReduced < hpPaid && atk != null)
        {
            atk.ApplyRawHpDamage(hpPaid - hpReduced);
            BattleUIManager.I?.UpdateStatus(BattleManager.I?.GetPlayerStatus(), BattleManager.I?.GetEnemyStatus());
        }

        if (tbSheet != null && tbSheetAtkTarget >= 0)
            tbSheet.SetAtkDefenseNumbers(tbSheetAtkTarget, defPow);

        _tributeBloodPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
        _tributeBloodPlayerAtkDisplayLocked = true;
        UpdateDisplay();
    }

    /// <summary>気狂いハンマー演出：カード合計のみ（加護・2倍前）。</summary>
    public int ComputeHammadnessRampFrom(List<CardData> cards, PlayerStatus attacker)
    {
        if (cards == null || attacker == null) return 0;
        int sum = HammadnessRules.SumAttackPowerExcludingHammadness(cards);
        sum += MagicalSwordRules.GetActivePowerBonus(cards, attacker);
        return sum;
    }

    /// <summary>気狂いハンマー演出：ランダム決定後のカード合計（加護・2倍前）。</summary>
    public int ComputeHammadnessRampTo(List<CardData> cards, PlayerStatus attacker)
    {
        if (cards == null || attacker == null) return 0;
        return HammadnessRules.SumCardAttackPowerForHammadnessCombo(cards, attacker);
    }

    /// <summary>
    /// TOTAL と気狂いハンマーの CardSheet ATK を同時にカウントアップ。完了後リッチ表示ロック。
    /// </summary>
    public async Task PlayHammadnessAttackRampAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        CardData hammadnessCard,
        int hammadnessSheetAtkTarget,
        int fromTotal,
        int toTotal,
        float totalDurationSec,
        CancellationToken cancellationToken)
    {
        _hammadnessPreRampLocked = false;
        var rampText = GetSequenceOwnerAtkDefText();
        var rampElement = GetSequenceOwnerAtkDefElementImage();

        if (rampText == null || totalDurationSec <= 0f)
        {
            _hammadnessPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
            _hammadnessPlayerAtkDisplayLocked = true;
            UpdateDisplay();
            return;
        }

        if (fromTotal == toTotal)
        {
            _hammadnessPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
            _hammadnessPlayerAtkDisplayLocked = true;
            UpdateDisplay();
            return;
        }

        int lo = Mathf.Min(fromTotal, toTotal);
        int hi = Mathf.Max(fromTotal, toTotal);
        int span = hi - lo;
        float stepSec = span > 0 ? totalDurationSec / span : 0f;

        CardSheetDisplay hammadnessSheet = null;
        if (hammadnessCard != null && BattleUIManager.I != null
            && BattleUIManager.I.TryGetCardSheetDisplayForCardData(hammadnessCard, out var sh))
            hammadnessSheet = sh;

        int defPow = hammadnessCard != null ? hammadnessCard.defensePower : 0;

        rampText.richText = false;
        {
            var el = attackCards != null && attackCards.Count > 0
                ? ElementHelper.GetCombinedElement(attackCards)
                : ElementType.None;
            rampText.color = ElementHelper.GetElementColor(el);
            ApplyTotalAtkDefElementImage(rampElement, el);
        }

        float invSpan = (hi - lo) > 0 ? 1f / (hi - lo) : 0f;
        for (int v = lo; v <= hi; v++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            float t = (v - lo) * invSpan;
            int sheetAtkVal = Mathf.RoundToInt(Mathf.Lerp(0f, hammadnessSheetAtkTarget, t));
            if (v == hi)
                sheetAtkVal = hammadnessSheetAtkTarget;
            if (hammadnessSheet != null)
                hammadnessSheet.SetAtkDefenseNumbers(sheetAtkVal, defPow);

            rampText.text = $"ATK {v}";
            if (v < hi && stepSec > 0f)
                await Task.Delay(TimeSpan.FromSeconds(stepSec), cancellationToken);
        }

        if (hammadnessSheet != null && hammadnessSheetAtkTarget >= 0)
            hammadnessSheet.SetAtkDefenseNumbers(hammadnessSheetAtkTarget, defPow);

        _hammadnessPlayerAtkDisplayRichText = FormatAttackPowerDisplayLabel(attackCards, atk, null, true);
        _hammadnessPlayerAtkDisplayLocked = true;
        UpdateDisplay();
    }

    /// <summary>
    /// タグなし部分（「ATK 10」など）のベース色。無属性は黒、その他は <see cref="ElementHelper.GetElementColor"/>。
    /// +n / -n はリッチテキストの &lt;color&gt; で上書き。
    /// </summary>
    private static void ApplyAttackLabelTextStyle(TMP_Text tmp, Color elementTint)
    {
        if (tmp == null) return;
        tmp.richText = true;
        tmp.color = elementTint;
    }

    private static PlayerStatus GetDefenderForAttackDisplay(PlayerStatus attacker)
    {
        var bm = BattleManager.I;
        if (bm == null || attacker == null) return null;
        var p = bm.GetPlayerStatus();
        var e = bm.GetEnemyStatus();
        if (attacker.HasConfusionEffect())
        {
            if (bm.TryGetConfusionAttackTargetResolved(out bool targetsSelf))
                return targetsSelf ? attacker : (attacker == p ? e : p);
            if ((bm.CurrentState == GameState.AttackPhase || bm.CurrentState == GameState.CombatResolvePhase)
                && bm.CurrentTurnOwner == (attacker == p ? PlayerType.Player : PlayerType.Enemy))
                return attacker == p ? e : p;
        }

        if (bm.IsPlayerSelfAttackTargetMode
            && bm.CurrentState == GameState.AttackPhase
            && bm.CurrentTurnOwner == PlayerType.Player
            && attacker == p
            && PostDeathCombatContext.Active == null
            && !bm.IsPostDeathSequenceActive)
            return p;
        return attacker == p ? e : (attacker == e ? p : null);
    }

    /// <summary>
    /// カード合計 ATK に対し、イフリートは「ATK base +n」（+n は赤字）、リヴァイアサンは「-n」（n は青字）。
    /// 計算値そのものは <see cref="GetDisplayedAttackStrength"/> と一致（衰弱などで最終が変わるときは → で補足）。
    /// </summary>
    /// <param name="defenderForBlessingsOverride">
    /// 指定時は <see cref="GetDefenderForAttackDisplay"/> を使わず加護の防御側に使う（反射 TOTAL の「相手側視点」用）。
    /// </param>
    /// <param name="forMeOnlyPostRampExcludeGodRageDouble">
    /// true のときゴッドレイジ 2 倍を適用しない（ME ランプ直後の TOTAL は MP×2 までのみ）。
    /// </param>
    private string FormatAttackPowerDisplayLabel(
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defenderForBlessingsOverride = null,
        bool forMeOnlyPostRampExcludeGodRageDouble = false)
    {
        if (cards == null || cards.Count == 0 || attacker == null) return "";

        int rawCombo = CalculateTotalAttackPower(cards, attacker);
        if (rawCombo <= 0)
        {
            if (HammadnessRules.ContainsHammadness(cards) || TributeBloodRules.ContainsTributeBlood(cards)
                || MillionDollarBazookaRules.ContainsMillionDollarBazooka(cards))
                return "ATK 0";
            return "";
        }

        bool applyGodDouble = GodrageRules.IsGodrageDoublingCombo(cards) && !forMeOnlyPostRampExcludeGodRageDouble
            && !_attackDisplaySuppressGodRageDouble;
        if (_suppressMagicalExplosionPredictionDuringSequenceReveal && MagicalExplosionRules.ContainsMagicalExplosion(cards))
            applyGodDouble = false;
        if (_suppressMillionDollarBazookaPredictionDuringSequenceReveal && MillionDollarBazookaRules.ContainsMillionDollarBazooka(cards))
            applyGodDouble = false;
        if (_suppressTributeBloodPredictionDuringSequenceReveal && TributeBloodRules.ContainsTributeBlood(cards))
            applyGodDouble = false;
        if (_suppressHammadnessPredictionDuringSequenceReveal && HammadnessRules.ContainsHammadness(cards))
            applyGodDouble = false;
        int baseForBlessings = applyGodDouble ? rawCombo * 2 : rawCombo;

        int afterIfrit = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, baseForBlessings);
        int ifritDelta = afterIfrit - baseForBlessings;

        PlayerStatus defender = defenderForBlessingsOverride ?? GetDefenderForAttackDisplay(attacker);
        int afterLevi = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(attacker, defender, cards, afterIfrit);
        int leviDelta = afterIfrit - afterLevi;

        int final = afterLevi;
        if (!CardRules.IsMagicClassifiedAttackCombo(cards))
            final = attacker.ApplyOutgoingDamageModifiers(afterLevi);

        if (ifritDelta <= 0 && leviDelta <= 0)
            return $"ATK {final}";

        var sb = new StringBuilder(48);
        sb.Append("ATK ").Append(baseForBlessings);
        if (ifritDelta > 0)
        {
            sb.Append(" <color=").Append(IfritBonusColorHex).Append(">+").Append(ifritDelta).Append("</color>");
        }
        if (leviDelta > 0)
        {
            sb.Append(" <color=").Append(LeviathanSuppressColorHex).Append("> -").Append(leviDelta).Append("</color>");
        }
        if (final != afterLevi)
            sb.Append(" → ").Append(final);

        return sb.ToString();
    }

    /// <summary>
    /// ゴッドレイジ適用後の表示：カード合計を 2 倍した値を起点にイフリート→リヴァ→衰弱。ATK 本体は緑、+n / -n は従来色。
    /// </summary>
    private string FormatGodRageDoubledAttackPowerDisplayLabel(List<CardData> cards, PlayerStatus attacker, PlayerStatus defender)
    {
        if (cards == null || cards.Count == 0 || attacker == null || defender == null) return "";

        int baseSum = CalculateTotalAttackPower(cards, attacker);
        if (baseSum <= 0) return "";

        int doubledBase = baseSum * 2;
        int afterIfrit = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, doubledBase);
        int ifritDelta = afterIfrit - doubledBase;

        int afterLevi = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(attacker, defender, cards, afterIfrit);
        int leviDelta = afterIfrit - afterLevi;

        int final = afterLevi;
        if (!CardRules.IsMagicClassifiedAttackCombo(cards))
            final = attacker.ApplyOutgoingDamageModifiers(afterLevi);

        if (ifritDelta <= 0 && leviDelta <= 0)
            return $"<color={GodRageAtkBaseGreenHex}>ATK {final}</color>";

        var sb = new StringBuilder(96);
        sb.Append("<color=").Append(GodRageAtkBaseGreenHex).Append(">ATK ").Append(doubledBase).Append("</color>");
        if (ifritDelta > 0)
        {
            sb.Append(" <color=").Append(IfritBonusColorHex).Append(">+").Append(ifritDelta).Append("</color>");
        }
        if (leviDelta > 0)
        {
            sb.Append(" <color=").Append(LeviathanSuppressColorHex).Append("> -").Append(leviDelta).Append("</color>");
        }
        if (final != afterLevi)
            sb.Append(" → ").Append(final);

        return sb.ToString();
    }

    /// <summary>
    /// TotalATK 表示用。攻撃側加護 → 防御側の攻撃力抑制（リヴァイアサン等）→ 衰弱時は与ダメ補正（魔法単体攻撃は除外）。
    /// ゴッドレイジは <see cref="BattleProcessor.CalculateTotalAttackPower"/> と同様、加護前にカード合計を 2 倍する。
    /// </summary>
    private int GetDisplayedAttackStrength(List<CardData> cards, PlayerStatus attacker)
    {
        return GetDisplayedAttackStrengthWithDefender(cards, attacker, GetDefenderForAttackDisplay(attacker));
    }

    private int GetDisplayedAttackStrengthWithDefender(
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defenderForBlessings)
    {
        if (cards == null || cards.Count == 0) return 0;
        var postDeathCtx = PostDeathCombatContext.Active;
        if (postDeathCtx != null && postDeathCtx.MatchesIncoming(cards))
            return postDeathCtx.FixedAttackPower;
        int sum = CalculateTotalAttackPower(cards, attacker);
        bool godDouble = GodrageRules.IsGodrageDoublingCombo(cards)
            && !(_suppressMagicalExplosionPredictionDuringSequenceReveal && MagicalExplosionRules.ContainsMagicalExplosion(cards))
            && !(_suppressMillionDollarBazookaPredictionDuringSequenceReveal && MillionDollarBazookaRules.ContainsMillionDollarBazooka(cards))
            && !(_suppressTributeBloodPredictionDuringSequenceReveal && TributeBloodRules.ContainsTributeBlood(cards))
            && !(_suppressHammadnessPredictionDuringSequenceReveal && HammadnessRules.ContainsHammadness(cards))
            && !_attackDisplaySuppressGodRageDouble;
        if (godDouble)
            sum *= 2;
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    /// <summary>カード攻撃力合計を起点に、イフリート→リヴァ→衰弱まで適用（ゴッドレイジ2倍は <paramref name="cardSum"/> に含める）。</summary>
    private int ComputeAttackPowerFromCardSum(
        int cardSum,
        List<CardData> cards,
        PlayerStatus attacker,
        PlayerStatus defenderForBlessings)
    {
        if (cards == null || cards.Count == 0) return 0;
        int raw = cardSum;
        if (attacker != null && raw > 0)
            raw = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, cards, raw);
        if (attacker != null && raw > 0 && defenderForBlessings != null)
            raw = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(attacker, defenderForBlessings, cards, raw);
        if (attacker == null || raw <= 0) return raw;
        if (CardRules.IsMagicClassifiedAttackCombo(cards)) return raw;
        return attacker.ApplyOutgoingDamageModifiers(raw);
    }

    /// <summary>ゴッドレイジ演出用：2倍前（命中・イフリート・リヴァ等は通常どおり、2倍は未適用）。</summary>
    public int ComputeGodRageRampFrom(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        int sum = CalculateTotalAttackPower(cards, attacker);
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    /// <summary>ゴッドレイジ演出用：カード合計2倍後にイフリート・リヴァ等を適用した最終値。</summary>
    public int ComputeGodRageRampTo(List<CardData> cards, PlayerStatus attacker, PlayerStatus defenderForBlessings)
    {
        int sum = CalculateTotalAttackPower(cards, attacker);
        if (GodrageRules.IsGodrageDoublingCombo(cards))
            sum *= 2;
        return ComputeAttackPowerFromCardSum(sum, cards, attacker, defenderForBlessings);
    }

    /// <summary>
    /// ゴッドレイジ：緑字・ロボットSE・ATK を整数カウントアップ。完了後は 2 倍後リッチ表示をロックし、攻撃シーケンス終了まで維持。
    /// </summary>
    public async Task PlayGodRageAttackRampAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        int from,
        int to,
        float totalDurationSec,
        CancellationToken cancellationToken)
    {
        ClearMagicalExplosionPlayerAtkDisplayLockOnly();
        ClearMillionDollarBazookaPlayerAtkDisplayLockOnly();
        ClearHammadnessPlayerAtkDisplayLockOnly();
        _tributeBloodPlayerAtkDisplayLocked = false;
        _tributeBloodPlayerAtkDisplayRichText = null;
        _millionDollarBazookaPlayerAtkDisplayLocked = false;
        _millionDollarBazookaPlayerAtkDisplayRichText = null;
        ClearMagicalSwordRampAttackDisplayLock();
        ClearAttackModifierRevealSuppressions();

        var rampText = GetSequenceOwnerAtkDefText();
        if (rampText == null || totalDurationSec <= 0f)
        {
            ClearGodRageAttackDisplayLock();
            UpdateDisplay();
            return;
        }

        if (from == to)
        {
            _godRagePlayerAtkDisplayRichText = FormatGodRageDoubledAttackPowerDisplayLabel(attackCards, atk, def);
            _godRagePlayerAtkDisplayLocked = true;
            UpdateDisplay();
            return;
        }

        int lo = Mathf.Min(from, to);
        int hi = Mathf.Max(from, to);
        int span = hi - lo;
        float stepSec = span > 0 ? totalDurationSec / span : 0f;

        rampText.richText = false;
        rampText.color = new Color(0.2f, 0.85f, 0.35f);
        SoundEffectPlayer.I?.Play("Assets/SE/ロボット合体2.mp3");

        for (int v = lo; v <= hi; v++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rampText.text = $"ATK {v}";
            if (v < hi && stepSec > 0f)
                await Task.Delay(TimeSpan.FromSeconds(stepSec), cancellationToken);
        }

        _godRagePlayerAtkDisplayRichText = FormatGodRageDoubledAttackPowerDisplayLabel(attackCards, atk, def);
        _godRagePlayerAtkDisplayLocked = true;
        UpdateDisplay();
    }

    /// <summary>マジカルソード上乗せ 0 相当の <see cref="GetDisplayedAttackStrengthWithDefender"/>（演出用）。</summary>
    public int ComputeMagicalSwordDisplayRampFrom(
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def)
    {
        if (attackCards == null || atk == null || def == null) return 0;
        var bm = BattleManager.I;
        int savePlayer = 0;
        int saveEnemy = 0;
        bool atkIsPlayer = bm != null && ReferenceEquals(atk, bm.GetPlayerStatus());
        bool atkIsEnemy = bm != null && ReferenceEquals(atk, bm.GetEnemyStatus());
        if (bm != null && atkIsPlayer)
        {
            savePlayer = bm.MagicalSwordAttackPowerBonus;
            bm.SetMagicalSwordAttackPowerBonus(0);
        }
        else if (bm != null && atkIsEnemy)
        {
            saveEnemy = bm.MagicalSwordEnemyAttackPowerBonus;
            bm.SetMagicalSwordEnemyAttackPowerBonus(0);
        }
        try
        {
            return GetDisplayedAttackStrengthWithDefender(attackCards, atk, def);
        }
        finally
        {
            if (bm != null && atkIsPlayer)
                bm.SetMagicalSwordAttackPowerBonus(savePlayer);
            if (bm != null && atkIsEnemy)
                bm.SetMagicalSwordEnemyAttackPowerBonus(saveEnemy);
        }
    }

    /// <summary>マジカルソード上乗せを含めた <see cref="GetDisplayedAttackStrengthWithDefender"/>（演出用）。</summary>
    public int ComputeMagicalSwordDisplayRampTo(
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def) =>
        GetDisplayedAttackStrengthWithDefender(attackCards, atk, def);

    /// <summary>マジカルソード：MP 払い分の緑色 TOTAL / カード ATK カウントアップ（ゴッドレイジ・ME と同系）。</summary>
    public async Task PlayMagicalSwordAttackRampAsync(
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        CardData msCard,
        int boost,
        float totalDurationSec,
        CancellationToken cancellationToken)
    {
        ClearMagicalExplosionPlayerAtkDisplayLockOnly();
        ClearMillionDollarBazookaPlayerAtkDisplayLockOnly();
        ClearHammadnessPlayerAtkDisplayLockOnly();
        ClearGodRageAttackDisplayLock();
        ClearMagicalSwordRampAttackDisplayLock();
        if (GodrageRules.IsGodrageDoublingCombo(attackCards)
            && MagicalSwordRules.ContainsMagicalSword(attackCards))
        {
            _attackDisplaySuppressGodRageDouble = true;
            _attackDisplaySuppressMagicalSwordBonus = false;
        }
        var rampText = GetSequenceOwnerAtkDefText();
        if (msCard == null || boost <= 0 || totalDurationSec <= 0f || rampText == null)
        {
            UpdateDisplay();
            return;
        }
        int fromTotal = ComputeMagicalSwordDisplayRampFrom(attackCards, atk, def);
        int toTotal = ComputeMagicalSwordDisplayRampTo(attackCards, atk, def);
        if (fromTotal == toTotal)
        {
            UpdateDisplay();
            return;
        }
        int lo = Mathf.Min(fromTotal, toTotal);
        int hi = Mathf.Max(fromTotal, toTotal);
        int span = hi - lo;
        float stepSec = span > 0 ? totalDurationSec / span : 0f;
        int fromSheet = msCard.attackPower;
        int toSheet = fromSheet + boost;
        CardSheetDisplay msSh = null;
        if (BattleUIManager.I != null
            && BattleUIManager.I.TryGetCardSheetDisplayForCardData(msCard, out var sh))
            msSh = sh;
        int defPow = msCard.defensePower;
        rampText.richText = false;
        rampText.color = new Color(0.2f, 0.86f, 0.32f, 1f);
        SoundEffectPlayer.I?.Play("Assets/SE/ロボット合体2.mp3");
        float invSpan = (hi - lo) > 0 ? 1f / (hi - lo) : 0f;
        for (int v = lo; v <= hi; v++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            float t = (v - lo) * invSpan;
            int sAtk = Mathf.RoundToInt(Mathf.Lerp(fromSheet, toSheet, t));
            if (v == hi) sAtk = toSheet;
            if (msSh != null)
                msSh.SetAtkDefenseNumbers(sAtk, defPow);
            rampText.text = $"ATK {v}";
            if (v < hi && stepSec > 0f)
                await Task.Delay(TimeSpan.FromSeconds(stepSec), cancellationToken);
        }
        if (msSh != null)
            msSh.SetAtkDefenseNumbers(toSheet, defPow);
        int finalMsDisplay = ComputeMagicalSwordDisplayRampTo(attackCards, atk, def);
        if (finalMsDisplay > 0)
            LockMagicalSwordRampAttackDisplay(finalMsDisplay);
        UpdateDisplay();
    }

    /// <summary>
    /// 合計防御力を計算
    /// </summary>
    private int CalculateTotalDefensePower(List<CardData> defenseCards)
    {
        return CalculateTotalPower(defenseCards, false);
    }
}

