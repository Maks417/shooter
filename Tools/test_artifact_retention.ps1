[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Synthetic regression for ADR-076 run retention (Remove-ShooterStaleRuns, Save-ShooterRun, Assert-ShooterFreeSpace).
# Every run tree below is fake. Nothing outside Artifacts/wrapper-tests and Artifacts/reports/<test prefix> is touched.
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$root = Get-ShooterRoot
$caseRoot = Join-Path $root ('Artifacts/wrapper-tests/retention-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$prefix = 'retention-test-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
$history = Join-Path $root "Artifacts/reports/$prefix"
$failures = [Collections.Generic.List[string]]::new()
function Check([bool]$Condition, [string]$Message) { if (!$Condition) { $failures.Add($Message) } }

function New-FakeRun([string]$Area, [string]$Commit, [string]$Stamp, [string]$Action = $prefix) {
    $path = Join-Path $Area "$Commit/$Action-$Stamp-0000abcd"
    $null = New-Item -ItemType Directory -Path (Join-Path $path 'player') -Force
    [IO.File]::WriteAllText((Join-Path $path 'player/Shooter.exe'), 'x')
    [IO.File]::WriteAllText((Join-Path $path 'run.json'), '{"result":"PASS"}')
    $path
}

$savedKeep = $env:SHOOTER_KEEP_RUNS
$savedFloor = $env:SHOOTER_MIN_FREE_GB
try {
    $commitA = 'a' * 40; $commitB = 'b' * 40
    $area = Join-Path $caseRoot 'area'

    # 1. Keep the current run only; older runs across commits are deleted and their reports archived.
    $env:SHOOTER_KEEP_RUNS = $null
    $old1 = New-FakeRun $area $commitA '20260101T000000000Z'
    $old2 = New-FakeRun $area $commitB '20260102T000000000Z'
    $other = New-FakeRun $area $commitA '20260101T000000000Z' ($prefix + 'x')
    $current = New-FakeRun $area $commitB '20260103T000000000Z'
    Remove-ShooterStaleRuns $area $prefix $current
    Check (Test-Path -LiteralPath $current) 'current run was deleted'
    Check (!(Test-Path -LiteralPath $old1)) 'older run on another commit was kept'
    Check (!(Test-Path -LiteralPath $old2)) 'older run on the same commit was kept'
    Check (Test-Path -LiteralPath $other) 'a run of a different action was deleted'
    Check (Test-Path -LiteralPath (Join-Path $history "$commitA/$(Split-Path -Leaf $old1)/run.json")) 'pruned run report was not archived'
    Check (!(Test-Path -LiteralPath (Join-Path $history "$commitA/$(Split-Path -Leaf $old1)/player"))) 'player payload was archived'

    # 2. A newer run than the kept one still counts toward SHOOTER_KEEP_RUNS.
    $env:SHOOTER_KEEP_RUNS = '2'
    $a = New-FakeRun $area $commitA '20260104T000000000Z'
    $b = New-FakeRun $area $commitA '20260105T000000000Z'
    Remove-ShooterStaleRuns $area $prefix $current
    Check (Test-Path -LiteralPath $current) 'keep path deleted with KEEP_RUNS=2'
    Check (Test-Path -LiteralPath $b) 'newest other run deleted with KEEP_RUNS=2'
    Check (!(Test-Path -LiteralPath $a)) 'run beyond KEEP_RUNS=2 kept'

    # 3. Save-ShooterRun prunes only when the run did not FAIL.
    $env:SHOOTER_KEEP_RUNS = $null
    $failed = New-FakeRun $area $commitA '20260106T000000000Z'
    $run = [pscustomobject]@{ Path = $failed; Report = [ordered]@{ action = $prefix; result = 'FAIL' } }
    Save-ShooterRun $run
    Check (Test-Path -LiteralPath $b) 'a FAIL run pruned the last good run'
    $run.Report.result = 'PendingHardware'
    Save-ShooterRun $run
    Check (!(Test-Path -LiteralPath $b)) 'a non-FAIL run did not prune older runs'
    Check (Test-Path -LiteralPath $failed) 'the saving run deleted itself'

    # 4. Free-space floor.
    $env:SHOOTER_MIN_FREE_GB = '999999'
    $threw = $false
    try { Assert-ShooterFreeSpace $root } catch { $threw = $true }
    Check $threw 'free-space floor did not refuse'
    $env:SHOOTER_MIN_FREE_GB = '0'
    Assert-ShooterFreeSpace $root
}
finally {
    $env:SHOOTER_KEEP_RUNS = $savedKeep
    $env:SHOOTER_MIN_FREE_GB = $savedFloor
    foreach ($path in @($caseRoot, $history)) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force } }
}

if ($failures.Count) { $failures | ForEach-Object { Write-Host "FAIL: $_" }; exit 1 }
Write-Host 'PASS: artifact retention regressions (4 cases).'
