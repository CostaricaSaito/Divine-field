using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MagicPanel の UI 管理クラス
///
/// 【階層構造】
/// MagicPanel（横長パネル）
///   └ MagicPlaceholder1,2,3（各スロットのルート：常時アクティブ。半透明枠等は子で配置しプレハブ側で表現）
///       ├ CardUI1,2,3（プールに載った魔法のみ表示。空スロットは非表示）
///       └ Magic1Rest,2Rest,3Rest（残使用回数：カードが載ったあと表示）
///
/// 【役割】
/// - MagicPool のカード一覧を CardUI で表示
/// - 各スロットに残り使用回数をテキスト表示
/// - カードクリックで BattleManager.SelectMagicPoolCard() を呼ぶ
/// </summary>
public class MagicPanelUI : MonoBehaviour
{
    [Header("スロット設定（MagicPlaceholder1〜3）")]
    [SerializeField] private List<MagicCardSlot> slots;

    [Header("参照")]
    [SerializeField] private Sprite cardBackSprite;

    // Refresh 時に SetInteractable 状態を維持するためのフラグ
    private bool currentInteractable = true;

    void Awake()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Count; i++)
            slots[i]?.InitializeEmptySlot();
    }

    // ===== 公開メソッド =====

    /// <summary>
    /// プールの内容に合わせてスロットを再描画する
    /// </summary>
    public void Refresh(List<MagicCardEntry> entries)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (i < entries.Count)
            {
                slots[i].Show(entries[i], cardBackSprite, currentInteractable);
            }
            else
            {
                slots[i].Hide();
            }
        }
    }

    /// <summary>
    /// 全スロットのインタラクティブ状態を設定する
    /// </summary>
    public void SetAllInteractable(bool interactable)
    {
        currentInteractable = interactable;
        foreach (var slot in slots)
        {
            slot.SetInteractable(interactable);
        }
    }

    /// <summary>
    /// 防御フェーズ：スロットごとに属性・MP を見て選択可否を切り替える。
    /// </summary>
    public void SetSlotsInteractableForDefense(
        bool panelEnabled,
        System.Collections.Generic.IReadOnlyList<CardData> incomingAttack,
        PlayerStatus defender)
    {
        currentInteractable = panelEnabled;
        foreach (var slot in slots)
        {
            var card = slot.GetCardData();
            bool slotOk = panelEnabled && card != null;
            if (slotOk && BlockingRules.IsPhysicalBlockingCard(card))
                slotOk = BlockingRules.CanPlayerSelectPhysicalBlockingDefense(card, incomingAttack, defender);
            else if (slotOk && card.cardType == CardType.Magic)
                slotOk = BlockingRules.CanAffordMagicDefenseMp(card, defender);
            slot.SetInteractable(slotOk);
        }
    }

    /// <summary>
    /// MagicPanel 内の CardData リストを返す
    /// </summary>
    public List<CardData> GetPooledCardDatas()
    {
        var result = new List<CardData>();
        foreach (var slot in slots)
        {
            var data = slot.GetCardData();
            if (data != null) result.Add(data);
        }
        return result;
    }

    /// <summary>
    /// 指定カードの CardUI を返す（ハイライト設定用）
    /// </summary>
    public CardUI GetCardUI(CardData card)
    {
        foreach (var slot in slots)
        {
            if (slot.GetCardData() == card) return slot.GetCardUI();
        }
        return null;
    }

    /// <summary>
    /// スロット index (0〜2) の着地点 Rect（手札→MagicPanel 飛行用）
    /// </summary>
    public bool TryGetSlotTargetRect(int index, out RectTransform rect)
    {
        rect = null;
        if (slots == null || index < 0 || index >= slots.Count) return false;
        rect = slots[index].GetFlyTargetRect();
        return rect != null;
    }
}

// ====================================================================
// MagicCardSlot — MagicPanelUI の各スロット
// ====================================================================

/// <summary>
/// MagicPanel 内の1スロット
///
/// 【Inspector設定】
/// - slotRoot: MagicPlaceholder（常時アクティブ。半透明枠は兄弟または子の Image で配置可）
/// - cardUI:   CardUI Prefab（プールに載ったカードを表示。空スロットでは GameObject 非表示）
/// - usesText: MagicXRest（残使用回数。カード表示後にのみアクティブにする）
/// </summary>
[System.Serializable]
public class MagicCardSlot
{
    [SerializeField] private GameObject slotRoot;   // MagicPlaceholderX
    [SerializeField] private CardUI cardUI;         // CardUIX
    [SerializeField] private TMP_Text usesText;     // MagicXRest

    private MagicCardEntry currentEntry;
    private bool isSetUp = false;

