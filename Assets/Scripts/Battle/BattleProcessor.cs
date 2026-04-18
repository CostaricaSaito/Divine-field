using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

/// <summary>
/// バトルの戦闘処理を担当するクラス
/// 
/// 【役割】
/// - カードの使用処理（手札からの削除、UI破棄）
/// - 戦闘解決（ダメージ計算、命中判定）
/// - 即時効果の処理（回復、特殊効果等）
/// - 戦闘結果の通知
/// 
/// 【責任範囲】
/// - カード使用時の手札・UI管理
/// - 攻撃・防御のダメージ計算
/// - 命中率の判定
/// - 戦闘アニメーション・効果音の制御
/// 
/// 【他のクラスとの関係】
/// - BattleManager: 戦闘処理の実行要求
/// - BattleUIManager: 戦闘結果の表示
/// - CardDealer: カードUIの管理
/// 
/// 【注意事項】
/// - 状態管理は行わない（BattleStateMachineに委譲）
/// - UI表示は指示のみ（BattleUIManagerに委譲）
/// - ビジネスロジックの判定は行わない
/// </summary>
public class BattleProcessor : MonoBehaviour
{
    //========================
    // シングルトン管理
    //========================

    public static BattleProcessor I; // シングルトンインスタンス
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    //========================
    // 依存関係（Initializeで設定）
    //========================

    [Header("ステータス参照")]
    public PlayerStatus playerStatus;
    public PlayerStatus enemyStatus;

    [Header("状態異常（未設定時はランタイム既定）")]
    [SerializeField] private StatusProgressionConfig statusProgressionConfig;

    [Header("音響")]
    public AudioClip damageSE;

    /// <summary>
    /// 戦闘解決（状態異常付与など）完了後、<see cref="CardSequenceManager"/> に戻る直前の待機。
    /// 従来 500ms。TurnEnd→「病が体を蝕む」までの体感にも効くため 1000ms（2倍）を既定とする。
    /// </summary>
    private const int CombatResolveTailDelayMs = 1000;

    private CardDealer cardDealer; // UI管理用（カードの一時表示等）

    //========================
    // 初期化
    //========================

    /// <summary>
    /// 初期化処理
    /// 
    /// 【処理内容】
    /// 各システムへの参照を設定し、戦闘処理の準備を行う
    /// </summary>
    /// <param name="playerStatus">プレイヤーのステータス</param>
    /// <param name="enemyStatus">敵のステータス</param>
    /// <param name="statusUI">ステータスUI</param>
    /// <param name="cardDealer">カードディーラー</param>
    public void Initialize(
        PlayerStatus playerStatus,
        PlayerStatus enemyStatus,
        BattleStatusUI statusUI,
        CardDealer cardDealer)
    {
        this.playerStatus = playerStatus;
        this.enemyStatus = enemyStatus;
        this.cardDealer = cardDealer;
    }

    /// <summary>BattleManager の Start で呼ばれる想定。同一 SO を参照する。</summary>
    public void ConfigureStatusEffects(StatusProgressionConfig config)
    {
        statusProgressionConfig = config;
    }

    //========================
    // カード使用処理
    //========================

    /// <summary>
    /// カードを使用する（裏向きにする）
    /// 
    /// 【処理内容】
    /// 1. カードを裏向きにする
    /// 2. カードUIを無効化
    /// 3. 使用ログの出力
    /// 
    /// 【使用例】
    /// battleProcessor.UseCard(attackCard, playerHand);
    /// </summary>
    /// <param name="card">使用するカード</param>
    /// <param name="hand">手札リスト</param>
    public void UseCard(CardData card, List<CardData> hand)
    {
        if (card == null || hand == null)
        {
            Debug.LogWarning("[BattleProcessor] カードまたは手札がnullです");
            return;
        }

        // カードを裏向きにする
        if (card.cardUI != null)
        {
            card.cardUI.Setup(null, cardDealer?.CardBackSprite, playerHandRareBackPresentation: false);
            card.cardUI.button.interactable = false;
        }

        Debug.Log($"[BattleProcessor] カード使用: {card.cardName}");
    }

    //========================
    // 即時効果処理
    //========================

