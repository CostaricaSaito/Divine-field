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
    public async Task ResolveImmediateEffectAsync(
        CardData card,
        PlayerStatus user,
        PlayerStatus target,
        CancellationToken cancellationToken = default)
    {
        if (card == null || user == null)
        {
            Debug.LogWarning("[BattleProcessor] カードまたは使用者がnullです");
            return;
        }

        Debug.Log($"[BattleProcessor] 即時効果解決開始: {card.cardName}");

        if (card.cardType == CardType.Special && card.specialCardEffect != null)
        {
            await card.specialCardEffect.ResolveOnImmediatePlayAsync(
                card, user, target, this, cancellationToken);
            ProcessSpecialEffects(card, user, target);
            UpdateStatusDisplay();
            await Task.Delay(DamagePopup.PostLastPresentationBeforeCombatResolveMs, cancellationToken);
            Debug.Log($"[BattleProcessor] 即時効果解決完了: {card.cardName}");
            return;
        }

        // 回復効果の適用（効果対象＝TOTAL で選んだ自分／相手。target が null のときのみ使用者へ）
        if (card.recoveryAmount > 0)
        {
            var recoveryRecipient = target ?? user;
            if (recoveryRecipient != null)
                await ApplyRecoveryAsync(card, recoveryRecipient, cancellationToken);
        }

        if (card.cureAllStatusEffects && target != null)
            await ApplyAllStatusAilmentsClearAsync(target, cancellationToken);

        if (card.canApplyStatusEffect && card.statusEffectToApply != StatusEffectType.None
            && card.statusEffectApplyTiming == StatusEffectApplyTiming.OnCardEffectResolve)
        {
            PlayerStatus recipient = card.statusEffectToApply == StatusEffectType.RandomOneAilment
                ? (target ?? user)
                : target;
            if (recipient != null)
                await TryApplyStatusOnCardEffectResolveAsync(
                    card.statusEffectToApply, card.statusEffectChance, recipient, cancellationToken, card.freezeDuration);
        }

        ProcessSpecialEffects(card, user, target);

        UpdateStatusDisplay();

        await Task.Delay(DamagePopup.PostLastPresentationBeforeCombatResolveMs, cancellationToken);

        Debug.Log($"[BattleProcessor] 即時効果解決完了: {card.cardName}");
    }

    /// <summary>
    /// OnCardEffectResolve 相当の状態異常付与（<see cref="SpecialCardEffectSO"/> から利用）。
    /// </summary>
    public async Task TryApplyStatusOnCardEffectResolveAsync(
        StatusEffectType effectType,
        int chance0To100,
        PlayerStatus recipient,
        CancellationToken ct,
        int freezeDurationFromCard = 0)
    {
        if (recipient == null || effectType == StatusEffectType.None) return;

        int roll = BattleRandom.Range(0, 100);
        if (roll >= chance0To100) return;

        var cfg = statusProgressionConfig != null ? statusProgressionConfig : StatusProgressionConfig.GetRuntimeFallback();
        var (applyResult, grantFade) = recipient.TryApplyStatusEffect(
            effectType, cfg, freezeDurationFromCard: freezeDurationFromCard);
        if (applyResult == ProgressiveApplyResult.ForcedParadiseEcstasy)
        {
            if (grantFade > 0f)
                await DamagePopup.WaitAfterPopupLifetimeAsync(grantFade, ct);
            await DiseaseTurnEndProcessor.ProcessForcedParadiseEcstasyAsync(recipient, ct);
        }
        else if (applyResult == ProgressiveApplyResult.NoChange)
            await ShowUnharmedPopupForNoProgressStatusAsync(recipient);
        else if (grantFade > 0f)
            await DamagePopup.WaitAfterPopupLifetimeAsync(grantFade, ct);
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

        if (defender.IsCastingArchMagic)
            defenseCard = null;

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
                await TryHandleCombatDeathIfAnyAsync(attacker, defender);
                return;
            }
        }

        IReadOnlyList<CardData> defenseList = defenseCard != null
            ? new List<CardData> { defenseCard }
            : null;
        await ApplyCombatDamageSequenceAfterHitAsync(
            attackCards, attackElement, attacker, defender, attackPower, defensePower, defenseList);
    }

    /// <summary>
    /// ダイナマイト反動：第1段与ダメージを攻撃者へ適用しポップアップ演出まで待つ。
    /// </summary>
    private async Task ApplyDynamiteRecoilAsync(PlayerStatus attacker, int recoilDamage)
    {
        if (attacker == null || recoilDamage <= 0) return;

        await Task.Delay(DamagePopup.PreDamagePopupBeatMs);

        ApplyDamage(attacker, recoilDamage);
        Debug.Log($"[BattleProcessor] Dynamite recoil: {recoilDamage} -> {attacker.DisplayName}");

        float recoilPopupLifetimeSec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowDamagePopup(recoilDamage, attacker)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (recoilPopupLifetimeSec <= 0f)
            recoilPopupLifetimeSec = DamagePopup.DefaultFadeDurationIfUnknown;

        PlayDamageSE(recoilDamage);
        UpdateStatusDisplay();

        await DamagePopup.WaitAfterPopupLifetimeAsync(recoilPopupLifetimeSec);
    }

    /// <summary>
    /// Reflection attack power after the shared pipeline (blessings, suppression, Kannaduki/Weaken).
    /// Uses original attacker/defender (e.g. when enemy targeted the player).
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

        if (defender.IsCastingArchMagic)
            defenseCard = null;

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
                await TryHandleCombatDeathIfAnyAsync(attacker, defender);
                return;
            }
        }

        IReadOnlyList<CardData> defenseList = defenseCard != null
            ? new List<CardData> { defenseCard }
            : null;
        await ApplyCombatDamageSequenceAfterHitAsync(
            attackCards, attackElement, attacker, defender, incomingAttackPower, defensePower, defenseList);
    }

    /// <summary>
    /// 宝玉「獄炎」等：受けた第1段ダメージ相当を基礎攻撃力にした単独反撃。命中は通常。宝玉連鎖内は再発火しない。
    /// </summary>
    public async Task ResolveOrbCounterCombatAsync(
        List<CardData> attackCards,
        int receivedFirstPhaseDamageAsBase,
        CardData defenseCard,
        PlayerStatus counterAttacker,
        PlayerStatus counterTarget,
        List<CardData> defenderHand,
        bool skipHitCheck)
    {
        if (attackCards == null || attackCards.Count == 0 || counterAttacker == null || counterTarget == null)
        {
            Debug.LogWarning("[BattleProcessor] ResolveOrbCounterCombatAsync: 無効なパラメータ");
            return;
        }

        if (counterTarget.IsCastingArchMagic)
            defenseCard = null;

        int attackPower = GetOrbCounterDisplayedAttackPower(attackCards, receivedFirstPhaseDamageAsBase, counterAttacker, counterTarget);
        int defensePower = CalculateTotalDefensePower(defenseCard, counterTarget);
        ElementType attackElement = ElementHelper.GetCombinedElement(attackCards);
        ElementType defElement = defenseCard != null ? defenseCard.element : ElementType.None;
        if (attackElement != ElementType.None && defenseCard != null
            && !ElementHelper.CanDefendAgainst(attackElement, defenseCard))
        {
            defensePower = 0;
        }

        if (!skipHitCheck)
        {
            bool hit = CheckHit(attackCards, counterAttacker, counterTarget);
            if (!hit)
            {
                SoundEffectPlayer.I?.Play("Assets/SE/剣の素振り1.mp3");
                BattleUIManager.I?.ShowMissPopup(counterTarget);
                await TryHandleCombatDeathIfAnyAsync(counterAttacker, counterTarget);
                return;
            }
        }

        IReadOnlyList<CardData> defenseList = defenseCard != null
            ? new List<CardData> { defenseCard }
            : null;
        await ApplyCombatDamageSequenceAfterHitAsync(
            attackCards,
            attackElement,
            counterAttacker,
            counterTarget,
            attackPower,
            defensePower,
            defenseList,
            skipDefenseOrbReactions: true,
            applyDynamiteRecoil: false,
            countsAsDirectAttack: false);
    }

    /// <summary>宝玉反撃力（TOTAL 表示用と戦闘解決のどちらでも同式）。</summary>
    public int GetOrbCounterDisplayedAttackPower(
        List<CardData> attackCards,
        int receivedFirstPhaseDamageAsBase,
        PlayerStatus counterAttacker,
        PlayerStatus counterTarget)
    {
        return CalculateOrbCounterAttackPower(
            attackCards, receivedFirstPhaseDamageAsBase, counterAttacker, counterTarget);
    }

    /// <summary>宝玉反撃：カードの ATK 合計の代わりに <paramref name="forcedBaseSum"/> を基礎に加護・抑制を適用。</summary>
    private int CalculateOrbCounterAttackPower(
        List<CardData> attackCards,
        int forcedBaseSum,
        PlayerStatus attacker,
        PlayerStatus defender)
    {
        if (attackCards == null || attackCards.Count == 0 || attacker == null) return 0;
        if (MagicalExplosionRules.ContainsMagicalExplosion(attackCards))
            return CalculateTotalAttackPower(attackCards, attacker, defender);
        if (MillionDollarBazookaRules.ContainsMillionDollarBazooka(attackCards))
            return CalculateTotalAttackPower(attackCards, attacker, defender);
        if (HammadnessRules.ContainsHammadness(attackCards))
            return CalculateTotalAttackPower(attackCards, attacker, defender);

        int totalAttackPower = Mathf.Max(0, forcedBaseSum);
        totalAttackPower += MagicalSwordRules.GetActivePowerBonus(attackCards, attacker);
        if (GodrageRules.IsGodrageDoublingCombo(attackCards))
            totalAttackPower *= 2;

        totalAttackPower = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, attackCards, totalAttackPower);
        totalAttackPower = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(
            attacker, defender, attackCards, totalAttackPower);
        return totalAttackPower;
    }

    /// <summary>冷水の宝玉：通常の HP 回復ポップ・SE だが量だけ <paramref name="hpAmount"/>（最大 HP は既存どおり）。</summary>
    public async Task ApplyOrbHpRecoveryAsync(CardData card, PlayerStatus target, int hpAmount, CancellationToken ct = default)
    {
        if (card == null || target == null || !card.healsHP || hpAmount <= 0) return;
        await ApplyRecoveryAsync(card, target, ct, hpAmount);
    }

    /// <summary>
    /// 天変地異（奇跡の船出・マナの奔流等）：発動者の状態異常を除去し、指定量だけ HP/MP を回復する。
    /// </summary>
    public async Task ApplyDisasterTriggerOwnerRecoveryAsync(
        PlayerStatus target,
        int hpRecover,
        int mpRecover,
        CancellationToken ct = default)
    {
        if (target == null) return;

        await ApplyAllStatusAilmentsClearAsync(target, ct);

        if (hpRecover > 0)
            await ApplyFlatStatRecoveryAsync(target, hpRecover, isHp: true, ct);

        if (mpRecover > 0)
            await ApplyFlatStatRecoveryAsync(target, mpRecover, isHp: false, ct);
    }

    /// <summary>
    /// 天変地異「感染症」：双方に煉獄病を100%付与。既存の病系段階は上書きする。
    /// </summary>
    public async Task ApplyDisasterInfectionAsync(
        PlayerStatus player,
        PlayerStatus enemy,
        CancellationToken ct = default)
    {
        if (player != null)
            await ForcePurgatorySicknessWithPopupAsync(player, ct);
        if (enemy != null)
            await ForcePurgatorySicknessWithPopupAsync(enemy, ct);

        UpdateStatusDisplay();
    }

    private static async Task ForcePurgatorySicknessWithPopupAsync(PlayerStatus target, CancellationToken ct)
    {
        if (target == null) return;
        if (!ProgressiveStatusApplicator.ForceSetDiseaseStage(target, StatusEffectType.PurgatorySickness))
            return;

        float fade = BattleUIManager.I != null
            ? BattleUIManager.I.ShowStatusAilmentGrantPopup(StatusEffectType.PurgatorySickness, target)
            : 0f;
        if (fade > 0f)
            await DamagePopup.WaitAfterPopupLifetimeAsync(fade, ct);
    }

    //========================
    // 内部処理メソッド
    //========================

    /// <param name="finalDamage">命中後の最終ダメージ。①は1以上のときのみ付与。②は0でも付与（ミス時は呼ばれない）。</param>
    /// <param name="defenseCards">濃霧付与など A 系魔法のとき、通常防具が誤選択されていれば付与を抑止するために渡す。</param>
    private async Task TryApplyAttackCardStatusEffectsAsync(
        List<CardData> attackCards,
        PlayerStatus attacker,
        PlayerStatus defender,
        int finalDamage,
        IReadOnlyList<CardData> defenseCards = null)
    {
        if (attackCards == null || defender == null) return;

        if (CardRules.IsStatusOnlyMagicAttackCombo(attackCards)
            && CardRules.DefenseContainsNormalPhysicalArmor(defenseCards))
            return;

        var cfg = statusProgressionConfig != null ? statusProgressionConfig : StatusProgressionConfig.GetRuntimeFallback();
        foreach (var card in attackCards)
        {
            if (card == null || !card.canApplyStatusEffect) continue;
            if (card.statusEffectToApply == StatusEffectType.None) continue;
            if (card.statusEffectApplyTiming == StatusEffectApplyTiming.WithDamageThrough && finalDamage <= 0)
                continue;
            if (BattleRandom.Range(0, 100) >= card.statusEffectChance) continue;

            // ダメージ通過時の付与は常にダメージを受けた側へ（混沌の球等の RandomOneAilment 含む）
            PlayerStatus recipient = defender;
            if (recipient == null) continue;

            var (applyResult, grantFade) = recipient.TryApplyStatusEffect(
                card.statusEffectToApply, cfg, freezeDurationFromCard: card.freezeDuration);
            if (applyResult == ProgressiveApplyResult.ForcedParadiseEcstasy)
            {
                if (grantFade > 0f)
                    await DamagePopup.WaitAfterPopupLifetimeAsync(grantFade, CancellationToken.None);
                await DiseaseTurnEndProcessor.ProcessForcedParadiseEcstasyAsync(recipient, CancellationToken.None);
            }
            else if (applyResult == ProgressiveApplyResult.NoChange)
                await ShowUnharmedPopupForNoProgressStatusAsync(recipient);
            else if (grantFade > 0f)
                await DamagePopup.WaitAfterPopupLifetimeAsync(grantFade, CancellationToken.None);

            UpdateStatusDisplay();
        }
    }

    /// <summary>
    /// 重複・段階進行なしなどで <see cref="ProgressiveApplyResult.NoChange"/> となったときの「無傷」表示と、
    /// <see cref="DamagePopup.WaitAfterPopupLifetimeAsync"/> による寿命後インターバル。
    /// </summary>
    private async Task ShowUnharmedPopupForNoProgressStatusAsync(PlayerStatus target)
    {
        if (target == null) return;

        float fadeSec = DamagePopup.DefaultFadeDurationIfUnknown;
        if (BattleUIManager.I != null)
        {
            fadeSec = BattleUIManager.I.ShowDamagePopup(0, target);
            if (fadeSec <= 0f) fadeSec = DamagePopup.DefaultFadeDurationIfUnknown;
        }

        PlayDamagePopupCompanionSound(0);
        await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec);
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

        var postDeathCtx = PostDeathCombatContext.Active;
        if (postDeathCtx != null && postDeathCtx.MatchesIncoming(attackCards))
            return postDeathCtx.FixedAttackPower;
        
        Debug.Log($"[BattleProcessor] ===== 攻撃力計算開始 =====");
        Debug.Log($"[BattleProcessor] 攻撃者: {attacker.DisplayName}");
        Debug.Log($"[BattleProcessor] 攻撃カード数: {attackCards.Count}");
        
        int totalAttackPower;
        if (MagicalExplosionRules.ContainsMagicalExplosion(attackCards))
        {
            totalAttackPower = MagicalExplosionRules.SumCardAttackPowerForMagicalExplosionCombo(attackCards, attacker);
            Debug.Log($"[BattleProcessor] マジカルエクスプロージョン込みのカード合計（加護前）: {totalAttackPower}");
        }
        else if (MillionDollarBazookaRules.ContainsMillionDollarBazooka(attackCards))
        {
            totalAttackPower = MillionDollarBazookaRules.SumCardAttackPowerForMillionDollarBazookaCombo(attackCards, attacker);
            Debug.Log($"[BattleProcessor] 100万ドルバズーカ込みのカード合計（加護前）: {totalAttackPower}");
        }
        else if (TributeBloodRules.ContainsTributeBlood(attackCards))
        {
            totalAttackPower = TributeBloodRules.SumCardAttackPowerForTributeBloodCombo(attackCards, attacker);
            Debug.Log($"[BattleProcessor] トリビュートブラッド込みのカード合計（加護前）: {totalAttackPower}");
        }
        else if (HammadnessRules.ContainsHammadness(attackCards))
        {
            totalAttackPower = HammadnessRules.SumCardAttackPowerForHammadnessCombo(attackCards, attacker);
            Debug.Log($"[BattleProcessor] 気狂いハンマー込みのカード合計（加護前）: {totalAttackPower}");
        }
        else
        {
            totalAttackPower = 0;
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
            totalAttackPower += MagicalSwordRules.GetActivePowerBonus(attackCards, attacker);
        }

        if (GodrageRules.IsGodrageDoublingCombo(attackCards))
        {
            totalAttackPower *= 2;
            Debug.Log($"[BattleProcessor] ゴッドレイジ: カード合計を2倍したあとに加護・抑制を適用 → 合計 {totalAttackPower}");
        }

        totalAttackPower = SummonPassiveBlessingApplier.ApplyAttackPowerBonus(attacker, attackCards, totalAttackPower);
        totalAttackPower = SummonPassiveBlessingApplier.ApplyDefenderOpponentAttackSuppression(
            attacker, defender, attackCards, totalAttackPower);

        // Match TOTALATKDEF / reflection pipeline: Kannaduki and Weaken apply to physical ATK.
        if (!CardRules.IsMagicClassifiedAttackCombo(attackCards))
            totalAttackPower = attacker.ApplyOutgoingDamageModifiers(totalAttackPower);

        Debug.Log($"[BattleProcessor] ===== final ATK (after outgoing modifiers): {totalAttackPower} =====");
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

        int finalPct = HitRateRules.ComputeFinalHitPercent(
            primary, attacker, defender, HitRateRules.ShouldApplyAttackerSmokeForCombat(primary));
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
        if (damage <= 0) return;

        // TakeDamage 経由にして大魔法（詠唱中の被ダメで中断）や被ダメ補正（ModifyDamage）と整合させる
        target.TakeDamage(damage);
        Debug.Log($"[BattleProcessor] ダメージ適用: {damage} → {target.DisplayName} (HP: {target.currentHP})");
    }

    /// <summary>
    /// 回復を適用する。各回復ポップアップの寿命＋ポストインターバル後まで待つ。
    /// </summary>
    private async Task ApplyRecoveryAsync(CardData card, PlayerStatus target, CancellationToken ct = default, int? hpRecoveryAmountOverride = null)
    {
        if (card == null || target == null) return;

        int amount = card.recoveryAmount;
        if (card.healsHP && hpRecoveryAmountOverride.HasValue)
            amount = hpRecoveryAmountOverride.Value;

        // HP回復
        if (card.healsHP)
        {
            int oldHP = target.currentHP;
            target.currentHP = Mathf.Min(target.maxHP, target.currentHP + amount);
            int actualRecovery = target.currentHP - oldHP;
            
            if (actualRecovery > 0)
            {
                Debug.Log($"[BattleProcessor] HP回復適用: {actualRecovery} → {target.DisplayName} (HP: {target.currentHP})");
                float fade = BattleUIManager.I != null
                    ? BattleUIManager.I.ShowHealPopup(actualRecovery, "HP", target)
                    : 0f;
                SoundEffectPlayer.I?.Play(DamagePopupSfx.HealHp);
                UpdateStatusDisplay(snapHpmgp: true);
                await DamagePopup.WaitAfterPopupLifetimeAsync(fade, ct);
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
                float fade = BattleUIManager.I != null
                    ? BattleUIManager.I.ShowHealPopup(actualRecovery, "MP", target)
                    : 0f;
                SoundEffectPlayer.I?.Play(DamagePopupSfx.HealMp);
                UpdateStatusDisplay(snapHpmgp: true);
                await DamagePopup.WaitAfterPopupLifetimeAsync(fade, ct);
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
                float fade = BattleUIManager.I != null
                    ? BattleUIManager.I.ShowHealPopup(actualRecovery, "GP", target)
                    : 0f;
                SoundEffectPlayer.I?.Play(DamagePopupSfx.HealGp);
                UpdateStatusDisplay(snapHpmgp: true);
                await DamagePopup.WaitAfterPopupLifetimeAsync(fade, ct);
            }
        }

    }

    /// <summary>
    /// 状態異常を一括除去し、異常が無ければ無傷ポップ、あれば回復魔法SE＋重ねポップ演出。
    /// 除去は演出より前に完了する。
    /// </summary>
    private async Task ApplyAllStatusAilmentsClearAsync(PlayerStatus target, CancellationToken ct)
    {
        if (target == null) return;

        var snapshot = target.GetActiveAilmentTypesOrdered();
        snapshot.RemoveAll(StatusEffectRules.IsIndelible);
        foreach (var t in snapshot)
            target.RemoveStatusEffectsOfType(t);

        UpdateStatusDisplay();

        if (snapshot.Count == 0)
        {
            await ShowUnharmedPopupForNoProgressStatusAsync(target);
            return;
        }

        SoundEffectPlayer.I?.Play("Assets/SE/回復魔法3.mp3");
        if (BattleUIManager.I != null)
            await BattleUIManager.I.PlayStatusAilmentBulkClearPresentationAsync(snapshot, target, ct);
    }

    private async Task ApplyFlatStatRecoveryAsync(
        PlayerStatus target, int amount, bool isHp, CancellationToken ct)
    {
        if (target == null || amount <= 0) return;

        if (isHp)
        {
            int old = target.currentHP;
            target.currentHP = Mathf.Min(target.maxHP, target.currentHP + amount);
            int actual = target.currentHP - old;
            if (actual <= 0) return;

            float fade = BattleUIManager.I != null
                ? BattleUIManager.I.ShowHealPopup(actual, "HP", target)
                : 0f;
            SoundEffectPlayer.I?.Play(DamagePopupSfx.HealHp);
            UpdateStatusDisplay(snapHpmgp: true);
            await DamagePopup.WaitAfterPopupLifetimeAsync(fade, ct);
            return;
        }

        int oldMp = target.currentMP;
        target.currentMP = Mathf.Min(target.maxMP, target.currentMP + amount);
        int actualMp = target.currentMP - oldMp;
        if (actualMp <= 0) return;

        float fadeMp = BattleUIManager.I != null
            ? BattleUIManager.I.ShowHealPopup(actualMp, "MP", target)
            : 0f;
        SoundEffectPlayer.I?.Play(DamagePopupSfx.HealMp);
        UpdateStatusDisplay(snapHpmgp: true);
        await DamagePopup.WaitAfterPopupLifetimeAsync(fadeMp, ct);
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
    /// ステータス表示を更新する。
    /// <paramref name="snapHpmgp"/> が true のとき HP/MP/GP 数値をポップアップと同時に即反映する（カウントアップ演出なし）。
    /// </summary>
    private void UpdateStatusDisplay(bool snapHpmgp = false)
    {
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus, snapHpmgp);
    }

    /// <summary>命中後：0 は「ピコッ」、1〜29 は <see cref="DamagePopupSfx.Slash"/>、30 以上は <see cref="DamagePopupSfx.Explosion"/>（Addressables）。</summary>
    private void PlayDamageSE(int finalDamage)
    {
        if (finalDamage <= 0)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ピコッ.mp3");
            return;
        }
        if (finalDamage >= DamagePopupSfx.HighDamageMin)
            SoundEffectPlayer.I?.Play(DamagePopupSfx.Explosion);
        else
            SoundEffectPlayer.I?.Play(DamagePopupSfx.Slash);
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

        if (defender.IsCastingArchMagic)
            defenseCards = null;

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
            && !ElementHelper.CanDefendAgainst(attackElement, defenseCards))
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
                await TryHandleCombatDeathIfAnyAsync(attacker, defender);
                return;
            }
        }

        await ApplyCombatDamageSequenceAfterHitAsync(
            attackCards, attackElement, attacker, defender, attackPower, defensePower, defenseCards);
    }

    /// <summary>
    /// After combat (or miss when attacker HP is already 0 from Tribute Blood etc.), run shared death handling.
    /// Returns true when battle end sequence started (caller should stop turn flow).
    /// </summary>
    private static async Task<bool> TryHandleCombatDeathIfAnyAsync(
        PlayerStatus attacker,
        PlayerStatus defender,
        CancellationToken cancellationToken = default)
    {
        if (attacker == null && defender == null) return false;
        bool anyDead = (attacker != null && attacker.currentHP <= 0)
            || (defender != null && defender.currentHP <= 0);
        if (!anyDead) return false;

        Debug.Log("[BattleProcessor] HP0 detected after combat — starting death handling");
        if (BattleManager.I == null) return false;
        return await BattleManager.I.TryHandleDeathIfAnyAsync(cancellationToken);
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
        int defensePower,
        IReadOnlyList<CardData> defenseCardsForStatusRule = null,
        bool skipDefenseOrbReactions = false,
        bool applyDynamiteRecoil = true,
        bool countsAsDirectAttack = true)
    {
        if (CardRules.IsStatusOnlyMagicAttackCombo(attackCards) && defenseCardsForStatusRule != null)
        {
            int stripNormalArmor = 0;
            foreach (var c in defenseCardsForStatusRule)
            {
                if (c != null && CardRules.IsNormalPhysicalDefenseCard(c))
                    stripNormalArmor += c.defensePower;
            }
            defensePower = Mathf.Max(0, defensePower - stripNormalArmor);
        }

        int baseDamage = attackPower - defensePower;
        int firstPhaseDamage = Mathf.Max(0, baseDamage);

        Debug.Log($"[BattleProcessor] ===== damage calc =====");
        Debug.Log($"[BattleProcessor] excess: {attackPower} - {defensePower} = {baseDamage}");
        Debug.Log($"[BattleProcessor] phase1 (ATK already includes Kannaduki/Weaken): {firstPhaseDamage}");

        if (defender.IsCastingArchMagic)
        {
            await ApplyArchMagicBarrierDamageSequenceAsync(
                attackElement, defender, firstPhaseDamage);
            await TryHandleCombatDeathIfAnyAsync(attacker, defender);
            return;
        }

        await Task.Delay(DamagePopup.PreDamagePopupBeatMs);

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

        await DamagePopup.WaitAfterPopupLifetimeAsync(normalPopupLifetimeSec);

        if (applyDynamiteRecoil
            && firstPhaseDamage > 0
            && DynamiteRules.ContainsDynamite(attackCards))
        {
            await ApplyDynamiteRecoilAsync(attacker, firstPhaseDamage);
        }

        if (attackElement == ElementType.Dark && firstPhaseDamage > 0 && defender.currentHP > 0)
        {
            int darkDamage = defender.currentHP;
            SoundEffectPlayer.I?.Play("Assets/SE/チーン1.mp3");
            float darkFade = BattleUIManager.I != null
                ? BattleUIManager.I.ShowDarkFollowupDamagePopup(darkDamage, defender)
                : 0f;
            ApplyDamage(defender, darkDamage);
            Debug.Log($"[BattleProcessor] 闇フォロー: 残HP相当 {darkDamage} → {defender.DisplayName}");
            UpdateStatusDisplay();
            await DamagePopup.WaitAfterPopupLifetimeAsync(darkFade);
        }

        bool anyWithDamageThrough =
            attackCards.Any(c => c != null && c.canApplyStatusEffect
                && c.statusEffectToApply != StatusEffectType.None
                && c.statusEffectApplyTiming == StatusEffectApplyTiming.WithDamageThrough);
        if (firstPhaseDamage > 0 && anyWithDamageThrough)
            await Task.Delay(DamagePopup.PreStatusEffectAfterDamagePopupDelayMs);

        await TryApplyAttackCardStatusEffectsAsync(attackCards, attacker, defender, firstPhaseDamage, defenseCardsForStatusRule);

        await ShivaDirectAttackFreezeFlow.TryApplyFreezeAfterDirectAttackAsync(
            attacker, defender, firstPhaseDamage, countsAsDirectAttack);
        UpdateStatusDisplay();

        if (!skipDefenseOrbReactions
            && firstPhaseDamage > 0
            && defenseCardsForStatusRule != null
            && BattleManager.I != null)
        {
            var orbs = OrbCardRules.CollectOrbsInDefenseOrder(defenseCardsForStatusRule);
            if (orbs.Count > 0)
            {
                await BattleManager.I.PresentOrbDefenseReactionsAsync(
                    this,
                    orbs,
                    firstPhaseDamage,
                    attacker,
                    defender,
                    CancellationToken.None);
            }
        }

        if (await TryHandleCombatDeathIfAnyAsync(attacker, defender))
            return;

        await Task.Delay(DamagePopup.PostLastPresentationBeforeCombatResolveMs);
        Debug.Log($"[BattleProcessor] 戦闘解決完了");
    }

    private static int ApplyIncomingDamageModifiers(PlayerStatus defender, int amount)
    {
        if (defender == null || amount <= 0) return amount;
        int modified = amount;
        foreach (var effect in defender.activeEffects)
        {
            if (effect != null)
                modified = effect.ModifyDamage(modified);
        }
        return modified;
    }

    /// <summary>大魔法詠唱中：実 HP ではなくバリアへダメージ。状態異常・反動・闇即死は発生しない。</summary>
    private async Task ApplyArchMagicBarrierDamageSequenceAsync(
        ElementType attackElement,
        PlayerStatus defender,
        int firstPhaseDamage)
    {
        int barrierDamage = ApplyIncomingDamageModifiers(defender, firstPhaseDamage);
        int barrierBefore = defender.archMagicBarrierRemaining;

        await Task.Delay(DamagePopup.PreDamagePopupBeatMs);

        if (barrierDamage > 0)
        {
            bool broken = defender.ApplyArchMagicBarrierDamage(barrierDamage, attackElement);
            int barrierAfter = defender.archMagicBarrierRemaining;
            Debug.Log($"[BattleProcessor] ArchMagic barrier: {barrierDamage} -> {defender.DisplayName} (rest {barrierAfter}, broken={broken})");

            if (BattleUIManager.I != null)
                await BattleUIManager.I.ShowBarriarDamagePopupAsync(
                    barrierBefore, barrierAfter, broken, defender, CancellationToken.None);

            BattleUIManager.I?.UpdateArchMagicBarrierForStatus(defender, barrierAfter);
        }
        else
        {
            Debug.Log($"[BattleProcessor] ArchMagic barrier: 0 damage (no change, rest {barrierBefore})");
        }

        UpdateStatusDisplay();
        await Task.Delay(DamagePopup.PostLastPresentationBeforeCombatResolveMs);
        Debug.Log("[BattleProcessor] 大魔法バリア解決完了");
    }

    /// <summary>
    /// 道連れの鎖：固定攻撃力・補正無視・闇第2段・命中は通常。死亡再チェックは PostDeath キュー側が担当。
    /// </summary>
    public async Task ResolvePostDeathDeadlyChainCombatAsync(
        List<CardData> attackCards,
        CardData defenseCard,
        PlayerStatus attacker,
        PlayerStatus defender,
        List<CardData> defenderHand,
        DeadlyChainPostDeathEffectSO effect,
        CancellationToken cancellationToken = default)
    {
        var defenseCards = defenseCard != null ? new List<CardData> { defenseCard } : new List<CardData>();
        await ResolvePostDeathDeadlyChainCombatAsync(
            attackCards, defenseCards, attacker, defender, defenderHand, effect, cancellationToken);
    }

    public async Task ResolvePostDeathDeadlyChainCombatAsync(
        List<CardData> attackCards,
        List<CardData> defenseCards,
        PlayerStatus attacker,
        PlayerStatus defender,
        List<CardData> defenderHand,
        DeadlyChainPostDeathEffectSO effect,
        CancellationToken cancellationToken = default)
    {
        if (attackCards == null || attackCards.Count == 0 || attacker == null || defender == null || effect == null)
            return;

        if (defender.IsCastingArchMagic)
            defenseCards = null;

        int attackPower = effect.fixedAttackPower;
        ElementType attackElement = effect.attackElement;
        int defensePower = defenseCards != null && defenseCards.Count > 0
            ? CalculateTotalDefensePower(defenseCards, defender)
            : 0;

        ElementType defElement = defenseCards != null && defenseCards.Count > 0
            ? ElementHelper.GetCombinedElement(defenseCards)
            : ElementType.None;
        if (attackElement != ElementType.None && defenseCards != null && defenseCards.Count > 0
            && !ElementHelper.CanDefendAgainst(attackElement, defenseCards))
        {
            defensePower = 0;
        }

        bool hit = CheckHit(attackCards, attacker, defender);
        if (!hit)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/剣の素振り1.mp3");
            BattleUIManager.I?.ShowMissPopup(defender);
            return;
        }

        await ApplyPostDeathCombatDamageSequenceAsync(
            attackElement, attacker, defender, attackPower, defensePower, cancellationToken);
    }

    private async Task ApplyPostDeathCombatDamageSequenceAsync(
        ElementType attackElement,
        PlayerStatus attacker,
        PlayerStatus defender,
        int attackPower,
        int defensePower,
        CancellationToken cancellationToken)
    {
        int firstPhaseDamage = Mathf.Max(0, attackPower - defensePower);

        if (defender.IsCastingArchMagic)
        {
            await ApplyPostDeathArchMagicBarrierDamageAsync(defender, firstPhaseDamage, cancellationToken);
            return;
        }

        await Task.Delay(DamagePopup.PreDamagePopupBeatMs, cancellationToken);

        float normalPopupLifetimeSec = DamagePopup.DefaultFadeDurationIfUnknown;
        if (firstPhaseDamage > 0)
        {
            defender.ApplyRawHpDamage(firstPhaseDamage);
            normalPopupLifetimeSec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowDamagePopup(firstPhaseDamage, defender)
                : DamagePopup.DefaultFadeDurationIfUnknown;
        }
        else
        {
            normalPopupLifetimeSec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowDamagePopup(0, defender)
                : DamagePopup.DefaultFadeDurationIfUnknown;
        }

        if (normalPopupLifetimeSec <= 0f)
            normalPopupLifetimeSec = DamagePopup.DefaultFadeDurationIfUnknown;

        PlayDamageSE(firstPhaseDamage);
        UpdateStatusDisplay();

        await DamagePopup.WaitAfterPopupLifetimeAsync(normalPopupLifetimeSec, cancellationToken);

        if (attackElement == ElementType.Dark && firstPhaseDamage > 0 && defender.currentHP > 0)
        {
            int darkDamage = defender.currentHP;
            SoundEffectPlayer.I?.Play("Assets/SE/チーン1.mp3");
            float darkFade = BattleUIManager.I != null
                ? BattleUIManager.I.ShowDarkFollowupDamagePopup(darkDamage, defender)
                : 0f;
            defender.ApplyRawHpDamage(darkDamage);
            UpdateStatusDisplay();
            await DamagePopup.WaitAfterPopupLifetimeAsync(darkFade, cancellationToken);
        }

        await Task.Delay(DamagePopup.PostLastPresentationBeforeCombatResolveMs, cancellationToken);
    }

    private async Task ApplyPostDeathArchMagicBarrierDamageAsync(
        PlayerStatus defender,
        int firstPhaseDamage,
        CancellationToken cancellationToken)
    {
        ElementType attackElement = PostDeathCombatContext.Active?.AttackElement ?? ElementType.None;
        int barrierBefore = defender.archMagicBarrierRemaining;

        await Task.Delay(DamagePopup.PreDamagePopupBeatMs, cancellationToken);

        if (firstPhaseDamage > 0)
        {
            bool broken = defender.ApplyArchMagicBarrierDamage(firstPhaseDamage, attackElement);
            int barrierAfter = defender.archMagicBarrierRemaining;
            if (BattleUIManager.I != null)
                await BattleUIManager.I.ShowBarriarDamagePopupAsync(
                    barrierBefore, barrierAfter, broken, defender, cancellationToken);
            BattleUIManager.I?.UpdateArchMagicBarrierForStatus(defender, barrierAfter);
        }

        UpdateStatusDisplay();
        await Task.Delay(DamagePopup.PostLastPresentationBeforeCombatResolveMs, cancellationToken);
    }
}