using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>防御側の1回分の応答（選んだ防御カード、またはオーディンの自動跳ね返し）。</summary>
public readonly struct ReactiveDefenseResponse
{
    public readonly List<CardData> Picks;
    public readonly bool AlreadyConsumed;
    public readonly bool AutoBounce;

    private ReactiveDefenseResponse(List<CardData> picks, bool alreadyConsumed, bool autoBounce)
    {
        Picks = picks ?? new List<CardData>();
        AlreadyConsumed = alreadyConsumed;
        AutoBounce = autoBounce;
    }

    /// <param name="alreadyConsumed">true: 手札消費・魔法プール処理が呼び出し側で済んでいる。</param>
    public static ReactiveDefenseResponse FromPicks(List<CardData> picks, bool alreadyConsumed)
        => new ReactiveDefenseResponse(picks != null ? new List<CardData>(picks) : null, alreadyConsumed, false);

    /// <summary>オーディン「切り払い」：カードを使わず跳ね返す。</summary>
    public static ReactiveDefenseResponse OrdinAutoBounce()
        => new ReactiveDefenseResponse(null, true, true);
}

/// <summary>
/// 反射・打ち払いの唯一の解決フロー（物理／魔法／完全反射、即時効果反射、打ち払い、オーディン自動反射、連鎖）。
///
/// 仕様:
/// ・攻撃カードは常に「現在の攻撃側（Holder）」のパネルにあり、TOTAL の ATK も Holder 側にだけ出る。
/// ・跳ね返し（反射成功／打ち払い成功／オーディン）で Holder が入れ替わり、カードと TOTAL が一緒に移動する。
/// ・打ち払い失敗では攻撃は動かず、同じ防御側が防御を選び直す（オーディン判定なし）。
/// ・攻撃力はチェーン開始時に元の攻撃者・防御者で1回だけ計算し、以後固定。
/// ・最後に跳ね返さなかった防御側が攻撃を受ける。一度も跳ね返っていなければ通常戦闘、跳ね返っていれば固定攻撃力で解決。
/// ・即時効果（状態異常魔法等）は元の使用者を user、最終防御側を target として効果を解決する。
/// </summary>
public static class ReactiveDefenseChain
{
    private const float SlideDurationSec = 0.5f;
    private const int PresentationDelayMs = 500;
    private const int ParryRollDelayMs = 1000;

    /// <summary>道連れの鎖など、最終戦闘の解決方法を差し替えるときに使う。</summary>
    public delegate Task CombatResolver(
        List<CardData> attackCards,
        List<CardData> defenseCards,
        PlayerStatus attacker,
        PlayerStatus defender,
        List<CardData> defenderHand,
        CancellationToken cancellationToken);

    /// <param name="attackerSide">攻撃カードを出した側（チェーン開始時の Holder）。</param>
    /// <param name="firstResponse">最初の防御側の応答。反射・打ち払い・オーディン自動反射のいずれか。</param>
    public static async Task RunAsync(
        BattleManager battleManager,
        List<CardData> attackCards,
        PlayerType attackerSide,
        ReactiveDefenseResponse firstResponse,
        CancellationToken cancellationToken,
        CombatResolver combatResolverOverride = null)
    {
        if (battleManager == null || battleManager.battleProcessor == null
            || attackCards == null || attackCards.Count == 0)
            return;

        var chain = new Chain(battleManager, attackCards, attackerSide, combatResolverOverride);
        chain.PublishTotal();
        try
        {
            await chain.RunAsync(firstResponse, cancellationToken);
        }
        finally
        {
            battleManager.ClearStatsDisplaySequenceCards();
            battleManager.ClearReflectionAttackTotalDisplay();
        }
    }

    private sealed class Chain
    {
        private readonly BattleManager _bm;
        private readonly BattleProcessor _processor;
        private readonly HandRefillService _handRefill;
        private readonly List<CardData> _attackCards;
        private readonly List<CardData> _slideCards;
        private readonly PlayerStatus _originalAttacker;
        private readonly int _attackPower;
        private readonly bool _isImmediate;
        private readonly bool _isMagic;
        private readonly CombatResolver _combatResolverOverride;