    /// <summary>
    /// 即時効果を解決する
    /// 
    /// 【処理内容】
    /// 1. 回復効果の適用
    /// 2. 状態異常の適用
    /// 3. 特殊効果の処理
    /// 4. ステータス更新
    /// 
    /// 【使用例】
    /// await battleProcessor.ResolveImmediateEffectAsync(healCard, playerStatus, enemyStatus);
    /// </summary>
    /// <param name="card">使用したカード</param>
    /// <param name="user">使用者</param>
    /// <param name="target">対象</param>
    /// <returns>処理完了まで待機</returns>
    public async Task ResolveImmediateEffectAsync(CardData card, PlayerStatus user, PlayerStatus target)
    {
        if (card == null || user == null)
        {
            Debug.LogWarning("[BattleProcessor] カードまたは使用者がnullです");
            return;
        }

        Debug.Log($"[BattleProcessor] 即時効果解決開始: {card.cardName}");

        // 回復効果の適用（拘束解除のみのカードも ApplyRecovery 内で処理）
        if (card.recoveryAmount > 0 || card.clearsRestraintOnUse)
        {
            ApplyRecovery(card, user);
        }

        if (card.canApplyStatusEffect && target != null && card.statusEffectToApply != StatusEffectType.None
            && card.statusEffectApplyTiming == StatusEffectApplyTiming.OnCardEffectResolve)
        {
            int roll = Random.Range(0, 100);
            if (roll < card.statusEffectChance)
            {
                var cfg = statusProgressionConfig != null ? statusProgressionConfig : StatusProgressionConfig.GetRuntimeFallback();
                var result = target.TryApplyStatusEffect(card.statusEffectToApply, cfg);
                if (ShouldShowStatusAilmentGrantPopup(result))
                    BattleUIManager.I?.ShowStatusAilmentGrantPopup(card.statusEffectToApply, target);
                if (result == ProgressiveApplyResult.ForcedParadiseEcstasy)
                    await DiseaseTurnEndProcessor.ProcessForcedParadiseEcstasyAsync(target, CancellationToken.None);
            }
        }

        ProcessSpecialEffects(card, user, target);

        UpdateStatusDisplay();

        Debug.Log($"[BattleProcessor] 即時効果解決完了: {card.cardName}");
    }

    //========================
    // 戦闘解決処理
    //========================

    /// <summary>
    /// 戦闘を解決する（複数カード対応）
    /// 
    /// 【処理内容】
    /// 1. 攻撃力・防御力の計算
    /// 2. 命中判定
    /// 3. ダメージ計算（状態異常考慮）
    /// 4. ダメージ適用
    /// 5. 戦闘結果の表示
    /// 
    /// 【使用例】
    /// await battleProcessor.ResolveCombatAsync(attackCards, defenseCard, attacker, defender, defenderHand);
    /// </summary>
    /// <param name="attackCards">攻撃カードリスト（複数選択対応）</param>
    /// <param name="defenseCard">防御カード</param>
    /// <param name="attacker">攻撃者</param>
    /// <param name="defender">防御者</param>
    /// <param name="defenderHand">防御者の手札</param>
    /// <returns>戦闘解決完了まで待機</returns>
    /// <param name="skipHitCheck">true のとき命中判定をスキップ（呼び出し側で既に判定済み）。</param>
    public async Task ResolveCombatAsync(List<CardData> attackCards, CardData defenseCard, PlayerStatus attacker, PlayerStatus defender, List<CardData> defenderHand, bool skipHitCheck = false)
    {
        if (attackCards == null || attackCards.Count == 0 || attacker == null || defender == null)
        {
            Debug.LogWarning("[BattleProcessor] 戦闘解決に必要なパラメータがnullです");
            return;
        }

        // 攻撃カード名をログ出力
        string attackCardNames = string.Join(" + ", attackCards.Select(c => c.cardName));
        Debug.Log($"[BattleProcessor] ===== 戦闘解決開始 =====");
        Debug.Log($"[BattleProcessor] 攻撃: {attackCardNames}");
        Debug.Log($"[BattleProcessor] 防御: {defenseCard?.cardName ?? "なし"}");
        Debug.Log($"[BattleProcessor] 攻撃者: {attacker.DisplayName} vs 防御者: {defender.DisplayName}");

        // 攻撃力・防御力の計算
        int attackPower = CalculateTotalAttackPower(attackCards, attacker, defender);
        int defensePower = CalculateTotalDefensePower(defenseCard, defender);

        // 属性マッチング: 属性が一致しない防御は無効
        ElementType attackElement = ElementHelper.GetCombinedElement(attackCards);
        ElementType defElement = defenseCard != null ? defenseCard.element : ElementType.None;
        if (attackElement != ElementType.None && defenseCard != null
            && !ElementHelper.CanDefendAgainst(attackElement, defenseCard))
        {
            Debug.Log($"[BattleProcessor] 属性不一致: 攻撃={attackElement} vs 防御={defElement} → 防御力0");
            defensePower = 0;
        }

        Debug.Log($"[BattleProcessor] 計算結果 - 攻撃力: {attackPower}, 防御力: {defensePower}, 攻撃属性: {attackElement}");

        if (!skipHitCheck)
        {
            bool hit = CheckHit(attackCards, attacker, defender);
            if (!hit)
            {
                Debug.Log($"[BattleProcessor] 攻撃が外れました: {attackCardNames}");
                SoundEffectPlayer.I?.Play("Assets/SE/剣の素振り1.mp3");
                BattleUIManager.I?.ShowMissPopup(defender);
                return;
            }
        }

        await ApplyCombatDamageSequenceAfterHitAsync(attackCards, attackElement, attacker, defender, attackPower, defensePower);
    }

