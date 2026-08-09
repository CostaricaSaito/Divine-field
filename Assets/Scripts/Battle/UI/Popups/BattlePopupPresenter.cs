using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// バトル中のポップアップ演出（ダメージ／回復／ミス／反射／無効化／状態異常／情報／Important／Ojyou）を
/// 一括で担当するサブマネージャ。
///
/// 【主な責務】
/// - 対象プレイヤーの CardDisplayPanel 中央への <see cref="DamagePopup"/> 生成と各種 Setup
/// - 物理／魔法反射と無効化の全画面白フラッシュ連動（<see cref="BattleUIManager.PlayFullscreenWhiteFlashMs"/> へ委譲）
/// - 状態異常付与／一括解除の連続ポップ演出
/// - 濃霧ポップ完了後の「濃霧画面演出」解除待ち指示（<see cref="BattleUIManager.ScheduleFogVisionRevealAfterPopup"/>）
/// - Canvas 中心 × CardDisplayPanel 縦中心に配置する <see cref="ImportantPopup"/>、指定側パネル中央への <see cref="OjyouPopup"/>
///
/// Canvas / CardDisplayPanel は <see cref="BattleUIManager"/> が保持するため、ファサード経由で取得する。
/// </summary>
public class BattlePopupPresenter : MonoBehaviour
{
    [Header("ダメージ / 情報 ポップアップ")]
    [SerializeField] private GameObject damagePopupPrefab;
    [Tooltip("Styled battle messages (freeze, disease intro, parry fail, intervention). Falls back to Resources/Prefab/MessagePopup.")]
    [SerializeField] private GameObject messagePopupPrefab;
    [SerializeField] private MessagePopupSettings messagePopupSettings;
    [Tooltip("DamagePopup の配色・背景スプライト。未設定時は Resources/DamagePopupSettings。")]
    [SerializeField] private DamagePopupSettings damagePopupSettings;
    [Tooltip("状態異常付与ポップの配色・背景。未設定時は Resources/StatusEffectPopupSettings。")]
    [SerializeField] private StatusEffectPopupSettings statusEffectPopupSettings;
    [Tooltip("大魔法バリア被ダメ演出。未設定時は Resources.Load(\"Prefab/BarriarDamage\")")]
    [SerializeField] private GameObject barrierDamagePopupPrefab;
    [Tooltip("未設定時は Resources.Load(\"Prefab/ImportantPopup\") を試す")]
    [SerializeField] private GameObject importantPopupPrefab;
    [Tooltip("未設定時は Resources.Load(\"Prefab/OjyouPopup\") を試す")]
    [SerializeField] private GameObject ojyouPopupPrefab;

