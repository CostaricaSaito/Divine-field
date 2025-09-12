using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public enum Side { Player, Enemy }

/// <summary>
/// バトル画面の見た目だけ担当：
/// ・ステータス表示更新
/// ・手札の操作可否(グレー/有効)
/// ・Useボタンのラベル/色
/// ・敵の使用カード(右上)の掲示
/// ・ダメージ/ミスのポップアップ
/// ※クリックの意味判定は BattleManager 側。ここは「押せる/見える」だけ。
/// ※ボタン onClick はいじらない（副作用防止）。
/// </summary>
public class BattleUIManager : MonoBehaviour
{
    public static BattleUIManager I;

    //==== 参照 ====
    [Header("UI 参照")]
    [SerializeField] private BattleStatusUI statusUI;
    [SerializeField] private Button useButton;
    [SerializeField] private TMP_Text useButtonLabelTMP;   // 任意
    [SerializeField] private Text useButtonLabelUGUI;      // 任意
    [SerializeField] private Image useButtonImage;         // 任意（未指定なら useButton.targetGraphic を使用）

    [Header("ポップアップ")]
    [SerializeField] private GameObject damagePopupPrefab;
    [SerializeField] private Canvas uiCanvas;

    [Header("Use ボタン色")]
    [SerializeField] private Color useButtonNormalColor = new Color(0.2f, 0.5f, 1f, 1f); // 使用：青
    [SerializeField] private Color useButtonDangerColor = new Color(0.9f, 0.2f, 0.25f, 1f); // 許す：赤
    [SerializeField] private Color useButtonPrayColor = new Color(1f, 0.95f, 0.6f, 1f);  // 祈祷：薄黄

    [Header("カード詳細表示（使用時）")]
    [SerializeField] private CardDisplayController playerCardDisplayController;
    [SerializeField] private CardDisplayController enemyCardDisplayController;


