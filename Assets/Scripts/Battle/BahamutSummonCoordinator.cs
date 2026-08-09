using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bahamut-specific summon popup (Mega Flare / Giga Flare) and Mega Flare launch.
/// </summary>
public sealed class BahamutSummonCoordinator
{
    private readonly ISummonSkillHost _host;
    private readonly SummonSkillCoordinator _manifestationHost;
    private GameObject _popupRoot;
    private bool _megaFlareFlowRunning;

    public BahamutSummonCoordinator(ISummonSkillHost host, SummonSkillCoordinator manifestationHost)
    {
        _host = host;
        _manifestationHost = manifestationHost;
    }

    public bool IsPopupOpen => _popupRoot != null;

    public bool IsMegaFlareFlowRunning => _megaFlareFlowRunning;

    public bool CanActivateSummonButton(PlayerStatus self, bool isLocalPlayerSide)
    {
        if (self == null || !BahamutRules.IsBahamut(self.summonData))
            return false;

        var summonerSide = isLocalPlayerSide ? PlayerType.Player : PlayerType.Enemy;
        if (!BahamutRules.CanOpenBahamutPopup(
                self, _host.SummonTurnCounters, _host.CurrentState, _host.CurrentTurnOwner, summonerSide))
            return false;

        if (_host.IsEconomicActionInProgress()) return false;
        if (_host.IsHandReloadPopupOpen()) return false;
        if (IsPopupOpen || _manifestationHost.IsManifestationFlowRunning || _megaFlareFlowRunning)
            return false;

        return true;
    }

    public bool TryOpenPopup(PlayerStatus summoner, PlayerStatus opponent)
    {
        if (_popupRoot != null || summoner == null || opponent == null)
            return false;

        bool summonerIsPlayer = ReferenceEquals(summoner, _host.PlayerStatus);
        var summonerSide = summonerIsPlayer ? PlayerType.Player : PlayerType.Enemy;

        if (!BahamutRules.CanOpenBahamutPopup(
                summoner, _host.SummonTurnCounters, _host.CurrentState, _host.CurrentTurnOwner, summonerSide))
            return false;

        if (_host.IsEconomicActionInProgress()) return false;

        var prefab = ResolveBahamutPopupPrefab();
        if (prefab == null)
        {
            Debug.LogError("[BahamutSummonCoordinator] BahamutPopup prefab not found");
            return false;
        }

        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetPopupCanvas() : null;
        if (canvas == null) return false;

        _host.ClearAttackSelectionNeutral();

        _popupRoot = Object.Instantiate(prefab, canvas.transform, false);
        var rt = _popupRoot.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            // Prefab root is a full-screen dim overlay; do not collapse to center anchor.
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        BindPopupUi(_popupRoot, summoner, opponent, summonerSide);
        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.SetUseButtonInteractable(false);
        BattleUIManager.I?.DisableEconomicActionButtonsTemporarily();
        _manifestationHost.RefreshButtonInteractables();
        return true;
    }

    public async Task TryRunEnemyMegaFlareAsync(CancellationToken cancellationToken)
    {
        if (_megaFlareFlowRunning || _host.EnemyStatus == null || _host.PlayerStatus == null)
            return;

        if (!BahamutRules.ShouldEnemyUseMegaFlareNow(
                _host.EnemyStatus, _host.SummonTurnCounters, _host.CurrentState, _host.CurrentTurnOwner))
            return;

        _megaFlareFlowRunning = true;
        try
        {
            _host.EnemyStatus.MarkMegaFlareUsed();
            _host.StatusUI?.UpdateStatus(_host.PlayerStatus, _host.EnemyStatus);

            if (_host.Sequences != null)
                await _host.Sequences.RunMegaFlareSequenceAsync(
                    _host.EnemyStatus, _host.PlayerStatus, cancellationToken);
        }
        finally
        {
            _megaFlareFlowRunning = false;
            _manifestationHost.RefreshButtonInteractables();
        }
    }

    private GameObject ResolveBahamutPopupPrefab()
    {
        if (_host.BahamutPopupPrefab != null)
            return _host.BahamutPopupPrefab;
        return Resources.Load<GameObject>("Prefab/BahamutPopup");
    }

