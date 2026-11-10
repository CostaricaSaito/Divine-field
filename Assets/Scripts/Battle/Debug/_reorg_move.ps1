# One-shot Battle folder reorg. Moves .cs + .meta only (no deletes).
$base = "c:\Users\コスタリカ斎藤\Divine2\Assets\Scripts\Battle"

function New-FolderMeta($path, $guid) {
    if (Test-Path "$path.meta") { return }
    @"
fileFormatVersion: 2
guid: $guid
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@ | Set-Content -Path "$path.meta" -Encoding utf8NoBOM
}

function Move-Pair($name, $sub) {
    $destDir = Join-Path $base $sub
    $from = Join-Path $base $name
    $fromMeta = "$from.meta"
    if (-not (Test-Path $from)) { Write-Warning "Missing $name"; return }
    Move-Item -LiteralPath $from -Destination $destDir -Force
    if (Test-Path $fromMeta) { Move-Item -LiteralPath $fromMeta -Destination $destDir -Force }
    Write-Host "  $sub\$name"
}

$folderGuids = @{
    Core = "a1b2c3d4e5f6478990a1b2c3d4e5f601"
    Bootstrap = "b2c3d4e5f6478990a1b2c3d4e5f60201"
    Phase = "c3d4e5f6478990a1b2c3d4e5f6030101"
    Combat = "d4e5f6478990a1b2c3d4e5f604010101"
    Hand = "e5f6478990a1b2c3d4e5f60501010101"
    Economy = "f6478990a1b2c3d4e5f6060101010101"
    Player = "06478990a1b2c3d4e5f6070101010101"
    Coordination = "1748990a1b2c3d4e5f6080101010101"
    Rules = "284990a1b2c3d4e5f609010101010101"
    Sequence = "394a90a1b2c3d4e5f60a010101010101"
    Magic = "4a5b90a1b2c3d4e5f60b010101010101"
    Online = "5b6c90a1b2c3d4e5f60c010101010101"
    Audio = "6c7d90a1b2c3d4e5f60d010101010101"
    Debug = "7d8e90a1b2c3d4e5f60e010101010101"
    Hosts = "8e9f90a1b2c3d4e5f60f010101010101"
}

foreach ($kv in $folderGuids.GetEnumerator()) {
    $dir = Join-Path $base $kv.Key
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    New-FolderMeta $dir $kv.Value
}

$moves = @{
    Core = @(
        "BattleManager.cs", "BattleManager.CombatSnapshots.cs", "BattleManager.UltimateReady.cs",
        "GameState.cs", "PlayerStatus.cs", "CombatSnapshotStore.cs", "IBattleContext.cs"
    )
    Bootstrap = @("BattleBootstrap.cs")
    Phase = @("BattlePhaseController.cs")
    Combat = @(
        "BattleProcessor.cs", "ElementHelper.cs", "HitRateApplicability.cs", "HitRateRules.cs",
        "CombatDefensePowerResolver.cs"
    )
    Hand = @(
        "HandRefillService.cs", "HandRefillService.Roster.cs", "CardDealer.cs", "CardDefinitionIdentity.cs",
        "HandCapacityRules.cs", "HandDestroyRules.cs", "HandDestroyService.cs", "HandRosterWatch.cs"
    )
    Economy = @(
        "BuyFeature.cs", "SellFeature.cs", "ExchangeFeature.cs", "EconomicAction.cs", "EconomicActionNames.cs"
    )
    Player = @(
        "PlayerInputController.cs", "PlayerDefenseInteractivityService.cs", "PrayerFlow.cs"
    )
    Coordination = @(
        "BattleOpeningCoordinator.cs", "GameEndOrchestrator.cs", "SummonSkillCoordinator.cs",
        "AdHocDefenseCoordinator.cs", "AdHocDefenseSession.cs", "DualBladeDefenseCoordinator.cs",
        "BattleExitCoordinator.cs"
    )
    Rules = @("CardRule.cs", "DisadvantageRules.cs")
    Sequence = @("CardSequenceManager.cs")
    Magic = @("MagicPoolManager.cs")
    Online = @("OnlineBattleSyncService.cs")
    Audio = @(
        "SoundEffectPlayer.cs", "SoundEffectCatalogSO.cs",
        "BattleBgmController.cs", "BattleBgmPlaylistSO.cs", "BattleBgmTitleView.cs"
    )
    Debug = @("BattleDebugTools.cs")
    Hosts = @(
        "IAdHocDefenseHost.cs", "IBattleBootstrapHost.cs", "IBattleOpeningHost.cs",
        "IBattlePhaseControllerHost.cs", "IBattlePhaseHost.cs", "IDualBladeDefenseHost.cs",
        "IGameEndOrchestratorHost.cs", "IOnlineBattleSyncHost.cs",
        "IPlayerDefenseInteractivityHost.cs", "IPlayerInputHost.cs", "ISummonSkillHost.cs"
    )
}

foreach ($sub in $moves.Keys) {
    Write-Host "[$sub]"
    foreach ($f in $moves[$sub]) { Move-Pair $f $sub }
}

$left = Get-ChildItem $base -File -Filter *.cs
if ($left.Count -gt 0) {
    Write-Host "`nRemaining at Battle root:"
    $left | ForEach-Object { $_.Name }
}
