[CmdletBinding()]
param(
    [switch]$Offline,
    [ValidateRange(30, 3600)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'TestResults.ps1')
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$reportDirectory = Join-Path $repoRoot "Artifacts/m8-runtime-composition/$sourceCommit/$runId"
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
    $logPath = Join-Path $reportDirectory "$Name.log"
    $report.commands += [ordered]@{ executable = $dotnet; arguments = $ProcessArguments; log = $logPath }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $dotnet
    $start.WorkingDirectory = $repoRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $ProcessArguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (!$process.Start()) { throw "Cannot start $Name" }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            [IO.File]::WriteAllText($logPath, $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult())
            throw "$Name timed out after $TimeoutSeconds seconds. See $logPath"
        }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText($logPath, $output)
        Write-Host $output
        if ($process.ExitCode -ne 0) { throw "$Name exited $($process.ExitCode). See $logPath" }
        return $output
    }
    finally { $process.Dispose() }
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
    $restoreArgs = @('restore', $projectPath, '--locked-mode')
    if ($Offline) {
        $packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
        if (!(Test-Path -LiteralPath $packageCache -PathType Container)) { throw "Missing package cache: $packageCache" }
        $restoreArgs += @('--source', $packageCache, '-p:NuGetAudit=false')
    }
    else { $restoreArgs += @('-p:NuGetAudit=true', '-p:NuGetAuditMode=all') }
    $null = Invoke-CheckedProcess 'restore' $restoreArgs
    if (!$Offline) { $report.nugetAdvisoryCheck = 'CompletedDuringRestore: all dependency levels' }
    $null = Invoke-CheckedProcess 'tests' @('test', $projectPath, '-c', 'Release', '--no-restore', '--logger', 'trx;LogFileName=game.trx', '--results-directory', $reportDirectory)
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