        private PlayerType _holder;
        private bool _bounced;

        public Chain(
            BattleManager bm,
            List<CardData> attackCards,
            PlayerType attackerSide,
            CombatResolver combatResolverOverride)
        {
            _bm = bm;
            _processor = bm.battleProcessor;
            _handRefill = bm.HandRefill;
            _attackCards = new List<CardData>(attackCards);
            _slideCards = FreeMagicCastRules.ResolveIncomingAttackCardsForReflectionSlide(bm, _attackCards);
            _holder = attackerSide;
            _originalAttacker = StatusOf(attackerSide);
            _attackPower = _processor.ComputeReflectionIncomingAttackPower(
                _attackCards, _originalAttacker, StatusOf(Opposite(attackerSide)));
            _isImmediate = ReflectionRules.ShouldUseImmediateEffectReflectionFlow(_attackCards);
            _isMagic = !_isImmediate
                && (CardRules.IsMagicClassifiedAttackCombo(_attackCards)
                    || GrandMagicRules.ContainsGrandMagicStyleAttack(_attackCards));
            _combatResolverOverride = combatResolverOverride;
        }

        private PlayerType Defender => Opposite(_holder);
        private bool DefenderIsPlayer => Defender == PlayerType.Player;
        private Side DefenderPanel => DefenderIsPlayer ? Side.Player : Side.Enemy;
        private PlayerStatus HolderStatus => StatusOf(_holder);
        private PlayerStatus DefenderStatus => StatusOf(Defender);
        private List<CardData> DefenderHand => DefenderIsPlayer ? _bm.playerHand : _bm.cpuHand;

        public void PublishTotal()
            => _bm.SetReflectionAttackTotalDisplay(_attackCards, _holder == PlayerType.Player, _attackPower);

        public async Task RunAsync(ReactiveDefenseResponse response, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                if (response.AutoBounce)
                {
                    await OrdinSlashReflectFlow.PlayAutoBouncePresentationAsync(DefenderStatus, ct);
                    await BounceAsync(ct);
                    response = await NextResponseAsync(allowOrdin: true, failedParryCard: null, ct);
                    continue;
                }

                var picks = FreeMagicCastRules.ExcludeCatalystsFromCombatStats(response.Picks);
                CardData primary = picks.Count > 0 ? picks[0] : null;

                switch (ReactiveDefenseRules.Classify(picks, _attackCards))
                {
                    case ReactiveDefenseKind.Reflect:
                        await ConsumeAsync(response, ct);
                        await WaitPopupAsync(
                            BattleUIManager.I != null
                                ? BattleUIManager.I.ShowReflectionBouncePopup(DefenderStatus, _isMagic)
                                : 0f,
                            ct);
                        RemoveDefenseSheet(primary);
                        await BounceAsync(ct);
                        response = await NextResponseAsync(allowOrdin: true, failedParryCard: null, ct);
                        continue;

                    case ReactiveDefenseKind.Parry:
                        await ConsumeAsync(response, ct);
                        bool parried = await RollParryAsync(ct);
                        RemoveDefenseSheet(primary);
                        if (parried)
                        {
                            await BounceAsync(ct);
                            response = await NextResponseAsync(allowOrdin: true, failedParryCard: null, ct);
                        }
                        else
                        {
                            response = await NextResponseAsync(allowOrdin: false, failedParryCard: primary, ct);
                        }
                        continue;

                    case ReactiveDefenseKind.Nullify:
                        await ConsumeAsync(response, ct);
                        await RunNullifyAsync(primary, ct);
                        return;

                    default:
                        await ConsumeAsync(response, ct);
                        await ResolveFinalAsync(picks, ct);
                        return;
                }
            }
        }

