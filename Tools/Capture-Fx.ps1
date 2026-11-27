[CmdletBinding()]
param([ValidateRange(60,3600)][int]$TimeoutSeconds=1200)
# ADR-126 editor review diagnostic: renders the licensed fire and smoke effects (grenade blast over time, rifle shot,
# impacts on concrete, dirt and metal, a burning truck) at Low and High. Review evidence only; never visual acceptance.
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity=Get-ShooterUnity
$run=New-ShooterRun 'capture-fx' $unity
try {
    $captureRoot=Join-Path $run.Path 'captures';$proofPath=Join-Path $run.Path 'capture-proof.json'
    Invoke-ShooterEditor $unity $run @('-buildTarget','StandaloneWindows64','-standaloneBuildSubtarget','Player','-quit','-executeMethod','ShooterFxCapture.Capture','-shooterCaptureRoot',$captureRoot,'-shooterReport',$proofPath) $TimeoutSeconds
    $proof=Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    if ($proof.result -cne 'Verified' -or $proof.captures.Count -eq 0 -or $proof.ambientFires -ne 1) { throw 'FX capture proof mismatch.' }
    foreach ($path in $proof.captures) { if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "FX capture missing: $path" } }
    $run.Report['captureRoot']=$captureRoot
    $run.Report['scope']='ADR-126 licensed effect editor diagnostic; not packaged-player look or visual acceptance'
    $run.Report.result='PASS'
}
catch { $run.Report.error=$_.Exception.Message;Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
