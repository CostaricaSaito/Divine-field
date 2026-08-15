using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 敵CPUの行動を管理するクラス
/// </summary>
public class EnemyAI
{
    /// <summary>直近の攻撃コンボ（CPU 単体／オンライン RemotePlayerAgent 共通）。</summary>
    public List<CardData> LastAttackSelection { get; protected set; }

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
    /// 経済アクションで売却対象のカードを選択する（ランダム）
    /// </summary>
    public CardData SelectCardForSale(List<CardData> cpuHand)
    {
        if (cpuHand == null || cpuHand.Count == 0)
        {
            Debug.LogWarning("[EnemyAI] 相手の手札が空のため、カードを選択できません");
            return null;
        }

        var selectedCard = cpuHand[Random.Range(0, cpuHand.Count)];
        if (!IsEnemyHandCardAvailable(selectedCard))
        {
            foreach (var c in cpuHand)
            {
                if (IsEnemyHandCardAvailable(c))
                {
                    selectedCard = c;
                    break;
                }
            }
        }
        if (selectedCard == null || !IsEnemyHandCardAvailable(selectedCard))
        {
            Debug.LogWarning("[EnemyAI] 相手の手札に未使用カードがありません");
            return null;
        }
        Debug.Log($"[EnemyAI] 売却対象カード選択: {selectedCard.cardName} (価値: {selectedCard.cardValue})");
        return selectedCard;
    }

    /// <summary>
    /// 攻撃カードの選び方。
    /// 手札の攻撃系魔法を最優先し、プール済みなら物理攻撃とランダム選択。
    /// 状態異常付与・回復魔法は有用な場合のみ。
    /// </summary>
    public CardData SelectAttackCard(
        List<CardData> enemyHand,
        PlayerStatus enemyStatus,
        PlayerStatus playerStatus = null,
        HandRefillService handRefill = null)
    {
        handRefill ??= BattleManager.I?.HandRefill;
        playerStatus ??= BattleManager.I?.GetPlayerStatus();
        if (enemyHand == null) return null;

        var handAttackMagic = EnemyMagicAiRules.CollectPrioritizedHandAttackMagic(
            enemyHand, enemyStatus, playerStatus, handRefill);
        if (handAttackMagic.Count > 0)
            return handAttackMagic[Random.Range(0, handAttackMagic.Count)];

        CardData magicFountain = FindMagicFountainCard(enemyHand, handRefill);
        if (magicFountain != null && OwnPoolHasMagic())
            return magicFountain;

        CardData magicSealer = FindMagicSealerCard(enemyHand, handRefill);
        if (magicSealer != null && OpponentHasPooledMagic())
            return magicSealer;

        CardData arrowOfIndra = FindArrowOfIndraCard(enemyHand, handRefill);
        if (arrowOfIndra != null && OpponentHasDestroyableHand())
            return arrowOfIndra;

        var poolMagic = EnemyMagicAiRules.CollectUsefulPoolMagic(enemyStatus, playerStatus);
        var physical = EnemyMagicAiRules.CollectPhysicalAttackCandidates(enemyHand, handRefill);

        CardData primaryPhysical = PickPreferredPhysicalAttack(physical);
        if (poolMagic.Count > 0 && primaryPhysical != null)
            return Random.Range(0, 2) == 0
                ? poolMagic[Random.Range(0, poolMagic.Count)]
                : primaryPhysical;

        if (poolMagic.Count > 0)
            return poolMagic[Random.Range(0, poolMagic.Count)];

        if (primaryPhysical != null)
            return primaryPhysical;

        var statusMagic = EnemyMagicAiRules.CollectUsefulHandStatusMagic(
            enemyHand, enemyStatus, playerStatus, handRefill);
        if (statusMagic.Count > 0)
            return statusMagic[Random.Range(0, statusMagic.Count)];

        var selfRecovery = EnemyMagicAiRules.CollectUsefulHandSelfRecoveryMagic(
            enemyHand, enemyStatus, handRefill);
        if (selfRecovery.Count > 0)
            return selfRecovery[Random.Range(0, selfRecovery.Count)];

        foreach (var c in enemyHand)
        {
            if (!IsEnemyHandCardAvailable(c, handRefill)) continue;
            if (c.cardType == CardType.Magic) continue;
            if (ArchMagicRules.IsArchMagicCard(c)) continue;
            if (CardRules.IsUsableInAttackPhase(c))
                return c;
        }

        if (magicSealer != null)
            return magicSealer;

        if (arrowOfIndra != null)
            return arrowOfIndra;

        return null;
    }