        /// <summary>攻撃カードを防御側パネルへ移し、防御側を新しい Holder にする。TOTAL も同時に移す。</summary>
        private async Task BounceAsync(CancellationToken ct)
        {
            bool towardPlayer = DefenderIsPlayer;
            if (BattleUIManager.I != null)
                await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                    _slideCards, towardPlayer, SlideDurationSec, ct);
            SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);

            _holder = Defender;
            _bounced = true;
            _bm.ClearStatsDisplaySequenceCards();
            PublishTotal();
        }

        /// <returns>true: 跳ね返し成功。false: 失敗（攻撃は防御側に戻る）。</returns>
        private async Task<bool> RollParryAsync(CancellationToken ct)
        {
            var defender = DefenderStatus;
            await WaitPopupAsync(
                BattleUIManager.I != null ? BattleUIManager.I.ShowParryIntroPopup(defender) : 0f, ct);
            await Task.Delay(ParryRollDelayMs, ct);

            bool failed = BattleRandom.Range(0, 2) == 0;
            if (!failed) return true;

            await WaitPopupAsync(
                BattleUIManager.I != null ? BattleUIManager.I.ShowParryReturnToSelfPopup(defender) : 0f, ct);
            return false;
        }

        private async Task<ReactiveDefenseResponse> NextResponseAsync(
            bool allowOrdin,
            CardData failedParryCard,
            CancellationToken ct)
        {
            _bm.ClearStatsDisplaySequenceCards();

            if (allowOrdin && OrdinSlashReflectFlow.TryRollChainAutoBounce(_bm, DefenderStatus, HolderStatus, _attackCards))
                return ReactiveDefenseResponse.OrdinAutoBounce();

            List<CardData> picks;
            if (DefenderIsPlayer)
            {
                picks = await _bm.WaitForReflectionChainDefenseAsync(_attackCards, ct);
            }
            else
            {
                var ai = _bm.GetEnemyAI();
                if (ai == null)
                {
                    picks = new List<CardData>();
                }
                else if (failedParryCard != null)
                {
                    CardData pick = await ai.ExecuteParryRerunDefenseSelectAsync(
                        _bm.cpuHand, ElementHelper.GetIncomingAttackElement(_attackCards), _attackCards, failedParryCard);
                    picks = EnemyDefenseSelectionHelper.GetParryRerunDefensePicks(pick);
                }
                else
                {
                    CardData pick = await ai.ExecuteDefenseSelectAsync(
                        _bm.cpuHand, ElementHelper.GetIncomingAttackElement(_attackCards), _attackCards);
                    picks = EnemyDefenseSelectionHelper.GetDefensePicks(ai, pick);
                }
            }

            await PresentPicksAsync(picks, ct);
            return ReactiveDefenseResponse.FromPicks(picks, alreadyConsumed: false);
        }

        private async Task PresentPicksAsync(List<CardData> picks, CancellationToken ct)
        {
            if (picks == null || picks.Count == 0) return;

            var ui = BattleUIManager.I;
            if (DefenderIsPlayer && _bm.Sequences != null)
            {
                await _bm.Sequences.PlayReflectionChainDefenseRevealAsync(picks, ct);
                return;
            }

            if (ui != null)
            {
                if (DefenderIsPlayer)
                {
                    foreach (var c in picks)
                        ui.DestroyCardSheetsForCardDataOnPanel(c, Side.Player);
                    ui.ShowCardSheetsVisualOnlyBatch(picks, Side.Player);
                }
                else if (picks.Count == 1)
                {
                    ui.ShowEnemyDefenseCardPresentation(picks[0]);
                }
                else
                {
                    await ui.ShowEnemyDefenseCardsPresentationSequenceAsync(picks);
                }
            }

            _bm.SetStatsDisplaySequenceCards(picks, "防御", DefenderPanel);
            SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
            await Task.Delay(PresentationDelayMs, ct);
        }

        private async Task RunNullifyAsync(CardData card, CancellationToken ct)
        {
            await WaitPopupAsync(
                BattleUIManager.I != null ? BattleUIManager.I.ShowBlockingNullifyPopup(DefenderStatus) : 0f, ct);
            RemoveDefenseSheet(card);
            BattleUIManager.I?.UpdateStatus(_bm.GetPlayerStatus(), _bm.GetEnemyStatus());
        }

        private async Task ResolveFinalAsync(List<CardData> picks, CancellationToken ct)
        {
            var attacker = HolderStatus;
            var defender = DefenderStatus;
            var defenseCards = picks != null && picks.Count > 0 ? picks : null;

            bool showEnemyYurusu = !DefenderIsPlayer && defenseCards == null && BattleUIManager.I != null;
            using (YurusuDisplayScope.ShowIf(showEnemyYurusu))
            {
                if (_isImmediate)
                {
                    await Task.Delay(DamagePopup.PreImmediateEffectDelayMs, ct);
                    foreach (var card in _attackCards)
                    {
                        if (card == null) continue;
                        ct.ThrowIfCancellationRequested();
                        await _processor.ResolveImmediateEffectAsync(card, _originalAttacker, defender, ct);
                    }
                }
                else if (_combatResolverOverride != null)
                {
                    await _combatResolverOverride(_attackCards, defenseCards, attacker, defender, DefenderHand, ct);
                }
                else if (_bounced)
                {
                    await _processor.ResolveReflectedCombatAsync(
                        _attackCards, _attackPower, defenseCards, attacker, defender, DefenderHand, skipHitCheck: true);
                }
                else
                {
                    await _processor.ResolveCombatAsync(
                        _attackCards, defenseCards, attacker, defender, DefenderHand, skipHitCheck: true);
                }
            }
        }

        /// <summary>防御カードを使用済みにする（同じカードを2回消費しない）。</summary>
        private async Task ConsumeAsync(ReactiveDefenseResponse response, CancellationToken ct)
        {
            if (response.AlreadyConsumed || response.Picks.Count == 0) return;

            if (!DefenderIsPlayer)
            {
                var fresh = new List<CardData>();
                foreach (var c in response.Picks)
                {
                    if (c != null && !IsConsumed(c, PlayerType.Enemy)) fresh.Add(c);
                }
                EnemyDefenseSelectionHelper.ConsumeEnemyDefenseCards(fresh, _bm, _processor, _handRefill);
                return;
            }

            foreach (var c in response.Picks)
            {
                if (c == null) continue;
                if (c.cardType == CardType.Magic)
                {
                    if (_bm.Sequences != null)
                        await _bm.Sequences.ApplyMagicCardToPoolForReflectionOrParryDefenseAsync(c, ct);
                    continue;
                }
                if (IsConsumed(c, PlayerType.Player)) continue;

                int slot = c.cardUI != null ? c.cardUI.transform.GetSiblingIndex() : -1;
                if (slot >= 0) _handRefill?.RecordPlayerUseSlot(slot);
                _processor.UseCard(c, _bm.playerHand);
            }
        }

        private bool IsConsumed(CardData card, PlayerType owner)
        {
            if (card.cardUI != null && card.cardUI.IsFaceDown()) return true;
            if (_handRefill == null) return false;
            return owner == PlayerType.Player
                ? _handRefill.IsPlayerCardUsedThisTurn(card)
                : _handRefill.IsEnemyCardUsedThisTurn(card);
        }

        private void RemoveDefenseSheet(CardData card)
        {
            if (card != null)
                BattleUIManager.I?.DestroyCardSheetsForCardDataOnPanel(card, DefenderPanel);
        }

        private PlayerStatus StatusOf(PlayerType side)
            => side == PlayerType.Player ? _bm.GetPlayerStatus() : _bm.GetEnemyStatus();

        private static PlayerType Opposite(PlayerType side)
            => side == PlayerType.Player ? PlayerType.Enemy : PlayerType.Player;

        private static async Task WaitPopupAsync(float popupSec, CancellationToken ct)
        {
            if (popupSec <= 0f) popupSec = DamagePopup.DefaultFadeDurationIfUnknown;
            await DamagePopup.WaitAfterPopupLifetimeAsync(popupSec, ct);
        }
    }
}
