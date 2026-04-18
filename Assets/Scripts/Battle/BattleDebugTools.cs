// BattleDebugTools.cs
using System.Threading;
using UnityEngine;

/// <summary>インスペクターで先攻・後攻をデバッグ指定するときのモード。</summary>
public enum OpeningTurnOwnerDebugMode
{
    /// <summary>毎回ランダム（50% / 50%）。</summary>
    Random = 0,
    /// <summary>プレイヤーが必ず先攻。</summary>
    PlayerFirst = 1,
    /// <summary>敵が必ず先攻。</summary>
    EnemyFirst = 2,
}

/// <summary>
/// バトル用デバッグ。コンポーネント右クリックのコンテキストメニュー、およびインスペクターで有効化した OnGUI パネル（Editor のみ）から実行する。
/// </summary>
public class BattleDebugTools : MonoBehaviour
{
    [Header("バトルコンポーネント参照")]
    public BattleManager battleManager;

    [Header("初期召喚獣（デバッグ・バトル開始時）")]
    [Tooltip("オン時、SummonSelectionManager／既定より優先してプレイヤーに割り当てます。")]
    [SerializeField] private bool overridePlayerInitialSummon;
    [SerializeField] private SummonData debugPlayerInitialSummon;
    [Tooltip("オン時、敵のランダム召喚より優先します。")]
    [SerializeField] private bool overrideEnemyInitialSummon;
    [SerializeField] private SummonData debugEnemyInitialSummon;

    [Header("先攻・後攻（開幕・Intro 終了時）")]
    [Tooltip("Random: ランダム。PlayerFirst / EnemyFirst: 常にその側が先攻。")]
    [SerializeField] private OpeningTurnOwnerDebugMode openingTurnOwnerMode = OpeningTurnOwnerDebugMode.Random;

    [Header("GameState デバッグ（再生中・右上）")]
    [Tooltip("現在の GameState と手番をリアルタイム表示。Editor / Development ビルドのみ。")]
    [SerializeField] private bool showGameStateDebugBox = true;
    [SerializeField] private float gameStateDebugBoxWidth = 380f;
    [SerializeField] private float gameStateDebugBoxHeight = 96f;
    [Tooltip("ラベルのフォントサイズ（既定 16）")]
    [SerializeField] [Range(10, 36)] private int gameStateDebugFontSize = 16;

    [Header("状態異常デバッグ（再生中・左上）")]
    [Tooltip("オンにすると15種の付与ボタンを表示。Factory未実装の4種はプレースホルダーで付与。")]
    [SerializeField] private bool showAilmentDebugPanel = true;

    /// <summary>左上デバッグパネル領域。高さは15種リスト＋「状態異常13（介入）」ブロックを収める。</summary>
    [SerializeField] private Rect ailmentDebugPanelRect = new Rect(8, 8, 300, 540);

    private Vector2 _ailmentScroll;
    private GUIStyle _gameStateDebugLabelStyle;

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (!Application.isPlaying || battleManager == null)
            return;

        if (showGameStateDebugBox)
        {
            float gw = Mathf.Max(120f, gameStateDebugBoxWidth);
            float gh = Mathf.Max(48f, gameStateDebugBoxHeight);
            float gx = Screen.width - gw - 8f;
            float gy = 8f;

            if (_gameStateDebugLabelStyle == null)
            {
                _gameStateDebugLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = gameStateDebugFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true,
                };
                _gameStateDebugLabelStyle.normal.textColor = Color.white;
            }
            else
                _gameStateDebugLabelStyle.fontSize = gameStateDebugFontSize;

            GUILayout.BeginArea(new Rect(gx, gy, gw, gh), GUI.skin.box);
            GUILayout.Label($"GameState: {battleManager.CurrentState}", _gameStateDebugLabelStyle);
            GUILayout.Label($"TurnOwner: {battleManager.CurrentTurnOwner}", _gameStateDebugLabelStyle);
            GUILayout.EndArea();
        }

#if UNITY_EDITOR
        if (!showAilmentDebugPanel)
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
        GUILayout.Label("召喚（デバッグ）", GUI.skin.box);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("プレイヤー→ガルーダ"))
        {
            DebugSetPlayerSummonGaruda();
        }
        if (GUILayout.Button("敵→ガルーダ"))
        {
            DebugSetEnemySummonGaruda();
        }
        GUILayout.EndHorizontal();

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
#endif
    }
#endif

    /// <summary>
    /// <see cref="BattleManager.Start"/> で通常の召喚割当の直後に呼ぶ。Editor / Development ビルドのみ有効。
    /// </summary>
    public void ApplyInitialSummonOverrides(PlayerStatus player, PlayerStatus enemy)
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif
        if (player != null && overridePlayerInitialSummon && debugPlayerInitialSummon != null)
            player.summonData = debugPlayerInitialSummon;
        if (enemy != null && overrideEnemyInitialSummon && debugEnemyInitialSummon != null)
            enemy.summonData = debugEnemyInitialSummon;
    }

    /// <summary>
    /// <see cref="BattleManager"/> の開幕先攻決定で使用。Random はその場で 50/50。
    /// </summary>
    public PlayerType ResolveOpeningTurnOwner()
    {
        switch (openingTurnOwnerMode)
        {
            case OpeningTurnOwnerDebugMode.PlayerFirst:
                return PlayerType.Player;
            case OpeningTurnOwnerDebugMode.EnemyFirst:
                return PlayerType.Enemy;
            default:
                return UnityEngine.Random.Range(0, 2) == 0 ? PlayerType.Player : PlayerType.Enemy;
        }
    }

    [ContextMenu("デバッグ：プレイヤー召喚をガルーダに切替")]
    public void DebugSetPlayerSummonGaruda()
    {
        if (!EnsurePlaying()) return;
        var g = Resources.Load<SummonData>("Summons/Garuda");
        if (g == null)
        {
            Debug.LogWarning("[BattleDebugTools] Resources/Summons/Garuda が見つかりません。");
            return;
        }
        battleManager.GetPlayerStatus().summonData = g;
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] プレイヤー召喚をガルーダに設定しました。");
    }

    [ContextMenu("デバッグ：敵召喚をガルーダに切替")]
    public void DebugSetEnemySummonGaruda()
    {
        if (!EnsurePlaying()) return;
        var g = Resources.Load<SummonData>("Summons/Garuda");
        if (g == null)
        {
            Debug.LogWarning("[BattleDebugTools] Resources/Summons/Garuda が見つかりません。");
            return;
        }
        battleManager.GetEnemyStatus().summonData = g;
        RefreshStatusUi();
        Debug.Log("[BattleDebugTools] 敵召喚をガルーダに設定しました。");
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