    /// <summary>
    /// 反射で跳ね返す攻撃の「既存パイプライン適用後」の攻撃力（加護・防御側抑制まで）。
    /// 元の攻撃者／防御者（例：敵がプレイヤーを狙ったとき）で計算する。
    /// </summary>
    public int ComputeReflectionIncomingAttackPower(
        List<CardData> attackCards,
        PlayerStatus originalAttacker,
        PlayerStatus originalDefender)
    {
        return CalculateTotalAttackPower(attackCards, originalAttacker, originalDefender);
    }

    /// <summary>
    /// 反射ダメージ解決。攻撃力は <paramref name="incomingAttackPower"/> をそのまま用いる（再計算しない）。
    /// </summary>
    public async Task ResolveReflectedCombatAsync(
        List<CardData> attackCards,
        int incomingAttackPower,
        CardData defenseCard,
        PlayerStatus attacker,
        PlayerStatus defender,
        List<CardData> defenderHand,
        bool skipHitCheck = true)
    {
        if (attackCards == null || attackCards.Count == 0 || attacker == null || defender == null)
        {
            Debug.LogWarning("[BattleProcessor] ResolveReflectedCombatAsync: 無効なパラメータ");
            return;
        }

        int defensePower = CalculateTotalDefensePower(defenseCard, defender);
        ElementType attackElement = ElementHelper.GetCombinedElement(attackCards);
        ElementType defElement = defenseCard != null ? defenseCard.element : ElementType.None;
        if (attackElement != ElementType.None && defenseCard != null
            && !ElementHelper.CanDefendAgainst(attackElement, defenseCard))
        {
            defensePower = 0;
        }

        if (!skipHitCheck)
        {
            bool hit = CheckHit(attackCards, attacker, defender);
            if (!hit)
            {
                SoundEffectPlayer.I?.Play("Assets/SE/剣の素振り1.mp3");
                BattleUIManager.I?.ShowMissPopup(defender);
                return;
            }
        }

        await ApplyCombatDamageSequenceAfterHitAsync(
            attackCards, attackElement, attacker, defender, incomingAttackPower, defensePower);
    }

    //========================
    // 内部処理メソッド
    //========================

