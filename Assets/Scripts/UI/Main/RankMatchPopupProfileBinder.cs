using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RankMatchPopup のプロフィール表示（ランク名・RP・スライダー・アイコン・ランク色）。
/// UI は Prefab 上のオブジェクトを Inspector または名前で参照します。
/// </summary>
[DisallowMultipleComponent]
public sealed class RankMatchPopupProfileBinder : MonoBehaviour
{
    [Header("検索ルート（未割当時は ContentRoot / 子階層を名前検索）")]
    [SerializeField] private Transform searchRoot;

    [Header("テキスト")]
    [SerializeField] private TMP_Text currentRankText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text rpLabelText;
    [SerializeField] private TMP_Text rpValueText;
    [SerializeField] private TMP_Text nextRankValueText;

    [Header("ランクアイコン")]
    [SerializeField] private Image currentRankBadgeImage;
    [SerializeField] private Image rankIconAsIsImage;
    [SerializeField] private Image rankIconNextImage;

    [Header("進捗")]
    [SerializeField] private Slider nextRankSlider;
    [Tooltip("未割当時は NextRankSlider の Fill を使用します。")]
    [SerializeField] private Image nextRankSliderFillImage;

    [Header("最大ランク時の NextRankValue 表示")]
    [SerializeField] private string maxRankNextValueText = "—";

    public void Refresh()
    {
        ResolveReferences();

        var name = ResolvePlayerName();
        var rp = ResolveCurrentRp();
        var settings = RankIconSettings.Resolve();
        var tierId = PlayerRank.GetTierId(rp);
        var accentColor = settings != null ? settings.GetAccentColor(tierId) : Color.white;

        SetText(currentRankText, PlayerRank.GetDisplayName(rp));
        SetText(playerNameText, name);
        SetText(rpValueText, rp.ToString());
        ApplyAccentColor(currentRankText, accentColor);
        ApplyAccentColor(playerNameText, accentColor);
        ApplyAccentColor(rpLabelText, accentColor);
        ApplyAccentColor(rpValueText, accentColor);

        if (PlayerRank.IsMaxRank(rp))
        {
            SetText(nextRankValueText, maxRankNextValueText);
            ApplySlider(1f);
        }
        else
        {
            SetText(nextRankValueText, PlayerRank.GetRemainingRpToNextTier(rp).ToString());
            ApplySlider(PlayerRank.GetProgressInCurrentTier01(rp));
        }

        ApplySliderFillColor(accentColor);
        ApplyRankIcons(rp, settings);
    }

    void ResolveReferences()
    {
        if (searchRoot == null)
        {
            var popup = GetComponent<RankMatchPopupView>();
            if (popup != null)
            {
                var content = transform.Find("OverlayRoot/ContentRoot");
                searchRoot = content != null ? content : transform;
            }
            else
            {
                searchRoot = transform;
            }
        }

        if (currentRankText == null) currentRankText = FindTmp("CurrentRankText");
        if (playerNameText == null) playerNameText = FindTmp("PlayerNameText");
        if (rpLabelText == null) rpLabelText = FindTmp("RPtext");
        if (rpValueText == null) rpValueText = FindTmp("RPvalue");
        if (nextRankValueText == null) nextRankValueText = FindTmp("NextRankValue");
        if (currentRankBadgeImage == null) currentRankBadgeImage = FindImage("CurrentRankBadge");
        if (rankIconAsIsImage == null) rankIconAsIsImage = FindImage("RankIconASIS");
        if (rankIconNextImage == null) rankIconNextImage = FindImage("RankIconNEXT");
        if (nextRankSlider == null) nextRankSlider = FindComp<Slider>("NextRankSlider");
        if (nextRankSliderFillImage == null && nextRankSlider != null)
            nextRankSliderFillImage = nextRankSlider.fillRect?.GetComponent<Image>();
    }

    TMP_Text FindTmp(string objectName) => FindComp<TMP_Text>(objectName);

    T FindComp<T>(string objectName) where T : Component
    {
        if (searchRoot == null || string.IsNullOrEmpty(objectName)) return null;

        foreach (var t in searchRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != objectName) continue;
            var c = t.GetComponent<T>();
            if (c != null) return c;
        }

        return null;
    }

    Image FindImage(string objectName) => FindComp<Image>(objectName);

    static string ResolvePlayerName()
    {
        if (GameProfile.I != null)
            return GameProfile.I.PlayerName;

        PlayerProfileService.EnsureLoaded();
        return PlayerProfileService.Data.displayName;
    }

    static int ResolveCurrentRp()
    {
        if (GameProfile.I != null)
            return GameProfile.I.CurrentRP;

        PlayerProfileService.EnsureLoaded();
        return Mathf.Max(0, PlayerProfileService.Data.currentRp);
    }

    void ApplyRankIcons(int rp, RankIconSettings settings)
    {
        if (settings == null)
            return;

        var current = settings.GetIconForRp(rp);
        var next = settings.GetIconForNextTier(rp);

        SetSprite(currentRankBadgeImage, current);
        SetSprite(rankIconAsIsImage, current);
        SetSprite(rankIconNextImage, next);
    }

    void ApplySlider(float progress01)
    {
        if (nextRankSlider == null) return;

        nextRankSlider.minValue = 0f;
        nextRankSlider.maxValue = 1f;
        nextRankSlider.value = Mathf.Clamp01(progress01);
    }

    void ApplySliderFillColor(Color color)
    {
        if (nextRankSliderFillImage != null)
            nextRankSliderFillImage.color = color;
    }

    static void ApplyAccentColor(TMP_Text target, Color color)
    {
        if (target != null)
            target.color = color;
    }

    static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }

    static void SetSprite(Image target, Sprite sprite)
    {
        if (target == null) return;
        target.sprite = sprite;
        target.enabled = sprite != null;
    }
}