    /// <returns>表示したポップアップが Destroy されるまでの秒数（<see cref="DamagePopup.fadeDuration"/>）。生成失敗時は 0。</returns>
    public float ShowDamagePopup(int amount, PlayerStatus target)
    {
        if (amount > 0)
            Debug.Log($"[BattlePopupPresenter] ダメージポップアップ表示: {amount}ダメージ 対象 {target?.DisplayName ?? "null"}");
        else
            Debug.Log($"[BattlePopupPresenter] ダメージポップアップ表示: 無傷 対象 {target?.DisplayName ?? "null"}");

        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] ポップアップの生成に失敗しました");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            bool hitPlayer = (target == BattleManager.I.GetPlayerStatus());
            damageText.SetupDamage(amount, hitPlayer);
            Debug.Log($"[BattlePopupPresenter] ダメージポップアップ設定完了: {amount}ダメージ");
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattlePopupPresenter] DamagePopup コンポーネントが見つかりません");
        return 0f;
    }

    /// <summary>大魔法 HP バリア被ダメ専用ポップアップ（BarriarDamage.prefab）。</summary>
    public async Task<float> ShowBarriarDamagePopupAsync(
        int valueBefore,
        int valueAfter,
        bool barrierBroken,
        PlayerStatus target,
        CancellationToken cancellationToken = default)
    {
        GameObject prefab = barrierDamagePopupPrefab != null
            ? barrierDamagePopupPrefab
            : Resources.Load<GameObject>("Prefab/BarriarDamage");
        if (prefab == null || target == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] BarriarDamage prefab or target is missing");
            return 0f;
        }

        bool isPlayer = target == BattleManager.I?.GetPlayerStatus();
        Transform parent = isPlayer
            ? (BattleUIManager.I != null ? BattleUIManager.I.GetPlayerCardDisplayPanel() : null)
            : (BattleUIManager.I != null ? BattleUIManager.I.GetEnemyCardDisplayPanel() : null);
        if (parent == null)
        {
            var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
            parent = canvas != null ? canvas.transform : null;
        }
        if (parent == null) return 0f;

        var go = Instantiate(prefab, parent, false);
        ApplyDamagePopupLayoutToPanelCenter(go.transform as RectTransform);

        var popup = go.GetComponent<BarriarDamagePopup>();
        if (popup == null)
            popup = go.AddComponent<BarriarDamagePopup>();

        return await popup.PlayAsync(valueBefore, valueAfter, barrierBroken, target, cancellationToken);
    }

    /// <summary>状態異常一括解除時、次のポップを出すまでの間隔（秒）。</summary>
    public const float StatusAilmentBulkClearStaggerSeconds = 0.2f;

    /// <summary>
    /// 一括解除済みの異常タイプを、付与時と同じ配色の <see cref="DamagePopup"/> で 0.2 秒ずつ重ね表示し、
    /// 最後のポップの寿命＋ポストインターバルまで待つ。
    /// </summary>
    public async Task PlayStatusAilmentBulkClearPresentationAsync(
        IReadOnlyList<StatusEffectType> clearedTypesOrdered,
        PlayerStatus target,
        CancellationToken cancellationToken = default)
    {
        if (clearedTypesOrdered == null || clearedTypesOrdered.Count == 0) return;

        float lastFade = DamagePopup.DefaultFadeDurationIfUnknown;
        for (int i = 0; i < clearedTypesOrdered.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var effectType = clearedTypesOrdered[i];
            if (effectType == StatusEffectType.None) continue;

            var popupGo = SpawnPopupFor(target);
            var damageText = popupGo != null ? popupGo.GetComponent<DamagePopup>() : null;
            if (damageText != null)
            {
                string name = StatusEffectPresentation.GetDisplayName(effectType);
                if (string.IsNullOrEmpty(name))
                    name = effectType.ToString();
                var style = ResolveStatusEffectPopupSettings().GetEntryOrDefault(effectType);
                damageText.SetupStatusAilmentGrant(name, style);
                lastFade = damageText.fadeDuration;
            }

            if (i < clearedTypesOrdered.Count - 1)
                await Task.Delay(TimeSpan.FromSeconds(StatusAilmentBulkClearStaggerSeconds), cancellationToken);
        }

        await DamagePopup.WaitAfterPopupLifetimeAsync(lastFade, cancellationToken);
    }

    /// <summary>
    /// 物理／魔法反射「弾き返す」ポップアップ。戻り値は <see cref="DamagePopup.fadeDuration"/>（秒）。
    /// </summary>
    public float ShowReflectionBouncePopup(PlayerStatus target, bool magicReflection = false)
    {
        BattleUIManager.I?.PlayFullscreenWhiteFlashMs(50f);
        SoundEffectPlayer.I?.Play(magicReflection ? ReflectionBounceAudio.Magic : ReflectionBounceAudio.Physical);
        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] 反射ポップアップ生成に失敗");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.SetupReflectionBounce();
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattlePopupPresenter] DamagePopup が見つかりません（反射）");
        return 0f;
    }

    /// <summary>打ち払い「打ち払う」。白フラッシュ50ms・金属バットSE・黄背景ポップアップ。</summary>
    public float ShowParryIntroPopup(PlayerStatus target)
    {
        BattleUIManager.I?.PlayFullscreenWhiteFlashMs(50f);
        SoundEffectPlayer.I?.Play("Assets/SE/金属バットで打つ1.mp3");
        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] 打ち払いポップアップ生成に失敗");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.SetupParryYellowBanner("打ち払う");
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattlePopupPresenter] DamagePopup が見つかりません（打ち払い）");
        return 0f;
    }

    /// <summary>打ち払い後、攻撃が自分側に戻ったときのメッセージ。</summary>
    public float ShowParryReturnToSelfPopup(PlayerStatus target)
    {
        SoundEffectPlayer.I?.Play("Assets/SE/ヒューンと落下.mp3");
        return ShowStyledMessagePopup(target, MessagePopupKind.ParryFailedReturn);
    }

    public float ShowBlockingNullifyPopup(PlayerStatus target)
    {
        BattleUIManager.I?.PlayFullscreenWhiteFlashMs(50f);
        SoundEffectPlayer.I?.Play(BlockingNullifyAudio.Physical);
        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] 無効化ポップアップ生成に失敗");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.SetupBlockingNullify();
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattlePopupPresenter] DamagePopup が見つかりません（無効化）");
        return 0f;
    }

    /// <summary>
    /// 闇属性：通常の超過ダメージ適用後の「残りHP分」表示（紫背景）。SE は呼び出し側で鳴らす。
    /// </summary>
    public float ShowDarkFollowupDamagePopup(int amount, PlayerStatus target)
    {
        Debug.Log($"[BattlePopupPresenter] 闇フォローダメージポップアップ: {amount} 対象 {target?.DisplayName ?? "null"}");

        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] 闇ポップアップの生成に失敗しました");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            bool hitPlayer = (target == BattleManager.I.GetPlayerStatus());
            damageText.SetupDarkFollowupDamage(amount, hitPlayer);
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattlePopupPresenter] DamagePopup コンポーネントが見つかりません");
        return 0f;
    }

    /// <summary>
    /// 状態異常が付与されたとき（ダメージポップと同じプレハブ）。表示成功時に SE を再生。
    /// </summary>
    /// <returns><see cref="DamagePopup.fadeDuration"/>（<see cref="DamagePopup.WaitAfterPopupLifetimeAsync"/> 用）。失敗時は 0。</returns>
    public float ShowStatusAilmentGrantPopup(StatusEffectType type, PlayerStatus target)
    {
        if (target == null || type == StatusEffectType.None) return 0f;

        string name = StatusEffectPresentation.GetDisplayName(type);
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning($"[BattlePopupPresenter] 状態異常の表示名がありません: {type}");
            return 0f;
        }

        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] 状態異常ポップアップの生成に失敗しました");
            return 0f;
        }

        var style = ResolveStatusEffectPopupSettings().GetEntryOrDefault(type);
        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            SoundEffectPlayer.I?.Play(StatusEffectApplyFeedback.GrantSoundAddress);
            damageText.SetupStatusAilmentGrant(name, style);
            Debug.Log($"[BattlePopupPresenter] 状態異常ポップアップ: {name}");

            // 濃霧：付与ポップアップの表示完了＋規定インターバル後まで、濃霧画面演出を遅延
            if (type == StatusEffectType.Fog
                && BattleManager.I != null
                && target == BattleManager.I.GetPlayerStatus()
                && BattleUIManager.I != null)
            {
                float waitSec = DamagePopup.TotalSecondsAfterPopupShown(damageText.fadeDuration);
                BattleUIManager.I.ScheduleFogVisionRevealAfterPopup(waitSec);
            }

            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattlePopupPresenter] DamagePopup コンポーネントが見つかりません");
        return 0f;
    }

    /// <summary>手札リロード（ピンクパネル・白字・濃いピンク縁）。SE は <c>Assets/SE/リロード.mp3</c>。</summary>
    /// <returns><see cref="DamagePopup.fadeDuration"/>。失敗時 0。</returns>
    public float ShowHandReloadPopup(PlayerStatus target)
    {
        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] 手札リロードポップアップ生成に失敗");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] DamagePopup がありません（手札リロード）");
            return 0f;
        }

        SoundEffectPlayer.I?.Play("Assets/SE/リロード.mp3");
        damageText.SetupHandReload("リロード");
        return damageText.fadeDuration;
    }

    /// <summary>運命の宝札：手札引き直し（HandReload と同系統の演出）。</summary>
    public float ShowHandDiscardRestartPopup(PlayerStatus target)
    {
        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] DiscardRestart popup spawn failed");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] DamagePopup missing (DiscardRestart)");
            return 0f;
        }

        SoundEffectPlayer.I?.Play("Assets/SE/リロード.mp3");
        damageText.SetupFromKind(DamagePopupKind.HandDiscardRestart, "引き直し");
        return damageText.fadeDuration;
    }

    public float ShowHealPopup(int amount, string statType, PlayerStatus target)
    {
        Debug.Log($"[BattlePopupPresenter] 回復ポップアップ表示: {statType}{amount} 対象 {target?.DisplayName ?? "null"}");

        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] ポップアップの生成に失敗しました");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.BindSettings(ResolveDamagePopupSettings());
            if (TryResolveHealKind(statType, out DamagePopupKind healKind))
            {
                var style = ResolveDamagePopupSettings().GetEntryOrDefault(healKind);
                string displayText = $"{statType}{amount}";
                damageText.Setup(displayText, style.textColor, style.outlineColor);
            }
            else
            {
                var healStyle = ResolveDamagePopupSettings().GetEntryOrDefault(DamagePopupKind.Heal);
                damageText.Setup(statType, healStyle.textColor, healStyle.outlineColor);
            }

            Debug.Log($"[BattlePopupPresenter] 回復ポップアップ設定完了");
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattlePopupPresenter] DamagePopup コンポーネントが見つかりません");
        return 0f;
    }

    private static bool TryResolveHealKind(string statType, out DamagePopupKind kind)
    {
        switch (statType)
        {
            case "HP":
                kind = DamagePopupKind.Heal;
                return true;
            case "MP":
                kind = DamagePopupKind.HealMp;
                return true;
            case "GP":
                kind = DamagePopupKind.HealGp;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    public void ShowMissPopup(PlayerStatus target)
    {
        Debug.Log($"[BattlePopupPresenter] ミスポップアップ表示 対象 {target?.DisplayName ?? "null"}");

        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] ミスポップアップの生成に失敗しました");
            return;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.BindSettings(ResolveDamagePopupSettings());
            damageText.SetupFromKind(DamagePopupKind.Miss);
            Debug.Log("[BattlePopupPresenter] ミスポップアップ設定完了");
        }
        else
        {
            Debug.LogWarning("[BattlePopupPresenter] DamagePopup コンポーネントが見つかりません");
        }
    }

    /// <summary>命中時（100% 未満のみ呼び出す想定）。SE は呼び出し側。</summary>
    public float ShowCombatHitConfirmedPopup(PlayerStatus target)
    {
        var popup = SpawnPopupFor(target);
        if (popup == null)
            return DamagePopup.DefaultFadeDurationIfUnknown;

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.BindSettings(ResolveDamagePopupSettings());
            damageText.SetupFromKind(DamagePopupKind.CombatHitConfirmed);
            return damageText.fadeDuration;
        }

        return DamagePopup.DefaultFadeDurationIfUnknown;
    }

    /// <summary>
    /// Styled <see cref="MessagePopup"/> for predefined battle messages (Inspector colors in <see cref="MessagePopupSettings"/>).
    /// </summary>
    public float ShowStyledMessagePopup(PlayerStatus target, MessagePopupKind kind)
    {
        var popup = SpawnMessagePopupForTarget(target, kind);
        return popup != null ? popup.FadeDuration : 0f;
    }

    /// <summary>天変地異用 MessagePopup。スタイルは kind、文言は messageOverride。</summary>
    public float ShowDisasterMessagePopup(PlayerStatus target, MessagePopupKind kind, string messageOverride)
    {
        var popup = SpawnDisasterMessagePopupForTarget(target, kind, messageOverride);
        return popup != null ? popup.FadeDuration : 0f;
    }

    public MessagePopup SpawnDisasterMessagePopupForTarget(
        PlayerStatus target,
        MessagePopupKind kind,
        string messageOverride)
    {
        if (target == null) return null;

        var entry = ResolveMessagePopupSettings().GetEntryOrDefault(kind);
        entry.message = messageOverride ?? entry.message;
        var go = SpawnMessagePopupObjectFor(target);
        if (go == null) return null;

        var popup = go.GetComponent<MessagePopup>();
        if (popup == null)
            popup = go.AddComponent<MessagePopup>();

        popup.BindSettings(ResolveMessagePopupSettings());
        popup.Setup(entry);
        return popup;
    }

    public MessagePopup SpawnMessagePopupForTarget(PlayerStatus target, MessagePopupKind kind)
    {
        if (target == null) return null;

        var entry = ResolveMessagePopupSettings().GetEntryOrDefault(kind);
        var go = SpawnMessagePopupObjectFor(target);
        if (go == null) return null;

        var popup = go.GetComponent<MessagePopup>();
        if (popup == null)
            popup = go.AddComponent<MessagePopup>();

        popup.BindSettings(ResolveMessagePopupSettings());
        popup.Setup(entry);
        return popup;
    }

    private MessagePopupSettings ResolveMessagePopupSettings()
    {
        if (messagePopupSettings != null) return messagePopupSettings;
        return MessagePopupSettings.GetRuntimeFallback();
    }

    private DamagePopupSettings ResolveDamagePopupSettings()
    {
        if (damagePopupSettings != null) return damagePopupSettings;
        return DamagePopupSettings.GetRuntimeFallback();
    }

    private StatusEffectPopupSettings ResolveStatusEffectPopupSettings()
    {
        if (statusEffectPopupSettings != null) return statusEffectPopupSettings;
        return StatusEffectPopupSettings.GetRuntimeFallback();
    }

    private GameObject SpawnMessagePopupObjectFor(PlayerStatus target)
    {
        GameObject prefab = messagePopupPrefab != null
            ? messagePopupPrefab
            : Resources.Load<GameObject>("Prefab/MessagePopup");
        if (prefab == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] MessagePopup prefab is missing");
            return null;
        }

        bool isPlayer = target == BattleManager.I?.GetPlayerStatus();
        Transform parent = isPlayer
            ? (BattleUIManager.I != null ? BattleUIManager.I.GetPlayerCardDisplayPanel() : null)
            : (BattleUIManager.I != null ? BattleUIManager.I.GetEnemyCardDisplayPanel() : null);
        if (parent == null)
        {
            var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
            parent = canvas != null ? canvas.transform : null;
        }
        if (parent == null) return null;

        var go = Instantiate(prefab, parent, false);
        ApplyDamagePopupLayoutToPanelCenter(go.transform as RectTransform);
        return go;
    }

    /// <summary>
    /// ステータス付近に任意メッセージのポップアップ（DamagePopup 経由。MessagePopup 対象外の汎用文言用）。
    /// </summary>
    public float ShowMessagePopupForTarget(PlayerStatus target, string message, Color color)
    {
        return ShowMessagePopupForTarget(target, message, color, Color.white);
    }

    /// <summary>
    /// Status-adjacent message popup with explicit outline color.
    /// </summary>
    public float ShowMessagePopupForTarget(PlayerStatus target, string message, Color color, Color outlineColor)
    {
        if (target == null || string.IsNullOrEmpty(message)) return 0f;

        var popup = SpawnPopupFor(target);
        if (popup == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] ShowMessagePopupForTarget: popup spawn failed");
            return 0f;
        }

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.Setup(message, color, outlineColor);
            return damageText.fadeDuration;
        }

        Debug.LogWarning("[BattlePopupPresenter] ShowMessagePopupForTarget: DamagePopup missing");
        return 0f;
    }

    public DamagePopup SpawnDamagePopupForTarget(PlayerStatus target)
    {
        var go = SpawnPopupFor(target);
        return go != null ? go.GetComponent<DamagePopup>() : null;
    }

    /// <summary>
    /// プレイヤーの CardDisplayPanel 中央に情報ポップアップを表示（MP不足、魔法容量不足など）。
    /// </summary>
    public DamagePopup ShowInfoPopupOnCardPanel(string message, Color color)
    {
        var playerPanel = BattleUIManager.I != null ? BattleUIManager.I.GetPlayerCardDisplayPanel() : null;
        if (damagePopupPrefab == null || playerPanel == null) return null;

        var go = Instantiate(damagePopupPrefab, playerPanel, false);
        ApplyDamagePopupLayoutToPanelCenter(go.transform as RectTransform);

        var popup = go.GetComponent<DamagePopup>();
        if (popup != null)
        {
            popup.BindSettings(ResolveDamagePopupSettings());
            popup.Setup(message, color);
        }
        return popup;
    }

    /// <summary>
    /// Canvas の水平中心 × 指定側 CardDisplayPanel の縦位置に重要メッセージを表示。
    /// </summary>
    public ImportantPopup ShowImportantPopup(string message, Color color, Side cardPanelSide)
    {
        GameObject prefab = importantPopupPrefab != null
            ? importantPopupPrefab
            : Resources.Load<GameObject>("Prefab/ImportantPopup");
        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
        if (prefab == null || canvas == null) return null;

        var go = Instantiate(prefab, canvas.transform, false);
        ApplyImportantPopupLayout(go.transform as RectTransform, cardPanelSide);

        var popup = go.GetComponent<ImportantPopup>();
        if (popup != null)
            popup.Setup(message, color);
        else
            Debug.LogWarning("[BattlePopupPresenter] ImportantPopup コンポーネントが見つかりません");
        return popup;
    }

    /// <summary>
    /// 「往生」ポップアップを表示する（ゲーム終了時）。指定側の CardDisplayPanel の子として生成し、
    /// 中央配置から <see cref="OjyouPopup"/> がパネル上端まで上昇しながらフェードする。
    /// </summary>
    public OjyouPopup ShowOjyouPopup(Side side)
    {
        GameObject prefab = ojyouPopupPrefab != null
            ? ojyouPopupPrefab
            : Resources.Load<GameObject>("Prefab/OjyouPopup");
        if (prefab == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] OjyouPopup プレハブが見つかりません");
            return null;
        }

        Transform parent = side == Side.Player
            ? (BattleUIManager.I != null ? BattleUIManager.I.GetPlayerCardDisplayPanel() : null)
            : (BattleUIManager.I != null ? BattleUIManager.I.GetEnemyCardDisplayPanel() : null);
        if (parent == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] CardDisplayPanel が未設定のため OjyouPopup を表示できません");
            return null;
        }

        var go = Instantiate(prefab, parent, false);
        ApplyDamagePopupLayoutToPanelCenter(go.transform as RectTransform);

        var popup = go.GetComponent<OjyouPopup>();
        if (popup != null)
            popup.Setup("往生", Color.black);
        else
            Debug.LogWarning("[BattlePopupPresenter] OjyouPopup コンポーネントが見つかりません");
        return popup;
    }

    /// <summary>介入発動時のメッセージ（病系処理より前）。</summary>
    public void ShowInterventionIntroPopup(PlayerStatus attackerStatus)
    {
        if (attackerStatus == null) return;
        SoundEffectPlayer.I?.Play("Assets/SE/介入.mp3");
        ShowStyledMessagePopup(attackerStatus, MessagePopupKind.InterventionAttack);
    }

    private GameObject SpawnPopupFor(PlayerStatus target)
    {
        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
        Debug.Log($"[BattlePopupPresenter] ポップアップ生成 - damagePopupPrefab: {damagePopupPrefab != null}, uiCanvas: {canvas != null}");

        if (damagePopupPrefab == null || canvas == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] DamagePopup / Canvas が設定されていません");
            return null;
        }

        bool isPlayer = target != null && target == BattleManager.I?.GetPlayerStatus();
        Transform parent = isPlayer
            ? (BattleUIManager.I != null ? BattleUIManager.I.GetPlayerCardDisplayPanel() : null)
            : (BattleUIManager.I != null ? BattleUIManager.I.GetEnemyCardDisplayPanel() : null);
        if (parent == null)
        {
            Debug.LogWarning("[BattlePopupPresenter] CardDisplayPanel / EnemyCardDisplayPanel が未設定のため Canvas 直下に出します");
            parent = canvas != null ? canvas.transform : null;
        }
        if (parent == null) return null;

        var go = Instantiate(damagePopupPrefab, parent, false);
        ApplyDamagePopupLayoutToPanelCenter(go.transform as RectTransform);
        var damagePopup = go.GetComponent<DamagePopup>();
        damagePopup?.BindSettings(ResolveDamagePopupSettings());
        Debug.Log($"[BattlePopupPresenter] ポップアップを {(isPlayer ? "CardDisplayPanel" : "EnemyCardDisplayPanel")} 中央に配置");
        return go;
    }

    /// <summary>
    /// 親パネル中央に重なるよう、ルート RectTransform を中央アンカー・位置0にそろえる。
    /// </summary>
    private static void ApplyDamagePopupLayoutToPanelCenter(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();
    }

    /// <summary>
    /// Canvas の X 中心と、指定側 CardDisplayPanel の Y 中心を合わせた位置にルートを置く。
    /// </summary>
    private void ApplyImportantPopupLayout(RectTransform popupRt, Side cardPanelSide)
    {
        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;
        if (popupRt == null || canvas == null) return;

        var canvasRt = canvas.transform as RectTransform;
        if (canvasRt == null) return;

        Transform panelTf = cardPanelSide == Side.Player
            ? (BattleUIManager.I != null ? BattleUIManager.I.GetPlayerCardDisplayPanel() : null)
            : (BattleUIManager.I != null ? BattleUIManager.I.GetEnemyCardDisplayPanel() : null);
        var panelRt = panelTf as RectTransform;

        popupRt.anchorMin = new Vector2(0.5f, 0.5f);
        popupRt.anchorMax = new Vector2(0.5f, 0.5f);
        popupRt.pivot = new Vector2(0.5f, 0.5f);
        popupRt.localScale = Vector3.one;

        Vector3 canvasCenterWorld = canvasRt.TransformPoint(canvasRt.rect.center);
        Vector3 panelCenterWorld = panelRt != null
            ? panelRt.TransformPoint(panelRt.rect.center)
            : canvasCenterWorld;

        Vector3 mixedWorld = new Vector3(canvasCenterWorld.x, panelCenterWorld.y, panelCenterWorld.z);
        Vector3 localInCanvas = canvasRt.InverseTransformPoint(mixedWorld);
        popupRt.anchoredPosition = new Vector2(localInCanvas.x, localInCanvas.y);
        popupRt.SetAsLastSibling();
    }
}
