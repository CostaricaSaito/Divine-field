// BattleDebugTools.cs
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
public class BattleDebugTools : MonoBehaviour
{
    [Header("バトルコンポーネント参照")]
    public BattleManager battleManager;
    public Button setHP10Button;

    void Start()
    {
        if (setHP10Button != null)
            setHP10Button.onClick.AddListener(SetPlayerHPTo10);
    }

    public void SetPlayerHPTo10()
    {
        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();

        player.currentHP = 10;
        battleManager.statusUI.UpdateStatus(player, enemy);

        Debug.Log("デバッグ：プレイヤーHPを10に設定しました");
    }
}
#endif