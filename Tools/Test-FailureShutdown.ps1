[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildRunDirectory,
    [ValidateRange(1,3)][int]$Attempts = 3)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$buildRoot = (Resolve-Path -LiteralPath $BuildRunDirectory).Path
$manifestPath = Join-Path $buildRoot 'build-manifest.json'
$build = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($build.result -cne 'Succeeded' -or $build.target -cne 'Client' -or $build.backend -cne 'IL2CPP' -or $build.development -or $build.toolchainHash -cne $unity.LockHash) { throw 'A matching release IL2CPP client is required.' }
if ((Get-ShooterHash (Join-Path $buildRoot 'harness-manifest.json')) -cne $build.contentManifestHash) { throw 'Harness manifest mismatch.' }
$run = New-ShooterRun 'failure-shutdown' $unity
try {
    Assert-ShooterPackedProvenance $build
    $player = Join-Path $buildRoot 'player/Shooter.exe'
    $run.Report['attempts'] = @()
    $run.Report['buildManifestSha256'] = Get-ShooterHash $manifestPath
    $run.Report['scope'] = 'Injected diagnostic failure and native shutdown only; never B01 acceptance or proof of the historical driver-hang cause'
    for ($index = 1; $index -le $Attempts; $index++) {
        $directory = Join-Path $run.Path "attempt-$index"
        $null = New-Item -ItemType Directory -Path $directory
        $log = Join-Path $directory 'player.log'
        $arguments = @('-force-d3d11', '-logFile', $log, '-shooterProfile', 'Low', '-shooterBenchmark', 'B01-minimal-v1',
            '-shooterBenchmarkOutput', $directory, '-shooterBenchmarkDiagnostic', 'true', '-shooterDiagnosticOverload', 'true')
        $run.Report.commands += [ordered]@{ executable = $player; arguments = $arguments; expectedExitCode = 1; timeoutSeconds = 30 }
        $watch = [Diagnostics.Stopwatch]::StartNew()
        Invoke-ShooterProcess -Executable $player -ProcessArguments $arguments -WorkingDirectory (Split-Path -Parent $player) -LogPath (Join-Path $directory 'process.log') -TimeoutSeconds 30 -ExpectedExitCode 1
        $watch.Stop()
        $text = Get-Content -LiteralPath $log -Raw
        foreach ($marker in @('SHOOTER_READY_CLIENT', 'SHOOTER_DIAGNOSTIC_OVERLOAD_INJECTED', 'Offline tick overload;', 'SHOOTER_BENCHMARK_ABORTED')) {
            if (!$text.Contains($marker)) { throw "Missing failure-path marker: $marker" }
        }
        if ($text.Contains('SHOOTER_BENCHMARK_COMPLETE') -or $text.Contains('SHOOTER_CLEANUP_FAILURE')) { throw 'Invalid successful completion or failed cleanup.' }
        foreach ($name in @('capture.json', 'frames.csv', 'ticks.csv', 'benchmark-result.json')) {
            if (Test-Path -LiteralPath (Join-Path $directory $name)) { throw "Failed capture published $name" }
        }
        $failure = Get-Content -LiteralPath (Join-Path $directory 'failure.json') -Raw | ConvertFrom-Json
        if ($failure.result -cne 'FAIL' -or !$failure.diagnostic -or !$failure.injectedOverload -or
            $failure.completedTicks -lt 120 -or $failure.lastAdvanceTicks -ne 4 -or
            $failure.elapsedSeconds -lt 0.2 -or $failure.debtSeconds -lt (1.0 / 60)) { throw 'Failure telemetry did not prove retained-debt overload.' }
        $run.Report.attempts += [ordered]@{ attempt = $index; result = 'PASS'; elapsedSeconds = $watch.Elapsed.TotalSeconds; failure = $failure }
    }
    $run.Report.result = 'PASS'
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
