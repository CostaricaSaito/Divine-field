// BattleDebugTools.cs
using System;
using System.Collections.Generic;
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

/// <summary>IMGUI デバッグパネルの初期配置。再生中はウィンドウをドラッグして移動できる。</summary>
public enum BattleDebugPanelPlacement
{
    /// <summary>positionX/Y を画面左上からの座標として使う。</summary>
    Custom = 0,
    /// <summary>幅・高さに基づき画面中央。</summary>
    Center = 1,
    /// <summary>右上基準。positionX=右端からの余白、positionY=上端からの余白。</summary>
    TopRight = 2,
}

/// <summary>各デバッグウィンドウのサイズ・位置・フォント（Inspector で変更可能）。</summary>
[Serializable]
public class BattleDebugPanelLayout
{
    public BattleDebugPanelPlacement placement = BattleDebugPanelPlacement.Center;
    [Min(50f)] public float width = 720f;
    [Min(50f)] public float height = 700f;
    [Tooltip("Custom: 左上 X。TopRight: 右端からの余白。")]
    public float positionX = 8f;
    [Tooltip("Custom: 左上 Y。TopRight: 上端からの余白。")]
    public float positionY = 8f;
    [Range(8, 48)] public int fontSize = 18;
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

    [Header("GameState デバッグ（Editor / Development・再生中）")]
    [Tooltip("Layer1 Turn / Layer2 Phase / Layer3 Step をリアルタイム表示。")]
    [SerializeField] private bool showGameStateDebugBox = true;
    [SerializeField] private BattleDebugPanelLayout gameStatePanelLayout = new BattleDebugPanelLayout
    {
        placement = BattleDebugPanelPlacement.TopRight,
        width = 480f,
        height = 300f,
        positionX = 8f,
        positionY = 8f,
        fontSize = 22,
    };

    [Header("状態異常デバッグ（Editor / Development・再生中）")]
    [Tooltip("オンにすると15種の付与ボタンを表示。Factory未実装の4種はプレースホルダーで付与。")]
    [SerializeField] private bool showAilmentDebugPanel = true;
    [SerializeField] private BattleDebugPanelLayout ailmentPanelLayout = new BattleDebugPanelLayout
    {
        placement = BattleDebugPanelPlacement.Center,
        width = 780f,
        height = 1000f,
        positionX = 0f,
        positionY = 0f,
        fontSize = 22,
    };

    [Header("天変地異デバッグ（Editor / Development・再生中）")]
    [Tooltip("オンにすると発生する天変地異の種別を固定できます。")]
    [SerializeField] private bool showDisasterDebugPanel = true;
    [SerializeField] private BattleDebugPanelLayout disasterPanelLayout = new BattleDebugPanelLayout
    {
        placement = BattleDebugPanelPlacement.Center,
        width = 780f,
        height = 520f,
        positionX = 0f,
        positionY = 0f,
        fontSize = 22,
    };

    [Header("手札チート（Editor / Development・再生中）")]
    [Tooltip("オン時、Resources/Cards から読み込んだ一覧でプレイヤー手札に追加できます。")]
    [SerializeField] private bool showCardCheatPanel = true;
    [SerializeField] private BattleDebugPanelLayout cardCheatPanelLayout = new BattleDebugPanelLayout
    {
        placement = BattleDebugPanelPlacement.Center,
        width = 780f,
        height = 1000f,
        positionX = 0f,
        positionY = 0f,
        fontSize = 22,
    };

    [Header("敵手札リアルタイム（Editor / Development・再生中）")]
    [Tooltip("cpuHand の一覧を表示。使用・消費の確認用。")]
    [SerializeField] private bool showEnemyHandDebugPanel = false;
    [SerializeField] private BattleDebugPanelLayout enemyHandPanelLayout = new BattleDebugPanelLayout
    {
        placement = BattleDebugPanelPlacement.Custom,
        width = 780f,
        height = 520f,
        positionX = 8f,
        positionY = 200f,
        fontSize = 22,
    };

    [Tooltip("Inspector から1枚指定してコンテキストメニューで即追加")]
    [SerializeField] private CardData cheatCardTemplateForContextMenu;

    private Vector2 _ailmentScroll;
    private Vector2 _disasterScroll;
    private Vector2 _cardCheatScroll;
    private Vector2 _enemyHandScroll;
    private string _cardCheatFilter = "";
    private static CardData[] _cachedAllCardsFromResources;
    private GUIStyle _debugPanelWindowStyle;
    private GUIStyle _debugPanelHeaderStyle;
    private GUIStyle _debugPanelLineStyle;
    private GUIStyle _debugPanelButtonStyle;
    private GUIStyle _debugPanelRowLabelStyle;
    private GUIStyle _debugPanelTextFieldStyle;
    private GUIStyle _debugPanelMinimizeButtonStyle;
    private int _debugPanelStyleFontCached = -1;

    private struct DebugPanelWindowState
    {
        public bool Minimized;
        public float ExpandedHeight;
    }

    private readonly Dictionary<int, DebugPanelWindowState> _debugPanelWindowStates = new();

    private const int WindowIdGameState = 21001;
    private const int WindowIdAilment = 21002;
    private const int WindowIdCardCheat = 21003;
    private const int WindowIdEnemyHand = 21004;
    private const int WindowIdDisaster = 21005;
    private static float DebugPanelDragTitleHeight(int fontSize)
        => Mathf.Max(28f, fontSize + 12f);