    private static CardData PickPreferredPhysicalAttack(List<CardData> physicalCandidates)
    {
        if (physicalCandidates == null || physicalCandidates.Count == 0) return null;

        foreach (var c in physicalCandidates)
        {
            if (c != null
                && (c.cardType == CardType.Attack || c.attackPhaseUseRule == AttackPhaseUseRule.Primary))
                return c;
        }

        return physicalCandidates[Random.Range(0, physicalCandidates.Count)];
    }

    private static CardData FindMagicFountainCard(List<CardData> enemyHand, HandRefillService handRefill)
    {
        if (enemyHand == null) return null;
        for (int i = 0; i < enemyHand.Count; i++)
        {
            var c = enemyHand[i];
            if (!IsEnemyHandCardAvailable(c, handRefill)) continue;
            if (!MagicFountainRules.IsMagicFountainCard(c)) continue;
            if (!CardRules.IsUsableInAttackPhase(c)) continue;
            return c;
        }
        return null;
    }

    private static bool OwnPoolHasMagic()
    {
        return MagicPoolManager.I != null
            && MagicPoolManager.I.GetPoolEntries(PlayerType.Enemy).Count > 0;
    }

    private static CardData FindMagicSealerCard(List<CardData> enemyHand, HandRefillService handRefill)
    {
        if (enemyHand == null) return null;
        for (int i = 0; i < enemyHand.Count; i++)
        {
            var c = enemyHand[i];
            if (!IsEnemyHandCardAvailable(c, handRefill)) continue;
            if (!MagicSealerRules.IsMagicSealerCard(c)) continue;
            if (!CardRules.IsUsableInAttackPhase(c)) continue;
            return c;
        }
        return null;
    }

    private static bool OpponentHasPooledMagic()
    {
        return MagicPoolManager.I != null
            && MagicPoolManager.I.GetPoolEntries(PlayerType.Player).Count > 0;
    }

    private static CardData FindArrowOfIndraCard(List<CardData> enemyHand, HandRefillService handRefill)
    {
        if (enemyHand == null) return null;
        for (int i = 0; i < enemyHand.Count; i++)
        {
            var c = enemyHand[i];
            if (!IsEnemyHandCardAvailable(c, handRefill)) continue;
            if (!ArrowOfIndraRules.IsArrowOfIndraCard(c)) continue;
            if (!CardRules.IsUsableInAttackPhase(c)) continue;
            return c;
        }
        return null;
    }

    private static bool OpponentHasDestroyableHand()
    {
        var bm = BattleManager.I;
        if (bm?.playerHand == null) return false;
        return HandDestroyRules.PickRandomDestroyableCard(bm.playerHand, PlayerType.Player) != null;
    }

    /// <summary>敵MagicPoolから使用可能なカードを選択（MPが足りる＋相手に効果があるもの）。</summary>
    public CardData SelectAttackFromPool(PlayerStatus enemyStatus, PlayerStatus playerStatus = null)
    {
        playerStatus ??= BattleManager.I?.GetPlayerStatus();
        var candidates = EnemyMagicAiRules.CollectUsefulPoolMagic(enemyStatus, playerStatus);
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
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
        if (enemyHand == null || enemyHand.Count == 0) return null;

        var choices = CardRules.GetDefenseChoicesAgainstAttack(enemyHand, attackElement, attackCards);
        if (choices == null || choices.Count == 0)
            return null;

        choices.RemoveAll(c => !IsEnemyHandCardSelectable(c));
        if (excludeInstance != null)
        {
            int excludeId = excludeInstance.GetInstanceID();
            choices.RemoveAll(c => c != null && c.GetInstanceID() == excludeId);
        }

        // Dedicated reactive-only cards use ExecuteDefenseSelectAsync priority branches.
        choices.RemoveAll(c =>
            c != null
            && BlockingRules.IsPhysicalBlockingCard(c)
            && !CardRules.CanServeAsNormalArmorDefense(c));
        choices.RemoveAll(c =>
            c != null
            && ParryRules.IsParryCard(c)
            && !CardRules.CanServeAsNormalArmorDefense(c));
        if (choices.Count == 0)
            return null;

        foreach (var c in choices)
        {
            if (c != null && CardRules.IsNormalPhysicalDefenseCard(c))
                return c;
        }

        foreach (var c in choices)
        {
            if (c != null && CardRules.IsPrimaryDefenseCard(c))
                return c;
        }

        foreach (var c in choices)
        {
            if (c != null)
                return c;
        }

        return null;
    }

