using UnityEngine;
using UnityEngine.UI; // ← Sliderに必要
using TMPro;

public class EnemyStatus : MonoBehaviour
{
    public int maxHP = 30;
    public int currentHP;

    public Slider hpSlider; // ← 追加
    public TextMeshProUGUI hpText; // ← 併用したければ

    void Start()
    {
        currentHP = maxHP;
        UpdateHPDisplay();
    }

    public void TakeDamage(int damage)
    {
        currentHP -= damage;
        currentHP = Mathf.Max(currentHP, 0);
        UpdateHPDisplay();

        if (currentHP == 0)
        {
            Debug.Log("敵を倒した！");
        }
    }

    private void UpdateHPDisplay()
    {
        if (hpSlider != null)
        {
            hpSlider.value = currentHP;
        }

        if (hpText != null)
        {
            hpText.text = $"HP: {currentHP}/{maxHP}";
        }
    }
}