    private Rect _gameStateWindowRect;
    private Rect _ailmentWindowRect;
    private Rect _disasterWindowRect;
    private Rect _cardCheatWindowRect;
    private Rect _enemyHandWindowRect;
    private bool _debugPanelRectsInitialized;
    private bool _showCardCheatPanelCached;
    private bool _showAilmentDebugPanelCached;
    private bool _showDisasterDebugPanelCached;
    private bool _showCardCheatPanelCacheInitialized;
    private bool _showAilmentDebugPanelCacheInitialized;
    private bool _showDisasterDebugPanelCacheInitialized;

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

    [ContextMenu("デバッグ：プレイヤーを HP10 / MP0 / GP0 に設定")]
    public void SetPlayerHp10Mp0Gp0()
    {
        if (!Application.isPlaying || battleManager == null)
        {
            Debug.LogWarning("[BattleDebugTools] 再生中かつ battleManager 設定が必要です。");
            return;
        }

        var player = battleManager.GetPlayerStatus();
        player.currentHP = Mathf.Clamp(10, 0, player.maxHP);
        player.currentMP = Mathf.Clamp(0, 0, player.maxMP);
        player.currentGP = Mathf.Clamp(0, 0, player.maxGP);
        RefreshStatusUi();

        Debug.Log("[BattleDebugTools] デバッグ：プレイヤーを HP10 / MP0 / GP0 に設定しました（合計10・劣勢境界）");
    }

    [ContextMenu("デバッグ：ターンカウントを49に設定")]
    public void DebugSetTurnCountTo49()
    {
        DebugSetBattleTurnDisplay(49);
    }

