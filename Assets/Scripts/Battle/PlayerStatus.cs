using UnityEngine;
using System.Collections.Generic;


[System.Serializable]
public class PlayerStatus
{
    public string DisplayName { get; private set; } = "プレイヤー";

    public void InitializeAsPlayer()
    {
        DisplayName = (GameProfile.I != null) ? GameProfile.I.PlayerName : "プレイヤー";
    }

    public void InitializeAsEnemy()
    {
        DisplayName = (GameProfile.I != null) ? GameProfile.I.EnemyName : "対敵者";
    }

    public void SetSummonData(SummonData data)
    {
        summonData = data;
    }

    public int maxHP = 99;
    public int maxMP = 99;
    public int maxGP = 99;

    public int currentHP = 50;
    public int currentMP = 50;
    public int currentGP = 10;

    public SummonData summonData;

    public List<IStatusEffect> activeEffects = new List<IStatusEffect>(); // 状態異常一覧

    // ===== 大魔法（ArchMagic）詠唱状態 =====
    /// <summary>詠唱中の大魔法カード。null のとき詠唱していない。</summary>
    public CardData archMagicCastingCard { get; private set; }
    /// <summary>発動時の効果対象（詠唱開始時に確定。自分攻撃のときは使用者本人）。</summary>
    public PlayerStatus archMagicEffectTarget { get; private set; }
    /// <summary>残り詠唱ターン数。0 になった自分の攻撃フェーズで発動する。</summary>
    public int archMagicRemainingTurns { get; private set; }
    /// <summary>残り HP バリア（0 以下で詠唱中断。実 HP とは別）。</summary>
    public int archMagicBarrierRemaining { get; private set; }
    /// <summary>バリア破壊により詠唱がキャンセルされたか（BattleManager が消費）。</summary>
    public bool archMagicCancelPending;
    /// <summary>バリア破壊演出（Dissolve + SE）を BarriarDamage 側で再生済みか。</summary>
    public bool archMagicBarrierBreakFxPlayed;

    public bool IsCastingArchMagic => archMagicCastingCard != null;

    /// <param name="effectTarget">解放時にダメージ等を受ける側（プレイヤーが自分に向けた大魔法なら使用者本人）。</param>
    public void BeginArchMagicCasting(CardData card, int turns, PlayerStatus effectTarget)
    {
        archMagicCastingCard = card;
        archMagicEffectTarget = effectTarget;
        archMagicRemainingTurns = Mathf.Max(1, turns);
        archMagicBarrierRemaining = ArchMagicRules.GetBarrierHp(card);
        archMagicCancelPending = false;
        archMagicBarrierBreakFxPlayed = false;
    }

    /// <summary>自分の攻撃フェーズでの残りターン減算。0 になったとき発動可能。</summary>
    public void DecrementArchMagicRemainingTurns()
    {
        if (!IsCastingArchMagic) return;
        archMagicRemainingTurns = Mathf.Max(0, archMagicRemainingTurns - 1);
    }

    public void ClearArchMagicCastingState()
    {
        archMagicCastingCard = null;
        archMagicEffectTarget = null;
        archMagicRemainingTurns = 0;
        archMagicBarrierRemaining = 0;
        archMagicCancelPending = false;
        archMagicBarrierBreakFxPlayed = false;
    }

    /// <summary>ターン境界同期用。詠唱中なら残りターン・バリア・対象を権威値で揃える。</summary>
    public void ApplyAuthoritativeArchMagicCasting(CardData card, int remainingTurns, PlayerStatus effectTarget, int barrierRemaining)
    {
        if (card == null || remainingTurns <= 0)
        {
            if (!archMagicCancelPending)
                ClearArchMagicCastingState();
            return;
        }

        if (IsCastingArchMagic && archMagicCastingCard != null && ArchMagicRules.NamesMatch(archMagicCastingCard, card))
        {
            archMagicRemainingTurns = Mathf.Max(0, remainingTurns);
            archMagicBarrierRemaining = Mathf.Max(0, barrierRemaining);
            archMagicEffectTarget = effectTarget;
            archMagicCancelPending = false;
            return;
        }

        archMagicCastingCard = card;
        archMagicEffectTarget = effectTarget;
        archMagicRemainingTurns = Mathf.Max(0, remainingTurns);
        archMagicBarrierRemaining = Mathf.Max(0, barrierRemaining);
        archMagicCancelPending = false;
    }

