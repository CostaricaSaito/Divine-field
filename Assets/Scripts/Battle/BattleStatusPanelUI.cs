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
        }

        if (enemy != null)
        {
            enemySummonIcon.sprite = enemy.summonData.characterSprite;
            enemyNameText.text = enemy.DisplayName;
            
            // ステータステキストを更新
            enemyStatusText.text = FormatStatusText(enemy.currentHP, enemy.maxHP, enemy.currentMP, enemy.maxMP, 
                                                  enemy.currentGP, enemy.maxGP, enemyHandCount);
            
        }
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
