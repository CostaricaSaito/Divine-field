// BattleDebugTools.cs
using System.Threading;
using UnityEngine;

#if UNITY_EDITOR
/// <summary>
/// バトル用デバッグ。コンポーネント右クリックのコンテキストメニュー、およびインスペクターで有効化した OnGUI パネルから実行する。
/// </summary>
public class BattleDebugTools : MonoBehaviour
{
    [Header("バトルコンポーネント参照")]
    public BattleManager battleManager;

    [Header("状態異常デバッグ（再生中・左上）")]
    [Tooltip("オンにすると15種の付与ボタンを表示。Factory未実装の4種はプレースホルダーで付与。")]
    [SerializeField] private bool showAilmentDebugPanel = true;

    /// <summary>左上デバッグパネル領域。高さは15種リスト＋「状態異常13（介入）」ブロックを収める。</summary>
    [SerializeField] private Rect ailmentDebugPanelRect = new Rect(8, 8, 300, 540);

    private Vector2 _ailmentScroll;

    [ContextMenu("デバッグ：プレイヤーHPを10に設定")]
    public void SetPlayerHPTo10()
    {
        if (!Application.isPlaying || battleManager == null)
        {
            Debug.LogWarning("[BattleDebugTools] 再生中かつ battleManager 設定が必要です。");
            return;
        }

        var player = battleManager.GetPlayerStatus();
        player.currentHP = 10;
        RefreshStatusUi();

        Debug.Log("[BattleDebugTools] デバッグ：プレイヤーHPを10に設定しました");
    }

    /// <summary>衰弱のアイコン・効果テスト用。</summary>
    [ContextMenu("テスト：プレイヤーに衰弱を付与")]
    public void TestApplyWeakenToPlayer()
    {
        if (!EnsurePlaying()) return;
        ApplyGrantForDebug(battleManager.GetPlayerStatus(), StatusEffectType.Weaken);
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] テスト：プレイヤーに衰弱を付与しました");
    }

    [ContextMenu("テスト：敵に衰弱を付与")]
    public void TestApplyWeakenToEnemy()
    {
        if (!EnsurePlaying()) return;
        ApplyGrantForDebug(battleManager.GetEnemyStatus(), StatusEffectType.Weaken);
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] テスト：敵に衰弱を付与しました");
    }

    [ContextMenu("テスト：プレイヤーに病を付与（ターン終了で病系処理）")]
    public void TestApplySicknessToPlayer()
    {
        if (!EnsurePlaying()) return;
        ApplyGrantForDebug(battleManager.GetPlayerStatus(), StatusEffectType.Sickness);
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] プレイヤーに「病」を付与。攻撃フェーズ終了後の TurnEnd で病系処理が走ります。");
    }

    [ContextMenu("テスト：プレイヤーに重病を付与")]
    public void TestApplySevereSicknessToPlayer()
    {
        if (!EnsurePlaying()) return;
        ApplyGrantForDebug(battleManager.GetPlayerStatus(), StatusEffectType.SevereSickness);
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] プレイヤーに「重病」を付与。");
    }

    [ContextMenu("テスト：プレイヤーに煉獄病を付与")]
    public void TestApplyPurgatorySicknessToPlayer()
    {
        if (!EnsurePlaying()) return;
        ApplyGrantForDebug(battleManager.GetPlayerStatus(), StatusEffectType.PurgatorySickness);
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] プレイヤーに「煉獄病」を付与。");
    }

    [ContextMenu("テスト：プレイヤーに楽園病を付与")]
    public void TestApplyParadiseSicknessToPlayer()
    {
        if (!EnsurePlaying()) return;
        ApplyGrantForDebug(battleManager.GetPlayerStatus(), StatusEffectType.ParadiseSickness);
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] プレイヤーに「楽園病」を付与。");
    }

    private void OnGUI()
    {
        if (!showAilmentDebugPanel || !Application.isPlaying || battleManager == null)
            return;

        GUILayout.BeginArea(ailmentDebugPanelRect, GUI.skin.box);
        GUILayout.Label("状態異常 付与（公式15種）");
        GUILayout.Label("→P=プレイヤー / →E=敵（未実装4種はプレースホルダー）", GUI.skin.box);

        // 上部2行ラベル＋下部「状態異常13（介入）」ブロック分を除いた残りをスクロールに割り当てる（はみ出しで介入が見えなくなるのを防ぐ）
        const float reservedForHeaderAndIntervention = 118f;
        _ailmentScroll = GUILayout.BeginScrollView(
            _ailmentScroll,
            GUILayout.Height(Mathf.Max(80f, ailmentDebugPanelRect.height - reservedForHeaderAndIntervention)));
        var all = StatusEffectCatalog.AllAilments;
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            string label = $"{i + 1:D2}. {StatusEffectCatalog.OfficialDisplayNames[i]}";
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("→P", GUILayout.Width(40)))
            {
                ApplyGrantForDebug(battleManager.GetPlayerStatus(), t);
                RefreshStatusUi();
            }
            if (GUILayout.Button("→E", GUILayout.Width(40)))
            {
                ApplyGrantForDebug(battleManager.GetEnemyStatus(), t);
                RefreshStatusUi();
            }
            GUILayout.Label(label);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        GUILayout.Space(6);
        GUILayout.Label("状態異常13（介入）", GUI.skin.box);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        InterventionTurnEndProcessor.DebugForceInterventionChance100 = GUILayout.Toggle(
            InterventionTurnEndProcessor.DebugForceInterventionChance100,
            "介入発生率100%（デバッグ）");