    /// <summary>起動時・空スロット: Placeholder を出し、CardUI/残数は隠す。</summary>
    public void InitializeEmptySlot()
    {
        if (currentEntry?.cardData != null && cardUI != null
            && ReferenceEquals(currentEntry.cardData.cardUI, cardUI))
        {
            currentEntry.cardData.cardUI = null;
        }

        currentEntry = null;
        isSetUp = false;
        if (slotRoot != null) slotRoot.SetActive(true);
        if (cardUI != null) cardUI.gameObject.SetActive(false);
        if (usesText != null) usesText.gameObject.SetActive(false);
    }

    /// <summary>
    /// エントリを表示する。同じカードが表示中なら回数テキストのみ更新
    /// </summary>
    public void Show(MagicCardEntry entry, Sprite backSprite, bool interactable)
    {
        if (slotRoot != null) slotRoot.SetActive(true);
        if (cardUI != null) cardUI.gameObject.SetActive(true);
        if (usesText != null) usesText.gameObject.SetActive(true);

        bool isSameCard = isSetUp && currentEntry != null
            && entry.cardData != null && currentEntry.cardData != null
            && currentEntry.cardData.cardName == entry.cardData.cardName;

        currentEntry = entry;

        if (cardUI != null)
        {
            if (!isSameCard)
            {
                cardUI.Setup(entry.cardData, backSprite);
                cardUI.Reveal();

                var btn = cardUI.button;
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(OnClick);

                    if (btn.targetGraphic == null)
                    {
                        var img = btn.GetComponent<Image>();
                        if (img == null)
                        {
                            img = btn.gameObject.AddComponent<Image>();
                            img.color = new Color(1, 1, 1, 0);
                        }
                        btn.targetGraphic = img;
                    }

                    if (btn.targetGraphic != null)
                    {
                        (btn.targetGraphic as Graphic).raycastTarget = true;
                    }
                }

                var cardImg = cardUI.cardImage;
                if (cardImg != null) cardImg.raycastTarget = true;
            }

            if (cardUI.button != null)
            {
                cardUI.button.interactable = interactable;
            }

            // プール表示中の CardData は Magic スロットの CardUI を指す（手札側参照のままだと発動が手札登録扱いになる）
            if (entry.cardData != null)
                entry.cardData.cardUI = cardUI;

            foreach (var cg in cardUI.GetComponentsInParent<CanvasGroup>(true))
            {
                if (!cg.blocksRaycasts || !cg.interactable)
                {
                    cg.blocksRaycasts = true;
                    cg.interactable = true;
                }
            }
        }

        UpdateUsesText(entry.remainingUses);
        isSetUp = true;
    }

    /// <summary>
    /// プールから外れた空スロット: Placeholder は残し、CardUI と残数のみ非表示
    /// </summary>
    public void Hide()
    {
        if (currentEntry?.cardData != null && cardUI != null
            && ReferenceEquals(currentEntry.cardData.cardUI, cardUI))
        {
            currentEntry.cardData.cardUI = null;
        }

        currentEntry = null;
        isSetUp = false;
        if (slotRoot != null) slotRoot.SetActive(true);
        if (cardUI != null) cardUI.gameObject.SetActive(false);
        if (usesText != null) usesText.gameObject.SetActive(false);
    }

    /// <summary>
    /// インタラクティブ状態を設定する
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (cardUI?.button != null)
        {
            bool value = interactable && currentEntry != null;
            cardUI.button.interactable = value;

            if (value)
            {
                foreach (var cg in cardUI.GetComponentsInParent<CanvasGroup>(true))
                {
                    cg.blocksRaycasts = true;
                    cg.interactable = true;
                }
            }
        }
    }

    public CardData GetCardData() => currentEntry?.cardData;
    public CardUI GetCardUI() => cardUI;

    /// <summary>
    /// 手札→MagicPanel 飛行アニメーションの着地点。
    /// プール上にカード表示中は <see cref="CardUI.cardImage"/>。空スロットは <see cref="slotRoot"/>
    ///（Placeholder 上に半透明枠を置く想定。CardUI は非表示のため slotRoot を測定する）。
    /// </summary>
    public RectTransform GetFlyTargetRect()
    {
        if (cardUI != null && cardUI.gameObject.activeInHierarchy
            && cardUI.cardImage != null)
        {
            var imgRt = cardUI.cardImage.rectTransform;
            if (imgRt != null) return imgRt;
        }

        if (slotRoot != null)
        {
            var rootRt = slotRoot.GetComponent<RectTransform>();
            if (rootRt != null) return rootRt;
        }

        if (cardUI != null)
        {
            var rt = cardUI.transform as RectTransform;
            if (rt != null) return rt;
        }
        return null;
    }

    private void UpdateUsesText(int uses)
    {
        if (usesText != null)
        {
            usesText.text = uses.ToString();
        }
    }

    private void OnClick()
    {
        if (currentEntry == null || BattleManager.I == null) return;
        BattleManager.I.SelectMagicPoolCard(currentEntry.cardData);
    }
}