    /// <summary>
    /// 詠唱中の HP バリアへダメージ。闇属性は即 0。残り 0 以下で詠唱中断フラグを立てる。
    /// </summary>
    /// <returns>バリア破壊（詠唱中断）したか。</returns>
    public bool ApplyArchMagicBarrierDamage(int damage, ElementType attackElement)
    {
        if (!IsCastingArchMagic || damage <= 0) return false;

        if (attackElement == ElementType.Dark)
        {
            archMagicBarrierRemaining = 0;
            MarkArchMagicBarrierBroken();
            return true;
        }

        archMagicBarrierRemaining -= damage;
        if (archMagicBarrierRemaining <= 0)
        {
            archMagicBarrierRemaining = 0;
            MarkArchMagicBarrierBroken();
            return true;
        }

        return false;
    }

    private void MarkArchMagicBarrierBroken()
    {
        archMagicCancelPending = true;
        archMagicCastingCard = null;
        archMagicEffectTarget = null;
        archMagicRemainingTurns = 0;
    }

    // ===== 顕現スキル（1バトル1回） =====
    /// <summary>顕現スキルを使用済みか。使用後は窮地でも虹演出・再発動不可。</summary>
    public bool hasUsedManifestationSkill { get; private set; }

    public void MarkManifestationSkillUsed() => hasUsedManifestationSkill = true;

    /// <summary>
    /// UI 用：現在かかっている状態異常の種類（重複なし・公式ID順）。
    /// </summary>
    public List<StatusEffectType> GetActiveAilmentTypesOrdered()
    {
        var set = new HashSet<StatusEffectType>();
        foreach (var effect in activeEffects)
        {
            if (effect == null) continue;
            if (effect.EffectType == StatusEffectType.None) continue;
            set.Add(effect.EffectType);
        }
        var list = new List<StatusEffectType>(set);
        list.Sort(CompareAilmentOrder);
        return list;
    }

    private static int CompareAilmentOrder(StatusEffectType a, StatusEffectType b)
    {
        return StatusEffectCatalog.ToOfficialId(a).CompareTo(StatusEffectCatalog.ToOfficialId(b));
    }


    // ダメージ計算（状態異常による補正あり）
    public void TakeDamage(int amount)
    {
        int modifiedAmount = amount;
        foreach (var effect in activeEffects)
        {
            modifiedAmount = effect.ModifyDamage(modifiedAmount);
        }

        currentHP = Mathf.Max(currentHP - modifiedAmount, 0);
        Debug.Log($"{DisplayName} に {modifiedAmount} ダメージ（元値: {amount}）");
    }

    /// <summary>
    /// 被ダメ補正を適用せず HP だけ減らす。病系ターン終了ダメージなど
    /// <see cref="TakeDamage"/> を通さない経路用。大魔法の中断条件は HP が実際に減ったときに適用する。
    /// </summary>
    public void ApplyRawHpDamage(int amount)
    {
        if (amount <= 0) return;
        int before = currentHP;
        currentHP = Mathf.Max(0, currentHP - amount);
        int lost = before - currentHP;
        if (lost > 0)
            Debug.Log($"{DisplayName} に {lost} ダメージ（raw、元指示: {amount}）");
    }

    /// <summary>
    /// 与えるダメージに状態異常を適用（衰弱など）。補正が終わった値を受け手へ渡す。
    /// </summary>
    public int ApplyOutgoingDamageModifiers(int amount)
    {
        int m = amount;
        foreach (var effect in activeEffects)
        {
            if (effect == null) continue;
            m = effect.ModifyOutgoingDamage(m);
        }
        return m;
    }

    public void UseMP(int amount)
    {
        currentMP = Mathf.Max(currentMP - amount, 0);
    }

    public void UseGP(int amount)
    {
        currentGP = Mathf.Max(currentGP - amount, 0);
    }

    public bool IsDead()
    {
        return currentHP <= 0;
    }

    /// <summary>濃霧が付与されているか（BottomStatusPanel の視界マスク用）。</summary>
    public bool HasFogEffect()
    {
        foreach (var e in activeEffects)
            if (e != null && e.EffectType == StatusEffectType.Fog) return true;
        return false;
    }

    /// <summary>群発頭痛が付与されているか（魔法使用不可）。</summary>
    public bool HasClusterHeadacheEffect()
    {
        foreach (var e in activeEffects)
            if (e != null && e.EffectType == StatusEffectType.ClusterHeadache) return true;
        return false;
    }