    /// <summary>UI の「ターンN」表示をデバッグで上書きする。</summary>
    public void DebugSetBattleTurnDisplay(int targetDisplay)
    {
        if (!EnsurePlaying()) return;

        var counters = battleManager.SummonTurnCounters;
        counters.DebugSetCurrentBattleTurnDisplay(targetDisplay);
        BattleUIManager.I?.RefreshTurnCountDisplay(counters, battleManager.CurrentTurnOwner);
        battleManager.RefreshSummonSkillButtonInteractables();

        Debug.Log(
            $"[BattleDebugTools] ターンカウントを {targetDisplay} に設定しました（次ターン終了で {targetDisplay + 1}）");
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
        Debug.Log("[BattleDebugTools] プレイヤーに「病」を付与。攻撃フェーズ終了後の EndPhase で病系処理が走ります。");
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
    private void OnEnable()
    {
        _debugPanelRectsInitialized = false;
        _showCardCheatPanelCacheInitialized = false;
        _showAilmentDebugPanelCacheInitialized = false;
        _showDisasterDebugPanelCacheInitialized = false;
        _debugPanelWindowStates.Clear();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        HandleDebugPanelToggle(ref showCardCheatPanel, ref _showCardCheatPanelCached,
            ref _showCardCheatPanelCacheInitialized, cardCheatPanelLayout, ref _cardCheatWindowRect);
        HandleDebugPanelToggle(ref showAilmentDebugPanel, ref _showAilmentDebugPanelCached,
            ref _showAilmentDebugPanelCacheInitialized, ailmentPanelLayout, ref _ailmentWindowRect);
        HandleDebugPanelToggle(ref showDisasterDebugPanel, ref _showDisasterDebugPanelCached,
            ref _showDisasterDebugPanelCacheInitialized, disasterPanelLayout, ref _disasterWindowRect);
    }
#endif

    private void OnGUI()
    {
        if (!Application.isPlaying || battleManager == null)
            return;

        HandleDebugPanelToggle(ref showCardCheatPanel, ref _showCardCheatPanelCached,
            ref _showCardCheatPanelCacheInitialized, cardCheatPanelLayout, ref _cardCheatWindowRect);
        HandleDebugPanelToggle(ref showAilmentDebugPanel, ref _showAilmentDebugPanelCached,
            ref _showAilmentDebugPanelCacheInitialized, ailmentPanelLayout, ref _ailmentWindowRect);
        HandleDebugPanelToggle(ref showDisasterDebugPanel, ref _showDisasterDebugPanelCached,
            ref _showDisasterDebugPanelCacheInitialized, disasterPanelLayout, ref _disasterWindowRect);
        EnsureDebugPanelRects();

        if (showAilmentDebugPanel)
        {
            DrawStandardDebugWindow(
                WindowIdAilment,
                ref _ailmentWindowRect,
                ailmentPanelLayout,
                "状態異常デバッグ",
                DrawAilmentDebugWindow);
        }

        if (showDisasterDebugPanel)
        {
            DrawStandardDebugWindow(
                WindowIdDisaster,
                ref _disasterWindowRect,
                disasterPanelLayout,
                "天変地異デバッグ",
                DrawDisasterDebugWindow);
        }

        if (showCardCheatPanel)
        {
            DrawStandardDebugWindow(
                WindowIdCardCheat,
                ref _cardCheatWindowRect,
                cardCheatPanelLayout,
                battleManager.IsOnlineMatch ? "手札チート（オンライン）" : "手札チート（CPU）",
                DrawCardCheatDebugWindow);
        }

        if (showEnemyHandDebugPanel)
        {
            DrawStandardDebugWindow(
                WindowIdEnemyHand,
                ref _enemyHandWindowRect,
                enemyHandPanelLayout,
                "敵手札（CPU・リアルタイム）",
                DrawEnemyHandDebugWindow);
        }

        if (showGameStateDebugBox)
        {
            DrawStandardDebugWindow(
                WindowIdGameState,
                ref _gameStateWindowRect,
                gameStatePanelLayout,
                "GameState",
                DrawGameStateDebugWindow);
        }
    }

    private void EnsureDebugPanelRects()
    {
        if (_debugPanelRectsInitialized)
            return;

        _gameStateWindowRect = ComputeInitialRect(gameStatePanelLayout);
        _ailmentWindowRect = ComputeInitialRect(ailmentPanelLayout);
        _disasterWindowRect = ComputeInitialRect(disasterPanelLayout);
        _cardCheatWindowRect = ComputeInitialRect(cardCheatPanelLayout);
        _enemyHandWindowRect = ComputeInitialRect(enemyHandPanelLayout);
        _debugPanelRectsInitialized = true;
    }

    private void HandleDebugPanelToggle(
        ref bool showPanel,
        ref bool cachedShowPanel,
        ref bool cacheInitialized,
        BattleDebugPanelLayout layout,
        ref Rect windowRect)
    {
        if (!cacheInitialized)
        {
            cachedShowPanel = showPanel;
            cacheInitialized = true;
            return;
        }

        if (showPanel == cachedShowPanel)
            return;

        windowRect = ComputeInitialRect(layout);
        cachedShowPanel = showPanel;
    }

    private void DrawStandardDebugWindow(
        int windowId,
        ref Rect windowRect,
        BattleDebugPanelLayout layout,
        string title,
        Action<int> drawer)
    {
        int fs = Mathf.Clamp(layout.fontSize, 12, 48);
        EnsureDebugPanelGuiStyles(fs);
        float w = Mathf.Max(50f, layout.width);
        float layoutH = Mathf.Max(50f, layout.height);
        float titleBarH = GetDebugPanelNativeTitleBarHeight(fs);

        if (!_debugPanelWindowStates.TryGetValue(windowId, out var state))
            state = new DebugPanelWindowState { ExpandedHeight = layoutH };

        if (state.ExpandedHeight < layoutH * 0.5f)
            state.ExpandedHeight = layoutH;

        float btnSize = Mathf.Clamp(titleBarH - 4f, 18f, 30f);
        float minimizedTotalH = titleBarH + btnSize + 6f;
        float expandedMinH = titleBarH + 8f;

        if (!state.Minimized && windowRect.height > minimizedTotalH + 4f)
            state.ExpandedHeight = windowRect.height;

        float targetH = state.Minimized
            ? minimizedTotalH
            : Mathf.Max(expandedMinH, state.ExpandedHeight);
        windowRect.width = w;
        windowRect.height = targetH;
        _debugPanelWindowStates[windowId] = state;

        windowRect = GUI.Window(windowId, windowRect, id =>
        {
            DrawDebugPanelMinimizeButton(
                w,
                btnSize,
                windowId,
                layoutH,
                expandedMinH);

            bool isMinimized = _debugPanelWindowStates.TryGetValue(windowId, out var liveState)
                && liveState.Minimized;
            if (!isMinimized)
            {
                GUILayout.Space(btnSize + 4f);
                drawer(id);
            }

            GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, w - btnSize - 10f), isMinimized ? btnSize + 4f : 24f));
        }, title, _debugPanelWindowStyle);

        if (_debugPanelWindowStates.TryGetValue(windowId, out state))
        {
            windowRect.width = w;
            windowRect.height = state.Minimized
                ? minimizedTotalH
                : Mathf.Max(expandedMinH, state.ExpandedHeight);
            _debugPanelWindowStates[windowId] = state;
        }
    }

    private float GetDebugPanelNativeTitleBarHeight(int fontSize)
    {
        EnsureDebugPanelGuiStyles(Mathf.Clamp(fontSize, 12, 48));
        return Mathf.Max(24f, _debugPanelWindowStyle.lineHeight + _debugPanelWindowStyle.padding.vertical + 4f);
    }

    private void DrawDebugPanelMinimizeButton(
        float windowWidth,
        float btnSize,
        int windowId,
        float layoutDefaultHeight,
        float expandedMinH)
    {
        if (!_debugPanelWindowStates.TryGetValue(windowId, out var state))
            state = new DebugPanelWindowState { ExpandedHeight = layoutDefaultHeight };

        Rect btnRect = new Rect(windowWidth - btnSize - 6f, 2f, btnSize, btnSize);
        string label = state.Minimized ? "\u25a1" : "\u2212";
        if (GUI.Button(btnRect, label, _debugPanelMinimizeButtonStyle))
        {
            if (state.Minimized)
            {
                state.Minimized = false;
                if (state.ExpandedHeight < expandedMinH + 4f)
                    state.ExpandedHeight = layoutDefaultHeight;
            }
            else
            {
                state.Minimized = true;
            }

            _debugPanelWindowStates[windowId] = state;
        }
    }

    private static Rect ComputeInitialRect(BattleDebugPanelLayout layout)
    {
        float w = Mathf.Max(50f, layout.width);
        float h = Mathf.Max(50f, layout.height);
        switch (layout.placement)
        {
            case BattleDebugPanelPlacement.Center:
                return new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            case BattleDebugPanelPlacement.TopRight:
                return new Rect(Screen.width - w - layout.positionX, layout.positionY, w, h);
            case BattleDebugPanelPlacement.Custom:
            default:
                return new Rect(layout.positionX, layout.positionY, w, h);
        }
    }

    private void DrawGameStateDebugWindow(int windowId)
    {
        int fs = Mathf.Clamp(gameStatePanelLayout.fontSize, 12, 48);
        float dragTitleH = DebugPanelDragTitleHeight(fs + 6);

        GUILayout.Label("GameState", _debugPanelHeaderStyle);
        var turnCounters = battleManager.SummonTurnCounters;
        int turnDisplay = turnCounters != null ? turnCounters.CurrentBattleTurnDisplay : 0;
        GUILayout.Label($"TurnCount: ターン{turnDisplay}", _debugPanelLineStyle);
        GUILayout.Label($"Turn:  {battleManager.GetBattleTurnDebugLabel()}", _debugPanelLineStyle);
        GUILayout.Label($"Phase: {battleManager.CurrentState}", _debugPanelLineStyle);
        BattleStep step = battleManager.CurrentBattleStep;
        GUILayout.Label($"Step:  {step}", _debugPanelLineStyle);
        GUILayout.Label($"      {BattleStepPresentation.GetDebugLabel(step)}", _debugPanelLineStyle);

        GUILayout.Space(4f);
        if (GUILayout.Button("ターンカウントを49に設定", _debugPanelButtonStyle))
            DebugSetTurnCountTo49();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, dragTitleH));
    }

    private void DrawAilmentDebugWindow(int windowId)
    {
        int fs = Mathf.Clamp(ailmentPanelLayout.fontSize, 12, 48);
        float rowH = Mathf.Max(40f, fs * 1.85f);
        float btnW = Mathf.Max(56f, fs * 2.4f);
        float dragTitleH = DebugPanelDragTitleHeight(fs + 6);

        GUILayout.Label("状態異常デバッグ", _debugPanelHeaderStyle);
        GUILayout.Label("→P=プレイヤー / →E=敵（未実装4種はプレースホルダー）", _debugPanelLineStyle);

        float scrollH = GetDebugPanelScrollViewHeight(ailmentPanelLayout, fs, 150f);
        DrawAilmentScrollList(scrollH, rowH, btnW);

        GUILayout.Space(6f);
        GUILayout.Label("召喚（デバッグ）", _debugPanelLineStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("プレイヤー→ガルーダ", _debugPanelButtonStyle))
            DebugSetPlayerSummonGaruda();
        if (GUILayout.Button("敵→ガルーダ", _debugPanelButtonStyle))
            DebugSetEnemySummonGaruda();
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("状態異常13（介入）", _debugPanelLineStyle);
        InterventionTurnEndProcessor.DebugForceInterventionChance100 = GUILayout.Toggle(
            InterventionTurnEndProcessor.DebugForceInterventionChance100,
            "介入発生率100%（デバッグ）");

        GUI.DragWindow(new Rect(0f, 0f, 10000f, dragTitleH));
    }

    private void DrawAilmentScrollList(float scrollH, float rowH, float btnW)
    {
        var all = StatusEffectCatalog.AllAilments;
        Rect scrollRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(scrollH), GUILayout.ExpandWidth(true));
        float contentW = Mathf.Max(1f, scrollRect.width - 18f);
        float contentH = Mathf.Max(scrollRect.height, all.Length * rowH);
        Rect viewRect = new Rect(0f, 0f, contentW, contentH);
        HandleDebugPanelScrollWheel(ref _ailmentScroll, scrollRect, contentH, scrollRect.height);

        _ailmentScroll = GUI.BeginScrollView(scrollRect, _ailmentScroll, viewRect, false, true);
        float y = 0f;
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            string label = $"{i + 1:D2}. {StatusEffectCatalog.OfficialDisplayNames[i]}";
            float labelW = contentW - (btnW * 2f) - 10f;
            if (GUI.Button(new Rect(0f, y, btnW, rowH), "→P", _debugPanelButtonStyle))
            {
                ApplyGrantForDebug(battleManager.GetPlayerStatus(), t);
                RefreshStatusUi();
            }
            if (GUI.Button(new Rect(btnW + 4f, y, btnW, rowH), "→E", _debugPanelButtonStyle))
            {
                ApplyGrantForDebug(battleManager.GetEnemyStatus(), t);
                RefreshStatusUi();
            }
            GUI.Label(new Rect(btnW * 2f + 8f, y, labelW, rowH), label, _debugPanelRowLabelStyle);
            y += rowH;
        }
        GUI.EndScrollView();
    }

    private void DrawDisasterDebugWindow(int windowId)
    {
        int fs = Mathf.Clamp(disasterPanelLayout.fontSize, 12, 48);
        float rowH = Mathf.Max(40f, fs * 1.85f);
        float btnW = Mathf.Max(80f, fs * 3.2f);
        float dragTitleH = DebugPanelDragTitleHeight(fs + 6);

        GUILayout.Label("天変地異デバッグ", _debugPanelHeaderStyle);
        GUILayout.Label("混沌のスイッチ等で発生する種別を固定します。", _debugPanelLineStyle);
        DisasterCatalog.DebugPinDisasterKind = GUILayout.Toggle(
            DisasterCatalog.DebugPinDisasterKind,
            "発生種別を固定");

        string currentLabel = DisasterCatalog.DebugPinDisasterKind
            ? $"固定中: {DisasterCatalog.GetDisplayName(DisasterCatalog.DebugPinnedDisasterKind)}"
            : "現在: ランダム抽選";
        GUILayout.Label(currentLabel, _debugPanelLineStyle);

        float scrollH = GetDebugPanelScrollViewHeight(disasterPanelLayout, fs, 120f);
        DrawDisasterScrollList(scrollH, rowH, btnW);

        GUI.DragWindow(new Rect(0f, 0f, 10000f, dragTitleH));
    }

    private void DrawDisasterScrollList(float scrollH, float rowH, float btnW)
    {
        var kinds = DisasterCatalog.AllKinds;
        Rect scrollRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(scrollH), GUILayout.ExpandWidth(true));
        float contentW = Mathf.Max(1f, scrollRect.width - 18f);
        float contentH = Mathf.Max(scrollRect.height, kinds.Count * rowH);
        Rect viewRect = new Rect(0f, 0f, contentW, contentH);
        HandleDebugPanelScrollWheel(ref _disasterScroll, scrollRect, contentH, scrollRect.height);

        _disasterScroll = GUI.BeginScrollView(scrollRect, _disasterScroll, viewRect, false, true);
        float y = 0f;
        for (int i = 0; i < kinds.Count; i++)
        {
            var kind = kinds[i];
            bool selected = DisasterCatalog.DebugPinDisasterKind
                && DisasterCatalog.DebugPinnedDisasterKind == kind;
            string label = selected
                ? $"★ {DisasterCatalog.GetDisplayName(kind)}"
                : DisasterCatalog.GetDisplayName(kind);
            float labelW = contentW - btnW - 6f;
            GUI.Label(new Rect(0f, y, labelW, rowH), label, _debugPanelRowLabelStyle);
            if (GUI.Button(new Rect(labelW + 4f, y, btnW, rowH), "固定", _debugPanelButtonStyle))
            {
                DisasterCatalog.DebugPinDisasterKind = true;
                DisasterCatalog.DebugPinnedDisasterKind = kind;
            }
            y += rowH;
        }
        GUI.EndScrollView();
    }

    private void DrawCardCheatDebugWindow(int windowId)
    {
        int fs = Mathf.Clamp(cardCheatPanelLayout.fontSize, 12, 48);
        float rowH = Mathf.Max(40f, fs * 1.85f);
        float btnW = Mathf.Max(80f, fs * 3.2f);
        float dragTitleH = DebugPanelDragTitleHeight(fs + 6);

        GUILayout.Label("手札チート", _debugPanelHeaderStyle);
        if (battleManager.IsOnlineMatch)
        {
            GUILayout.Label("オンライン：両端末へ同期注入（Desync 防止）", _debugPanelLineStyle);
            string role = OnlineMatchContext.IsHost ? "ホスト" : "クライアント";
            GUILayout.Label($"あなたの役割: {role}", _debugPanelLineStyle);
            GUILayout.Label($"自分 {battleManager.playerHand?.Count ?? 0} / {BattleManager.MaxHandCards}", _debugPanelLineStyle);
        }
        else
        {
            GUILayout.Label("CPU 対戦：ローカル手札に直接追加", _debugPanelLineStyle);
            GUILayout.Label(
                $"プレイヤー {battleManager.playerHand?.Count ?? 0} / CPU {battleManager.cpuHand?.Count ?? 0} / 上限 {BattleManager.MaxHandCards}",
                _debugPanelLineStyle);
        }

        _cardCheatFilter = GUILayout.TextField(_cardCheatFilter ?? "", _debugPanelTextFieldStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label("フィルタ（カード名・asset名の部分一致）", _debugPanelLineStyle);

        var catalog = GetCheatCardCatalog();
        if (catalog == null || catalog.Length == 0)
        {
            GUILayout.Label("Resources/Cards に CardData がありません", _debugPanelLineStyle);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, dragTitleH));
            return;
        }

        float scrollH = GetCardCheatScrollViewHeight(battleManager.IsOnlineMatch, fs);
        Rect scrollRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(scrollH), GUILayout.ExpandWidth(true));
        string f = (_cardCheatFilter ?? "").Trim();

        int rowCount = 0;
        for (int i = 0; i < catalog.Length; i++)
        {
            CardData c = catalog[i];
            if (c == null) continue;
            if (f.Length > 0)
            {
                string disp = NameForCheatDisplay(c);
                string asset = c.name ?? "";
                if (disp.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0
                    && asset.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
            }
            rowCount++;
        }

        float contentW = Mathf.Max(1f, scrollRect.width - 18f);
        float contentH = Mathf.Max(scrollRect.height, rowCount * rowH);
        Rect viewRect = new Rect(0f, 0f, contentW, contentH);
        HandleDebugPanelScrollWheel(ref _cardCheatScroll, scrollRect, contentH, scrollRect.height);

        _cardCheatScroll = GUI.BeginScrollView(scrollRect, _cardCheatScroll, viewRect, false, true);

        float y = 0f;
        for (int i = 0; i < catalog.Length; i++)
        {
            CardData c = catalog[i];
            if (c == null) continue;
            if (f.Length > 0)
            {
                string disp = NameForCheatDisplay(c);
                string asset = c.name ?? "";
                if (disp.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0
                    && asset.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
            }

            float labelW = contentW - (btnW * 2f) - 10f;

            GUI.Label(new Rect(0f, y, labelW, rowH), NameForCheatDisplay(c), _debugPanelRowLabelStyle);
            if (battleManager.IsOnlineMatch)
            {
                if (GUI.Button(new Rect(labelW + 4f, y, btnW, rowH), "自分", _debugPanelButtonStyle))
                    TryAddCheatCardToPlayerHand(c);
                if (GUI.Button(new Rect(labelW + btnW + 8f, y, btnW, rowH), "相手", _debugPanelButtonStyle))
                    TryAddCheatCardToOpponentHandOnline(c);
            }
            else
            {
                if (GUI.Button(new Rect(labelW + 4f, y, btnW, rowH), "自分", _debugPanelButtonStyle))
                    TryAddCheatCardToPlayerHand(c);
                if (GUI.Button(new Rect(labelW + btnW + 8f, y, btnW, rowH), "CPU", _debugPanelButtonStyle))
                    TryAddCheatCardToCpuHand(c);
            }

            y += rowH;
        }

        GUI.EndScrollView();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, dragTitleH));
    }

    private float GetDebugPanelScrollViewHeight(BattleDebugPanelLayout layout, int fontSize, float headerReserve)
    {
        float titleBar = DebugPanelDragTitleHeight(fontSize + 6);
        return Mathf.Max(120f, layout.height - titleBar - headerReserve);
    }

    private float GetCardCheatScrollViewHeight(bool online, int fontSize)
        => GetDebugPanelScrollViewHeight(cardCheatPanelLayout, fontSize, online ? 230f : 220f);

    private static void HandleDebugPanelScrollWheel(ref Vector2 scrollPos, Rect guiRect, float contentHeight, float visibleHeight)
    {
        Event e = Event.current;
        if (e.type != EventType.ScrollWheel)
            return;

        Rect hitRect = GUIUtility.GUIToScreenRect(guiRect);
        if (!hitRect.Contains(e.mousePosition) && !guiRect.Contains(e.mousePosition))
            return;

        scrollPos.y += e.delta.y * 24f;
        float maxScroll = Mathf.Max(0f, contentHeight - visibleHeight);
        scrollPos.y = Mathf.Clamp(scrollPos.y, 0f, maxScroll);
        e.Use();
    }

    void EnsureDebugPanelGuiStyles(int fontSize)
    {
        if (_debugPanelLineStyle != null && _debugPanelStyleFontCached == fontSize)
            return;

        _debugPanelButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        _debugPanelRowLabelStyle = new GUIStyle(_debugPanelButtonStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(4, 4, 0, 0),
        };
        _debugPanelRowLabelStyle.normal.background = null;
        _debugPanelRowLabelStyle.hover.background = null;
        _debugPanelRowLabelStyle.active.background = null;
        _debugPanelRowLabelStyle.focused.background = null;
        _debugPanelRowLabelStyle.onNormal.background = null;

        _debugPanelLineStyle = new GUIStyle(_debugPanelRowLabelStyle)
        {
            wordWrap = false,
        };
        _debugPanelLineStyle.normal.textColor = Color.white;

        _debugPanelHeaderStyle = new GUIStyle(_debugPanelLineStyle)
        {
            fontSize = fontSize + 4,
            fontStyle = FontStyle.Bold,
        };

        _debugPanelTextFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            font = _debugPanelButtonStyle.font,
            fontSize = fontSize,
        };

        _debugPanelWindowStyle = new GUIStyle(GUI.skin.window)
        {
            font = _debugPanelButtonStyle.font,
            fontSize = fontSize + 6,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        _debugPanelMinimizeButtonStyle = new GUIStyle(GUI.skin.button)
        {
            font = _debugPanelButtonStyle.font,
            fontSize = Mathf.Max(10, fontSize - 2),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(0, 0, 0, 0),
        };

        _debugPanelStyleFontCached = fontSize;
    }

    private void DrawEnemyHandDebugWindow(int windowId)
    {
        int fs = Mathf.Clamp(enemyHandPanelLayout.fontSize, 12, 48);
        float rowH = Mathf.Max(40f, fs * 1.85f);
        float dragTitleH = DebugPanelDragTitleHeight(fs + 6);

        var cpu = battleManager.cpuHand;
        int count = cpu?.Count ?? 0;
        GUILayout.Label("敵手札（CPU・リアルタイム）", _debugPanelHeaderStyle);
        GUILayout.Label($"枚数 {count} / {BattleManager.MaxHandCards}（BattleManager.cpuHand）", _debugPanelLineStyle);
        GUILayout.Label("インデックス順。使用・消費後にリストから消えているか確認できます。", _debugPanelLineStyle);

        if (cpu == null || count == 0)
        {
            GUILayout.Label(count == 0 ? "手札が空です。" : "cpuHand が null です。", _debugPanelLineStyle);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, dragTitleH));
            return;
        }

        float scrollH = GetDebugPanelScrollViewHeight(enemyHandPanelLayout, fs, 96f);
        Rect scrollRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(scrollH), GUILayout.ExpandWidth(true));
        float contentW = Mathf.Max(1f, scrollRect.width - 18f);
        float contentH = Mathf.Max(scrollRect.height, count * rowH);
        Rect viewRect = new Rect(0f, 0f, contentW, contentH);
        HandleDebugPanelScrollWheel(ref _enemyHandScroll, scrollRect, contentH, scrollRect.height);

        _enemyHandScroll = GUI.BeginScrollView(scrollRect, _enemyHandScroll, viewRect, false, true);
        float y = 0f;
        for (int i = 0; i < cpu.Count; i++)
        {
            CardData c = cpu[i];
            string line = c == null
                ? $"[{i}] (null)"
                : $"[{i}] {NameForCheatDisplay(c)}  ({c.name})";
            GUI.Label(new Rect(0f, y, contentW, rowH), line, _debugPanelRowLabelStyle);
            y += rowH;
        }
        GUI.EndScrollView();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, dragTitleH));
    }

