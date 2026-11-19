[CmdletBinding()]
param(
    [switch]$Offline,
    # Run the four suites one after another instead of in parallel (for example while profiling).
    [switch]$Sequential,
    [ValidateRange(30, 3600)][int]$TimeoutSeconds = 600
)
# The default pure lane in one call: one locked restore of Tools/Shooter.Pure.slnx (with the NuGet advisory check unless
# -Offline), one Release build, then Test-Contracts, Test-Generation, Test-Simulation and Test-Game with -NoBuild. Each
# suite still writes its own evidence folder and checks its own frozen locks; this wrapper adds a summary run.json.
# Parked networking is not part of this lane (Tools/Test-Networking.ps1).
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'TestResults.ps1')
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$reportDirectory = Join-Path $repoRoot "Artifacts/m0-foundation/$sourceCommit/test-all-$runId"
$null = New-Item -ItemType Directory -Path $reportDirectory
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$solution = Join-Path $PSScriptRoot 'Shooter.Pure.slnx'
$suites = @('Test-Contracts', 'Test-Generation', 'Test-Simulation', 'Test-Game')
$report = [ordered]@{
    schemaVersion = 1
    action = 'Test-All'
    sourceCommit = $sourceCommit
    sourceDirty = [bool](& git -C $repoRoot status --porcelain)
    offlineRestore = [bool]$Offline
    nugetAdvisoryCheck = $(if ($Offline) { 'PendingNetwork: offline restore disables advisory lookup' } else { 'RequestedDuringRestore' })
    dotnetSdk = $null
    parallel = -not $Sequential
    result = 'FAIL'
    scope = 'Default pure lanes (C0, generation, simulation, Game-v1) from one restore and build. No Unity, player or hardware acceptance.'
    commands = @()
    suites = @()
    elapsedSeconds = $null
    failure = $null
}
$clock = [Diagnostics.Stopwatch]::StartNew()
try {
    Write-Host "Test-All report: $reportDirectory"
    $report.dotnetSdk = (Invoke-ShooterCheckedProcess -Name 'sdk' -Executable $dotnet -ProcessArguments @('--version') -ReportDirectory $reportDirectory -Report $report).Trim()
    $expectedSdk = (Get-Content -LiteralPath (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
    if ($report.dotnetSdk -ne $expectedSdk) { throw "Expected SDK $expectedSdk; got $($report.dotnetSdk)" }
    $null = Invoke-ShooterCheckedProcess -Name 'restore' -Executable $dotnet -ProcessArguments (Get-ShooterRestoreArguments $solution ([bool]$Offline)) -ReportDirectory $reportDirectory -Report $report -TimeoutSeconds $TimeoutSeconds
    if (!$Offline) { $report.nugetAdvisoryCheck = 'CompletedDuringRestore: all dependency levels' }
    $null = Invoke-ShooterCheckedProcess -Name 'build' -Executable $dotnet -ProcessArguments @('build', $solution, '--no-restore', '--configuration', 'Release') -ReportDirectory $reportDirectory -Report $report -TimeoutSeconds $TimeoutSeconds

    $pwsh = Join-Path $PSHOME 'pwsh.exe'
    $running = @()
    foreach ($suite in $suites) {
        $arguments = @('-NoProfile', '-File', (Join-Path $PSScriptRoot "$suite.ps1"), '-NoBuild', '-TimeoutSeconds', [string]$TimeoutSeconds)
        if ($Offline) { $arguments += '-Offline' }
        $start = [Diagnostics.ProcessStartInfo]::new($pwsh)
        foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
        $start.WorkingDirectory = $repoRoot
        $start.UseShellExecute = $false
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $process = [Diagnostics.Process]::Start($start)
        $entry = [pscustomobject]@{
            Suite = $suite; Process = $process; Stdout = $process.StandardOutput.ReadToEndAsync(); Stderr = $process.StandardError.ReadToEndAsync()
            Log = (Join-Path $reportDirectory "$suite.log")
        }
        $running += $entry
        if ($Sequential) { $null = $process.WaitForExit($TimeoutSeconds * 1000) }
    }
    foreach ($entry in $running) {
        if (!$entry.Process.WaitForExit($TimeoutSeconds * 1000)) { $entry.Process.Kill($true); $entry.Process.WaitForExit() }
        $seconds = ($entry.Process.ExitTime - $entry.Process.StartTime).TotalSeconds
        $output = $entry.Stdout.GetAwaiter().GetResult() + $entry.Stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText($entry.Log, $output)
        $evidence = ([regex]::Matches($output, 'Evidence: (\S+)') | Select-Object -Last 1)
        $report.suites += [ordered]@{
            suite = $entry.Suite
            exitCode = $entry.Process.ExitCode
            result = $(if ($entry.Process.ExitCode -eq 0) { 'PASS' } else { 'FAIL' })
            seconds = [math]::Round($seconds, 1)
            evidence = $(if ($evidence) { $evidence.Groups[1].Value } else { $null })
            log = $entry.Log
        }
        Write-Host ("{0}: {1} in {2:n1} s" -f $entry.Suite, $report.suites[-1].result, $seconds)
    }
    $failed = @($report.suites | Where-Object { $_.result -ne 'PASS' })
    if ($failed.Count) { throw ('Suites failed: ' + (($failed | ForEach-Object { $_.suite }) -join ', ') + '; see their logs in ' + $reportDirectory) }
    $report.result = 'PASS'
}
catch { $report.failure = $_.Exception.Message; Write-Warning $report.failure }
finally {
    $report.elapsedSeconds = [math]::Round($clock.Elapsed.TotalSeconds, 1)
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reportDirectory 'run.json') -Encoding utf8NoBOM
}
if ($report.result -ne 'PASS') { exit 1 }
Complete-ShooterReportRun $reportDirectory 'test-all'
Write-Host "PASS: $($suites.Count) pure suites in $($report.elapsedSeconds) s. Evidence: $reportDirectory"
