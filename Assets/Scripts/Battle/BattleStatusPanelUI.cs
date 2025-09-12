using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleStatusUI : MonoBehaviour
{
    [Header("UI参照（プレイヤー）")]
    public Image playerSummonIcon;
    public TMP_Text playerNameText;
    public Slider playerHPSlider;
    public TMP_Text playerHPText;
    public Slider playerMPSlider;
    public TMP_Text playerMPText;
    public Slider playerGPSlider;
    public TMP_Text playerGPText;

    [Header("UI参照（敵）")]
    public Image enemySummonIcon;
    public TMP_Text enemyNameText;
    public Slider enemyHPSlider;
    public TMP_Text enemyHPText;
    public Slider enemyMPSlider;
    public TMP_Text enemyMPText;
    public Slider enemyGPSlider;
    public TMP_Text enemyGPText;

    public void UpdateStatus(PlayerStatus player, PlayerStatus enemy)
    {
        if (player != null)
        {
            playerSummonIcon.sprite = player.summonData.characterSprite;
            playerNameText.text = player.DisplayName;
            playerHPSlider.maxValue = player.maxHP;
            playerHPSlider.value = player.currentHP;
            playerHPText.text = $"{player.currentHP}/{player.maxHP}";
            playerMPSlider.maxValue = player.maxMP;
            playerMPSlider.value = player.currentMP;
            playerMPText.text = $"{player.currentMP}/{player.maxMP}";
            playerGPSlider.maxValue = player.maxGP;
            playerGPSlider.value = player.currentGP;
            playerGPText.text = $"{player.currentGP}/{player.maxGP}";

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
                enemyHPSlider.maxValue = enemy.maxHP;
            enemyHPSlider.value = enemy.currentHP;
            enemyHPText.text = $"{enemy.currentHP}/{enemy.maxHP}";
            enemyMPSlider.maxValue = enemy.maxMP;
            enemyMPSlider.value = enemy.currentMP;
            enemyMPText.text = $"{enemy.currentMP}/{enemy.maxMP}";
            enemyGPSlider.maxValue = enemy.maxGP;
            enemyGPSlider.value = enemy.currentGP;
            enemyGPText.text = $"{enemy.currentGP}/{enemy.maxGP}";
        }
    }
}