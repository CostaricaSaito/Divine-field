using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 敵CPUの行動を管理するクラス
/// </summary>
public class EnemyAI
{
    /// <summary>直近の攻撃コンボ（CPU 単体／オンライン RemotePlayerAgent 共通）。</summary>
    public List<CardData> LastAttackSelection { get; protected set; }

    /// <summary>直近の防御選択（無料詠唱触媒を含む場合あり）。</summary>
    public List<CardData> LastDefenseSelection { get; protected set; }

    /// <summary>
    /// ランダムに敵の召喚データを選択する
    /// </summary>
    public virtual SummonData SelectRandomEnemySummon()
    {
        var list = SummonSelectionManager.I?.GetAllSummonData();
        if (list == null || list.Length == 0)
        {
            list = Resources.LoadAll<SummonData>("Summons");
            if (list == null || list.Length == 0)
            {
                Debug.LogWarning("[EnemyAI] 召喚データリストがnullまたは空です");
                return null;
            }
        }

        Debug.Log($"[EnemyAI] 全召喚データ数: {list.Length}, プレイヤー選択インデックス: {SummonSelectionManager.I?.SelectedIndex ?? -1}");

        var enemyCandidates = new List<SummonData>(list);
        if (SummonSelectionManager.I != null)
        {
            int playerIndex = SummonSelectionManager.I.SelectedIndex;
            if (playerIndex >= 0 && playerIndex < enemyCandidates.Count)
            {
                var removedSummon = enemyCandidates[playerIndex];
                Debug.Log($"[EnemyAI] プレイヤーが選択した召喚獣を除外: {removedSummon?.summonName ?? "null"} (インデックス: {playerIndex})");
                enemyCandidates.RemoveAt(playerIndex);
            }
            else
            {
                Debug.LogWarning($"[EnemyAI] 無効なSelectedIndex: {playerIndex} (候補数: {enemyCandidates.Count})");
            }
        }

        if (enemyCandidates.Count == 0)
        {
            Debug.LogWarning("[EnemyAI] 敵の候補召喚データがありません");
            return null;
        }

        int randomIndex = Random.Range(0, enemyCandidates.Count);
        var selected = enemyCandidates[randomIndex];
        Debug.Log($"[EnemyAI] ランダム選択: インデックス {randomIndex}, 召喚獣: {selected?.summonName ?? "null"}");
        return selected;
    }

    /// <summary>
    /// 敵手札でまだ選べるカードか（使用済み＝裏向きは除外）。
    /// 敵手札は cardUI を持たないため <see cref="HandRefillService.IsEnemyCardUsedThisTurn"/> も参照する。
    /// </summary>
    public static bool IsEnemyHandCardAvailable(CardData c, HandRefillService handRefill = null)
    {
        if (c == null) return false;
        if (c.cardUI != null && c.cardUI.IsFaceDown()) return false;
        handRefill ??= BattleManager.I?.HandRefill;
        if (handRefill != null && handRefill.IsEnemyCardUsedThisTurn(c)) return false;
        return true;
    }

    /// <summary>防御フェーズで敵 AI が選べる手札カードか。</summary>
    public static bool IsEnemyHandCardSelectable(CardData c, HandRefillService handRefill = null)
    {
        return IsEnemyHandCardAvailable(c, handRefill) && CardRules.IsUsableInDefensePhase(c);
    }

    /// <summary>
    /// 防御カードの選び方。プレイヤーと同じく属性＋濃霧付与などの攻撃内容を考慮した候補から、
    /// PrimaryDefense／Defense型を優先して選ぶ。候補がなければ null（許す）。
    /// </summary>
    public CardData SelectDefenseCard(
        List<CardData> enemyHand,
        ElementType attackElement,
        IReadOnlyList<CardData> attackCards = null,
        CardData excludeInstance = null)
    {
        return EnemyCpuDefensePlanner.SelectNormalArmor(
            enemyHand, attackElement, attackCards, excludeInstance);
    }

    /// <summary>
    /// 打ち払い失敗後の再防御。通常防具を優先し、なければ未使用の別枚打ち払いを許可。それもなければ null（許す）。
    /// </summary>
    public virtual async Task<CardData> ExecuteParryRerunDefenseSelectAsync(
        List<CardData> cpuHand,
        ElementType attackElement,
        List<CardData> incomingAttack,
        CardData usedParryCard)
    {
        if (BattleManager.I != null && BattleManager.I.IsOnlineMatch)
        {
            Debug.LogError(
                "[EnemyAI] ExecuteParryRerunDefenseSelectAsync must not run CPU selection during online match.");
            await Task.Delay(500);
            return null;
        }

        Debug.Log("[EnemyAI] Parry rerun defense select");
        var pick = EnemyCpuDefensePlanner.PickParryRerun(
            cpuHand, attackElement, incomingAttack, usedParryCard);
        LastDefenseSelection = pick.Selection;
        await Task.Delay(500);
        return pick.Primary;
    }

