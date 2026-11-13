[CmdletBinding()]
param(
    [switch]$Offline,
    [ValidateRange(30, 3600)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
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
    result = 'FAIL'
    scope = 'Pure Game-v1 contract/session/TDM reducer tests. No Unity, player or hardware acceptance.'
    testCount = 0
    failure = $null
}
try {
    Write-Host "Game report: $reportDirectory"
    & python (Join-Path $PSScriptRoot 'verify_game_contract_lock.py') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Frozen Game-v1 identity check failed.' }
    & python (Join-Path $PSScriptRoot 'verify_contract_lock.py') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Frozen C0 identity check failed.' }
    $restoreArgs = @('restore', $projectPath)
    if ($Offline) { $restoreArgs += '--locked-mode' }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $dotnet
    $start.WorkingDirectory = $repoRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($a in $restoreArgs) { $start.ArgumentList.Add($a) }
    $p = [Diagnostics.Process]::new(); $p.StartInfo = $start
    if (!$p.Start()) { throw 'restore start failed' }
    $out = $p.StandardOutput.ReadToEnd() + $p.StandardError.ReadToEnd()
    $p.WaitForExit($TimeoutSeconds * 1000) | Out-Null
    [IO.File]::WriteAllText((Join-Path $reportDirectory 'restore.log'), $out)
    if ($p.ExitCode -ne 0) { throw "restore failed: $out" }
    $p.Dispose()

    $testArgs = @('test', $projectPath, '-c', 'Release', '--no-restore', '--logger', 'trx;LogFileName=game.trx', '--results-directory', $reportDirectory)
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $dotnet
    $start.WorkingDirectory = $repoRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($a in $testArgs) { $start.ArgumentList.Add($a) }
    $p = [Diagnostics.Process]::new(); $p.StartInfo = $start
    if (!$p.Start()) { throw 'test start failed' }
    $out = $p.StandardOutput.ReadToEnd() + $p.StandardError.ReadToEnd()
    if (!$p.WaitForExit($TimeoutSeconds * 1000)) { $p.Kill($true); throw 'test timeout' }
    [IO.File]::WriteAllText((Join-Path $reportDirectory 'test.log'), $out)
    Write-Host $out
    if ($p.ExitCode -ne 0) { throw "tests failed: see $reportDirectory" }
    $p.Dispose()
    if ($out -match 'Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+)') {
        $report.testCount = [int]$Matches[2]
        if ([int]$Matches[1] -ne 0) { throw 'nonzero failures' }
    }
    $report.result = 'PASS'
}
catch {
    $report.failure = "$_"
    $report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $reportDirectory 'validation.json') -Encoding UTF8
    throw
}
$report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $reportDirectory 'validation.json') -Encoding UTF8
Write-Host ($report | ConvertTo-Json -Depth 6)
if ($report.result -ne 'PASS') { exit 1 }
