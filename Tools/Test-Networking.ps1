[CmdletBinding()]
param(
    [switch]$Offline,
    # Set by Tools/Test-All.ps1 after its single locked restore (with the NuGet advisory check) and solution build:
    # skip this wrapper's own restore and build and test the prebuilt binaries.
    [switch]$NoBuild,
    [ValidateRange(30, 3600)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'TestResults.ps1')
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$reportDirectory = Join-Path $repoRoot "Artifacts/m2-combat-harness/$sourceCommit/test-networking-$runId"
$null = New-Item -ItemType Directory -Path $reportDirectory
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$projectPath = Join-Path $repoRoot 'Tools/Networking.Tests/Networking.Tests.csproj'
$report = [ordered]@{
    schemaVersion = 1
    contractVersion = 'C0-v1'
    fixtureVersion = 'specification-v1.1'
    milestone = 'M2'
    sourceCommit = $sourceCommit
    sourceDirty = [bool](& git -C $repoRoot status --porcelain)
    offlineRestore = [bool]$Offline
    nugetAdvisoryCheck = $(if ($Offline) { 'PendingNetwork: offline restore disables advisory lookup' } else { 'RequestedDuringRestore' })
    dotnetSdk = $null
    result = 'FAIL'
    scope = 'Parked networking (ADR-076) codec/prediction/lag-compensation/session tests only. Not part of the default pure lane; no Unity, player, transport or hardware acceptance.'
    testCount = 0
    executedTestCount = 0
    failedTestCount = 0
    skippedTestCount = 0
    failure = $null
}
function Invoke-CheckedProcess([string]$Name, [string[]]$ProcessArguments) {
    Invoke-ShooterCheckedProcess -Name $Name -Executable $dotnet -ProcessArguments $ProcessArguments -ReportDirectory $reportDirectory -Report $report -WorkingDirectory $repoRoot -TimeoutSeconds $TimeoutSeconds
}
try {
    Write-Host "Parked networking report: $reportDirectory"
    & python (Join-Path $PSScriptRoot 'verify_contract_lock.py') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Frozen C0 identity check failed.' }
    $report.dotnetSdk = (Invoke-CheckedProcess 'sdk' @('--version')).Trim()
    $expectedSdk = (Get-Content -LiteralPath (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
    if ($report.dotnetSdk -ne $expectedSdk) { throw "Expected SDK $expectedSdk; got $($report.dotnetSdk)" }
    if ($NoBuild) { $report.nugetAdvisoryCheck = 'CoveredBy Test-All restore'; $report['prebuilt'] = $true }
    else {
        $restoreArguments = Get-ShooterRestoreArguments $projectPath ([bool]$Offline)
        $null = Invoke-CheckedProcess 'restore' $restoreArguments
        if (!$Offline) { $report.nugetAdvisoryCheck = 'CompletedDuringRestore: all dependency levels' }
    }
    $buildMode = if ($NoBuild) { @('--no-build') } else { @() }
    $null = Invoke-CheckedProcess 'tests' (@('test', $projectPath, '--no-restore', '--configuration', 'Release', '--logger', 'trx;LogFileName=networking.trx', '--results-directory', $reportDirectory) + $buildMode)
    $trxPath = Join-Path $reportDirectory 'networking.trx'
    $summary = Get-StrictTrxSummary -Path $trxPath -SuiteName 'Networking'
    $report.testCount = $summary.Total
    $report.executedTestCount = $summary.Executed
    $report.failedTestCount = $summary.Failed
    $report.skippedTestCount = $summary.Skipped
    $report.result = 'PASS'
}
catch { $report.failure = $_.Exception.Message; Write-Warning $report.failure }
finally {
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reportDirectory 'run.json') -Encoding utf8NoBOM
}
if ($report.result -ne 'PASS') { exit 1 }
Complete-ShooterReportRun $reportDirectory 'test-networking'
Write-Host "PASS: $($report.testCount) parked networking tests. Evidence: $reportDirectory"
