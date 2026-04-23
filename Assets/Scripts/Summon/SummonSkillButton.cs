using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BottomStatusPanel の召喚アイコン：窮地時に顕現ポップアップを開く。
/// </summary>
public class SummonSkillButton : MonoBehaviour
{
    [Tooltip("true: 人間プレイヤー側アイコン。false: 対戦相手側（PvP 想定）")]
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

    /// <summary>戦闘開始時に呼ぶ。self = このボタンが属するプレイヤー、opponent = 相手。</summary>
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

        bool can = _self != null
            && !_self.hasUsedManifestationSkill
            && DisadvantageRules.IsDisadvantaged(_self)
            && _self.summonData != null
            && _self.summonData.manifestationCard != null
            && turnOk
            && !_self.IsCastingArchMagic
            && (BattleManager.I == null || !BattleManager.I.IsSummonSkillPopupOpen)
            && (BattleManager.I == null || !BattleManager.I.IsEconomicActionInProgress())
            && (CardSelectionManager.I == null || CardSelectionManager.I.SelectedCardCount == 0);

        _button.interactable = can;
    }

    void OnClickSummonIcon()
    {
        if (_self == null || _opponent == null || BattleManager.I == null) return;

        BattleManager.I.ClearPlayerSelfAttackTargetMode();

        if (!BattleManager.I.TryOpenSummonSkillPopup(_self, _opponent))
            Debug.Log("[SummonSkillButton] 顕現ポップアップを開けませんでした");
    }
}