    /// <summary>
    /// 打ち払い失敗後の再防御。通常防具を優先し、なければ未使用の別枚打ち払いを許可。それもなければ null（許す）。
    /// </summary>
    public async Task<CardData> ExecuteParryRerunDefenseSelectAsync(
        List<CardData> cpuHand,
        ElementType attackElement,
        List<CardData> incomingAttack,
        CardData usedParryCard)
    {
        Debug.Log("[EnemyAI] Parry rerun defense select");

        CardData normal = SelectDefenseCard(cpuHand, attackElement, incomingAttack, usedParryCard);
        if (normal != null)
        {
            await Task.Delay(500);
            return normal;
        }

        if (incomingAttack != null && incomingAttack.Count > 0 && cpuHand != null)
        {
            int usedId = usedParryCard != null ? usedParryCard.GetInstanceID() : 0;
            foreach (var c in cpuHand)
            {
                if (!IsEnemyHandCardSelectable(c)) continue;
                if (usedParryCard != null && c.GetInstanceID() == usedId) continue;
                if (ParryRules.RequiresParryExclusiveLock(c, incomingAttack))
                {
                    Debug.Log($"[EnemyAI] Parry rerun: another parry card {c.cardName}");
                    await Task.Delay(500);
                    return c;
                }
            }
        }

        Debug.Log("[EnemyAI] Parry rerun: no defense, accept damage");
        await Task.Delay(500);
        return null;
    }

    /// <summary>MagicPool から攻撃可能な魔法候補（無意味な状態異常魔法は除外）。</summary>
    protected List<CardData> GetPoolAttackCandidates(PlayerStatus enemyStatus, PlayerStatus playerStatus = null)
    {
        playerStatus ??= BattleManager.I?.GetPlayerStatus();
        return EnemyMagicAiRules.CollectUsefulPoolMagic(enemyStatus, playerStatus);
    }

    /// <summary>
    /// プレイヤーと同じコンボルールで攻撃カード群を選ぶ。
    /// </summary>
    public virtual List<CardData> SelectAttackCombo(List<CardData> enemyHand, PlayerStatus enemyStatus, HandRefillService handRefill = null)
    {
        handRefill ??= BattleManager.I?.HandRefill;
        var playerStatus = BattleManager.I?.GetPlayerStatus();
        var handCandidates = CardRules.GetAttackChoices(enemyHand, PlayerType.Enemy);
        handCandidates.RemoveAll(c => c == null || ArchMagicRules.IsArchMagicCard(c) || !IsEnemyHandCardAvailable(c, handRefill));
        handCandidates.RemoveAll(c =>
        {
            if (c == null || c.cardType != CardType.Magic) return false;
            if (CardRules.IsRecoveryCard(c))
                return !EnemyMagicAiRules.IsEnemySelfRecoveryMagicUseful(c, enemyStatus);
            return !EnemyMagicAiRules.IsEnemyMagicUsefulAgainstOpponent(c, enemyStatus, playerStatus);
        });

        var poolCandidates = GetPoolAttackCandidates(enemyStatus, playerStatus);

        var primary = SelectAttackCard(enemyHand, enemyStatus, playerStatus, handRefill);
        if (primary == null && poolCandidates.Count > 0)
            primary = poolCandidates[0];

        if (primary == null)
            return null;

        return AttackComboSelectionRules.BuildGreedyAttackCombo(
            handCandidates,
            poolCandidates,
            primary,
            enemyStatus,
            PlayerType.Enemy);
    }