    private static CardData GetPrimaryFromAttackCombo(List<CardData> combo)
    {
        if (combo == null || combo.Count == 0) return null;
        foreach (var c in combo)
        {
            if (c != null && c.cardType != CardType.Magic
                && !FreeMagicCastRules.IsCombatCatalyst(c, combo))
                return c;
        }
        foreach (var c in combo)
        {
            if (c != null && c.cardType == CardType.Magic)
                return c;
        }
        return combo[0];
    }

    private static void ApplyEnemyMagicalSwordChoice(List<CardData> combo, PlayerStatus enemyStatus)
    {
        if (combo == null || enemyStatus == null) return;
        if (!MagicalSwordRules.ContainsMagicalSword(combo))
        {
            BattleManager.I?.SetMagicalSwordEnemyAttackPowerBonus(0);
            return;
        }

        int bonus = 0;
        if (MagicalSwordRules.TryGetFirstMagicalSwordRule(combo, out var rule)
            && MagicalSwordRules.CanAffordOptionalMagicalSwordAfterOtherComboMagic(combo, enemyStatus, rule.optionalMpCost))
            bonus = rule.attackPowerBonus;

        BattleManager.I?.SetMagicalSwordEnemyAttackPowerBonus(bonus);
    }

    private void ApplyEnemyAttackBookkeeping(
        List<CardData> combo,
        List<CardData> cpuHand,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        PlayerStatus enemyStatus)
    {
        if (combo == null) return;

        foreach (var card in combo)
        {
            if (card == null || card.cardType != CardType.Magic) continue;

            bool isFromPool = MagicPoolManager.I != null && MagicPoolManager.I.IsInPool(card, PlayerType.Enemy);
            if (enemyStatus != null && card.mpCost > 0 && !FreeMagicCastRules.WaivesMagicMp(combo))
            {
                int pay = enemyStatus.GetEffectiveMagicMpCost(card.mpCost);
                enemyStatus.UseMP(pay);
                Debug.Log($"[EnemyAI] MP消費: {card.cardName} -{pay}MP (残り={enemyStatus.currentMP})");
            }

            if (isFromPool)
            {
                MagicPoolManager.I?.ConsumeUse(card, PlayerType.Enemy);
                Debug.Log($"[EnemyAI] プールカード使用回数消費: {card.cardName}");
            }
            else
            {
                MagicPoolManager.I?.TryUseMagicCard(card, cpuHand, 10, null, PlayerType.Enemy);
                battleProcessor.UseCard(card, cpuHand);
                handRefill?.RecordEnemyUse(card);
                Debug.Log($"[EnemyAI] 手札魔法をプール登録: {card.cardName}");
            }
        }

        foreach (var card in combo)
        {
            if (card == null || card.cardType == CardType.Magic) continue;
            if (MagicalExplosionRules.IsMagicalExplosionCard(card)
                || MillionDollarBazookaRules.IsMillionDollarBazookaCard(card)
                || TributeBloodRules.IsTributeBloodCard(card)
                || HammadnessRules.IsHammadnessCard(card))
                continue;

            battleProcessor.UseCard(card, cpuHand);
            handRefill?.RecordEnemyUse(card);
        }
    }

