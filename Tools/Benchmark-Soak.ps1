[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BuildRunDirectory,
    [ValidateSet('TDM-soak-6v6-compound-v1','TDM-soak-6v6-depot-v1')][string]$Workload = 'TDM-soak-6v6-compound-v1',
    [string]$MapCacheRoot = '',
    [string]$HardwareEvidence = '',
    [switch]$PlanOnly
)
<#
.SYNOPSIS
    FIX12 normal-AI 6v6 soak (≥30 minutes / two matches+replay intent) per map.
.NOTES
    Reference execution remains PendingHardware. Does not claim PASS without a completed soak capture.
#>
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$run = New-ShooterRun 'benchmark-soak' $unity
$build = (Resolve-Path -LiteralPath $BuildRunDirectory).Path
$run.Report['scope'] = 'FIX12/T070 ≥30-minute normal-AI 6v6 soak; ProductTdmHost finish/results/replay across normal match rules; PendingHardware until reference run.'
$run.Report['workloadId'] = $Workload
$run.Report['buildDirectory'] = $build
$run.Report['captureSeconds'] = 1800
$run.Report['matchesRequired'] = 2
$run.Report['replayRequired'] = $true
$run.Report['collectorVersion'] = 5
$run.Report['gateStatus'] = 'PendingHardware'
try {
    if (!$MapCacheRoot) { $MapCacheRoot = Join-Path (Get-ShooterRoot) 'Artifacts/map-packages' }
    if (!(Test-Path -LiteralPath $MapCacheRoot -PathType Container)) { throw 'Verified map package cache missing; run Tools/Compile-GameContent.ps1.' }
    $run.Report['mapCacheRoot'] = (Resolve-Path -LiteralPath $MapCacheRoot).Path
    if ($PlanOnly) {
        $run.Report.result = 'PendingHardware'
        $run.Report['planOnly'] = $true
        $run.Report['statusNote'] = 'Soak command plan recorded; reference-machine soak not executed.'
        Save-ShooterRun $run
        Write-Host "FIX12 soak plan recorded at $($run.Path); PendingHardware."
        exit 0
    }
    $argsForWrapper = @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Benchmark.ps1'),
        '-BuildRunDirectory',$build,'-Workload',$Workload,'-RunIndex',1,'-PresentMon',
        '-MapCacheRoot',$run.Report.mapCacheRoot)
    if ($HardwareEvidence) { $argsForWrapper += @('-HardwareEvidence',(Resolve-Path -LiteralPath $HardwareEvidence).Path) }
    $run.Report.commands += [ordered]@{ executable=(Join-Path $PSHOME 'pwsh.exe'); arguments=$argsForWrapper }
    Save-ShooterRun $run
    & (Join-Path $PSHOME 'pwsh.exe') @argsForWrapper 2>&1 | Tee-Object -FilePath (Join-Path $run.Path 'soak-console.log')
    if ($LASTEXITCODE -ne 0) { throw "Soak capture wrapper failed ($LASTEXITCODE)." }
    $run.Report.result = 'PendingHardware'
    $run.Report['collectionComplete'] = $true
}
catch {
    $run.Report.result = 'FAIL'
    $run.Report.error = $_.Exception.Message
    Write-Warning $run.Report.error
}
finally { Save-ShooterRun $run }
if ($run.Report.result -eq 'FAIL') { exit 1 }
