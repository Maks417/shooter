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
$reportDirectory = Join-Path $repoRoot "Artifacts/m1-procedural-feasibility/$sourceCommit/test-generation-$runId"
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
    scope = 'M1 pure generation, compiler/cache, byte leases, geometry/material/weapon/soldier and analytic motion checks. Numeric tests do not establish visual, gameplay-hit or reference-hardware acceptance.'
    testCount = 0
    executedTestCount = 0
    failedTestCount = 0
    skippedTestCount = 0
    failedTests = @()
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
    Invoke-ShooterCheckedProcess -Name $Name -Executable $dotnet -ProcessArguments $ProcessArguments -ReportDirectory $reportDirectory -Report $report -WorkingDirectory $repoRoot -TimeoutSeconds $TimeoutSeconds
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
    $restoreArguments = Get-ShooterRestoreArguments $projectPath ([bool]$Offline)
    $null = Invoke-CheckedProcess 'restore' $restoreArguments
    if (!$Offline) { $report.nugetAdvisoryCheck = 'CompletedDuringRestore: all dependency levels' }
    $null = Invoke-CheckedProcess 'tests' @('test', $projectPath, '--no-restore', '--configuration', 'Release', '--logger', 'trx;LogFileName=generation.trx', '--results-directory', $reportDirectory)
    $trxPath = Join-Path $reportDirectory 'generation.trx'
    if (!(Test-Path -LiteralPath $trxPath -PathType Leaf)) { throw 'Fresh test XML was not produced.' }
    $summary = Get-StrictTrxSummary -Path $trxPath -SuiteName 'Generation'
    $results = $summary.Results
    # Normalize fresh TRX to portable JUnit XML, preserving every individual result.
    $xmlSettings = [Xml.XmlWriterSettings]::new()
    $xmlSettings.Indent = $true
    $writer = [Xml.XmlWriter]::Create((Join-Path $reportDirectory 'test-results.xml'), $xmlSettings)
    try {
        $writer.WriteStartElement('testsuite')
        $writer.WriteAttributeString('name', 'Shooter.Generation.Core')
        $writer.WriteAttributeString('tests', [string]$summary.Total)
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
    $report.testCount = $summary.Total
    $report.result = 'PASS'
}
catch { $report.failure = $_.Exception.Message; Write-Warning $report.failure }
finally {
    # A failing test process still produces useful fresh TRX. Preserve its actual counts;
    # never turn a known acceptance failure into a misleading zero-test report.
    $finalTrxPath = Join-Path $reportDirectory 'generation.trx'
    if (Test-Path -LiteralPath $finalTrxPath -PathType Leaf) {
        try {
            [xml]$finalTrx = Get-Content -LiteralPath $finalTrxPath -Raw
            $finalCounters = $finalTrx.TestRun.ResultSummary.Counters
            $report.testCount = [int]$finalCounters.total
            $report.executedTestCount = [int]$finalCounters.executed
            $report.failedTestCount = [int]$finalCounters.failed
            $report.skippedTestCount = [int]$finalCounters.total - [int]$finalCounters.executed
            $report.failedTests = @($finalTrx.TestRun.Results.UnitTestResult | Where-Object outcome -ne 'Passed' | ForEach-Object testName)
        }
        catch { $report['testResultParseFailure'] = $_.Exception.Message; $report.result = 'FAIL' }
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reportDirectory 'run.json') -Encoding utf8NoBOM
}
if ($report.result -ne 'PASS') { exit 1 }
Complete-ShooterReportRun $reportDirectory 'test-generation'
Write-Host "PASS: $($report.testCount) pure generation tests. Evidence: $reportDirectory"
