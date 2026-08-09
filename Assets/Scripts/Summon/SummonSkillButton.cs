using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BottomStatusPanel summon icon: manifestation popup (or Bahamut dual-skill popup).
/// </summary>
public class SummonSkillButton : MonoBehaviour
{
    [Tooltip("true: local player side. false: opponent side (PvP).")]
    [SerializeField] private bool isLocalPlayerSide = true;

    private PlayerStatus _self;
    private PlayerStatus _opponent;
    private Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OnClickSummonIcon);
    }

    /// <summary>Configure at battle start. self = this button's owner, opponent = the other player.</summary>
    public void Configure(PlayerStatus self, PlayerStatus opponent)
    {
        _self = self;
        _opponent = opponent;
        RefreshInteractable();
    }

    public void RefreshInteractable()
    {
        if (_button == null) return;

        bool turnOk = BattleManager.I != null
            && BattleManager.I.CurrentState == GameState.AttackPhase
            && BattleManager.I.CurrentTurnOwner == (isLocalPlayerSide ? PlayerType.Player : PlayerType.Enemy);

        bool can;
        if (_self != null && BahamutRules.IsBahamut(_self.summonData))
        {
            can = turnOk
                && BattleManager.I != null
                && BattleManager.I.CanActivateBahamutSummonButton(isLocalPlayerSide)
                && !_self.IsCastingArchMagic
                && !_self.HasFreezeEffect()
                && (BattleManager.I == null || !BattleManager.I.IsSummonSkillPopupOpen)
                && (BattleManager.I == null || !BattleManager.I.IsAnySummonSkillFlowRunning)
                && (BattleManager.I == null || !BattleManager.I.IsEconomicActionInProgress())
                && (BattleManager.I == null || !BattleManager.I.IsHandReloadPopupOpen);
        }
        else
        {
            can = _self != null
                && !_self.hasUsedManifestationSkill
                && DisadvantageRules.IsDisadvantaged(_self)
                && _self.summonData != null
                && _self.summonData.manifestationCard != null
                && turnOk
                && !_self.IsCastingArchMagic
                && !_self.HasFreezeEffect()
                && (BattleManager.I == null || !BattleManager.I.IsSummonSkillPopupOpen)
                && (BattleManager.I == null || !BattleManager.I.IsAnySummonSkillFlowRunning)
                && (BattleManager.I == null || !BattleManager.I.IsEconomicActionInProgress())
                && (BattleManager.I == null || !BattleManager.I.IsHandReloadPopupOpen)
                && (CardSelectionManager.I == null || CardSelectionManager.I.SelectedCardCount == 0);
        }

        _button.interactable = can;
    }

    void OnClickSummonIcon()
    {
        if (_self == null || _opponent == null || BattleManager.I == null) return;

        BattleManager.I.ClearPlayerSelfAttackTargetMode();

        if (!BattleManager.I.TryOpenSummonSkillPopup(_self, _opponent))
            Debug.Log("[SummonSkillButton] Could not open summon skill popup");
    }
}
