[CmdletBinding()]
param(
    [switch]$Offline,
    [ValidateRange(30, 3600)][int]$TimeoutSeconds = 300,
    [string]$SamplesPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
$runId = 'weapon-lod-transition-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$reportDirectory = Join-Path $repoRoot "Artifacts/m1-procedural-feasibility/$sourceCommit/$runId"
$null = New-Item -ItemType Directory -Path $reportDirectory -Force
if ($SamplesPath -and !(Test-Path -LiteralPath $SamplesPath -PathType Leaf)) {
    $early = [ordered]@{
        schemaVersion = 1
        scope = 'M1 P03 formal weapon LOD transition numeric evidence. Does not establish silhouette/ADS visual acceptance.'
        sourceCommit = $sourceCommit
        result = 'FAIL'
        numericPolicyResult = 'FAIL'
        visualCaptureResult = 'Pending'
        visualCaptureNote = 'No Unity silhouette/ADS transition images produced. Integrator must wire capture and formal visual sign-off separately.'
        failure = "SamplesPath not found: $SamplesPath"
    }
    $early | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $reportDirectory 'run.json') -Encoding utf8NoBOM
    Write-Warning $early.failure
    exit 1
}
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$python = (Get-Command python -ErrorAction Stop).Source
$projectPath = Join-Path $repoRoot 'Tools/Generation.Tests/Generation.Tests.csproj'
$analyzer = Join-Path $PSScriptRoot 'weapon_lod_transition.py'
$report = [ordered]@{
    schemaVersion = 1
    scope = 'M1 P03 formal weapon LOD transition numeric evidence. Does not establish silhouette/ADS visual acceptance.'
    sourceCommit = $sourceCommit
    sourceDirty = [bool](& git -C $repoRoot status --porcelain)
    toolchain = [ordered]@{
        note = 'Offline-friendly pure numeric harness around ScreenHeightLod (10% hysteresis). No PresentMon/GPU/Unity editor required.'
        dotnetSdk = $null
        python = (& $python --version 2>&1 | Out-String).Trim()
        offlineRestore = [bool]$Offline
    }
    result = 'FAIL'
    numericPolicyResult = 'FAIL'
    visualCaptureResult = 'Pending'
    visualCaptureNote = 'No Unity silhouette/ADS transition images produced. Integrator must wire capture and formal visual sign-off separately.'
    testFilter = 'FullyQualifiedName~WeaponLodTransitionTests'
    testCount = 0
    executedTestCount = 0
    failedTestCount = 0
    failedTests = @()
    samplesPath = $null
    analysisPath = $null
    failure = $null
    commands = @()
}
function Invoke-CheckedProcess([string]$Name, [string]$Executable, [string[]]$ProcessArguments) {
    $logPath = Join-Path $reportDirectory "$Name.log"
    $report.commands += [ordered]@{ executable = $Executable; arguments = $ProcessArguments; log = $logPath }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
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
    Write-Host "Weapon LOD transition evidence: $reportDirectory"
    $report.toolchain.dotnetSdk = (Invoke-CheckedProcess 'sdk' $dotnet @('--version')).Trim()
    $expectedSdk = (Get-Content -LiteralPath (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
    if ($report.toolchain.dotnetSdk -ne $expectedSdk) { throw "Expected SDK $expectedSdk; got $($report.toolchain.dotnetSdk)" }
    $restoreArguments = @('restore', $projectPath, '--locked-mode')
    if ($Offline) {
        $packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
        if (!(Test-Path -LiteralPath $packageCache -PathType Container)) { throw "Missing package cache: $packageCache" }
        $restoreArguments += @('--source', $packageCache, '-p:NuGetAudit=false')
    }
    $null = Invoke-CheckedProcess 'restore' $dotnet $restoreArguments
    $null = Invoke-CheckedProcess 'tests' $dotnet @(
        'test', $projectPath, '--no-restore', '--configuration', 'Release',
        '--filter', $report.testFilter,
        '--logger', 'trx;LogFileName=weapon-lod-transition.trx',
        '--results-directory', $reportDirectory
    )
    $trxPath = Join-Path $reportDirectory 'weapon-lod-transition.trx'
    if (!(Test-Path -LiteralPath $trxPath -PathType Leaf)) { throw 'Fresh weapon LOD transition TRX was not produced.' }
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    $report.testCount = [int]$counters.total
    $report.executedTestCount = [int]$counters.executed
    $report.failedTestCount = [int]$counters.failed
    $report.failedTests = @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -ne 'Passed' | ForEach-Object testName)
    if ([int]$counters.total -le 0 -or [int]$counters.failed -ne 0 -or [int]$counters.executed -ne [int]$counters.total) {
        throw 'Weapon LOD transition pure tests failed, skipped, or did not run.'
    }

    $samplesOut = Join-Path $reportDirectory 'samples.json'
    $analysisOut = Join-Path $reportDirectory 'analysis.json'
    if ($SamplesPath) {
        if (!(Test-Path -LiteralPath $SamplesPath -PathType Leaf)) { throw "SamplesPath not found: $SamplesPath" }
        Copy-Item -LiteralPath $SamplesPath -Destination $samplesOut -Force
        $null = Invoke-CheckedProcess 'analyze' $python @($analyzer, '--input', $samplesOut, '--output', $analysisOut)
    }
    else {
        $null = Invoke-CheckedProcess 'analyze' $python @($analyzer, '--emit-sweep', $samplesOut, '--output', $analysisOut)
    }
    $report.samplesPath = $samplesOut
    $report.analysisPath = $analysisOut
    $analysis = Get-Content -LiteralPath $analysisOut -Raw | ConvertFrom-Json
    $report.numericPolicyResult = [string]$analysis.numericPolicyResult
    $report.visualCaptureResult = [string]$analysis.visualCaptureResult
    if ($analysis.result -cne 'PASS' -or $report.numericPolicyResult -cne 'PASS') {
        throw 'LOD transition analyzer reported numeric policy failure.'
    }
    # Overall PASS only for numeric policy. Visual capture stays Pending and must not be faked.
    $report.result = 'PASS'
}
catch {
    $report.failure = $_.Exception.Message
    Write-Warning $report.failure
}
finally {
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reportDirectory 'run.json') -Encoding utf8NoBOM
}
if ($report.result -ne 'PASS') { exit 1 }
Write-Host "PASS (numeric): $($report.testCount) WeaponLodTransitionTests; visual capture Pending. Evidence: $reportDirectory"