    /// <param name="finalDamage">命中後の最終ダメージ。①は1以上のときのみ付与。②は0でも付与（ミス時は呼ばれない）。</param>
    private async Task TryApplyAttackCardStatusEffectsAsync(List<CardData> attackCards, PlayerStatus defender, int finalDamage)
    {
        if (attackCards == null || defender == null) return;

        var cfg = statusProgressionConfig != null ? statusProgressionConfig : StatusProgressionConfig.GetRuntimeFallback();
        foreach (var card in attackCards)
        {
            if (card == null || !card.canApplyStatusEffect) continue;
            if (card.statusEffectToApply == StatusEffectType.None) continue;
            if (card.statusEffectApplyTiming == StatusEffectApplyTiming.WithDamageThrough && finalDamage <= 0)
                continue;
            if (Random.Range(0, 100) >= card.statusEffectChance) continue;

            var result = defender.TryApplyStatusEffect(card.statusEffectToApply, cfg);
            if (ShouldShowStatusAilmentGrantPopup(result))
                BattleUIManager.I?.ShowStatusAilmentGrantPopup(card.statusEffectToApply, defender);
            if (result == ProgressiveApplyResult.ForcedParadiseEcstasy)
                await DiseaseTurnEndProcessor.ProcessForcedParadiseEcstasyAsync(defender, CancellationToken.None);

            UpdateStatusDisplay();
        }
    }

    private static bool ShouldShowStatusAilmentGrantPopup(ProgressiveApplyResult result)
    {
        return result == ProgressiveApplyResult.Applied
            || result == ProgressiveApplyResult.DiseaseProgressed
            || result == ProgressiveApplyResult.ForcedParadiseEcstasy;
    }

    /// <summary>
    /// 複数カードの合計攻撃力を計算する（攻撃側加護の後に防御側の攻撃力抑制を適用）。
    /// </summary>
    private int CalculateTotalAttackPower(List<CardData> attackCards, PlayerStatus attacker, PlayerStatus defender)
    {
        if (attackCards == null || attackCards.Count == 0 || attacker == null) 
        {
            Debug.LogWarning("[BattleProcessor] 攻撃力計算: 無効なパラメータ");
            return 0;
        }
        
        Debug.Log($"[BattleProcessor] ===== 攻撃力計算開始 =====");
        Debug.Log($"[BattleProcessor] 攻撃者: {attacker.DisplayName}");
        Debug.Log($"[BattleProcessor] 攻撃カード数: {attackCards.Count}");
        
        int totalAttackPower = 0;
        for (int i = 0; i < attackCards.Count; i++)
        {
            var card = attackCards[i];
            if (card != null)
            {
                totalAttackPower += card.attackPower;
                Debug.Log($"[BattleProcessor] [{i+1}] {card.cardName}: ATK {card.attackPower} (累計: {totalAttackPower})");
            }
            else
            {
                Debug.LogWarning($"[BattleProcessor] [{i+1}] カードがnullです");
            }
        }

        totalAttackPower = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, attackCards, totalAttackPower);
        totalAttackPower = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(
            attacker, defender, attackCards, totalAttackPower);