    private static CardData GetPrimaryFromAttackCombo(List<CardData> combo)
    {
        if (combo == null || combo.Count == 0) return null;
        foreach (var c in combo)
        {
            if (c != null && c.cardType != CardType.Magic)
                return c;
        }
        return combo[0];
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
            if (enemyStatus != null && card.mpCost > 0)
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

        LastAttackSelection = SelectAttackCombo(cpuHand, enemyStatus, handRefill);
        if (LastAttackSelection == null || LastAttackSelection.Count == 0)
        {
            Debug.Log("[EnemyAI] 攻撃カードが見つからないため、ターン終了");
            return null;
        }

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

        var enemyStatus = BattleManager.I != null ? BattleManager.I.GetEnemyStatus() : null;
        if (enemyStatus != null && enemyStatus.IsCastingArchMagic)
        {
            Debug.Log("[EnemyAI] 大魔法詠唱中のため防御不可");
            await Task.Delay(500);
            return null;
        }

        CardData defenseCard = null;
        if (incomingForReflection != null
            && CardRules.IncomingRequiresFullOnlyReactiveDefense(incomingForReflection))
        {
            if (cpuHand != null)
            {
                foreach (var c in cpuHand)
                {
                    if (!IsEnemyHandCardSelectable(c)) continue;
                    if (ReflectionRules.IsFullReflectionCard(c)
                        && ReflectionRules.CanReflectIncoming(c, incomingForReflection))
                    {
                        defenseCard = c;
                        Debug.Log($"[EnemyAI] 完全反射を優先: {defenseCard.cardName}");
                        break;
                    }
                }
            }

            if (defenseCard == null)
                Debug.Log("[EnemyAI] 即時回復／即時効果系：許す");
            await Task.Delay(500);
            return defenseCard;
        }

        if (incomingForReflection != null && incomingForReflection.Count > 0 && cpuHand != null)
        {
            if (ReflectionRules.CanReflectPhysical(incomingForReflection))
            {
                foreach (var c in cpuHand)
                {
                    if (!IsEnemyHandCardSelectable(c)) continue;
                    if (ReflectionRules.IsFullReflectionCard(c)
                        && ReflectionRules.CanReflectIncoming(c, incomingForReflection))
                    {
                        defenseCard = c;
                        Debug.Log($"[EnemyAI] 完全反射を優先: {defenseCard.cardName}");
                        break;
                    }
                    if (ReflectionRules.IsPhysicalReflectionCard(c))
                    {
                        defenseCard = c;
                        Debug.Log($"[EnemyAI] 物理反射を優先: {defenseCard.cardName}");
                        break;
                    }
                }
            }
            else if (ReflectionRules.CanReflectMagic(incomingForReflection))
            {
                foreach (var c in cpuHand)
                {
                    if (!IsEnemyHandCardSelectable(c)) continue;
                    if (ReflectionRules.IsFullReflectionCard(c)
                        && ReflectionRules.CanReflectIncoming(c, incomingForReflection))
                    {
                        defenseCard = c;
                        Debug.Log($"[EnemyAI] 完全反射を優先: {defenseCard.cardName}");
                        break;
                    }
                    if (ReflectionRules.IsMagicReflectionCard(c))
                    {
                        defenseCard = c;
                        Debug.Log($"[EnemyAI] 魔法反射を優先: {defenseCard.cardName}");
                        break;
                    }
                }
            }
            else
            {
                foreach (var c in cpuHand)
                {
                    if (!IsEnemyHandCardSelectable(c)) continue;
                    if (ReflectionRules.IsFullReflectionCard(c)
                        && ReflectionRules.CanReflectIncoming(c, incomingForReflection))
                    {
                        defenseCard = c;
                        Debug.Log($"[EnemyAI] 完全反射を優先: {defenseCard.cardName}");
                        break;
                    }
                }
            }

            if (defenseCard == null)
            {
                foreach (var c in cpuHand)
                {
                    if (!IsEnemyHandCardSelectable(c)) continue;
                    if (ParryRules.RequiresParryExclusiveLock(c, incomingForReflection))
                    {
                        defenseCard = c;
                        Debug.Log($"[EnemyAI] 打ち払いを優先: {defenseCard.cardName}");
                        break;
                    }
                }
            }

            if (defenseCard == null && BlockingRules.CanBlockPhysical(incomingForReflection))
            {
                foreach (var c in cpuHand)
                {
                    if (!IsEnemyHandCardSelectable(c)) continue;
                    if (BlockingRules.CanUsePhysicalBlockingAgainstAttack(c, incomingForReflection)
                        && BlockingRules.CanAffordMagicDefenseMp(c, enemyStatus))
                    {
                        defenseCard = c;
                        Debug.Log($"[EnemyAI] 物理無効を優先: {defenseCard.cardName}");
                        break;
                    }
                }
            }
        }

        if (defenseCard == null)
            defenseCard = SelectDefenseCard(cpuHand, attackElement, incomingForReflection);

        if (defenseCard == null && incomingForReflection != null
            && ShiningBarrierRules.CanUseAgainstIncoming(incomingForReflection))
        {
            defenseCard = FindShiningBarrierCard(cpuHand);
            if (defenseCard != null)
                Debug.Log($"[EnemyAI] 光のバリアを使用: {defenseCard.cardName}");
        }

        if (defenseCard != null)
        {
            Debug.Log($"[EnemyAI] 防御カード選択完了: {defenseCard.cardName}");
        }
        else
        {
            Debug.Log("[EnemyAI] 有効な防御カードがないため、許す");
        }

        await Task.Delay(500);
        return defenseCard;
    }

    private static CardData FindShiningBarrierCard(List<CardData> hand)
    {
        if (hand == null) return null;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (c != null && ShiningBarrierRules.IsShiningBarrierCard(c))
                return c;
        }
        return null;
    }

    /// <summary>
    /// 防御カードを使用する（裏向きにする処理）
    /// </summary>
    public void UseDefenseCard(
        CardData defenseCard,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        List<CardData> cpuHand)
    {
        if (defenseCard == null) return;

        if (handRefill != null)
        {
            handRefill.RecordEnemyUse(defenseCard);
            Debug.Log($"[EnemyAI] 防御カード使用記録: {defenseCard.cardName}");
        }

        battleProcessor.UseCard(defenseCard, cpuHand);
        Debug.Log($"[EnemyAI] 防御カード使用: {defenseCard.cardName}");
    }
}
