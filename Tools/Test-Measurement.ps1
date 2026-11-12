[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildRunDirectory)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$run = New-ShooterRun 'measurement-controls' $unity
try {
    $run.Report['scope'] = 'Real release-player positive controls and diagnostic collection only; not full B01/hardware acceptance'
    $run.Report['captures'] = @()
    foreach ($inject in @($false, $true)) {
        $argsForWrapper = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'Benchmark.ps1'), '-BuildRunDirectory', (Resolve-Path -LiteralPath $BuildRunDirectory).Path, '-Diagnostic')
        if ($inject) { $argsForWrapper += '-DiagnosticAllocate' }
        $run.Report.commands += [ordered]@{ executable=(Join-Path $PSHOME 'pwsh.exe'); arguments=$argsForWrapper }
        & (Join-Path $PSHOME 'pwsh.exe') @argsForWrapper
        if ($LASTEXITCODE -ne 0) { throw 'Native diagnostic capture failed.' }
        $base = Join-Path (Get-ShooterRoot) ('Artifacts/m0-foundation/' + (git rev-parse HEAD).Trim())
        $capture = Get-ChildItem -LiteralPath $base -Directory -Filter 'benchmark-diagnostic-*' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $meta = Get-Content -LiteralPath (Join-Path $capture.FullName 'capture.json') -Raw | ConvertFrom-Json
        if ($meta.collectorVersion -ne 2 -or !$meta.frameAllocationAvailable -or !$meta.tickAllocationAvailable -or $meta.allocationMainControlBytes -lt 4352 -or $meta.allocationWorkerControlBytes -lt 4352 -or $meta.allocationEndControlBytes -lt 4096 -or $meta.tickAllocationEndControlBytes -lt 4096) { throw 'Release allocation positive controls are unavailable or failed.' }
        $samples = @(Import-Csv -LiteralPath (Join-Path $capture.FullName 'frames.csv'))
        $bytes = @($samples | ForEach-Object { [long]$_.managedFrameBytes })
        if (@($bytes | Where-Object { $_ -lt 0 }).Count) { throw 'Calibrated whole-frame allocation has missing samples.' }
        $maximum = ($bytes | Measure-Object -Maximum).Maximum
        if ($inject -and $maximum -lt 4096) { throw 'Known in-frame allocation was not detected.' }
        $run.Report.captures += [ordered]@{ directory=$capture.FullName; injected=$inject; maximumFrameAllocationBytes=$maximum; frames=$samples.Count; mappedTimings=$meta.mappedTimings; ambiguousTimings=$meta.ambiguousTimings }
    }
    $run.Report.result = 'PASS'
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