    /// <summary>眼精疲労のみ（魔法MP消費2倍）。群発と同時には通常なら存在しない。</summary>
    public bool HasEyeStrainEffect()
    {
        foreach (var e in activeEffects)
            if (e != null && e.EffectType == StatusEffectType.EyeStrain) return true;
        return false;
    }

    /// <summary>群発頭痛により魔法が一切使えない。</summary>
    public bool IsMagicUseForbidden()
    {
        return HasClusterHeadacheEffect();
    }

    /// <summary>拘束が付与されているか（防御は1枚まで）。</summary>
    public bool HasRestraintEffect()
    {
        foreach (var e in activeEffects)
            if (e != null && e.EffectType == StatusEffectType.Restraint) return true;
        return false;
    }

    /// <summary>凍結が付与されているか（AttackSelect スキップ判定）。</summary>
    public bool HasFreezeEffect()
    {
        foreach (var e in activeEffects)
            if (e != null && e.EffectType == StatusEffectType.Freeze) return true;
        return false;
    }

    /// <summary>凍結効果の実体。なければ null。</summary>
    public FreezeEffect GetFreezeEffect()
    {
        foreach (var e in activeEffects)
            if (e is FreezeEffect freeze) return freeze;
        return null;
    }

    /// <summary>呪縛が付与されているか（加護パッシブ無効・ガルーダ5n等のスキップ判定）。</summary>
    public bool HasCurseBindEffect()
    {
        foreach (var e in activeEffects)
            if (e != null && e.EffectType == StatusEffectType.CurseBind) return true;
        return false;
    }

    /// <summary>介入が付与されているか（TurnEnd で追加攻撃抽選の対象）。</summary>
    public bool HasInterventionEffect()
    {
        foreach (var e in activeEffects)
            if (e != null && e.EffectType == StatusEffectType.Intervention) return true;
        return false;
    }

    /// <summary>混乱が付与されているか（攻撃対象ランダム・TotalATKDEF 黄色）。</summary>
    public bool HasConfusionEffect()
    {
        foreach (var e in activeEffects)
            if (e != null && e.EffectType == StatusEffectType.Confusion) return true;
        return false;
    }

