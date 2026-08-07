using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Online battle: replaces <see cref="EnemyAI"/> decisions with the real
/// opponent's selections received over <see cref="NetworkBattleBridge"/>.
///
/// Both machines run the full battle simulation; this agent resolves the
/// received card names against the local mirror of the opponent hand
/// (kept identical through the shared draw streams of <see cref="BattleRandom"/>)
/// and performs the same bookkeeping the opponent's own machine performed
/// (MP cost, magic pool, used-card records) so the two simulations stay in sync.
/// </summary>
public class RemotePlayerAgent : EnemyAI
{
    /// <summary>Full defense selection of the last ExecuteDefenseSelectAsync (may be multi-card).</summary>
    public List<CardData> LastDefenseSelection { get; private set; }

    /// <summary>
    /// TOTAL tap toggle state of the remote attacker for the last attack:
    /// attack aimed at the attacker itself / recovery aimed at the opponent.
    /// </summary>
    public bool LastAttackTargetSelf { get; private set; }

    public override SummonData SelectRandomEnemySummon()
    {
        var list = SummonSelectionManager.I?.GetAllSummonData();
        if (list == null || list.Length == 0) return null;
        int idx = Mathf.Clamp(OnlineMatchContext.RemoteSummonIndex, 0, list.Length - 1);
        return list[idx];
    }

