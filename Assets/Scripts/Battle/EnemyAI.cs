using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 敵CPUの行動を管理するクラス
/// </summary>
public class EnemyAI
{
    /// <summary>
    /// ランダムに敵の召喚データを選択する
    /// </summary>
    public SummonData SelectRandomEnemySummon()
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
        Debug.Log($"[EnemyAI] 売却対象カード選択: {selectedCard.cardName} (価値: {selectedCard.cardValue})");
        return selectedCard;
    }

    /// <summary>
    /// 攻撃カードの選び方：通常攻撃を優先、なければ使えるものから選択
    /// 魔法カードはMP消費可能な場合のみ候補
    /// </summary>
    public CardData SelectAttackCard(List<CardData> enemyHand, PlayerStatus enemyStatus)
    {
        // 大魔法（ArchMagic）は敵 AI の自動選択対象から除外する（現状は全て）。
        // 将来 AI を拡張した場合のために明示的にスキップする。
        // 第1優先: 通常攻撃カード（PrimaryAttack or Attack型）
        foreach (var c in enemyHand)
        {
            if (c.cardType == CardType.Magic) continue;
            if (ArchMagicRules.IsArchMagicCard(c)) continue;
            if (CardRules.IsUsableInAttackPhase(c) && (c.isPrimaryAttack || c.cardType == CardType.Attack))
                return c;
        }

        // 第2優先: その他の通常カード
        foreach (var c in enemyHand)
        {
            if (c.cardType == CardType.Magic) continue;
            if (ArchMagicRules.IsArchMagicCard(c)) continue;
            if (CardRules.IsUsableInAttackPhase(c))
                return c;
        }

        // 第3優先: 手札の魔法カード（MPが足りる＋プールに空きがある場合）
        foreach (var c in enemyHand)
        {
            if (c.cardType != CardType.Magic) continue;
            if (!CardRules.IsUsableInAttackPhase(c)) continue;
            if (enemyStatus != null && enemyStatus.IsMagicUseForbidden()) continue;
            if (enemyStatus != null && enemyStatus.currentMP < enemyStatus.GetEffectiveMagicMpCost(c.mpCost)) continue;
            if (MagicPoolManager.I != null && !MagicPoolManager.I.CanAddToPool(c, PlayerType.Enemy)) continue;
            return c;
        }

        return null;
    }

    /// <summary>
    /// 敵MagicPoolから使用可能なカードを選択（MPが足りるもの）
    /// </summary>
    public CardData SelectAttackFromPool(PlayerStatus enemyStatus)
    {
        if (MagicPoolManager.I == null) return null;
        var poolEntries = MagicPoolManager.I.GetPoolEntries(PlayerType.Enemy);
        foreach (var entry in poolEntries)
        {
            if (entry.cardData == null) continue;
            if (!CardRules.IsUsableInAttackPhase(entry.cardData)) continue;
            if (enemyStatus != null && enemyStatus.IsMagicUseForbidden()) continue;
            if (enemyStatus != null && enemyStatus.currentMP < enemyStatus.GetEffectiveMagicMpCost(entry.cardData.mpCost)) continue;
            return entry.cardData;
        }
        return null;
    }

    /// <summary>
    /// 防御カードの選び方。プレイヤーと同じく属性＋濃霧付与などの攻撃内容を考慮した候補から、
    /// PrimaryDefense／Defense型を優先して選ぶ。候補がなければ null（許す）。
    /// </summary>
    public CardData SelectDefenseCard(
        List<CardData> enemyHand,
        ElementType attackElement,
        IReadOnlyList<CardData> attackCards = null)
    {
        if (enemyHand == null || enemyHand.Count == 0) return null;

        var choices = CardRules.GetDefenseChoicesAgainstAttack(enemyHand, attackElement, attackCards);
        if (choices == null || choices.Count == 0)
            return null;

        // 物理無効カードは通常防御候補に含めない（無効化ルート専用）
        choices.RemoveAll(c => c != null && BlockingRules.IsPhysicalBlockingCard(c));
        if (choices.Count == 0)
            return null;

        foreach (var c in choices)
        {
            if (c != null && CardRules.IsUsableInDefensePhase(c)
                && (c.isPrimaryDefense || c.cardType == CardType.Defense))
                return c;
        }

        foreach (var c in choices)
        {
            if (c != null && CardRules.IsUsableInDefensePhase(c))
                return c;
        }

        return null;
    }

    /// <summary>
    /// 敵の攻撃ターンを実行する（魔法カード・MagicPool対応）
    /// </summary>
    public async Task<CardData> ExecuteAttackTurnAsync(
        List<CardData> cpuHand,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        PlayerStatus enemyStatus)
    {
        SoundEffectPlayer.I?.Play("Assets/SE/鳩時計1.mp3");
        Debug.Log("[EnemyAI] 相手の攻撃フェーズ開始");

        await Task.Delay(500);

        // 手札から攻撃カードを選択
        var attack = SelectAttackCard(cpuHand, enemyStatus);
        bool isFromPool = false;

        // 手札にカードがなければプールから
        if (attack == null)
        {
            attack = SelectAttackFromPool(enemyStatus);
            isFromPool = true;
        }

        if (attack == null)
        {
            Debug.Log("[EnemyAI] 攻撃カードが見つからないため、ターン終了");
            return null;
        }

        // 魔法カードの場合はMP消費＋プール処理
        if (attack.cardType == CardType.Magic)
        {
            if (enemyStatus != null && attack.mpCost > 0)
            {
                int pay = enemyStatus.GetEffectiveMagicMpCost(attack.mpCost);
                enemyStatus.UseMP(pay);
                Debug.Log($"[EnemyAI] MP消費: {attack.cardName} -{pay}MP (残り={enemyStatus.currentMP})");
            }

            if (isFromPool)
            {
                MagicPoolManager.I?.ConsumeUse(attack, PlayerType.Enemy);
                Debug.Log($"[EnemyAI] プールカード使用回数消費: {attack.cardName}");
            }
            else
            {
                // 手札の魔法 → 敵プールに登録
                MagicPoolManager.I?.TryUseMagicCard(attack, cpuHand, 10, null, PlayerType.Enemy);
                battleProcessor.UseCard(attack, cpuHand);
                handRefill?.RecordEnemyUse(attack);
                Debug.Log($"[EnemyAI] 手札魔法をプール登録: {attack.cardName}");
            }
        }
        else
        {
            // 通常カード
            battleProcessor.UseCard(attack, cpuHand);
            handRefill?.RecordEnemyUse(attack);
        }

        Debug.Log($"[EnemyAI] 攻撃カード選択: {attack.cardName}");
        return attack;
    }

    /// <summary>
    /// 敵の防御選択を実行する
    /// </summary>
    /// <param name="attackElement">攻撃側の合算属性（<see cref="ElementHelper.GetCombinedElement"/> と一致させる）</param>
    /// <param name="incomingForReflection">反射可否判定用の攻撃カード一覧（null なら反射優先なし）</param>
    public async Task<CardData> ExecuteDefenseSelectAsync(
        List<CardData> cpuHand,
        ElementType attackElement,
        List<CardData> incomingForReflection = null)
    {
        Debug.Log($"[EnemyAI] 防御カード選択開始（攻撃属性={attackElement}）");

        CardData defenseCard = null;
        if (incomingForReflection != null && incomingForReflection.Count > 0 && cpuHand != null)
        {
            if (ReflectionRules.CanReflectPhysical(incomingForReflection))
            {
                foreach (var c in cpuHand)
                {
                    if (c != null && ReflectionRules.IsPhysicalReflectionCard(c))
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
                    if (c != null && ReflectionRules.IsMagicReflectionCard(c))
                    {
                        defenseCard = c;
                        Debug.Log($"[EnemyAI] 魔法反射を優先: {defenseCard.cardName}");
                        break;
                    }
                }
            }

            if (defenseCard == null && BlockingRules.CanBlockPhysical(incomingForReflection))
            {
                foreach (var c in cpuHand)
                {
                    if (c != null && BlockingRules.IsPhysicalBlockingCard(c))
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