    // ボタン表示モード（色切替に使用）
    private enum UseButtonMode { Use, Allow, Pray }

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        // ラベルが未設定なら拾う（初回だけ）
        if (useButton != null)
        {
            if (useButtonLabelTMP == null) useButtonLabelTMP = useButton.GetComponentInChildren<TMP_Text>(true);
            if (useButtonLabelUGUI == null) useButtonLabelUGUI = useButton.GetComponentInChildren<Text>(true);
            if (useButtonImage == null) useButtonImage = useButton.targetGraphic as Image;
        }
    }



    //======== 公開API：表示 ========

    /// ステータスUI更新
    public void UpdateStatus(PlayerStatus player, PlayerStatus enemy)
    {
        statusUI?.UpdateStatus(player, enemy);
    }

    /// Useボタンの文言（色は自動）
    public void SetUseButtonLabel(string text)
    {
        if (useButton == null) return;

        if (useButtonLabelTMP != null) useButtonLabelTMP.text = text;
        if (useButtonLabelUGUI != null) useButtonLabelUGUI.text = text;

        var mode = text == "許す" ? UseButtonMode.Allow
                 : text == "祈祷" ? UseButtonMode.Pray
                 : UseButtonMode.Use;
        ApplyUseButtonMode(mode);

        // 基本は押せる（押せるかどうかの最終判断は BM 側が Guard）
        useButton.interactable = true;
    }

    /// Useボタンの on/off（BM から利用）
    public void SetUseButtonInteractable(bool interactable)
    {
        if (useButton != null) useButton.interactable = interactable;
    }

    //======== 公開API：手札の操作可否 ========

    /// AttackSelect：攻撃で使える札だけ有効。他はグレー。
    public void RefreshAttackInteractivity(List<CardData> hand)
    {
        bool playerAttacks =
            (BattleManager.I?.CurrentState == GameState.AttackSelect) &&
            (BattleManager.I?.CurrentTurnOwner == PlayerType.Player);

        foreach (var c in hand)
        {
            if (c?.cardUI == null) continue;
            bool can = playerAttacks && CardRules.IsUsableInAttackPhase(c);
            SetCardInteractable(c, can);
        }

        if (playerAttacks)
        {
            SetUseButtonLabel("使用");
        }
    }

    /// DefenseSelect：allowed のみ有効。未選択スタートのためボタンは「許す」。
    public void RefreshDefenseInteractivity(List<CardData> allowed)
    {
        bool playerDefends =
            (BattleManager.I?.CurrentState == GameState.DefenseSelect) &&
            (BattleManager.I?.CurrentTurnOwner == PlayerType.Enemy);

        var hand = BattleManager.I.playerHand; // プレイヤーの手札
        var allowedSet = new HashSet<CardData>(allowed ?? hand);

        foreach (var c in hand)
        {
            if (c?.cardUI == null) continue;
            bool can = playerDefends && allowedSet.Contains(c);
            SetCardInteractable(c, can); // リスナーは触らない
        }

        if (playerDefends)
        {
            SetUseButtonLabel("許す"); // 0防御も選べる
        }
    }

    /// 祈祷モード：ボタンは「祈祷」、手札は全グレー
    public void SetPrayModeUI(List<CardData> hand)
    {
        SetUseButtonLabel("祈祷");
        if (hand == null) hand = BattleManager.I.playerHand;
        SetHandInteractivity(hand, false);
    }

    /// 手札すべてを一括で無効化
    public void DisableAllPlayerHandInteractivity()
    {
        SetHandInteractivity(BattleManager.I.playerHand, false);
    }

    //======== 公開API：カード詳細表示（使用時のみ） ========

    public void ShowCardDetail(CardData card, Side side)
    {
        if (side == Side.Player)
            playerCardDisplayController?.ShowCard(card);
        else
            enemyCardDisplayController?.ShowCard(card);
    }

    public void HideCardDetail(Side side)
    {
        if (side == Side.Player)
            playerCardDisplayController?.HideCard();
        else
            enemyCardDisplayController?.HideCard();
    }

    public void HideAllCardDetails()
    {
        playerCardDisplayController?.HideCard();
        enemyCardDisplayController?.HideCard();
    }
 
    //======== 公開API：ポップアップ ========

    public void ShowDamagePopup(int amount, PlayerStatus target)
    {
        var popup = SpawnPopupFor(target);
        if (popup == null) return;

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            bool hitPlayer = (target == BattleManager.I.GetPlayerStatus());
            damageText.Setup($"{amount} ダメージ！", hitPlayer ? Color.cyan : Color.red);
        }
    }

    public void ShowMissPopup(PlayerStatus target)
    {
        var popup = SpawnPopupFor(target);
        if (popup == null) return;

        var damageText = popup.GetComponent<DamagePopup>();
        if (damageText != null)
        {
            damageText.Setup("ミス！", Color.yellow);
        }
    }

        private void ApplyUseButtonMode(UseButtonMode mode)
    {
        if (useButton == null) return;
        var img = useButtonImage ?? (useButton.targetGraphic as Image);
        if (img == null) return;

        img.color = mode == UseButtonMode.Allow ? useButtonDangerColor
                 : mode == UseButtonMode.Pray ? useButtonPrayColor
                 : useButtonNormalColor;
    }

    private void SetCardInteractable(CardData c, bool interactable)
    {
        if (c?.cardUI == null) return;

        var btn = c.cardUI.button;
        if (btn != null) btn.interactable = interactable;

        // グレーアウト表現
        var cg = c.cardUI.GetComponent<CanvasGroup>();
        if (cg == null) cg = c.cardUI.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = interactable ? 1f : 0.5f;
        cg.blocksRaycasts = interactable;
    }

    private void SetHandInteractivity(List<CardData> hand, bool interactable)
    {
        if (hand == null) return;
        foreach (var c in hand) SetCardInteractable(c, interactable);
    }

    private GameObject SpawnPopupFor(PlayerStatus target)
    {
        if (damagePopupPrefab == null || uiCanvas == null)
        {
            Debug.LogWarning("[BattleUIManager] DamagePopup / Canvas 未設定");
            return null;
        }

        var go = Instantiate(damagePopupPrefab, uiCanvas.transform);
        var rt = go.transform as RectTransform;
        if (rt != null)
        {
            rt.anchoredPosition = GetPopupAnchor(target);
            rt.localScale = Vector3.one;
        }
        return go;
    }

    private Vector2 GetPopupAnchor(PlayerStatus target)
    {
        bool isPlayer = (target == BattleManager.I.GetPlayerStatus());
        return isPlayer ? new Vector2(-300, -200) : new Vector2(300, 200);
    }
}
