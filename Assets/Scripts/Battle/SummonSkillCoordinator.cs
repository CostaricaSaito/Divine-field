using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Summon skill popup, player manifestation flow, and enemy manifestation handoff.
/// </summary>
public sealed class SummonSkillCoordinator
{
    private readonly ISummonSkillHost _host;
    private readonly BahamutSummonCoordinator _bahamut;
    private GameObject _popupRoot;
    private bool _manifestationFlowRunning;

    public SummonSkillCoordinator(ISummonSkillHost host)
    {
        _host = host;
        _bahamut = new BahamutSummonCoordinator(host, this);
    }

    public bool IsPopupOpen => _popupRoot != null || _bahamut.IsPopupOpen;

    public bool IsManifestationFlowRunning => _manifestationFlowRunning;

    public bool IsMegaFlareFlowRunning => _bahamut.IsMegaFlareFlowRunning;

    public bool IsAnySummonFlowRunning =>
        _manifestationFlowRunning || _bahamut.IsMegaFlareFlowRunning;

    public static SummonData ResolveRandomEnemySummon()
    {
        var list = SummonSelectionManager.I?.GetAllSummonData();
        if (list == null || list.Length == 0) return null;

        var enemyCandidates = new List<SummonData>(list);
        if (SummonSelectionManager.I != null)
            enemyCandidates.RemoveAt(SummonSelectionManager.I.SelectedIndex);

        return enemyCandidates[Random.Range(0, enemyCandidates.Count)];
    }

    public void RefreshButtonInteractables()
    {
        _host.PlayerSummonButton?.RefreshInteractable();
        _host.EnemySummonButton?.RefreshInteractable();
    }

    public bool CanActivateBahamutSummonButton(PlayerStatus self, bool isLocalPlayerSide)
        => _bahamut.CanActivateSummonButton(self, isLocalPlayerSide);

    public bool TryOpenPopup(PlayerStatus summoner, PlayerStatus opponent)
    {
        if (_popupRoot != null || _bahamut.IsPopupOpen || summoner == null || opponent == null)
            return false;
        if (_host.IsEconomicActionInProgress()) return false;

        if (BahamutRules.IsBahamut(summoner.summonData))
            return _bahamut.TryOpenPopup(summoner, opponent);

        return TryOpenStandardManifestationPopup(summoner, opponent);
    }

    public void DismissPopupIfOpen()
    {
        if (_bahamut.IsPopupOpen)
            _bahamut.DismissPopupIfOpen();
        if (_popupRoot != null)
            OnPopupCancelClicked();
    }

    public async Task TryRunEnemyMegaFlareAsync(CancellationToken cancellationToken)
        => await _bahamut.TryRunEnemyMegaFlareAsync(cancellationToken);

    internal void StartManifestationFromBahamutPopup(PlayerStatus summoner, PlayerStatus opponent)
    {
        if (_manifestationFlowRunning) return;
        RefreshButtonInteractables();
        _manifestationFlowRunning = true;
        summoner.MarkManifestationSkillUsed();
        _host.StatusUI?.UpdateStatus(_host.PlayerStatus, _host.EnemyStatus);
        _ = RunManifestationFlowAsync(summoner, opponent);
    }

    public async Task PresentEnemyManifestationAttackToPlayerDefenseAsync(
        List<CardData> atkList,
        CancellationToken cancellationToken)
    {
        if (atkList == null || atkList.Count == 0
            || _host.EnemyStatus == null || _host.PlayerStatus == null)
            return;

        if (_host.EnemyStatus.HasConfusionEffect())
        {
            bool confusionTargetSelf = BattleRandom.Range(0, 2) == 0;
            _host.SetConfusionAttackTargetResolvedForDisplay(confusionTargetSelf);
            if (confusionTargetSelf)
            {
                _host.UpdateCardStatsDisplay();
                await Task.Delay(500, cancellationToken);
                if (!await _host.ResolveConfusionSelfAttackAsync(atkList, cancellationToken))
                    return;
                await _host.RunAfterCombatSharedCleanupAsync(cancellationToken);
                return;
            }
        }

        var primary = HitRateRules.GetPrimaryForHitRate(atkList);
        int finalPct = HitRateRules.ComputeFinalHitPercent(primary, _host.EnemyStatus, _host.PlayerStatus);
        bool rolledHit = HitRateRules.RollHit(finalPct);
        if (!rolledHit)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ニュッ1.mp3");
            BattleUIManager.I?.ShowMissPopup(_host.PlayerStatus);
            await DamagePopup.WaitAfterPopupLifetimeAsync(DamagePopup.DefaultFadeDurationIfUnknown, cancellationToken);
            BattleUIManager.I?.HideAllCardDetails();
            _host.SetCurrentAttackCard(null);
            _host.ClearCardStatsSequence();
            _host.UpdateCardStatsDisplay();
            _host.SetGameState(GameState.CombatResolvePhase);
            return;
        }

