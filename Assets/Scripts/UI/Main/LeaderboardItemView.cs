using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <c>Resources/Prefab/LeaderboardItem</c> の1行表示。
/// </summary>
[DisallowMultipleComponent]
public sealed class LeaderboardItemView : MonoBehaviour
{
    static readonly Color HighlightColor = new Color(1f, 0.92f, 0.35f, 0.28f);

    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text rankPointText;
    [SerializeField] private Image highlightImage;

    void Awake()
    {
        if (rankText == null)
            rankText = transform.Find("RankText")?.GetComponent<TMP_Text>();
        if (playerNameText == null)
            playerNameText = transform.Find("PlayerNameText")?.GetComponent<TMP_Text>();
        if (rankPointText == null)
            rankPointText = transform.Find("RankPointText")?.GetComponent<TMP_Text>();

        EnsureHighlightImage();
        SetHighlighted(false);
    }

    public void Bind(RankLeaderboardService.RankLeaderboardEntry entry, bool isLocalPlayer)
    {
        if (rankText != null)
            rankText.text = entry.Rank.ToString();
        if (playerNameText != null)
            playerNameText.text = RankLeaderboardService.FormatDisplayName(entry.DisplayName);
        if (rankPointText != null)
            rankPointText.text = entry.RankPoints.ToString();

        SetHighlighted(isLocalPlayer);
    }

    void EnsureHighlightImage()
    {
        if (highlightImage != null)
            return;

        highlightImage = GetComponent<Image>();
        if (highlightImage == null)
        {
            highlightImage = gameObject.AddComponent<Image>();
            highlightImage.raycastTarget = false;
        }

        highlightImage.color = Color.clear;
    }

    void SetHighlighted(bool highlighted)
    {
        EnsureHighlightImage();
        if (highlightImage == null)
            return;

        highlightImage.color = highlighted ? HighlightColor : Color.clear;
    }
}
