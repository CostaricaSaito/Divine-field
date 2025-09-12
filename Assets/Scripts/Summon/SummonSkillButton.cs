using System.Collections; // ← これが必要！
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SummonSkillButton : MonoBehaviour
{
    [Header("参照")]
    public PlayerStatus playerStatus;
    public PlayerStatus enemyStatus;
    public GameObject popupPanel;
    public TMP_Text skillNameText;
    public TMP_Text skillDescText;
    public Button activateButton;

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClickSummonIcon);
        popupPanel.SetActive(false);

    }

    public void SetStatus(PlayerStatus playerStatus, PlayerStatus enemyStatus)
    {
        this.playerStatus = playerStatus;
        this.enemyStatus = enemyStatus;
    }

    void ApplyTextStyle(TMP_Text text, SummonTextStyle style)
    {
        text.color = style.fontColor;
        text.enableVertexGradient = style.useGradient;
        if (style.useGradient)
        {
            text.colorGradient = new VertexGradient(style.topColor, style.topColor, style.bottomColor, style.bottomColor);
        }
        text.outlineColor = style.outlineColor;
        text.outlineWidth = style.outlineThickness;
    }

    void OnClickSummonIcon()
    {

        if (BattleManager.I.CurrentState != GameState.AttackSelect)
        {
            Debug.Log("召喚スキルは今使えません");
            return;
        }

        Debug.Log($"[顕現チェック] 現在のHP: {playerStatus.currentHP}");

        if (playerStatus.currentHP > 10)
        {
            Debug.Log("顕現スキルの条件を満たしていません");
            return;
        }

        var summon = playerStatus.summonData;

        // ポップアップを開いて情報を表示
        popupPanel.SetActive(true);
        skillNameText.text = playerStatus.summonData.specialSkillName;
        skillDescText.text = playerStatus.summonData.specialSkillDescription;

        // スタイル適用（ここがポイント！）
        ApplyTextStyle(skillNameText, summon.popupSkillNameStyle);
        ApplyTextStyle(skillDescText, summon.popupSkillDescStyle);


        activateButton.onClick.RemoveAllListeners();
        activateButton.onClick.AddListener(ActivateSkill);
    }


    void ActivateSkill()
    {
        popupPanel.SetActive(false);

        // カットイン演出 → スキル効果へ（仮）
        StartCoroutine(PlayCutInAndActivate());
    }

    IEnumerator PlayCutInAndActivate()
    {
        // カットイン演出（背景、召喚獣、スキル名）
        SummonSkillCutInController.I.PlayCutIn(
            playerStatus.summonData.specialSkillCutInSprite, 
            playerStatus.summonData.specialSkillName
        );

        var summon = playerStatus.summonData;

        // 効果音を再生（AudioClipが設定されていれば）
        if (summon.specialSkillSE != null)
        {
            AudioSource.PlayClipAtPoint(summon.specialSkillSE, Camera.main.transform.position);
        }

        yield return new WaitForSeconds(2f);  // アニメ時間に合わせて調整

        // スキル効果処理
        playerStatus.summonData.ActivateSpecialSkill(playerStatus, enemyStatus);

        // ステータスUI更新
        BattleManager.I.statusUI.UpdateStatus(playerStatus, enemyStatus);
    }
}