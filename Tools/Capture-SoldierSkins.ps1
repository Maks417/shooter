[CmdletBinding()]
param([ValidateRange(60,3600)][int]$TimeoutSeconds=1500)
# ADR-124 editor review diagnostic: renders every licensed authored soldier variant in both team kits (aim, jog,
# crouch, head close-up) with stand-in materials. Review evidence only; never P05/REL03 acceptance.
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity=Get-ShooterUnity
$run=New-ShooterRun 'capture-soldier-skins' $unity
try {
    $captureRoot=Join-Path $run.Path 'captures';$proofPath=Join-Path $run.Path 'capture-proof.json'
    Invoke-ShooterEditor $unity $run @('-buildTarget','StandaloneWindows64','-standaloneBuildSubtarget','Player','-quit','-executeMethod','ShooterSoldierSkinCapture.Capture','-shooterCaptureRoot',$captureRoot,'-shooterReport',$proofPath) $TimeoutSeconds
    $proof=Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    if ($proof.result -cne 'Verified' -or $proof.captures.Count -eq 0) { throw 'Soldier skin capture proof mismatch.' }
    foreach ($path in $proof.captures) { if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Soldier skin capture missing: $path" } }
    $run.Report['captureRoot']=$captureRoot
    $run.Report['scope']='ADR-124 authored soldier editor diagnostic; not packaged-player look or visual acceptance'
    $run.Report.result='PASS'
}
catch { $run.Report.error=$_.Exception.Message;Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
