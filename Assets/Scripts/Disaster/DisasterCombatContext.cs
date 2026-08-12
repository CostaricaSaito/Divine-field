using System.Collections.Generic;



/// <summary>

/// 天変地異に伴う通常攻撃解決中フラグ（煙幕無効など）。

/// 防御側 TOTAL 表示で攻撃シーケンスが上書きされても、発動側 ATK を維持する。

/// </summary>

public static class DisasterCombatContext

{

    public static bool IsActive { get; private set; }



    private static List<CardData> _strikeAttackCards;

    private static Side _cardDisplaySide = Side.Player;

    private static Side _attackerSide = Side.Player;



    public static void Begin() => IsActive = true;



    public static void End()

    {

        IsActive = false;

        _strikeAttackCards = null;

        _cardDisplaySide = Side.Player;

        _attackerSide = Side.Player;

    }



    public static void SetCurrentStrike(

        IReadOnlyList<CardData> attackCards,

        Side cardDisplaySide,

        Side attackerSide)

    {

        _strikeAttackCards = null;

        if (attackCards != null && attackCards.Count > 0)

        {

            _strikeAttackCards = new List<CardData>(attackCards.Count);

            foreach (var c in attackCards)

            {

                if (c != null)

                    _strikeAttackCards.Add(c);

            }

        }



        _cardDisplaySide = cardDisplaySide;

        _attackerSide = attackerSide;

    }



    /// <summary>

    /// 現在の天変地異攻撃段を、指定パネル（プレイヤー下 / CPU上）向けに取得する。

    /// </summary>

    public static bool TryGetAttackerStrikeForPanel(

        bool forPlayerPanel,

        out List<CardData> cards,

        out PlayerStatus attacker)

    {

        cards = null;

        attacker = null;

        if (!IsActive || _strikeAttackCards == null || _strikeAttackCards.Count == 0)

            return false;



        bool displayOnPlayer = _cardDisplaySide == Side.Player;

        if (forPlayerPanel != displayOnPlayer)

            return false;



        var bm = BattleManager.I;

        if (bm == null) return false;



        attacker = _attackerSide == Side.Player ? bm.GetPlayerStatus() : bm.GetEnemyStatus();

        if (attacker == null) return false;



        cards = _strikeAttackCards;

        return true;

    }

}