        if (finalPct < 100)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/小パンチ.mp3");
            float popupSec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowCombatHitConfirmedPopup(_host.PlayerStatus)
                : DamagePopup.DefaultFadeDurationIfUnknown;
            await DamagePopup.WaitAfterPopupLifetimeAsync(popupSec, cancellationToken);
        }

        _host.SetGameState(GameState.DefensePhase);
    }

    private bool TryOpenStandardManifestationPopup(PlayerStatus summoner, PlayerStatus opponent)
    {
        if (summoner.hasUsedManifestationSkill) return false;
        if (summoner.HasFreezeEffect()) return false;
        if (_host.CurrentState != GameState.AttackPhase) return false;

        bool summonerIsPlayer = ReferenceEquals(summoner, _host.PlayerStatus);
        if (_host.CurrentTurnOwner != (summonerIsPlayer ? PlayerType.Player : PlayerType.Enemy))
            return false;

        if (summoner.summonData == null || summoner.summonData.manifestationCard == null) return false;
        if (CardSelectionManager.I != null && CardSelectionManager.I.SelectedCardCount > 0) return false;

        var prefab = Resources.Load<GameObject>("Prefab/SummonSkillPopup");
        if (prefab == null)
        {
            Debug.LogError("[SummonSkillCoordinator] Resources/Prefab/SummonSkillPopup not found");
            return false;
        }

        var canvas = BattleUIManager.I != null ? BattleUIManager.I.GetPopupCanvas() : null;
        if (canvas == null) return false;

        _popupRoot = Object.Instantiate(prefab, canvas.transform, false);
        var rt = _popupRoot.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        BindStandardPopupUi(_popupRoot, summoner, opponent);
        BattleUIManager.I?.SetHandClickable(false);
        BattleUIManager.I?.SetUseButtonInteractable(false);
        BattleUIManager.I?.DisableEconomicActionButtonsTemporarily();
        RefreshButtonInteractables();
        return true;
    }

    private static Transform ResolvePopupPanel(Transform root)
    {
        if (root == null) return null;
        if (root.Find("SummonButton") != null) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child != null && child.Find("SummonButton") != null)
                return child;
        }
        return root;
    }

    private void BindStandardPopupUi(GameObject root, PlayerStatus summoner, PlayerStatus opponent)
    {
        var summon = summoner.summonData;
        if (summon == null || root == null) return;

        var panel = ResolvePopupPanel(root.transform);
        if (panel == null) return;

        var nameT = panel.Find("SummonSkillName")?.GetComponent<TMPro.TMP_Text>();
        var descT = panel.Find("SummonSkillDesc")?.GetComponent<TMPro.TMP_Text>();
        var manifestBtn = panel.Find("SummonButton")?.GetComponent<Button>();
        var cancelBtn = panel.Find("CancelButton")?.GetComponent<Button>();

        if (nameT != null)
        {
            nameT.text = summon.specialSkillName;
            summon.ApplyStyleTo(nameT, summon.textStyle);
        }
        if (descT != null)
        {
            descT.text = summon.specialSkillDescription;
            summon.ApplyStyleTo(descT, summon.popupSkillDescStyle);
        }

        if (manifestBtn != null)
        {
            manifestBtn.onClick.RemoveAllListeners();
            manifestBtn.onClick.AddListener(() => OnManifestClicked(summoner, opponent));
        }
        if (cancelBtn != null)
        {
            cancelBtn.onClick.RemoveAllListeners();
            cancelBtn.onClick.AddListener(OnPopupCancelClicked);
        }

        if (manifestBtn == null || cancelBtn == null)
            Debug.LogWarning("[SummonSkillCoordinator] SummonButton/CancelButton not found on popup prefab.");
    }

    private void OnPopupCancelClicked()
    {
        DestroyPopup();
        RefreshButtonInteractables();
        if (_host.CurrentState == GameState.AttackPhase && _host.CurrentTurnOwner == PlayerType.Player)
            _host.EnterAttackPhase();
        else if (_host.CurrentState == GameState.AttackPhase && _host.CurrentTurnOwner == PlayerType.Enemy)
        {
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.SetIntroModeUI(_host.PlayerHand);
        }
    }

    private void OnManifestClicked(PlayerStatus summoner, PlayerStatus opponent)
    {
        if (_manifestationFlowRunning) return;
        DestroyPopup();
        StartManifestationFromBahamutPopup(summoner, opponent);
    }

    private void DestroyPopup()
    {
        if (_popupRoot == null) return;
        Object.Destroy(_popupRoot);
        _popupRoot = null;
    }

    private async Task RunManifestationFlowAsync(PlayerStatus summoner, PlayerStatus opponent)
    {
        try
        {
            BattleUIManager.I?.SetHandClickable(false);
            BattleUIManager.I?.SetUseButtonInteractable(false);

            if (_host.Sequences != null)
                await _host.Sequences.RunManifestationSkillSequenceAsync(summoner, opponent);
        }
        finally
        {
            _manifestationFlowRunning = false;
            RefreshButtonInteractables();
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
}
