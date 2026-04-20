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
    /// 段階型・排他型（病・眼精／群発・封印）と単純付与（衰弱など）を統合した付与API。
    /// </summary>
    /// <param name="suppressGrantPopupAndSound">true のとき付与ポップアップと SE を出さない（デバッグ付与など）。</param>
    /// <returns>強制絶頂が必要な場合は <see cref="ProgressiveApplyResult.ForcedParadiseEcstasy"/>。呼び出し側で非同期処理すること。</returns>
    public ProgressiveApplyResult TryApplyStatusEffect(
        StatusEffectType type,
        StatusProgressionConfig config,
        bool suppressGrantPopupAndSound = false)
    {
        if (type == StatusEffectType.None)
            return ProgressiveApplyResult.NoChange;

        config ??= StatusProgressionConfig.GetRuntimeFallback();

        if (type == StatusEffectType.Seal
            || DiseaseLineEffect.IsDiseaseFamily(type)
            || type == StatusEffectType.EyeStrain
            || type == StatusEffectType.ClusterHeadache)
        {
            var complex = ProgressiveStatusApplicator.Apply(this, type, config);
            if (suppressGrantPopupAndSound) return complex;
            return NotifyApplyFeedbackAndReturn(type, complex);
        }

        if (ProgressiveStatusApplicator.TryAddSimpleEffect(this, type, config))
        {
            if (suppressGrantPopupAndSound) return ProgressiveApplyResult.Applied;
            return NotifyApplyFeedbackAndReturn(type, ProgressiveApplyResult.Applied);
        }

        return ProgressiveApplyResult.NoChange;
    }

    private ProgressiveApplyResult NotifyApplyFeedbackAndReturn(StatusEffectType requested, ProgressiveApplyResult result)
    {
        if (StatusEffectApplyFeedback.ShouldShowGrantPopup(result))
        {
            var popupType = StatusEffectApplyFeedback.GetGrantPopupEffectType(this, requested, result);
            BattleUIManager.I?.ShowStatusAilmentGrantPopup(popupType, this);
        }

        return result;
    }

    /// <summary>従来の単純付与。内部で <see cref="TryApplyStatusEffect"/> を使用。</summary>
    public void AddStatusEffect(StatusEffectType type)
    {
        var result = TryApplyStatusEffect(type, null);
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
