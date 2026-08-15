using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Host surface for <see cref="BattleBootstrap"/> (serialized refs and mutable battle state).
/// </summary>
public interface IBattleBootstrapHost
{
    BattleManager Manager { get; }
    bool IsOnlineMatch { get; }

    List<CardData> PlayerHand { get; }
    List<CardData> CpuHand { get; }

    PlayerStatus PlayerStatus { get; }
    PlayerStatus EnemyStatus { get; }
    void SetPlayerStatus(PlayerStatus value);
    void SetEnemyStatus(PlayerStatus value);

    EnemyAI EnemyAI { get; set; }

    Transform HandPanel { get; }
    GameObject CardUiPrefab { get; }
    Sprite CardBackSprite { get; }
    CardDealer CardDealer { get; }
    BattleProcessor BattleProcessor { get; }
    BattleStatusUI StatusUI { get; }
    HandRefillService HandRefill { get; }
    CardSequenceManager CardSequenceManager { get; }
    CardStatsDisplay CardStatsDisplay { get; }
    MagicPoolManager MagicPoolManager { get; }
    CardPurchaseAnimation CardPurchaseAnimation { get; }
    CardSellAnimation CardSellAnimation { get; }
    ExchangeFeature ExchangeFeature { get; }
    SummonSkillButton SummonSkillButton { get; }
    SummonSkillButton EnemySummonSkillButton { get; }

    StatusProgressionConfig StatusProgressionConfig { get; }
    DiseaseTurnEndSettings DiseaseTurnEndSettings { get; }
    ShivaDirectAttackFreezeSettings ShivaDirectAttackFreezeSettings { get; }
    OrdinSlashReflectSettings OrdinSlashReflectSettings { get; }

    BuyFeature BuyFeature { get; }
    SellFeature SellFeature { get; }

    void RefreshEnemyMagicPoolSnapshot();
    void EnsureBattleBgmController();
    void BeginOpeningSequence();

    BattleBgmController BattleBgmController { get; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    BattleDebugTools BattleDebugTools { get; }
    void SubscribeOnlineDebugInject();
    void UnsubscribeOnlineDebugInject();
#endif
}
