// BattleDebugTools.cs
using UnityEngine;

#if UNITY_EDITOR
/// <summary>
/// バトル用デバッグ。コンポーネント右クリックのコンテキストメニューから実行する。
/// </summary>
public class BattleDebugTools : MonoBehaviour
{
    [Header("バトルコンポーネント参照")]
    public BattleManager battleManager;

    [ContextMenu("デバッグ：プレイヤーHPを10に設定")]
    public void SetPlayerHPTo10()
    {
        if (!Application.isPlaying || battleManager == null)
        {
            Debug.LogWarning("[BattleDebugTools] 再生中かつ battleManager 設定が必要です。");
            return;
        }

        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();

        player.currentHP = 10;
        RefreshStatusUi();

        Debug.Log("[BattleDebugTools] デバッグ：プレイヤーHPを10に設定しました");
    }

    /// <summary>衰弱のアイコン・効果テスト用。</summary>
    [ContextMenu("テスト：プレイヤーに衰弱を付与")]
    public void TestApplyWeakenToPlayer()
    {
        if (!EnsurePlaying()) return;
        battleManager.GetPlayerStatus().AddStatusEffect(StatusEffectType.Weaken);
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] テスト：プレイヤーに衰弱を付与しました");
    }

    [ContextMenu("テスト：敵に衰弱を付与")]
    public void TestApplyWeakenToEnemy()
    {
        if (!EnsurePlaying()) return;
        battleManager.GetEnemyStatus().AddStatusEffect(StatusEffectType.Weaken);
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] テスト：敵に衰弱を付与しました");
    }

    [ContextMenu("テスト：プレイヤーに病を付与（ターン終了で病系処理）")]
    public void TestApplySicknessToPlayer()
    {
        if (!EnsurePlaying()) return;
        ApplyDiseaseStage(battleManager.GetPlayerStatus(), StatusEffectType.Sickness);
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] プレイヤーに「病」を付与。攻撃フェーズ終了後の TurnEnd で病系処理が走ります。");
    }

    [ContextMenu("テスト：プレイヤーに重病を付与")]
    public void TestApplySevereSicknessToPlayer()
    {
        if (!EnsurePlaying()) return;
        ApplyDiseaseStage(battleManager.GetPlayerStatus(), StatusEffectType.SevereSickness);
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] プレイヤーに「重病」を付与。");
    }

    [ContextMenu("テスト：プレイヤーに煉獄病を付与")]
    public void TestApplyPurgatorySicknessToPlayer()
    {
        if (!EnsurePlaying()) return;
        ApplyDiseaseStage(battleManager.GetPlayerStatus(), StatusEffectType.PurgatorySickness);
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] プレイヤーに「煉獄病」を付与。");
    }

    [ContextMenu("テスト：プレイヤーに楽園病を付与")]
    public void TestApplyParadiseSicknessToPlayer()
    {
        if (!EnsurePlaying()) return;
        ApplyDiseaseStage(battleManager.GetPlayerStatus(), StatusEffectType.ParadiseSickness);
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] プレイヤーに「楽園病」を付与。");
    }

    private static void ApplyDiseaseStage(PlayerStatus status, StatusEffectType stage)
    {
        if (status == null) return;
        status.activeEffects.RemoveAll(e => e != null && DiseaseLineEffect.IsDiseaseFamily(e.EffectType));
        var created = StatusEffectFactory.Create(stage);
        if (created != null)
            status.activeEffects.Add(created);
    }

    private bool EnsurePlaying()
    {
        if (!Application.isPlaying || battleManager == null)
        {
            Debug.LogWarning("[BattleDebugTools] 再生中かつ battleManager 設定が必要です。");
            return false;
        }
        return true;
    }

    private void RefreshStatusUi()
    {
        if (BattleUIManager.I != null)
            BattleUIManager.I.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
        else if (battleManager.statusUI != null)
            battleManager.statusUI.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
    }
}
#endif