    /// <summary>
    /// 敵の攻撃ターンを実行する（魔法カード・MagicPool対応）
    /// </summary>
    public virtual async Task<CardData> ExecuteAttackTurnAsync(
        List<CardData> cpuHand,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        PlayerStatus enemyStatus)
    {
        SoundEffectPlayer.I?.Play("Assets/SE/鳩時計1.mp3");
        Debug.Log("[EnemyAI] 相手の攻撃フェーズ開始");

        await Task.Delay(500);

        var playerStatus = BattleManager.I?.GetPlayerStatus();
        var plan = EnemyCpuAttackPlanner.TryPriority(cpuHand, enemyStatus, playerStatus, handRefill);

        if (plan.Kind == EnemyCpuPlanKind.Prayer)
        {
            Debug.Log("[EnemyAI] 祈り（優先）");
            await PrayerFlow.RunCpuPrayerAsync(enemyStatus, cpuHand, handRefill, CancellationToken.None);
            LastAttackSelection = null;
            return null;
        }

        if (plan.Kind == EnemyCpuPlanKind.Reload)
        {
            if (EnemyCpuHandReloadPlanner.Execute(cpuHand, handRefill))
                Debug.Log("[EnemyAI] 手札リロード後、通常行動へ");
            plan = EnemyCpuAttackPlanner.PlanNormal(cpuHand, enemyStatus, playerStatus, handRefill);
        }

        if (plan.Kind == EnemyCpuPlanKind.Exchange)
        {
            Debug.Log("[EnemyAI] 両替（優先）");
            EnemyCpuExchangePlanner.ApplyEmergencyExchange(enemyStatus);
            BattleManager.I?.EnemyCpuEconomy?.SetExchangeCooldown();
            BattleUIManager.I?.UpdateStatus(BattleManager.I?.GetPlayerStatus(), enemyStatus);
            BattleManager.I?.SetGameState(GameState.CombatResolvePhase);
            LastAttackSelection = null;
            return null;
        }

        if (plan.Kind == EnemyCpuPlanKind.Ultimate)
        {
            Debug.Log("[EnemyAI] アルティメットスキル（優先）");
            var bm = BattleManager.I;
            if (bm?.SummonSkills != null)
                await bm.SummonSkills.RunCpuUltimateSkillAsync(enemyStatus, playerStatus, CancellationToken.None);
            LastAttackSelection = null;
            return null;
        }

        if (plan.Kind == EnemyCpuPlanKind.None)
            plan = EnemyCpuAttackPlanner.PlanNormal(cpuHand, enemyStatus, playerStatus, handRefill);

        if (plan.Kind == EnemyCpuPlanKind.ArchMagic || plan.Kind == EnemyCpuPlanKind.Attack)
            LastAttackSelection = plan.Combo;
        else
            LastAttackSelection = null;

        if (LastAttackSelection == null || LastAttackSelection.Count == 0)
        {
            if (PrayerFlow.IsPrayOnlyAttackMode(cpuHand))
            {
                Debug.Log("[EnemyAI] 祈り（フォールバック）");
                await PrayerFlow.RunCpuPrayerAsync(enemyStatus, cpuHand, handRefill, CancellationToken.None);
                return null;
            }

            Debug.Log("[EnemyAI] 攻撃カードが見つからないため、ターン終了");
            return null;
        }

        ApplyEnemyMagicalSwordChoice(LastAttackSelection, enemyStatus);

        if (TributeBloodRules.ContainsTributeBlood(LastAttackSelection) && enemyStatus != null)
        {
            int hpPaid = enemyStatus.currentHP / 2;
            BattleManager.I?.SetTributeBloodEnemyHpPaidSnapshot(hpPaid);
            Debug.Log($"[EnemyAI] Tribute Blood HP payment: {hpPaid}");
        }

        var attack = GetPrimaryFromAttackCombo(LastAttackSelection);
        bool deferBookkeeping = RemotePlayerAgent.ShouldDeferRemoteAttackBookkeeping(LastAttackSelection);

        if (MagicalExplosionRules.IsMagicalExplosionCard(attack))
        {
            Debug.Log("[EnemyAI] マジカルエクスプロージョンは演出完了後に手札から除去します");
            return attack;
        }
        if (MillionDollarBazookaRules.IsMillionDollarBazookaCard(attack))
        {
            Debug.Log("[EnemyAI] 100万ドルバズーカは演出完了後に手札から除去します");
            return attack;
        }
        if (TributeBloodRules.IsTributeBloodCard(attack))
        {
            Debug.Log("[EnemyAI] トリビュートブラッドは演出完了後に手札から除去します");
            return attack;
        }
        if (HammadnessRules.IsHammadnessCard(attack))
        {
            Debug.Log("[EnemyAI] 気狂いハンマーは演出完了後に手札から除去します");
            return attack;
        }

        if (!deferBookkeeping)
            ApplyEnemyAttackBookkeeping(LastAttackSelection, cpuHand, battleProcessor, handRefill, enemyStatus);

        if (LastAttackSelection.Count > 1)
            Debug.Log($"[EnemyAI] 攻撃コンボ選択: {attack.cardName} +{LastAttackSelection.Count - 1}枚");
        else
            Debug.Log($"[EnemyAI] 攻撃カード選択: {attack.cardName}");

        return attack;
    }

    /// <summary>
    /// 敵の防御選択を実行する
    /// </summary>
    /// <param name="attackElement">攻撃側の合算属性（<see cref="ElementHelper.GetCombinedElement"/> と一致させる）</param>
    /// <param name="incomingForReflection">反射可否判定用の攻撃カード一覧（null なら反射優先なし）</param>
    public virtual async Task<CardData> ExecuteDefenseSelectAsync(
        List<CardData> cpuHand,
        ElementType attackElement,
        List<CardData> incomingForReflection = null)
    {
        Debug.Log($"[EnemyAI] 防御カード選択開始（攻撃属性={attackElement}）");
        LastDefenseSelection = null;

        var enemyStatus = BattleManager.I != null ? BattleManager.I.GetEnemyStatus() : null;
        if (enemyStatus != null && enemyStatus.IsCastingArchMagic)
        {
            Debug.Log("[EnemyAI] 大魔法詠唱中のため防御不可");
            await Task.Delay(500);
            return null;
        }

        var pick = EnemyCpuDefensePlanner.Pick(
            cpuHand, attackElement, incomingForReflection, enemyStatus);
        LastDefenseSelection = pick.Selection;
        await Task.Delay(500);
        return pick.Primary;
    }
}
