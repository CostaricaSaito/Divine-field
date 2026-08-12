using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Read-only enemy MagicPool display for <see cref="EnemyCurrentMagicPopupPresenter"/>.
/// </summary>
public sealed class EnemyCurrentMagicPopupView : MonoBehaviour
{
    private sealed class SlotBinding
    {
        public GameObject PlaceholderRoot;
        public CardUI CardUi;
        public TMP_Text UsesText;
        public string LastCardName;
    }

    private readonly List<SlotBinding> _slots = new List<SlotBinding>(3);
    private GameObject _nothingLabel;
    private bool _hierarchyBound;

    public void EnsureHierarchyBound()
    {
        if (_hierarchyBound) return;
        _hierarchyBound = true;

        var panel = transform.Find("CurrentMagicPanel");
        if (panel == null) panel = transform;

        _nothingLabel = transform.Find("Nothing")?.gameObject;
        if (_nothingLabel == null) _nothingLabel = panel.Find("Nothing")?.gameObject;
        if (_nothingLabel != null) _nothingLabel.SetActive(false);

        for (int i = 1; i <= MagicPoolManager.MaxPoolSize; i++)
        {
            var placeholder = panel.Find($"MagicPlaceholder{i}");
            if (placeholder == null) continue;

            _slots.Add(new SlotBinding
            {
                PlaceholderRoot = placeholder.gameObject,
                CardUi = placeholder.GetComponentInChildren<CardUI>(true),
                UsesText = placeholder.Find($"Magic{i}Rest")?.GetComponent<TMP_Text>(),
            });
        }
    }

    public void Refresh(IReadOnlyList<MagicCardEntry> entries, Sprite cardBackSprite)
    {
        EnsureHierarchyBound();

        bool empty = entries == null || entries.Count == 0;
        if (_nothingLabel != null) _nothingLabel.SetActive(empty);

        for (int i = 0; i < _slots.Count; i++)
        {
            if (empty || i >= entries.Count)
            {
                HideSlot(_slots[i]);
                continue;
            }

            ShowSlot(_slots[i], entries[i], cardBackSprite);
        }
    }

    private static void HideSlot(SlotBinding slot)
    {
        if (slot == null) return;
        slot.LastCardName = null;
        if (slot.PlaceholderRoot != null) slot.PlaceholderRoot.SetActive(true);
        if (slot.CardUi != null) slot.CardUi.gameObject.SetActive(false);
        if (slot.UsesText != null) slot.UsesText.gameObject.SetActive(false);
    }

    private static void ShowSlot(SlotBinding slot, MagicCardEntry entry, Sprite cardBackSprite)
    {
        if (slot == null || entry?.cardData == null) return;

        if (slot.PlaceholderRoot != null) slot.PlaceholderRoot.SetActive(true);
        if (slot.UsesText != null)
        {
            slot.UsesText.gameObject.SetActive(true);
            slot.UsesText.text = entry.remainingUses.ToString();
        }

        if (slot.CardUi == null) return;

        slot.CardUi.gameObject.SetActive(true);
        bool sameCard = slot.LastCardName == entry.cardData.cardName;
        if (!sameCard)
        {
            slot.LastCardName = entry.cardData.cardName;
            slot.CardUi.Setup(
                entry.cardData,
                cardBackSprite,
                hitRateHandContext: HitRateApplicability.HandContext.MagicPanel);
            slot.CardUi.Reveal();
            ApplyReadOnly(slot.CardUi);
        }
    }

    private static void ApplyReadOnly(CardUI cardUi)
    {
        if (cardUi == null) return;

        if (cardUi.highlightBorder != null)
            cardUi.highlightBorder.gameObject.SetActive(false);

        if (cardUi.button != null)
        {
            cardUi.button.onClick.RemoveAllListeners();
            cardUi.button.interactable = false;
            if (cardUi.button.targetGraphic != null)
                cardUi.button.targetGraphic.raycastTarget = false;
        }

        if (cardUi.cardImage != null)
            cardUi.cardImage.raycastTarget = false;
    }
}
