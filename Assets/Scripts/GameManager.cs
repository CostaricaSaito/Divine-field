using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public PlayerStatus player;
    public PlayerStatus enemy;  // EnemyStatus‚©‚ç“ˆê

    public TextMeshProUGUI resultText;

    public bool isPlayerTurn = true;
    private bool gameEnded = false;

    public void OnAttackButtonClicked()
    {
        if (!isPlayerTurn || gameEnded) return;

        enemy.TakeDamage(10);

        if (enemy.IsDead())
        {
            EndGame("Ÿ—˜I");
            return;
        }

        isPlayerTurn = false;
        Invoke("EnemyTurn", 1.0f);
    }

    void EnemyTurn()
    {
        if (gameEnded) return;

        player.TakeDamage(8);

        if (player.IsDead())
        {
            EndGame("”s–k...");
            return;
        }

        isPlayerTurn = true;
    }

    void EndGame(string message)
    {
        gameEnded = true;
        resultText.text = message;
        Debug.Log("ƒQ[ƒ€I—¹F" + message);
    }
}