#else
        GUILayout.Label("介入デバッグは Editor / Development のみ");
#endif

        GUILayout.EndArea();
    }

    [ContextMenu("デバッグ：介入(13) 発生率100%をトグル")]
    public void DebugToggleInterventionChance100()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        InterventionTurnEndProcessor.DebugForceInterventionChance100 =
            !InterventionTurnEndProcessor.DebugForceInterventionChance100;
        Debug.Log($"[BattleDebugTools] 介入発生率100%: {InterventionTurnEndProcessor.DebugForceInterventionChance100}");
#else
        Debug.LogWarning("[BattleDebugTools] 介入デバッグは Editor / Development ビルドでのみ利用できます。");
#endif
    }

    /// <summary>
    /// <see cref="StatusEffectFactory"/> で実体が作れるものは <see cref="PlayerStatus.TryApplyStatusEffect"/>。
    /// 未実装の4種は <see cref="PlaceholderStatusEffect"/>。
    /// </summary>
    public static void ApplyGrantForDebug(PlayerStatus target, StatusEffectType type)
    {
        if (target == null || type == StatusEffectType.None) return;

        // Factory 未実装の4種は TryApply しない（警告ログを出さずプレースホルダーのみ）
        if (UsesDebugPlaceholderOnly(type))
        {
            if (!HasActiveEffect(target, type))
            {
                target.activeEffects.Add(new PlaceholderStatusEffect(type));
                Debug.Log($"[BattleDebugTools] {StatusEffectPresentation.GetDisplayName(type)} をプレースホルダーで付与（未実装）");
            }
            return;
        }

        var cfg = StatusProgressionConfig.GetRuntimeFallback();
        var result = target.TryApplyStatusEffect(type, cfg);
        if (result == ProgressiveApplyResult.ForcedParadiseEcstasy)
            _ = DiseaseTurnEndProcessor.ProcessForcedParadiseEcstasyAsync(target, CancellationToken.None);
    }

    private static bool HasActiveEffect(PlayerStatus status, StatusEffectType type)
    {
        foreach (var e in status.activeEffects)
        {
            if (e != null && e.EffectType == type)
                return true;
        }
        return false;
    }

    /// <summary><see cref="StatusEffectFactory"/> の default 枝に該当する列挙値（実装追加時にここから外す）。</summary>
    private static bool UsesDebugPlaceholderOnly(StatusEffectType type)
    {
        return type == StatusEffectType.Confusion
            || type == StatusEffectType.CurseBind;
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
