[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildRunDirectory, [Parameter(Mandatory)][string]$CacheRoot,
    [ValidateSet('walk','run','crouch','aim','reload','reload-down','reload-up','slope','steps','turn')][string]$Scenario='walk')
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity=Get-ShooterUnity
$buildRoot=(Resolve-Path -LiteralPath $BuildRunDirectory).Path
$build=Get-Content -LiteralPath (Join-Path $buildRoot 'build-manifest.json') -Raw | ConvertFrom-Json
if ($build.result -cne 'Succeeded' -or $build.target -cne 'Client' -or $build.backend -cne 'IL2CPP' -or $build.development -or $build.toolchainHash -cne $unity.LockHash) { throw 'Locked IL2CPP release client required.' }
Assert-ShooterPackedProvenance $build
$CacheRoot=(Resolve-Path -LiteralPath $CacheRoot).Path
$run=New-ShooterRun 'measure-eight-rigs' $unity
try {
    $proofPath=Join-Path $run.Path 'rig-proof.json';$log=Join-Path $run.Path 'player.log'
    $player=Join-Path $buildRoot 'player/Shooter.exe'
    $arguments=@('-batchmode','-force-d3d11','-logFile',$log,'-shooterProfile','Low','-shooterSoldierGallery',$CacheRoot,
        '-shooterContentToolchainHash',$unity.LockHash,'-shooterRigReport',$proofPath,'-shooterRigScenario',$Scenario,'-shooterSourceCommit',$build.sourceCommit)
    Invoke-ShooterProcess -Executable $player -ProcessArguments $arguments -WorkingDirectory (Split-Path -Parent $player) -LogPath (Join-Path $run.Path 'process.log') -TimeoutSeconds 60
    $proof=Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    if ($proof.result -cne 'Verified' -or $proof.workload -cne 'M1-eight-rig-diagnostic-v1' -or $proof.rigs -ne 8 -or $proof.samples -lt 60 -or $proof.toolchainHash -cne $unity.LockHash -or $proof.sourceCommit -cne $build.sourceCommit -or $proof.scenario -cne $Scenario) { throw 'Incomplete eight-rig diagnostic.' }
    if ((Get-ShooterHash (Join-Path $CacheRoot 'soldier-gallery.json')) -cne $proof.contentPlanHash) { throw 'Rig content plan changed.' }
    if (!(Select-String -LiteralPath $log -SimpleMatch 'SHOOTER_SOLDIER_GALLERY_READY rigs=8 joints=23 lods=3' -Quiet) -or !(Select-String -LiteralPath $log -SimpleMatch 'SHOOTER_RIG_DIAGNOSTIC_COMPLETE' -Quiet) -or (Select-String -LiteralPath $log -SimpleMatch 'SHOOTER_FAILURE' -Quiet)) { throw 'Rig lifecycle failed.' }
    $run.Report['scope']=$proof.scope
    $run.Report['rigSamplesSha256']=Get-ShooterHash $proof.rawCsv
    $run.Report['gateStatus']=$proof.gateStatus
    $run.Report['gripWithinTwoCentimeters']=$proof.gripWithinTwoCentimeters
    $run.Report['plantSlideWithinFiveCentimeters']=$proof.plantSlideWithinFiveCentimeters
    $run.Report.result='PASS'
}
catch { $run.Report.error=$_.Exception.Message;Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