    /// <summary>指定タイプの状態異常をすべて除去（回復による拘束解除など）。</summary>
    public bool RemoveStatusEffectsOfType(StatusEffectType type)
    {
        if (type == StatusEffectType.None) return false;
        bool removed = false;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var e = activeEffects[i];
            if (e != null && e.EffectType == type)
            {
                e.OnRemove(this);
                activeEffects.RemoveAt(i);
                removed = true;
            }
        }
        return removed;
    }

    /// <summary>魔法1回の実際のMP消費（眼精疲労で2倍）。群発時は使用不可のため呼び出し側でガード。</summary>
    public int GetEffectiveMagicMpCost(int baseMpCost)
    {
        if (baseMpCost <= 0) return 0;
        if (HasClusterHeadacheEffect()) return baseMpCost;
        if (HasEyeStrainEffect()) return baseMpCost * 2;
        return baseMpCost;
    }

    /// <summary>選択中の魔法カードの合計消費MP（眼精疲労の倍率反映）。</summary>
    public int GetTotalEffectiveMagicMpForCards(System.Collections.Generic.IEnumerable<CardData> cards)
    {
        if (cards == null) return 0;
        int sum = 0;
        foreach (var c in cards)
        {
            if (c == null || c.cardType != CardType.Magic) continue;
            sum += GetEffectiveMagicMpCost(c.mpCost);
        }
        return sum;
    }

    /// <summary>
    /// 段階型・排他型（病・眼精／群発・凍結）と単純付与（衰弱など）を統合した付与API。
    /// </summary>
    /// <param name="suppressGrantPopupAndSound">true のとき付与ポップアップと SE を出さない（デバッグ付与など）。</param>
    /// <returns>
    /// result: 強制絶頂が必要な場合は <see cref="ProgressiveApplyResult.ForcedParadiseEcstasy"/>（呼び出し側で非同期処理）。
    /// grantPopupFadeSeconds: 付与ポップを出したときの <see cref="DamagePopup.fadeDuration"/>。0 なら寿命待ち不要。
    /// </returns>
    public (ProgressiveApplyResult result, float grantPopupFadeSeconds) TryApplyStatusEffect(
        StatusEffectType type,
        StatusProgressionConfig config,
        bool suppressGrantPopupAndSound = false,
        int freezeDurationFromCard = 0)
    {
        if (type == StatusEffectType.None)
            return (ProgressiveApplyResult.NoChange, 0f);

        if (type == StatusEffectType.RandomOneAilment)
            return TryApplyRandomOneAilmentFromCatalog(config, suppressGrantPopupAndSound, freezeDurationFromCard);

        config ??= StatusProgressionConfig.GetRuntimeFallback();

        if (type == StatusEffectType.Freeze)
        {
            int dur = freezeDurationFromCard > 0
                ? freezeDurationFromCard
                : config.defaultDebugFreezeDurationTurns;
            var freezeResult = ProgressiveStatusApplicator.ApplyFreeze(this, dur, stackExisting: false);
            if (suppressGrantPopupAndSound) return (freezeResult, 0f);
            return NotifyApplyFeedbackAndReturn(type, freezeResult);
        }

        if (DiseaseLineEffect.IsDiseaseFamily(type)
            || type == StatusEffectType.EyeStrain
            || type == StatusEffectType.ClusterHeadache)
        {
            var complex = ProgressiveStatusApplicator.Apply(this, type, config);
            if (suppressGrantPopupAndSound) return (complex, 0f);
            return NotifyApplyFeedbackAndReturn(type, complex);
        }

        if (ProgressiveStatusApplicator.TryAddSimpleEffect(this, type, config))
        {
            if (suppressGrantPopupAndSound) return (ProgressiveApplyResult.Applied, 0f);
            return NotifyApplyFeedbackAndReturn(type, ProgressiveApplyResult.Applied);
        }

        return (ProgressiveApplyResult.NoChange, 0f);
    }

    /// <summary>
    /// <see cref="StatusEffectType.RandomOneAilment"/>：15種から等確率で抽選し <see cref="TryApplyStatusEffect"/> へ委譲。
    /// 病系の進行は <see cref="ProgressiveStatusApplicator"/> に任せる。凍結は既存時は無傷。
    /// </summary>
    private (ProgressiveApplyResult result, float grantPopupFadeSeconds) TryApplyRandomOneAilmentFromCatalog(
        StatusProgressionConfig config,
        bool suppressGrantPopupAndSound,
        int freezeDurationFromCard = 0)
    {
        config ??= StatusProgressionConfig.GetRuntimeFallback();
        StatusEffectType pick = StatusEffectCatalog.PickRandomAilmentUniform();
        if (pick == StatusEffectType.None)
            return (ProgressiveApplyResult.NoChange, 0f);

        if (pick == StatusEffectType.Freeze && HasActiveEffectType(StatusEffectType.Freeze))
            return (ProgressiveApplyResult.NoChange, 0f);

        return TryApplyStatusEffect(pick, config, suppressGrantPopupAndSound, freezeDurationFromCard);
    }

    private bool HasActiveEffectType(StatusEffectType t)
    {
        foreach (var e in activeEffects)
            if (e != null && e.EffectType == t) return true;
        return false;
    }

    private (ProgressiveApplyResult result, float grantPopupFadeSeconds) NotifyApplyFeedbackAndReturn(
        StatusEffectType requested,
        ProgressiveApplyResult result)
    {
        if (!StatusEffectApplyFeedback.ShouldShowGrantPopup(result))
            return (result, 0f);

        var popupType = StatusEffectApplyFeedback.GetGrantPopupEffectType(this, requested, result);
        float fade = BattleUIManager.I != null
            ? BattleUIManager.I.ShowStatusAilmentGrantPopup(popupType, this)
            : 0f;
        return (result, fade);
    }

    /// <summary>従来の単純付与。内部で <see cref="TryApplyStatusEffect"/> を使用。</summary>
    public void AddStatusEffect(StatusEffectType type)
    {
        var (result, _) = TryApplyStatusEffect(type, null);
        if (result == ProgressiveApplyResult.ForcedParadiseEcstasy)
        {
            Debug.LogWarning($"{DisplayName}: 楽園病＋「病」は AddStatusEffect では処理されません。TryApplyStatusEffect の戻り値に応じて強制絶頂を実行してください。");
        }
    }

    // ターン開始時の状態異常処理（BattleManager から呼ぶ想定）
    public void OnTurnStart()
    {
        foreach (var effect in activeEffects)
        {
            effect.OnTurnStart(this);
        }

        activeEffects.RemoveAll(e =>
        {
            if (e.IsExpired())
            {
                e.OnRemove(this);
                Debug.Log($"{DisplayName} の {e.GetEffectName()} は終了しました");
                return true;
            }
            return false;
        });
    }
}
