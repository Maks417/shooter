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
$reportDirectory = Join-Path $repoRoot "Artifacts/m1-procedural-feasibility/$sourceCommit/$runId"
$null = New-Item -ItemType Directory -Path $reportDirectory
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$projectPath = Join-Path $repoRoot 'Tools/Generation.Tests/Generation.Tests.csproj'
$report = [ordered]@{
    schemaVersion = 1
    contractVersion = 'C0-v1'
    fixtureVersion = 'specification-v1.1'
    generatorFormat = 'deterministic-format-v1'
    seed = '460072030'
    sourceCommit = $sourceCommit
    sourceDirty = [bool](& git -C $repoRoot status --porcelain)
    sourceTreeSha256 = $null
    fixtureTreeSha256 = $null
    dotnetSdk = $null
    toolchainLockHash = $null
    offlineRestore = [bool]$Offline
    nugetAdvisoryCheck = $(if ($Offline) { 'PendingNetwork: offline restore disables advisory lookup' } else { 'RequestedDuringRestore' })
    result = 'FAIL'
    scope = 'P01/P02 production primitives, codecs, recipe, compiler/cache and byte-lease tests. Numeric diagnostic content only; no full slice, rendering, gameplay or hardware acceptance.'
    testCount = 0
    failure = $null
    commands = @()
}
function Get-InventoryHash([string[]]$Paths) {
    $rows = foreach ($relativePath in ($Paths | Sort-Object -CaseSensitive)) {
        $absolutePath = Join-Path $repoRoot $relativePath
        if (Test-Path -LiteralPath $absolutePath -PathType Leaf) {
            $relativePath.Replace('\', '/') + ':' + (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($rows -join "`n")))).ToLowerInvariant() }
    finally { $sha.Dispose() }
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
    Write-Host "Generation report: $reportDirectory"
    & python (Join-Path $PSScriptRoot 'verify_contract_lock.py') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Frozen C0 identity check failed.' }
    & python (Join-Path $PSScriptRoot 'verify_content_format.py') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Frozen P02 format identity check failed.' }
    & python (Join-Path $PSScriptRoot 'verify_feature_registry.py') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Feature source registry check failed.' }
    $sourcePaths = @(& git -C $repoRoot ls-files --cached --others --exclude-standard)
    $report.sourceTreeSha256 = Get-InventoryHash $sourcePaths
    $report.fixtureTreeSha256 = Get-InventoryHash @($sourcePaths | Where-Object { $_ -like 'docs/fixtures/*' })
    $lockPath = Join-Path $repoRoot 'toolchain-lock.json'
    if (Test-Path -LiteralPath $lockPath -PathType Leaf) { $report.toolchainLockHash = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash.ToLowerInvariant() }
    $report.dotnetSdk = (Invoke-CheckedProcess 'sdk' @('--version')).Trim()
    $expectedSdk = (Get-Content -LiteralPath (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
    if ($report.dotnetSdk -ne $expectedSdk) { throw "Expected SDK $expectedSdk; got $($report.dotnetSdk)" }
    $restoreArguments = @('restore', $projectPath, '--locked-mode')
    if ($Offline) {
        $packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
        if (!(Test-Path -LiteralPath $packageCache -PathType Container)) { throw "Missing package cache: $packageCache" }
        $restoreArguments += @('--source', $packageCache, '-p:NuGetAudit=false')
    }
    else { $restoreArguments += @('-p:NuGetAudit=true', '-p:NuGetAuditMode=all') }
    $null = Invoke-CheckedProcess 'restore' $restoreArguments
    if (!$Offline) { $report.nugetAdvisoryCheck = 'CompletedDuringRestore: all dependency levels' }
    $null = Invoke-CheckedProcess 'tests' @('test', $projectPath, '--no-restore', '--configuration', 'Release', '--logger', 'trx;LogFileName=generation.trx', '--results-directory', $reportDirectory)
    $trxPath = Join-Path $reportDirectory 'generation.trx'
    if (!(Test-Path -LiteralPath $trxPath -PathType Leaf)) { throw 'Fresh test XML was not produced.' }
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    $results = @($trx.TestRun.Results.UnitTestResult)
    if ([int]$counters.total -le 0 -or [int]$counters.failed -ne 0 -or [int]$counters.executed -ne [int]$counters.total -or [int]$counters.passed -ne [int]$counters.total -or $results.Count -ne [int]$counters.total) {
        throw 'Tests failed, skipped, aborted, or did not run.'
    }
    # Normalize fresh TRX to portable JUnit XML, preserving every individual result.
    $xmlSettings = [Xml.XmlWriterSettings]::new()
    $xmlSettings.Indent = $true
    $writer = [Xml.XmlWriter]::Create((Join-Path $reportDirectory 'test-results.xml'), $xmlSettings)
    try {
        $writer.WriteStartElement('testsuite')
        $writer.WriteAttributeString('name', 'Shooter.Generation.Core')
        $writer.WriteAttributeString('tests', [string]$counters.total)
        $writer.WriteAttributeString('failures', '0')
        foreach ($result in $results) {
            $writer.WriteStartElement('testcase')
            $writer.WriteAttributeString('name', [string]$result.testName)
            $writer.WriteAttributeString('time', ([TimeSpan]::Parse([string]$result.duration).TotalSeconds.ToString([Globalization.CultureInfo]::InvariantCulture)))
            $writer.WriteEndElement()
        }
        $writer.WriteEndElement()
    }
    finally { $writer.Dispose() }
    $report.testCount = [int]$counters.total
    $report.result = 'PASS'
}
catch { $report.failure = $_.Exception.Message; Write-Warning $report.failure }
finally {
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reportDirectory 'run.json') -Encoding utf8NoBOM
}
if ($report.result -ne 'PASS') { exit 1 }
Write-Host "PASS: $($report.testCount) pure generation tests. Evidence: $reportDirectory"
