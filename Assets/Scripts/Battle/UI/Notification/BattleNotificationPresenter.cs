using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Queues battle notifications onto the owner CardDisplayPanel.
/// Same-frame requests on one side stagger by 0.3s and step +80 X/Y.
/// </summary>
public static class BattleNotificationPresenter
{
    public const string YourTurnMessage = "Your Turn";
    public const string OpponentTurnMessage = "Opponent Turn";
    public const string UltimateReadyMessage = "Ultimate Ready";
    public const string BuyReadyMessage = "BUY READY";
    public const string SellReadyMessage = "SELL READY";
    public const string ExchangeReadyMessage = "EXCHANGE READY";

    public const float StackStep = 80f;
    public const float StaggerSeconds = 0.3f;

    private const string PlayerPrefabPath = "Prefab/NotificationSample";
    private const string EnemyPrefabPath = "Prefab/EnemyNotificationSample";

    private static readonly Lane PlayerLane = new Lane();
    private static readonly Lane EnemyLane = new Lane();
    private static GameObject _playerPrefab;
    private static GameObject _enemyPrefab;
    private static int _generation;
    private static int _enemyExoDionDraws;

    public static void ResetBattle()
    {
        _generation++;
        _enemyExoDionDraws = 0;
        PlayerLane.Clear();
        EnemyLane.Clear();
    }

    public static void ShowTurnStart(Side side)
    {
        Show(side, side == Side.Player ? YourTurnMessage : OpponentTurnMessage);
    }

    public static void ShowEconomyReady(Side side, BattleEconomyCooldownReady ready)
    {
        if (ready.Buy) Show(side, BuyReadyMessage);
        if (ready.Sell) Show(side, SellReadyMessage);
        if (ready.Exchange) Show(side, ExchangeReadyMessage);
    }

    /// <summary>
    /// Opponent drew Sealed EXO-DION. Message is <c>exo-dion (n/3)</c>.
    /// </summary>
    public static void NotifyEnemySealedExoDionDrawn(CardData card, PlayerType drawer, CardDealer dealer)
    {
        if (drawer != PlayerType.Enemy) return;
        if (!SealedExoDionRules.IsSealedExoDionHandInstance(card, dealer)) return;

        _enemyExoDionDraws++;
        int goal = SealedExoDionRules.WinStackCount;
        Show(Side.Enemy, $"exo-dion ({_enemyExoDionDraws}/{goal})");
    }

    /// <param name="soundClip">Null uses <see cref="SoundEffectCatalogSO.PlayBattleNotification"/> catalog entry.</param>
    /// <param name="soundVolumeScale">Used only with <paramref name="soundClip"/>; 0 means 1.</param>
    public static void Show(Side side, string message, AudioClip soundClip = null, float soundVolumeScale = 0f)
    {
        if (string.IsNullOrEmpty(message)) return;

        var lane = LaneFor(side);
        lane.Pending.Add(new Request
        {
            Message = message,
            SoundClip = soundClip,
            SoundVolumeScale = soundVolumeScale,
        });

        if (lane.Flushing) return;
        lane.Flushing = true;
        int generation = _generation;
        _ = FlushAsync(side, generation);
    }

    private static async Task FlushAsync(Side side, int generation)
    {
        var lane = LaneFor(side);
        await Task.Yield();
        try
        {
            while (generation == _generation && lane.Pending.Count > 0)
            {
                var batch = lane.Pending.ToArray();
                lane.Pending.Clear();
                for (int i = 0; i < batch.Length; i++)
                {
                    if (generation != _generation) return;
                    Spawn(side, batch[i], i);
                    if (i < batch.Length - 1)
                        await WaitUnscaledAsync(StaggerSeconds, generation);
                }
            }
        }
        finally
        {
            if (generation == _generation)
            {
                lane.Flushing = false;
                if (lane.Pending.Count > 0)
                {
                    lane.Flushing = true;
                    _ = FlushAsync(side, generation);
                }
            }
        }
    }

    private static async Task WaitUnscaledAsync(float seconds, int generation)
    {
        float elapsed = 0f;
        while (elapsed < seconds && generation == _generation)
        {
            await Task.Yield();
            elapsed += Time.unscaledDeltaTime;
        }
    }

    private static void Spawn(Side side, Request request, int stackIndex)
    {
        var panel = ResolvePanel(side) as RectTransform;
        if (panel == null)
        {
            Debug.LogWarning($"[BattleNotification] CardDisplayPanel missing for {side}");
            return;
        }

        var prefab = LoadPrefab(side);
        if (prefab == null)
        {
            Debug.LogWarning($"[BattleNotification] Prefab missing for {side}");
            return;
        }

        var prefabRect = prefab.GetComponent<RectTransform>();
        // Prefab anchoredPosition is the canvas-center rest pose. Parent is the CardDisplayPanel.
        Vector2 authored = prefabRect != null ? prefabRect.anchoredPosition : Vector2.zero;
        Vector2 step = new Vector2(StackStep, StackStep) * stackIndex;
        Vector2 rest = authored - panel.anchoredPosition + step;

        float bannerWidth = prefabRect != null ? prefabRect.sizeDelta.x : 560f;
        float halfBanner = bannerWidth * 0.5f;
        var canvas = panel.GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        float canvasWidth = canvasRect != null ? canvasRect.rect.width : 0f;
        if (canvasWidth < 1f) canvasWidth = 1080f;

        bool fromLeft = side == Side.Player;
        float offCanvasX = fromLeft
            ? -canvasWidth * 0.5f - halfBanner - 48f
            : canvasWidth * 0.5f + halfBanner + 48f;
        var start = new Vector2(offCanvasX - panel.anchoredPosition.x, rest.y);

        var instance = Object.Instantiate(prefab, panel);
        instance.name = side == Side.Player ? "Notification" : "EnemyNotification";
        instance.transform.SetAsLastSibling();

        var view = instance.GetComponent<BattleNotificationView>();
        if (view == null)
            view = instance.AddComponent<BattleNotificationView>();
        view.Play(request.Message, start, rest, request.SoundClip, request.SoundVolumeScale);
    }

    private static Transform ResolvePanel(Side side)
    {
        if (BattleUIManager.I == null) return null;
        return side == Side.Player
            ? BattleUIManager.I.GetPlayerCardDisplayPanel()
            : BattleUIManager.I.GetEnemyCardDisplayPanel();
    }

    private static GameObject LoadPrefab(Side side)
    {
        if (side == Side.Player)
        {
            if (_playerPrefab == null)
                _playerPrefab = Resources.Load<GameObject>(PlayerPrefabPath);
            return _playerPrefab;
        }

        if (_enemyPrefab == null)
            _enemyPrefab = Resources.Load<GameObject>(EnemyPrefabPath);
        return _enemyPrefab;
    }

    private static Lane LaneFor(Side side) => side == Side.Player ? PlayerLane : EnemyLane;

    private sealed class Lane
    {
        public readonly List<Request> Pending = new List<Request>(4);
        public bool Flushing;

        public void Clear()
        {
            Pending.Clear();
            Flushing = false;
        }
    }

    private struct Request
    {
        public string Message;
        public AudioClip SoundClip;
        public float SoundVolumeScale;
    }
}
