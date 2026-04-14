using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleStatusUI : MonoBehaviour
{
    [Header("UI参照（プレイヤー）")]
    public Image playerSummonIcon;
    public TMP_Text playerNameText;
    public TMP_Text playerStatusText; // HP/MP/GP/HAND表示用

    [Header("UI参照（敵）")]
    public Image enemySummonIcon;
    public TMP_Text enemyNameText;
    public TMP_Text enemyStatusText; // HP/MP/GP/HAND表示用

    [Header("状態異常アイコン")]
    [Tooltip("Create > Divine > Battle > Status Effect Icon Settings で作成し、Debuff フォルダの Sprite を割り当てる")]
    [SerializeField] private StatusEffectIconSettings statusEffectIconSettings;

    [Tooltip("プレイヤー行：HP 等のテキストの下に置く空の RectTransform。子に HorizontalLayoutGroup を付けると横並びになります。")]
    [SerializeField] private RectTransform playerAilmentIconRow;
    [Tooltip("敵行：同上")]
    [SerializeField] private RectTransform enemyAilmentIconRow;
    [Tooltip("任意：1個分のアイコンPrefab（Image＋LayoutElement 推奨）。未設定時は実行時に Image を生成します。")]
    [SerializeField] private GameObject ailmentIconPrefab;
    [SerializeField] private Vector2 ailmentIconSize = new Vector2(36f, 36f);

    /// <summary>フェーズ1以降の UI やデバッグから参照。</summary>
    public StatusEffectIconSettings StatusEffectIconSettings => statusEffectIconSettings;

    public Sprite GetStatusEffectIcon(StatusEffectType type)
    {
        return statusEffectIconSettings != null ? statusEffectIconSettings.GetIcon(type) : null;
    }

    public void UpdateStatus(PlayerStatus player, PlayerStatus enemy, int playerHandCount = 0, int enemyHandCount = 0)
    {
        Debug.Log($"[BattleStatusUI] UpdateStatus呼び出し - プレイヤー手札: {playerHandCount}, 敵手札: {enemyHandCount}");
        
        if (player != null)
        {
            playerSummonIcon.sprite = player.summonData.characterSprite;
            playerNameText.text = player.DisplayName;
            
            // ステータステキストを更新
            playerStatusText.text = FormatStatusText(player.currentHP, player.maxHP, player.currentMP, player.maxMP, 
                                                   player.currentGP, player.maxGP, playerHandCount);
            
            // 「PlayerSummonIcon」の子として存在するRainbowOverlay
            Transform overlay = playerSummonIcon.transform.Find("RainbowOverlay");

            if (player.currentHP <= 10)
            {
                if (!overlay.GetComponent<RainbowOutline>())
                    overlay.gameObject.AddComponent<RainbowOutline>();
            }
            else
            {
                if (overlay.GetComponent<RainbowOutline>())
                    Destroy(overlay.GetComponent<RainbowOutline>());

                overlay.GetComponent<Image>().color = new Color(1, 1, 1, 0); // 完全透明に戻す
            }

            RefreshAilmentIconRow(playerAilmentIconRow, player);
        }
        else
            RefreshAilmentIconRow(playerAilmentIconRow, null);

        if (enemy != null)
        {
            enemySummonIcon.sprite = enemy.summonData.characterSprite;
            enemyNameText.text = enemy.DisplayName;
            
            // ステータステキストを更新
            enemyStatusText.text = FormatStatusText(enemy.currentHP, enemy.maxHP, enemy.currentMP, enemy.maxMP, 
                                                  enemy.currentGP, enemy.maxGP, enemyHandCount);

            RefreshAilmentIconRow(enemyAilmentIconRow, enemy);
        }
        else
            RefreshAilmentIconRow(enemyAilmentIconRow, null);
    }

    private void RefreshAilmentIconRow(RectTransform row, PlayerStatus status)
    {
        if (row == null) return;

        for (int i = row.childCount - 1; i >= 0; i--)
            Destroy(row.GetChild(i).gameObject);

        if (status == null) return;

        List<StatusEffectType> types = status.GetActiveAilmentTypesOrdered();
        for (int i = 0; i < types.Count; i++)
        {
            Sprite spr = GetStatusEffectIcon(types[i]);
            if (spr == null) continue;

            GameObject iconGo = ailmentIconPrefab != null
                ? Instantiate(ailmentIconPrefab, row)
                : CreateRuntimeAilmentIcon(row);
            if (iconGo == null) continue;

            Image img = iconGo.GetComponent<Image>();
            if (img == null) img = iconGo.GetComponentInChildren<Image>(true);
            if (img != null)
            {
                img.sprite = spr;
                img.preserveAspect = true;
            }
        }
    }

    private GameObject CreateRuntimeAilmentIcon(RectTransform parent)
    {
        var go = new GameObject("AilmentIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.sizeDelta = ailmentIconSize;

        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = ailmentIconSize.x;
        le.preferredHeight = ailmentIconSize.y;
        le.minWidth = ailmentIconSize.x;
        le.minHeight = ailmentIconSize.y;

        return go;
    }

    /// <summary>
    /// ステータステキストをフォーマット（HP MP GP HAND形式）
    /// </summary>
    private string FormatStatusText(int currentHP, int maxHP, int currentMP, int maxMP, int currentGP, int maxGP, int handCount)
    {
        return $"<color=#FF0000><size=80%>HP</size></color> <color=white><size=120%>{currentHP}</size></color> " +
               $"<color=#00FFFF><size=80%>MP</size></color> <color=white><size=120%>{currentMP}</size></color> " +
               $"<color=#FFFF00><size=80%>GP</size></color> <color=white><size=120%>{currentGP}</size></color> " +
               $"<color=#FF00FF><size=80%>HAND</size></color> <color=white><size=120%>{handCount}</size></color>";
    }
}
