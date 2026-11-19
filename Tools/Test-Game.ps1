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
$reportDirectory = Join-Path $repoRoot "Artifacts/m8-runtime-composition/$sourceCommit/test-game-$runId"
$null = New-Item -ItemType Directory -Path $reportDirectory -Force
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$projectPath = Join-Path $repoRoot 'Tools/Game.Contracts.Tests/Game.Contracts.Tests.csproj'
$report = [ordered]@{
    schemaVersion = 1
    contractVersion = 'Game-v1'
    dependsOnContract = 'C0-v1'
    sourceCommit = $sourceCommit
    sourceDirty = [bool](& git -C $repoRoot status --porcelain)
    offlineRestore = [bool]$Offline
    nugetAdvisoryCheck = $(if ($Offline) { 'PendingNetwork: offline restore disables advisory lookup' } else { 'RequestedDuringRestore' })
    dotnetSdk = $null
    result = 'FAIL'
    scope = 'Pure Game-v1 contract/session/TDM reducer tests. No Unity, player or hardware acceptance.'
    testCount = 0
    executedTestCount = 0
    failedTestCount = 0
    skippedTestCount = 0
    failure = $null
    commands = @()
}
function Invoke-CheckedProcess([string]$Name, [string[]]$ProcessArguments) {
    Invoke-ShooterCheckedProcess -Name $Name -Executable $dotnet -ProcessArguments $ProcessArguments -ReportDirectory $reportDirectory -Report $report -WorkingDirectory $repoRoot -TimeoutSeconds $TimeoutSeconds
}
try {
    Write-Host "Game report: $reportDirectory"
    & python (Join-Path $PSScriptRoot 'verify_game_contract_lock.py') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Frozen Game-v1 identity check failed.' }
    & python (Join-Path $PSScriptRoot 'verify_contract_lock.py') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Frozen C0 identity check failed.' }
    $report.dotnetSdk = (Invoke-CheckedProcess 'sdk' @('--version')).Trim()
    $expectedSdk = (Get-Content -LiteralPath (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
    if ($report.dotnetSdk -ne $expectedSdk) { throw "Expected SDK $expectedSdk; got $($report.dotnetSdk)" }
    if ($NoBuild) { $report.nugetAdvisoryCheck = 'CoveredBy Test-All restore'; $report['prebuilt'] = $true }
    else {
        $restoreArgs = Get-ShooterRestoreArguments $projectPath ([bool]$Offline)
        $null = Invoke-CheckedProcess 'restore' $restoreArgs
        if (!$Offline) { $report.nugetAdvisoryCheck = 'CompletedDuringRestore: all dependency levels' }
    }
    $buildMode = if ($NoBuild) { @('--no-build') } else { @() }
    $null = Invoke-CheckedProcess 'tests' (@('test', $projectPath, '-c', 'Release', '--no-restore', '--logger', 'trx;LogFileName=game.trx', '--results-directory', $reportDirectory) + $buildMode)
    $summary = Get-StrictTrxSummary -Path (Join-Path $reportDirectory 'game.trx') -SuiteName 'Game'
    $report.testCount = $summary.Total
    $report.executedTestCount = $summary.Executed
    $report.failedTestCount = $summary.Failed
    $report.skippedTestCount = $summary.Skipped
    $report.result = 'PASS'
}
catch {
    $report.failure = $_.Exception.Message
    Write-Warning $report.failure
}
finally {
    $report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $reportDirectory 'validation.json') -Encoding utf8NoBOM
}
Write-Host ($report | ConvertTo-Json -Depth 6)
if ($report.result -ne 'PASS') { exit 1 }
Complete-ShooterReportRun $reportDirectory 'test-game'
Write-Host "PASS: $($report.testCount) Game-v1 tests. Evidence: $reportDirectory"
