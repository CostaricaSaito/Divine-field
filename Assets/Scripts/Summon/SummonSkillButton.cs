using System.Collections; // © ‚±‚ê‚ª•K—vI
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SummonSkillButton : MonoBehaviour
{
    [Header("ŽQÆ")]
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

        if (BattleManager.I.CurrentState != GameState.AttackPhase)
        {
            Debug.Log("¢Š«ƒXƒLƒ‹‚Í¡Žg‚¦‚Ü‚¹‚ñ");
            return;
        }

        BattleManager.I?.ClearPlayerSelfAttackTargetMode();

        Debug.Log($"[顕現チェック] HP+MP+GP={playerStatus.currentHP + playerStatus.currentMP + playerStatus.currentGP} (劣勢: {DisadvantageRules.IsDisadvantaged(playerStatus)})");

        if (!DisadvantageRules.IsDisadvantaged(playerStatus))
        {
            Debug.Log("顕現スキルの条件を満たしていません（劣勢時のみ）");
            return;
        }

        var summon = playerStatus.summonData;

        // ƒ|ƒbƒvƒAƒbƒv‚ðŠJ‚¢‚Äî•ñ‚ð•\Ž¦
        popupPanel.SetActive(true);
        skillNameText.text = playerStatus.summonData.specialSkillName;
        skillDescText.text = playerStatus.summonData.specialSkillDescription;

        // ƒXƒ^ƒCƒ‹“K—pi‚±‚±‚ªƒ|ƒCƒ“ƒgIj
        ApplyTextStyle(skillNameText, summon.popupSkillNameStyle);
        ApplyTextStyle(skillDescText, summon.popupSkillDescStyle);


        activateButton.onClick.RemoveAllListeners();
        activateButton.onClick.AddListener(ActivateSkill);
    }


    void ActivateSkill()
    {
        popupPanel.SetActive(false);

        // ƒJƒbƒgƒCƒ“‰‰o ¨ ƒXƒLƒ‹Œø‰Ê‚Öi‰¼j
        StartCoroutine(PlayCutInAndActivate());
    }

    IEnumerator PlayCutInAndActivate()
    {
        // ƒJƒbƒgƒCƒ“‰‰oi”wŒiA¢Š«bAƒXƒLƒ‹–¼j
        SummonSkillCutInController.I.PlayCutIn(
            playerStatus.summonData.specialSkillCutInSprite, 
            playerStatus.summonData.specialSkillName
        );

        var summon = playerStatus.summonData;

        // Œø‰Ê‰¹‚ðÄ¶iAudioClip‚ªÝ’è‚³‚ê‚Ä‚¢‚ê‚Îj
        if (summon.specialSkillSE != null)
        {
            AudioSource.PlayClipAtPoint(summon.specialSkillSE, Camera.main.transform.position);
        }

        yield return new WaitForSeconds(2f);  // ƒAƒjƒŽžŠÔ‚É‡‚í‚¹‚Ä’²®

        // ƒXƒLƒ‹Œø‰Êˆ—
        playerStatus.summonData.ActivateSpecialSkill(playerStatus, enemyStatus);

        // ƒXƒe[ƒ^ƒXUIXV
        BattleManager.I.statusUI.UpdateStatus(playerStatus, enemyStatus);
    }
}