    public override async Task<CardData> ExecuteAttackTurnAsync(
        List<CardData> cpuHand,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        PlayerStatus enemyStatus)
    {
        SoundEffectPlayer.I?.Play("Assets/SE/鳩時計1.mp3");
        Debug.Log("[RemotePlayerAgent] Waiting for remote attack selection...");
        LastAttackSelection = null;
        LastAttackTargetSelf = false;

        NetworkBattleBridge.RemoteAttack remoteAttack;
        try
        {
            remoteAttack = await NetworkBattleBridge.WaitForRemoteAttackAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        await Task.Delay(400);

        if (remoteAttack.EconomicKind == NetworkBattleBridge.RemoteEconomicKind.Exchange)
        {
            Debug.Log("[RemotePlayerAgent] Remote exchange action");
            var bm = BattleManager.I;
            if (bm != null && bm.ExchangeFeatureInternal != null)
            {
                bm.ExchangeFeatureInternal.MirrorRemoteExchange(
                    bm.GetEnemyStatus(),
                    remoteAttack.ExchangeAfterHp,
                    remoteAttack.ExchangeAfterMp,
                    remoteAttack.ExchangeAfterGp);
            }
            return null;
        }

        var names = remoteAttack.CardNames;
        if (names == null || names.Count == 0)
        {
            Debug.Log("[RemotePlayerAgent] Remote passed the turn");
            return null;
        }

        if (remoteAttack.EconomicKind == NetworkBattleBridge.RemoteEconomicKind.Buy)
        {
            var bm = BattleManager.I;
            var target = EconomicActionNames.FindFirstByName(bm?.playerHand, remoteAttack.EconomicCardName);
            if (target == null || bm?.BuyFeatureInternal == null || !bm.BuyFeatureInternal.SetupMirroredBuy(target))
            {
                Debug.LogError("[RemotePlayerAgent] Could not mirror remote economic buy");
                return null;
            }

            var dummy = EconomicActionNames.CreateBuyDummy();
            LastAttackSelection = new List<CardData> { dummy };
            Debug.Log($"[RemotePlayerAgent] Remote economic buy: {remoteAttack.EconomicCardName}");
            return dummy;
        }

        if (remoteAttack.EconomicKind == NetworkBattleBridge.RemoteEconomicKind.Sell)
        {
            var bm = BattleManager.I;
            var sold = EconomicActionNames.FindFirstByName(cpuHand, remoteAttack.EconomicCardName);
            if (sold == null || bm?.SellFeatureInternal == null || !bm.SellFeatureInternal.SetupMirroredSell(sold))
            {
                Debug.LogError("[RemotePlayerAgent] Could not mirror remote economic sell");
                return null;
            }

            var dummy = EconomicActionNames.CreateSellDummy();
            LastAttackSelection = new List<CardData> { dummy };
            Debug.Log($"[RemotePlayerAgent] Remote economic sell: {remoteAttack.EconomicCardName}");
            return dummy;
        }

        var resolved = ResolveCards(names, cpuHand, out var poolSourced);
        if (resolved.Count == 0)
        {
            Debug.LogError("[RemotePlayerAgent] Could not resolve any remote attack card. Possible desync.");
            return null;
        }

        LastAttackSelection = resolved;
        LastAttackTargetSelf = remoteAttack.TargetSelf;
        if (remoteAttack.TargetSelf)
            Debug.Log("[RemotePlayerAgent] Remote attack targets the attacker itself (TOTAL toggle)");

        // Magical Sword: the attacker always sends its optional-MP choice when the
        // combo contains the card, so mirror the MP payment and the power bonus here.
        if (MagicalSwordRules.ContainsMagicalSword(resolved))
            await MirrorMagicalSwordChoiceAsync(enemyStatus);

        if (TributeBloodRules.ContainsTributeBlood(resolved))
            await MirrorTributeBloodChoiceAsync();

        CardData primaryNormal = null;
        bool deferBookkeeping = ShouldDeferRemoteAttackBookkeeping(resolved);
        if (deferBookkeeping)
        {
            if (resolved.Count > 1)
                BattleManager.I?.SetOnlineEnemyAttackCombo(resolved);
            foreach (var card in resolved)
            {
                if (card == null || card.cardType == CardType.Magic) continue;
                if (primaryNormal == null) primaryNormal = card;
            }
        }
        else
        {
            foreach (var card in resolved)
            {
                if (card != null && card.cardType == CardType.Magic)
                    ApplyRemoteMagicBookkeeping(card, poolSourced.Contains(card), cpuHand, battleProcessor, handRefill, enemyStatus);
            }

            foreach (var card in resolved)
            {
                if (card == null || card.cardType == CardType.Magic) continue;
                if (primaryNormal == null) primaryNormal = card;
                handRefill?.RecordEnemyUse(card);
                battleProcessor.UseCard(card, cpuHand);
            }
        }

        var primary = primaryNormal != null ? primaryNormal : resolved[0];
        Debug.Log($"[RemotePlayerAgent] Remote attack: {primary.cardName} (+{resolved.Count - 1} more)");
        return primary;
    }

    public override async Task<CardData> ExecuteDefenseSelectAsync(
        List<CardData> cpuHand,
        ElementType attackElement,
        List<CardData> incomingForReflection = null)
    {
        Debug.Log("[RemotePlayerAgent] Waiting for remote defense selection...");
        LastDefenseSelection = null;

        List<string> names;
        try
        {
            names = await NetworkBattleBridge.WaitForRemoteDefenseAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        await Task.Delay(400);

        if (names == null || names.Count == 0)
        {
            Debug.Log("[RemotePlayerAgent] Remote allows the attack (no defense)");
            return null;
        }

        var resolved = ResolveCards(names, cpuHand, out var poolSourced);
        if (resolved.Count == 0)
        {
            Debug.LogError("[RemotePlayerAgent] Could not resolve any remote defense card. Possible desync.");
            return null;
        }

        LastDefenseSelection = resolved;

        if (incomingForReflection != null && incomingForReflection.Count > 0)
        {
            foreach (var card in resolved)
            {
                if (card == null || !ReflectionRules.IsReflectionCard(card)) continue;
                if (!ReflectionRules.CanReflectIncoming(card, incomingForReflection))
                {
                    Debug.LogWarning(
                        $"[RemotePlayerAgent] Remote reflection defense '{card.cardName}' "
                        + "does not match incoming attack on this machine. Possible desync.");
                }
            }
        }

        // Magic defense cards go through the magic pool on the opponent machine
        // (except during intervention resolution, where they are used plainly).
        if (!InterventionTurnEndProcessor.IsResolving)
        {
            var bm = BattleManager.I;
            foreach (var card in resolved)
            {
                if (card != null && card.cardType == CardType.Magic)
                    ApplyRemoteMagicBookkeeping(
                        card, poolSourced.Contains(card), cpuHand,
                        bm != null ? bm.battleProcessor : null,
                        bm != null ? bm.HandRefill : null,
                        bm != null ? bm.GetEnemyStatus() : null);
            }
        }

        Debug.Log($"[RemotePlayerAgent] Remote defense: {resolved[0].cardName} ({resolved.Count} cards)");
        return resolved[0];
    }

    /// <summary>
    /// Mirror the attacker's Magical Sword optional-MP choice: deduct the MP the
    /// attacker paid and register the power bonus so the local damage calculation
    /// matches the attacker's machine.
    /// </summary>
    async Task MirrorMagicalSwordChoiceAsync(PlayerStatus enemyStatus)
    {
        NetworkBattleBridge.MagicalSwordChoice choice;
        try
        {
            var waitTask = NetworkBattleBridge.WaitForMagicalSwordChoiceAsync(CancellationToken.None);
            var finished = await Task.WhenAny(waitTask, Task.Delay(30000));
            if (finished != waitTask)
            {
                Debug.LogError("[RemotePlayerAgent] MagicalSword choice timed out; assuming no boost");
                return;
            }
            choice = await waitTask;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!choice.Paid || choice.PowerBonus <= 0) return;

        if (enemyStatus != null && choice.MpCost > 0)
        {
            enemyStatus.UseMP(choice.MpCost);
            BattleUIManager.I?.UpdateStatus(BattleManager.I?.GetPlayerStatus(), enemyStatus);
        }

        BattleManager.I?.SetMagicalSwordEnemyAttackPowerBonus(choice.PowerBonus);
        Debug.Log($"[RemotePlayerAgent] Mirrored MagicalSword boost (+{choice.PowerBonus} ATK, -{choice.MpCost} MP)");
    }

    async Task MirrorTributeBloodChoiceAsync()
    {
        NetworkBattleBridge.TributeBloodChoice choice;
        try
        {
            var waitTask = NetworkBattleBridge.WaitForTributeBloodChoiceAsync(CancellationToken.None);
            var finished = await Task.WhenAny(waitTask, Task.Delay(30000));
            if (finished != waitTask)
            {
                Debug.LogError("[RemotePlayerAgent] TributeBlood choice timed out; assuming 0 HP paid");
                BattleManager.I?.SetTributeBloodEnemyHpPaidSnapshot(0);
                return;
            }
            choice = await waitTask;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        BattleManager.I?.SetTributeBloodEnemyHpPaidSnapshot(Mathf.Max(0, choice.HpPaid));
        Debug.Log($"[RemotePlayerAgent] Mirrored TributeBlood HP paid ({choice.HpPaid})");
    }

    /// <summary>
    /// Mirror of the opponent's ApplyMagicCardToPoolAsync:
    /// MP cost -> (hand magic: record + flip + pool register) / (pool magic: consume + bonus draw).
    /// </summary>
    void ApplyRemoteMagicBookkeeping(
        CardData card,
        bool fromPool,
        List<CardData> cpuHand,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        PlayerStatus enemyStatus)
    {
        if (card == null) return;

        if (enemyStatus != null && card.mpCost > 0)
        {
            int pay = enemyStatus.GetEffectiveMagicMpCost(card.mpCost);
            enemyStatus.UseMP(pay);
            BattleUIManager.I?.UpdateStatus(BattleManager.I?.GetPlayerStatus(), enemyStatus);
        }

        if (fromPool)
        {
            MagicPoolManager.I?.ConsumeUse(card, PlayerType.Enemy);
            // The opponent draws one face-down bonus card when casting from the panel.
            DrawMirroredEnemyCard(cpuHand);
        }
        else
        {
            handRefill?.RecordEnemyUse(card);
            battleProcessor?.UseCard(card, cpuHand);
            MagicPoolManager.I?.TryUseMagicCard(
                card, cpuHand, BattleManager.MaxHandCards,
                () => DrawMirroredEnemyCard(cpuHand), PlayerType.Enemy);
        }
    }

    static void DrawMirroredEnemyCard(List<CardData> cpuHand)
    {
        var dealer = BattleManager.I != null ? BattleManager.I.cardDealer : null;
        if (dealer == null || cpuHand == null) return;
        var card = dealer.DrawRandomCard(PlayerType.Enemy);
        if (card != null)
            cpuHand.Add(card);
    }

    /// <summary>
    /// Resolve received card names against the local opponent-hand mirror,
    /// falling back to the opponent's magic pool. Duplicates are matched
    /// one instance at a time.
    /// </summary>
    static List<CardData> ResolveCards(List<string> names, List<CardData> hand, out HashSet<CardData> poolSourced)
    {
        var result = new List<CardData>();
        poolSourced = new HashSet<CardData>();
        var taken = new HashSet<CardData>();

        List<CardData> pooled = MagicPoolManager.I != null
            ? MagicPoolManager.I.GetPooledCardDatas(PlayerType.Enemy)
            : null;

        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name)) continue;

            CardData found = null;
            if (hand != null)
            {
                foreach (var c in hand)
                {
                    if (c != null && c.cardName == name && !taken.Contains(c))
                    {
                        found = c;
                        break;
                    }
                }
            }

            bool isFromPool = false;
            if (found == null && pooled != null)
            {
                foreach (var p in pooled)
                {
                    if (p != null && p.cardName == name && !taken.Contains(p))
                    {
                        found = p;
                        isFromPool = true;
                        break;
                    }
                }
            }

            if (found == null)
            {
                Debug.LogError($"[RemotePlayerAgent] Remote card '{name}' not found in mirrored hand/pool");
                continue;
            }

            taken.Add(found);
            if (isFromPool) poolSourced.Add(found);
            result.Add(found);
        }

        return result;
    }

    public static bool ShouldDeferRemoteAttackBookkeeping(List<CardData> resolved)
    {
        if (resolved == null || resolved.Count == 0) return false;
        if (resolved.Count > 1) return true;
        if (resolved.Count == 1 && ArchMagicRules.IsArchMagicCard(resolved[0])) return true;
        if (MagicalExplosionRules.ContainsMagicalExplosion(resolved)) return true;
        if (MillionDollarBazookaRules.ContainsMillionDollarBazooka(resolved)) return true;
        if (TributeBloodRules.ContainsTributeBlood(resolved)) return true;
        if (HammadnessRules.ContainsHammadness(resolved)) return true;
        if (GodrageRules.IsGodrageDoublingCombo(resolved)) return true;
        return false;
    }
}
