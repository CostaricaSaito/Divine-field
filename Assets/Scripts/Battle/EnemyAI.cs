using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 敵CPUの行動を管理するクラス
/// BattleManagerからCPU関連の処理を全て移設
/// </summary>
public class EnemyAI
{
    /// <summary>
    /// ランダムに敵の召喚データを選択する
    /// BattleManagerのGetRandomEnemySummonから移設
    /// </summary>
    public SummonData SelectRandomEnemySummon()
    {
        var list = SummonSelectionManager.I?.GetAllSummonData();
        if (list == null || list.Length == 0)
        {
            // SummonSelectionManagerがnullの場合は直接Resourcesから読み込む
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

        Debug.Log($"[EnemyAI] 敵の候補召喚データ数: {enemyCandidates.Count}");
        foreach (var candidate in enemyCandidates)
        {
            Debug.Log($"[EnemyAI] 候補: {candidate?.summonName ?? "null"}");
        }

        int randomIndex = Random.Range(0, enemyCandidates.Count);
        var selected = enemyCandidates[randomIndex];
        Debug.Log($"[EnemyAI] ランダム選択: インデックス {randomIndex}, 召喚獣: {selected?.summonName ?? "null"}");
        return selected;
    }

    /// <summary>
    /// 経済アクションで売却対象のカードを選択する（ランダム）
    /// BattleManagerの買うアクション処理から移設
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

    // 攻撃カードの選び方：PrimaryAttack を優先、無ければ使える中から先頭
    public CardData SelectAttackCard(List<CardData> enemyHand)
    {
        foreach (var c in enemyHand)
            if (CardRules.IsUsableInAttackPhase(c) && (c.isPrimaryAttack || c.cardType == CardType.Attack))
                return c;

        foreach (var c in enemyHand)
            if (CardRules.IsUsableInAttackPhase(c))
                return c;

        return null;
    }

    // 防御カードの選び方：PrimaryDefense を優先、無ければ使える中から先頭
    public CardData SelectDefenseCard(List<CardData> enemyHand)
    {
        foreach (var c in enemyHand)
            if (CardRules.IsUsableInDefensePhase(c) && (c.isPrimaryDefense || c.cardType == CardType.Defense))
                return c;

        foreach (var c in enemyHand)
            if (CardRules.IsUsableInDefensePhase(c))
                return c;

        return null;
    }

    /// <summary>
    /// 敵の攻撃ターンを実行する
    /// BattleManagerのRunEnemyTurnAsyncから移設
    /// </summary>
    public async Task<CardData> ExecuteAttackTurnAsync(
        List<CardData> cpuHand,
        BattleProcessor battleProcessor,
        HandRefillService handRefill)
    {
        // 相手の攻撃フェーズ開始時の効果音
        SoundEffectPlayer.I?.Play("Assets/SE/鳩時計1.mp3");
        Debug.Log("[EnemyAI] 相手の攻撃フェーズ開始");
        
        // 鳩時計効果音後のインターバル
        await Task.Delay(500);
        Debug.Log("[EnemyAI] 鳩時計効果音後、0.5秒待機");

        // 攻撃カードを選択
        var attack = SelectAttackCard(cpuHand);
        if (attack == null)
        {
            Debug.Log("[EnemyAI] 攻撃カードが見つからないため、ターン終了");
            return null;
        }

        // カードを使用
        battleProcessor.UseCard(attack, cpuHand);
        handRefill?.RecordEnemyUse(attack);

        Debug.Log($"[EnemyAI] 攻撃カード選択: {attack.cardName}");

        return attack;
    }

    /// <summary>
    /// 敵の防御選択を実行する
    /// BattleManagerのRunDefenseSelectAsyncから移設
    /// </summary>
    public async Task<CardData> ExecuteDefenseSelectAsync(List<CardData> cpuHand)
    {
        Debug.Log("[EnemyAI] 防御カード選択開始");

        // 防御カードを選択
        var defenseCard = SelectDefenseCard(cpuHand);

        if (defenseCard != null)
        {
            Debug.Log($"[EnemyAI] 防御カード選択完了: {defenseCard.cardName}");
        }
        else
        {
            Debug.Log("[EnemyAI] 防御カードが見つからないため、許す");
        }

        // 防御カード選択後の0.5秒インターバル
        await Task.Delay(500);
        Debug.Log("[EnemyAI] 防御カード選択完了、0.5秒待機");

        return defenseCard;
    }

    /// <summary>
    /// 防御カードを使用する（裏向きにする処理）
    /// BattleManagerのRunDefenseConfirmAsyncから移設
    /// </summary>
    public void UseDefenseCard(
        CardData defenseCard,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        List<CardData> cpuHand)
    {
        if (defenseCard == null) return;

        // HandRefillServiceに使用を記録
        if (handRefill != null)
        {
            handRefill.RecordEnemyUse(defenseCard);
            Debug.Log($"[EnemyAI] 防御カード使用記録: {defenseCard.cardName}");
        }

        // カードを使用（裏向きにする）
        battleProcessor.UseCard(defenseCard, cpuHand);
        Debug.Log($"[EnemyAI] 防御カード使用: {defenseCard.cardName}");
    }
}
