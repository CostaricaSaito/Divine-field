#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// CardData の旧行動分類 bool（isPrimaryDefense 等）を CardType / PhaseUseRule / usableIn* へ移行する。
/// </summary>
public static class CardDataPhaseUseMigrator
{
    [MenuItem("DivineField/Migrate Card Phase Use From Legacy Action Flags")]
    public static void MigrateFromLegacyActionFlags()
    {
        int changed = 0;
        foreach (var card in CardDataRarityMigrator.LoadAllCardDataAssetsInCardsFolder())
        {
            if (card == null) continue;

            bool dirty = false;
            var so = new SerializedObject(card);
            var legacyPrimary = so.FindProperty("_legacyIsPrimaryDefense");
            var legacyCounter = so.FindProperty("_legacyIsCounterAttack");
            var legacyRecovery = so.FindProperty("_legacyIsRecovery");
            var legacySpecial = so.FindProperty("_legacyIsSpecialEffect");

            if (legacyPrimary != null && legacyPrimary.boolValue)
            {
                if (!card.usableInDefensePhase)
                {
                    card.usableInDefensePhase = true;
                    dirty = true;
                }
                if (card.defensePhaseUseRule == DefensePhaseUseRule.None)
                {
                    card.defensePhaseUseRule = DefensePhaseUseRule.Primary;
                    dirty = true;
                }
            }

            if (legacyCounter != null && legacyCounter.boolValue)
            {
                if (!card.usableInAttackPhase)
                {
                    card.usableInAttackPhase = true;
                    dirty = true;
                }
                if (!card.usableInDefensePhase)
                {
                    card.usableInDefensePhase = true;
                    dirty = true;
                }
                if (card.defensePhaseUseRule == DefensePhaseUseRule.None)
                {
                    card.defensePhaseUseRule = DefensePhaseUseRule.Standalone;
                    dirty = true;
                }
            }

            if (legacyRecovery != null && legacyRecovery.boolValue)
            {
                if (card.cardType == CardType.Attack)
                {
                    card.cardType = CardType.Recovery;
                    dirty = true;
                }
                if (!card.usableInAttackPhase)
                {
                    card.usableInAttackPhase = true;
                    dirty = true;
                }
            }

            if (legacySpecial != null && legacySpecial.boolValue)
            {
                if (card.cardType == CardType.Attack)
                {
                    card.cardType = CardType.Special;
                    dirty = true;
                }
            }

            if (dirty)
            {
                EditorUtility.SetDirty(card);
                changed++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CardDataPhaseUseMigrator] Updated {changed} CardData assets from legacy action flags.");
    }
}
#endif
