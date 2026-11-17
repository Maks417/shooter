[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BuildRunDirectory,
    [Parameter(Mandatory)][string]$SensorLog,
    [string]$MapCacheRoot = '',
    [string]$HardwareEvidence = '',
    [ValidateRange(0,1800)][int]$PreconditionSeconds = 1200,
    [switch]$PlanOnly
)
<#
.SYNOPSIS
    Orchestrates FIX12 twelve-capture matrix: both maps × both sizes × three runs.
.NOTES
    Reference-machine execution remains PendingHardware. This wrapper records the exact command plan
    and optionally launches Benchmark-Series for each measurement workload. Does not alter workloads.
#>
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$run = New-ShooterRun 'benchmark-product-matrix' $unity
$workloads = @('TDM-5v5-v1','TDM-15v15-v1','TDM-5v5-depot-v1','TDM-15v15-depot-v1','TDM-5v5-oil-v1','TDM-15v15-oil-v1')
$build = (Resolve-Path -LiteralPath $BuildRunDirectory).Path
$sensor = (Resolve-Path -LiteralPath $SensorLog).Path
$run.Report['scope'] = 'FIX12 product matrix: 6 workloads × 3 runs = 18 captures (min/max size per map, ADR-101); hardware acceptance PendingHardware until reference proof.'
$run.Report['buildDirectory'] = $build
$run.Report['sensorLog'] = $sensor
$run.Report['workloads'] = $workloads
$run.Report['expectedCaptures'] = 18
$run.Report['warmupSeconds'] = 60
$run.Report['captureSeconds'] = 180
$run.Report['profile'] = 'Low'
$run.Report['internalResolution'] = @(1280, 720)
$run.Report['attempts'] = @()
$run.Report.result = 'Planning'
try {
    if (!$MapCacheRoot) { $MapCacheRoot = Join-Path (Get-ShooterRoot) 'Artifacts/map-packages' }
    if (!(Test-Path -LiteralPath $MapCacheRoot -PathType Container)) { throw 'Verified map package cache missing; run Tools/Compile-GameContent.ps1.' }
    $run.Report['mapCacheRoot'] = (Resolve-Path -LiteralPath $MapCacheRoot).Path
    Save-ShooterRun $run
    if ($PlanOnly) {
        $run.Report.result = 'PendingHardware'
        $run.Report['planOnly'] = $true
        $run.Report['statusNote'] = 'Command plan recorded; reference-machine twelve captures not executed.'
        Save-ShooterRun $run
        Write-Host "FIX12 matrix plan recorded at $($run.Path); PendingHardware."
        exit 0
    }
    foreach ($workload in $workloads) {
        $argsForSeries = @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Benchmark-Series.ps1'),
            '-BuildRunDirectory',$build,'-SensorLog',$sensor,'-Workload',$workload,
            '-MapCacheRoot',$run.Report.mapCacheRoot,'-PreconditionSeconds',$PreconditionSeconds)
        if ($HardwareEvidence) { $argsForSeries += @('-HardwareEvidence',(Resolve-Path -LiteralPath $HardwareEvidence).Path) }
        $run.Report.commands += [ordered]@{ executable=(Join-Path $PSHOME 'pwsh.exe'); arguments=$argsForSeries }
        Save-ShooterRun $run
        $started = [DateTime]::UtcNow.ToString('o')
        & (Join-Path $PSHOME 'pwsh.exe') @argsForSeries 2>&1 | Tee-Object -FilePath (Join-Path $run.Path "$workload-console.log")
        $code = $LASTEXITCODE
        $run.Report.attempts += [ordered]@{ workload=$workload; wrapperExitCode=$code; startedUtc=$started; endedUtc=[DateTime]::UtcNow.ToString('o') }
        Save-ShooterRun $run
        if ($code -ne 0) { throw "Series failed for $workload (exit $code)." }
    }
    $run.Report.result = 'PendingHardware'
    $run.Report['collectionComplete'] = $true
    $run.Report['statusNote'] = 'Twelve capture wrappers completed collection path; thermal/reference gate remains PendingHardware.'
}
catch {
    $run.Report.result = 'FAIL'
    $run.Report.error = $_.Exception.Message
    Write-Warning $run.Report.error
}
finally { Save-ShooterRun $run }
if ($run.Report.result -eq 'FAIL') { exit 1 }