#if UNITY_EDITOR
    [ContextMenu("デバッグ：ウィンドウ位置・サイズを Inspector に書き戻し（再生中）")]
    private void WriteBackDebugWindowLayoutsToInspector()
    {
        if (!Application.isPlaying)
            return;

        gameStatePanelLayout.placement = BattleDebugPanelPlacement.Custom;
        gameStatePanelLayout.positionX = _gameStateWindowRect.x;
        gameStatePanelLayout.positionY = _gameStateWindowRect.y;
        gameStatePanelLayout.width = _gameStateWindowRect.width;
        gameStatePanelLayout.height = _gameStateWindowRect.height;

        ailmentPanelLayout.placement = BattleDebugPanelPlacement.Custom;
        ailmentPanelLayout.positionX = _ailmentWindowRect.x;
        ailmentPanelLayout.positionY = _ailmentWindowRect.y;
        ailmentPanelLayout.width = _ailmentWindowRect.width;
        ailmentPanelLayout.height = _ailmentWindowRect.height;

        disasterPanelLayout.placement = BattleDebugPanelPlacement.Custom;
        disasterPanelLayout.positionX = _disasterWindowRect.x;
        disasterPanelLayout.positionY = _disasterWindowRect.y;
        disasterPanelLayout.width = _disasterWindowRect.width;
        disasterPanelLayout.height = _disasterWindowRect.height;

        cardCheatPanelLayout.placement = BattleDebugPanelPlacement.Custom;
        cardCheatPanelLayout.positionX = _cardCheatWindowRect.x;
        cardCheatPanelLayout.positionY = _cardCheatWindowRect.y;
        cardCheatPanelLayout.width = _cardCheatWindowRect.width;
        cardCheatPanelLayout.height = _cardCheatWindowRect.height;

        enemyHandPanelLayout.placement = BattleDebugPanelPlacement.Custom;
        enemyHandPanelLayout.positionX = _enemyHandWindowRect.x;
        enemyHandPanelLayout.positionY = _enemyHandWindowRect.y;
        enemyHandPanelLayout.width = _enemyHandWindowRect.width;
        enemyHandPanelLayout.height = _enemyHandWindowRect.height;

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
#endif

    /// <summary>
    /// <see cref="BattleManager.Start"/> で通常の召喚割当の直後に呼ぶ。Editor / Development ビルドのみ有効。
    /// </summary>
    public void ApplyInitialSummonOverrides(PlayerStatus player, PlayerStatus enemy)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (player != null && overridePlayerInitialSummon && debugPlayerInitialSummon != null)
        {
            player.SetSummonData(debugPlayerInitialSummon);
            Debug.Log($"[BattleDebugTools] Player initial summon override: {debugPlayerInitialSummon.summonName}");
        }

        if (enemy != null && overrideEnemyInitialSummon && debugEnemyInitialSummon != null)
        {
            enemy.SetSummonData(debugEnemyInitialSummon);
            Debug.Log($"[BattleDebugTools] Enemy initial summon override: {debugEnemyInitialSummon.summonName}");
        }
#endif
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

    [ContextMenu("デバッグ：天変地異固定をトグル")]
    public void DebugTogglePinDisasterKind()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DisasterCatalog.DebugPinDisasterKind = !DisasterCatalog.DebugPinDisasterKind;
        Debug.Log(
            "[BattleDebugTools] 天変地異固定: " +
            $"{DisasterCatalog.DebugPinDisasterKind} → {DisasterCatalog.GetDisplayName(DisasterCatalog.DebugPinnedDisasterKind)}");
#else
        Debug.LogWarning("[BattleDebugTools] 天変地異デバッグは Editor / Development ビルドでのみ利用できます。");
#endif
    }

    [ContextMenu("デバッグ：天変地異固定種別を次へ")]
    public void DebugCyclePinnedDisasterKind()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DisasterCatalog.DebugPinDisasterKind = true;
        DisasterCatalog.CycleDebugPinnedDisasterKind(1);
        Debug.Log(
            "[BattleDebugTools] 天変地異固定: " +
            DisasterCatalog.GetDisplayName(DisasterCatalog.DebugPinnedDisasterKind));
