using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main シーン：メイン4ボタンの入場演出（LeanTween）と、<see cref="SceneFadeNavigation"/> による遷移をまとめます。
/// Title の剣入場（<see cref="TitleLogoIntroController"/>）と同様に <see cref="LeanTween.value"/> で anchoredPosition を補間します。
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuPrimaryButtonsController : MonoBehaviour
{
    [System.Serializable]
    public struct MainMenuButtonSlot
    {
        public RectTransform root;
        [Tooltip("Build Settings のシーン名。空ならクリックしても遷移しません（Summon は SummonSelect.unity を想定）。")]
        public string sceneName;
        [Tooltip("着地点の anchoredPosition に対する開始オフセット（＋が右・上）。画面外から飛ばす量です。")]
        public Vector2 flyInStartOffset;
        [Tooltip("このスロットだけの移動時間（ミリ秒）。0 以下でグループ既定を使用。")]
        public float moveDurationMs;
        [Tooltip("カスケード遅延に加える追遅延（ミリ秒）。")]
        public float additionalDelayMs;
    }

    [Header("ボタン（上から順に 1→2→3→4 の入場）")]
    [SerializeField] private MainMenuButtonSlot battle;
    [SerializeField] private MainMenuButtonSlot summon;
    [SerializeField] private MainMenuButtonSlot cpu;
    [SerializeField] private MainMenuButtonSlot friendBattle;

    [Header("入場（共通）")]
    [Tooltip("各スロットの移動時間の既定（ミリ秒）。スロット側 moveDurationMs が正のときはそちらを優先。")]
    [SerializeField] [Min(1f)] private float defaultMoveDurationMs = 350f;
    [Tooltip("入場開始の間隔（ミリ秒）。1番目は 0、2番目はこの値、3番目は 2 倍 …")]
    [SerializeField] [Min(0f)] private float cascadeStartDelayMs = 50f;
    [SerializeField] private LeanTweenType flyEase = LeanTweenType.easeInQuad;
    [Tooltip("Time.timeScale=0 でも入場したいときオン。")]
    [SerializeField] private bool useUnscaledTime;

    [Header("SE（各ボタンが着地点に達したとき）")]
    [SerializeField] private string landSoundAddress = "Assets/SE/メインボタン移動.mp3";

    [Header("ランクマッチ")]
    [SerializeField] private MainRankMatchPopupController rankMatchPopup;

    readonly List<(MainMenuButtonSlot slot, Vector2 rest)> _active = new List<(MainMenuButtonSlot, Vector2)>();

    void Awake()
    {
        RebuildActiveList();
        ApplyButtonWiring();
    }

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        PlayFlyIn();
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;
        CancelTweens();
    }

    void OnDestroy()
    {
        CancelTweens();
    }

    void RebuildActiveList()
    {
        _active.Clear();
        AddIfValid(battle);
        AddIfValid(summon);
        AddIfValid(cpu);
        AddIfValid(friendBattle);
    }

    void AddIfValid(MainMenuButtonSlot slot)
    {
        if (slot.root == null) return;
        _active.Add((slot, slot.root.anchoredPosition));
    }

    void ApplyButtonWiring()
    {
        for (var i = 0; i < _active.Count; i++)
        {
            var slot = _active[i].slot;
            var scene = slot.sceneName;
            var btn = EnsureButton(slot.root);
            btn.onClick.RemoveAllListeners();

            if (IsRankMatchSlot(slot))
            {
                btn.onClick.AddListener(() => rankMatchPopup.TryOpen(btn));
                continue;
            }

            btn.onClick.AddListener(() => OnPrimaryClicked(scene, btn));
        }
    }

    bool IsRankMatchSlot(MainMenuButtonSlot slot)
    {
        return rankMatchPopup != null
            && slot.root != null
            && slot.root == battle.root;
    }

    static Button EnsureButton(RectTransform root)
    {
        var b = root.GetComponent<Button>();
        if (b == null)
            b = root.gameObject.AddComponent<Button>();
        var g = root.GetComponent<Graphic>();
        if (g != null)
            b.targetGraphic = g;
        return b;
    }

    void OnPrimaryClicked(string sceneName, Button btn)
    {
        if (!SceneFadeNavigation.TryFadeToScene(sceneName))
            return;
        btn.interactable = false;
    }

    void PlayFlyIn()
    {
        if (_active.Count == 0) return;

        CancelTweens();
        LeanTween.init();

        for (var i = 0; i < _active.Count; i++)
        {
            var (slot, rest) = _active[i];
            var rt = slot.root;
            if (rt == null) continue;

            var start = rest + slot.flyInStartOffset;
            rt.anchoredPosition = start;

            var btn = rt.GetComponent<Button>();
            if (btn != null)
                btn.interactable = false;

            var durMs = slot.moveDurationMs > 0f ? slot.moveDurationMs : defaultMoveDurationMs;
            var durSec = Mathf.Max(0.001f, durMs / 1000f);
            var delaySec = (i * cascadeStartDelayMs + slot.additionalDelayMs) / 1000f;

            var dc = LeanTween.delayedCall(gameObject, delaySec, () =>
            {
                if (rt == null) return;
                var tw = LeanTween.value(rt.gameObject, 0f, 1f, durSec)
                    .setEase(flyEase)
                    .setOnUpdate(t =>
                    {
                        if (rt == null) return;
                        rt.anchoredPosition = Vector2.Lerp(start, rest, t);
                    })
                    .setOnComplete(() =>
                    {
                        OnSlotLanded(rt, rest, btn);
                    });
                if (useUnscaledTime) tw.setIgnoreTimeScale(true);
            });
            if (useUnscaledTime) dc.setIgnoreTimeScale(true);
        }
    }

    void OnSlotLanded(RectTransform rt, Vector2 rest, Button btn)
    {
        if (rt != null)
            rt.anchoredPosition = rest;
        if (btn != null)
            btn.interactable = true;
        if (!string.IsNullOrEmpty(landSoundAddress))
            SoundEffectPlayer.I?.Play(landSoundAddress);
    }

    void CancelTweens()
    {
        LeanTween.cancel(gameObject);
        for (var i = 0; i < _active.Count; i++)
        {
            var rt = _active[i].slot.root;
            if (rt == null) continue;
            LeanTween.cancel(rt);
            LeanTween.cancel(rt.gameObject);
        }
    }
}