    private void BindPopupUi(
        GameObject root,
        PlayerStatus summoner,
        PlayerStatus opponent,
        PlayerType summonerSide)
    {
        var summon = summoner.summonData;
        if (summon == null || root == null) return;

        var panel = root.transform.Find("Bahamut");
        if (panel == null) panel = root.transform;

        var nameT = panel.Find("BahamutPopupName")?.GetComponent<TMP_Text>();
        if (nameT != null)
            nameT.text = "バハムート能力発動";

        BindSkillDesc(panel, "PassiveSkillButton", summon.passiveSkillDescription);
        BindSkillDesc(panel, "SpecialSkillButton", summon.specialSkillDescription);

        var passiveBtn = panel.Find("PassiveSkillButton")?.GetComponent<Button>();
        var specialBtn = panel.Find("SpecialSkillButton")?.GetComponent<Button>();
        var cancelBtn = panel.Find("CancelButton")?.GetComponent<Button>();

        bool canMega = BahamutRules.CanUseMegaFlare(
            summoner, _host.SummonTurnCounters, _host.CurrentState, _host.CurrentTurnOwner, summonerSide);
        bool canGiga = BahamutRules.CanUseGigaFlare(
            summoner, _host.CurrentState, _host.CurrentTurnOwner, summonerSide);

        if (passiveBtn != null)
        {
            passiveBtn.interactable = canMega;
            passiveBtn.onClick.RemoveAllListeners();
            if (canMega)
                passiveBtn.onClick.AddListener(() => OnMegaFlareClicked(summoner, opponent));
        }

        if (specialBtn != null)
        {
            specialBtn.interactable = canGiga;
            specialBtn.onClick.RemoveAllListeners();
            if (canGiga)
                specialBtn.onClick.AddListener(() => OnGigaFlareClicked(summoner, opponent));
        }

        if (cancelBtn != null)
        {
            cancelBtn.onClick.RemoveAllListeners();
            cancelBtn.onClick.AddListener(OnPopupCancelClicked);
        }
    }

    private static void BindSkillDesc(Transform panel, string buttonName, string description)
    {
        var btn = panel.Find(buttonName);
        if (btn == null) return;
        TMP_Text descT = btn.Find($"{buttonName}Desc")?.GetComponent<TMP_Text>();
        if (descT == null)
        {
            for (int i = 0; i < btn.childCount; i++)
            {
                var child = btn.GetChild(i);
                if (!child.name.Contains("Desc")) continue;
                descT = child.GetComponent<TMP_Text>();
                if (descT != null) break;
            }
        }
        if (descT != null && !string.IsNullOrEmpty(description))
            descT.text = description;
    }

    private void OnPopupCancelClicked()
    {
        DestroyPopup();
        _manifestationHost.RefreshButtonInteractables();
        if (_host.CurrentState == GameState.AttackPhase && _host.CurrentTurnOwner == PlayerType.Player)
            _host.EnterAttackPhase();
        else if (_host.CurrentState == GameState.AttackPhase && _host.CurrentTurnOwner == PlayerType.Enemy)
        {
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.SetIntroModeUI(_host.PlayerHand);
        }
    }

    private void OnMegaFlareClicked(PlayerStatus summoner, PlayerStatus opponent)
    {
        if (_megaFlareFlowRunning) return;

        bool summonerIsPlayer = ReferenceEquals(summoner, _host.PlayerStatus);
        var summonerSide = summonerIsPlayer ? PlayerType.Player : PlayerType.Enemy;
        if (!BahamutRules.CanUseMegaFlare(
                summoner, _host.SummonTurnCounters, _host.CurrentState, _host.CurrentTurnOwner, summonerSide))
            return;

        DestroyPopup();
        _megaFlareFlowRunning = true;
        summoner.MarkMegaFlareUsed();
        _host.StatusUI?.UpdateStatus(_host.PlayerStatus, _host.EnemyStatus);
        _ = RunMegaFlareFlowAsync(summoner, opponent);
    }

    private void OnGigaFlareClicked(PlayerStatus summoner, PlayerStatus opponent)
    {
        DestroyPopup();
        _manifestationHost.StartManifestationFromBahamutPopup(summoner, opponent);
    }

    private async Task RunMegaFlareFlowAsync(PlayerStatus summoner, PlayerStatus opponent)
    {
        try
        {
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.SetUseButtonInteractable(false);

            if (_host.Sequences != null)
                await _host.Sequences.RunMegaFlareSequenceAsync(summoner, opponent, CancellationToken.None);
        }
        finally
        {
            _megaFlareFlowRunning = false;
            _manifestationHost.RefreshButtonInteractables();
            if (_host.CurrentState == GameState.AttackPhase && _host.CurrentTurnOwner == PlayerType.Player)
                _host.EnterAttackPhase();
            else if (_host.CurrentState == GameState.DefensePhase && _host.Defender == PlayerType.Player)
            {
                BattleUIManager.I?.SetHandClickable(true);
                _host.RefreshPlayerDefensePhaseInteractivity();
                BattleUIManager.I?.RefreshMagicCardInteractivity(_host.PlayerHand);
            }
        }
    }

    private void DestroyPopup()
    {
        if (_popupRoot == null) return;
        Object.Destroy(_popupRoot);
        _popupRoot = null;
    }
}