#else
        Debug.LogWarning("[BattleDebugTools] 天変地異デバッグは Editor / Development ビルドでのみ利用できます。");
#endif
    }

    /// <summary>
    /// <see cref="StatusEffectFactory"/> で実体が作れるものは <see cref="PlayerStatus.TryApplyStatusEffect"/>。
    /// 未実装でプレースホルダー経由のみのものは <see cref="PlaceholderStatusEffect"/>。
    /// </summary>
    public static void ApplyGrantForDebug(PlayerStatus target, StatusEffectType type)
    {
        if (target == null || type == StatusEffectType.None) return;

        // Factory 未実装でプレースホルダーのみ（TryApply しない）
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
        var (result, _) = target.TryApplyStatusEffect(type, cfg, suppressGrantPopupAndSound: true);
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
        return false;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static string NameForCheatDisplay(CardData c)
    {
        if (c == null) return "";
        return string.IsNullOrEmpty(c.cardName) ? c.name : c.cardName;
    }

    private static CardData[] GetCheatCardCatalog()
    {
        if (_cachedAllCardsFromResources != null && _cachedAllCardsFromResources.Length > 0)
            return _cachedAllCardsFromResources;

        var loaded = Resources.LoadAll<CardData>("Cards");
        if (loaded == null || loaded.Length == 0)
        {
            _cachedAllCardsFromResources = Array.Empty<CardData>();
            return _cachedAllCardsFromResources;
        }

        Array.Sort(loaded, (a, b) =>
            string.CompareOrdinal(NameForCheatDisplay(a) ?? "", NameForCheatDisplay(b) ?? ""));
        _cachedAllCardsFromResources = loaded;
        return _cachedAllCardsFromResources;
    }

    /// <summary>Resources を再スキャンしたい場合（カード追加後など）。</summary>
    [ContextMenu("デバッグ：手札チート用カード一覧キャッシュをクリア")]
    public void ClearCheatCardCatalogCache()
    {
        _cachedAllCardsFromResources = null;
        Debug.Log("[BattleDebugTools] 手札チートのカード一覧キャッシュをクリアしました。");
    }

    private static string ResolveCheatCardNetworkId(CardData template)
    {
        if (template == null) return "";
        return string.IsNullOrEmpty(template.cardName) ? template.name : template.cardName;
    }

    private void TryAddCheatCardToPlayerHand(CardData template)
    {
        if (!EnsurePlaying()) return;
        if (template == null)
        {
            Debug.LogWarning("[BattleDebugTools] テンプレートが null です");
            return;
        }

        if (battleManager.IsOnlineMatch)
        {
            string id = ResolveCheatCardNetworkId(template);
            bool targetIsHostPlayer = OnlineMatchContext.IsHost;
            if (OnlineMatchContext.IsHost)
            {
                if (!battleManager.HostBroadcastOnlineDebugCardInject(id, targetIsHostPlayer))
                    Debug.LogWarning("[BattleDebugTools] オンライン注入に失敗しました");
            }
            else if (!battleManager.RequestOnlineDebugCardInject(id, targetIsHostPlayer))
            {
                Debug.LogWarning("[BattleDebugTools] オンライン注入リクエストに失敗しました");
            }
            else
            {
                Debug.Log($"[BattleDebugTools] ホストへ注入リクエスト: {NameForCheatDisplay(template)}");
            }
            return;
        }

        if (battleManager.playerHand.Count >= BattleManager.MaxHandCards)
        {
            Debug.LogWarning($"[BattleDebugTools] 手札上限（{BattleManager.MaxHandCards}）です");
            return;
        }

        if (battleManager.cardDealer == null)
        {
            Debug.LogWarning("[BattleDebugTools] CardDealer がありません");
            return;
        }

        CardData instance = battleManager.cardDealer.InstantiateCardFromTemplate(template);
        if (instance == null)
        {
            Debug.LogWarning("[BattleDebugTools] カードの Instantiate に失敗しました");
            return;
        }

        battleManager.playerHand.Add(instance);
        battleManager.cardDealer.CreateCardUIForHand(instance);
        // 通常のドローは裏面から始まり Reveal で表向け。デバッグ入手は最初から表向きにする。
        if (instance.cardUI != null)
            instance.cardUI.Reveal();

        battleManager.UpdateTotalATKDEFDisplay();
        BattleUIManager.I?.RefreshMagicCardInteractivity(battleManager.playerHand);
        BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
        battleManager.RefreshPlayerDefensePhaseInteractivity();

        Debug.Log($"[BattleDebugTools] 手札に追加: {NameForCheatDisplay(instance)}");
    }

    private void TryAddCheatCardToCpuHand(CardData template)
    {
        if (!EnsurePlaying()) return;
        if (template == null)
        {
            Debug.LogWarning("[BattleDebugTools] テンプレートが null です");
            return;
        }

        if (battleManager.IsOnlineMatch)
        {
            Debug.LogWarning("[BattleDebugTools] CPU 手札チートはオフライン対戦専用です");
            return;
        }

        if (battleManager.cpuHand == null)
        {
            Debug.LogWarning("[BattleDebugTools] cpuHand が null です");
            return;
        }

        if (battleManager.cpuHand.Count >= BattleManager.MaxHandCards)
        {
            Debug.LogWarning($"[BattleDebugTools] CPU 手札上限（{BattleManager.MaxHandCards}）です");
            return;
        }

        if (battleManager.cardDealer == null)
        {
            Debug.LogWarning("[BattleDebugTools] CardDealer がありません");
            return;
        }

        CardData instance = battleManager.cardDealer.InstantiateCardFromTemplate(template);
        if (instance == null)
        {
            Debug.LogWarning("[BattleDebugTools] カードの Instantiate に失敗しました");
            return;
        }

        battleManager.cpuHand.Add(instance);
        Debug.Log($"[BattleDebugTools] CPU 手札に追加: {NameForCheatDisplay(instance)}");
    }

    private void TryAddCheatCardToOpponentHandOnline(CardData template)
    {
        if (!EnsurePlaying()) return;
        if (template == null) return;
        if (!battleManager.IsOnlineMatch)
        {
            Debug.LogWarning("[BattleDebugTools] 相手手札注入はオンライン対戦専用です");
            return;
        }

        string id = ResolveCheatCardNetworkId(template);
        bool targetIsHostPlayer = !OnlineMatchContext.IsHost;
        if (OnlineMatchContext.IsHost)
        {
            if (!battleManager.HostBroadcastOnlineDebugCardInject(id, targetIsHostPlayer))
                Debug.LogWarning("[BattleDebugTools] オンライン注入（相手）に失敗しました");
        }
        else if (!battleManager.RequestOnlineDebugCardInject(id, targetIsHostPlayer))
        {
            Debug.LogWarning("[BattleDebugTools] オンライン注入リクエスト（相手）に失敗しました");
        }
        else
        {
            Debug.Log($"[BattleDebugTools] ホストへ相手手札注入リクエスト: {NameForCheatDisplay(template)}");
        }
    }

    [ContextMenu("デバッグ：Inspector の cheatCardTemplate を手札に追加")]
    private void ContextMenuAddInspectorCheatCardToHand()
    {
        TryAddCheatCardToPlayerHand(cheatCardTemplateForContextMenu);
    }
#endif
}