        Debug.Log($"[BattleProcessor] ===== 最終攻撃力（防御側抑制後）: {totalAttackPower} =====");
        return totalAttackPower;
    }
    
    /// <summary>
    /// 攻撃力を計算する（単一カード用）
    /// </summary>
    private int CalculateAttackPower(CardData card, PlayerStatus attacker)
    {
        if (card == null || attacker == null) return 0;
        return card.attackPower;
    }

    /// <summary>
    /// 防御力を計算する（複数カード対応）
    /// </summary>
    private int CalculateTotalDefensePower(CardData card, PlayerStatus defender)
    {
        if (card == null || defender == null) return 0;
        return card.defensePower;
    }

    /// <summary>
    /// 防御力を計算する（複数カード対応）
    /// </summary>
    private int CalculateTotalDefensePower(List<CardData> cards, PlayerStatus defender)
    {
        if (cards == null || cards.Count == 0 || defender == null) return 0;
        
        int totalDefense = 0;
        foreach (var card in cards)
        {
            if (card != null)
            {
                totalDefense += card.defensePower;
            }
        }
        
        Debug.Log($"[BattleProcessor] ===== 防御力計算開始 =====");
        Debug.Log($"[BattleProcessor] 防御者: {defender.DisplayName}");
        Debug.Log($"[BattleProcessor] 防御カード数: {cards.Count}");
        
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card != null)
            {
                Debug.Log($"[BattleProcessor] [{i + 1}] {card.cardName}: DEF {card.defensePower} (累計: {totalDefense})");
            }
        }
        
        Debug.Log($"[BattleProcessor] ===== 最終防御力: {totalDefense} =====");
        return totalDefense;
    }

    /// <summary>
    /// 命中判定（Primary の hitRate・防御側の不運・攻撃側の煙幕補正）。
    /// </summary>
    private bool CheckHit(List<CardData> attackCards, PlayerStatus attacker, PlayerStatus defender)
    {
        var primary = HitRateRules.GetPrimaryForHitRate(attackCards);
        if (primary == null) return false;

        int finalPct = HitRateRules.ComputeFinalHitPercent(primary, attacker, defender);
        bool result = HitRateRules.RollHit(finalPct);

        Debug.Log($"[BattleProcessor] 命中判定: Primary={primary.cardName}, 最終{finalPct}%, 結果{(result ? "命中" : "ミス")}");
        return result;
    }

    /// <summary>
    /// ダメージを適用する
    /// </summary>
    private void ApplyDamage(PlayerStatus target, int damage)
    {
        if (target == null) return;

        target.currentHP = Mathf.Max(0, target.currentHP - damage);
        Debug.Log($"[BattleProcessor] ダメージ適用: {damage} → {target.DisplayName} (HP: {target.currentHP})");
    }

    /// <summary>
    /// 回復を適用する
    /// </summary>
    private void ApplyRecovery(CardData card, PlayerStatus target)
    {
        if (card == null || target == null) return;

        int amount = card.recoveryAmount;

        // HP回復
        if (card.healsHP)
        {
            int oldHP = target.currentHP;
            target.currentHP = Mathf.Min(target.maxHP, target.currentHP + amount);
            int actualRecovery = target.currentHP - oldHP;
            
            if (actualRecovery > 0)
            {
                Debug.Log($"[BattleProcessor] HP回復適用: {actualRecovery} → {target.DisplayName} (HP: {target.currentHP})");
                BattleUIManager.I?.ShowHealPopup(actualRecovery, "HP", target);
                // HP回復効果音を再生
                SoundEffectPlayer.I?.Play("Assets/SE/power09(DFHP回復).wav");
            }
        }

        // MP回復
        if (card.healsMP)
        {
            int oldMP = target.currentMP;
            target.currentMP = Mathf.Min(target.maxMP, target.currentMP + amount);
            int actualRecovery = target.currentMP - oldMP;
            
            if (actualRecovery > 0)
            {
                Debug.Log($"[BattleProcessor] MP回復適用: {actualRecovery} → {target.DisplayName} (MP: {target.currentMP})");
                BattleUIManager.I?.ShowHealPopup(actualRecovery, "MP", target);
                // MP回復効果音を再生
                SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す25.mp3");
            }
        }

        // GP回復
        if (card.healsGP)
        {
            int oldGP = target.currentGP;
            target.currentGP = Mathf.Min(target.maxGP, target.currentGP + amount);
            int actualRecovery = target.currentGP - oldGP;
            
            if (actualRecovery > 0)
            {
                Debug.Log($"[BattleProcessor] GP回復適用: {actualRecovery} → {target.DisplayName} (GP: {target.currentGP})");
                BattleUIManager.I?.ShowHealPopup(actualRecovery, "GP", target);
                // GP回復効果音を再生
                SoundEffectPlayer.I?.Play("Assets/SE/レジスターで精算.mp3");
            }
        }

        if (card.clearsRestraintOnUse && target.RemoveStatusEffectsOfType(StatusEffectType.Restraint))
            Debug.Log($"[BattleProcessor] 拘束解除（カード）: {target.DisplayName}");
    }

    /// <summary>
    /// 特殊効果を処理する（将来的に拡張）
    /// </summary>
    private void ProcessSpecialEffects(CardData card, PlayerStatus user, PlayerStatus target)
    {
        // 将来的に特殊効果の処理をここに追加
    }

    /// <summary>
    /// カードUIを破棄する
    /// </summary>
    private void DestroyCardUI(CardData card)
    {
        if (card?.cardUI != null)
        {
            Destroy(card.cardUI.gameObject);
            card.cardUI = null;
        }
    }

    /// <summary>
    /// ステータス表示を更新する
    /// </summary>
    private void UpdateStatusDisplay()
    {
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
    }

    /// <summary>ミス時など、命中後のダメージ結果に依らないSE。</summary>
    private void PlayDamageSE()
    {
        if (damageSE != null)
            SoundEffectPlayer.I?.Play(damageSE);
    }

    /// <summary>命中後：ダメージ0（ダメージなし！）はピコッ、1以上は従来の damageSE。</summary>
    private void PlayDamageSE(int finalDamage)
    {
        if (finalDamage <= 0)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ピコッ.mp3");
            return;
        }
        if (damageSE != null)
            SoundEffectPlayer.I?.Play(damageSE);
    }

    /// <summary>
    /// <see cref="BattleUIManager.ShowDamagePopup"/> と同じタイミングの SE（病ターン終了など、戦闘解決を経由しない経路用）。
    /// </summary>
    public void PlayDamagePopupCompanionSound(int finalDamage)
    {
        PlayDamageSE(finalDamage);
    }

    /// <summary>
    /// 死亡判定
    /// </summary>
    private bool IsDead(PlayerStatus status)
    {
        return status != null && status.currentHP <= 0;
    }

    /// <summary>
    /// 戦闘を解決する（複数防御カード対応）
    /// </summary>
    /// <param name="attackCards">攻撃カードリスト</param>
    /// <param name="defenseCards">防御カードリスト</param>
    /// <param name="attacker">攻撃者</param>
    /// <param name="defender">防御者</param>
    /// <param name="defenderHand">防御者の手札</param>
    /// <returns>戦闘解決完了まで待機</returns>
    /// <param name="skipHitCheck">true のとき命中判定をスキップ（呼び出し側で既に判定済み）。</param>
    public async Task ResolveCombatAsync(List<CardData> attackCards, List<CardData> defenseCards, PlayerStatus attacker, PlayerStatus defender, List<CardData> defenderHand, bool skipHitCheck = false)
    {
        if (attackCards == null || attackCards.Count == 0 || attacker == null || defender == null)
        {
            Debug.LogWarning("[BattleProcessor] 戦闘解決に必要なパラメータがnullです");
            return;
        }

        // 攻撃カード名をログ出力
        string attackCardNames = string.Join(" + ", attackCards.Select(c => c.cardName));
        string defenseCardNames = defenseCards != null && defenseCards.Count > 0 ? string.Join(" + ", defenseCards.Select(c => c.cardName)) : "なし";
        Debug.Log($"[BattleProcessor] ===== 戦闘解決開始（複数防御カード対応） =====");
        Debug.Log($"[BattleProcessor] 攻撃: {attackCardNames}");
        Debug.Log($"[BattleProcessor] 防御: {defenseCardNames}");
        Debug.Log($"[BattleProcessor] 攻撃者: {attacker.DisplayName} vs 防御者: {defender.DisplayName}");

        // 攻撃力・防御力の計算
        int attackPower = CalculateTotalAttackPower(attackCards, attacker, defender);
        int defensePower = CalculateTotalDefensePower(defenseCards, defender);

        // 属性マッチング: 防御の合算属性が攻撃属性と一致しなければ防御力0
        ElementType attackElement = ElementHelper.GetCombinedElement(attackCards);
        ElementType defElement = ElementHelper.GetCombinedElement(defenseCards);
        if (attackElement != ElementType.None && defenseCards != null && defenseCards.Count > 0
            && !ElementHelper.CanDefendAgainst(attackElement, defElement))
        {
            Debug.Log($"[BattleProcessor] 属性不一致: 攻撃={attackElement} vs 防御={defElement} → 防御力0");
            defensePower = 0;
        }

        Debug.Log($"[BattleProcessor] 計算結果 - 攻撃力: {attackPower}, 防御力: {defensePower}, 攻撃属性: {attackElement}");

        if (!skipHitCheck)
        {
            bool hit = CheckHit(attackCards, attacker, defender);
            if (!hit)
            {
                Debug.Log($"[BattleProcessor] 攻撃が外れました: {attackCardNames}");
                SoundEffectPlayer.I?.Play("Assets/SE/ニュッ1.mp3");
                BattleUIManager.I?.ShowMissPopup(defender);
                return;
            }
        }

        await ApplyCombatDamageSequenceAfterHitAsync(attackCards, attackElement, attacker, defender, attackPower, defensePower);
    }

    /// <summary>
    /// 命中後：超過ダメージ（第1段）→ 闇ならその直後に「その時点の残りHP」分（第2段・紫ポップアップ＋チーン1）。
    /// </summary>
    private async Task ApplyCombatDamageSequenceAfterHitAsync(
        List<CardData> attackCards,
        ElementType attackElement,
        PlayerStatus attacker,
        PlayerStatus defender,
        int attackPower,
        int defensePower)
    {
        int baseDamage = attackPower - defensePower;
        int firstPhaseDamage = Mathf.Max(0, baseDamage);
        if (!CardRules.IsMagicOnlyAttackCombo(attackCards))
            firstPhaseDamage = attacker.ApplyOutgoingDamageModifiers(firstPhaseDamage);

        Debug.Log($"[BattleProcessor] ===== ダメージ計算 =====");
        Debug.Log($"[BattleProcessor] 基本ダメージ: {attackPower} - {defensePower} = {baseDamage}");
        Debug.Log($"[BattleProcessor] 第1段（超過・与ダメ補正後）: {firstPhaseDamage}");

        await Task.Delay(500);

        float normalPopupLifetimeSec = DamagePopup.DefaultFadeDurationIfUnknown;
        if (firstPhaseDamage > 0)
        {
            ApplyDamage(defender, firstPhaseDamage);
            Debug.Log($"[BattleProcessor] ダメージ適用完了: {firstPhaseDamage} → {defender.DisplayName}");
            normalPopupLifetimeSec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowDamagePopup(firstPhaseDamage, defender)
                : DamagePopup.DefaultFadeDurationIfUnknown;
        }
        else
        {
            Debug.Log($"[BattleProcessor] ダメージ0: 攻撃力{attackPower} - 防御力{defensePower} = {baseDamage}");
            normalPopupLifetimeSec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowDamagePopup(0, defender)
                : DamagePopup.DefaultFadeDurationIfUnknown;
        }

        if (normalPopupLifetimeSec <= 0f)
            normalPopupLifetimeSec = DamagePopup.DefaultFadeDurationIfUnknown;

        PlayDamageSE(firstPhaseDamage);
        UpdateStatusDisplay();

        if (attackElement == ElementType.Dark && firstPhaseDamage > 0 && defender.currentHP > 0)
        {
            await Task.Delay(System.TimeSpan.FromSeconds(normalPopupLifetimeSec));
            await Task.Delay(DamagePopup.PostPopupIntervalMs);
            int darkDamage = defender.currentHP;
            SoundEffectPlayer.I?.Play("Assets/SE/チーン1.mp3");
            BattleUIManager.I?.ShowDarkFollowupDamagePopup(darkDamage, defender);
            ApplyDamage(defender, darkDamage);
            Debug.Log($"[BattleProcessor] 闇フォロー: 残HP相当 {darkDamage} → {defender.DisplayName}");
            UpdateStatusDisplay();
        }

        bool anyWithDamageThrough =
            attackCards.Any(c => c != null && c.canApplyStatusEffect
                && c.statusEffectToApply != StatusEffectType.None
                && c.statusEffectApplyTiming == StatusEffectApplyTiming.WithDamageThrough);
        if (firstPhaseDamage > 0 && anyWithDamageThrough)
            await Task.Delay(1000);

        await TryApplyAttackCardStatusEffectsAsync(attackCards, defender, firstPhaseDamage);

        if (IsDead(attacker) || IsDead(defender))
            Debug.Log($"[BattleProcessor] 戦闘終了: どちらかが死亡");

        await Task.Delay(CombatResolveTailDelayMs);
        Debug.Log($"[BattleProcessor] 戦闘解決完了");
    }
}