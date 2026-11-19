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
$reportDirectory = Join-Path $repoRoot "Artifacts/m0-foundation/$sourceCommit/test-contracts-$runId"
$null = New-Item -ItemType Directory -Path $reportDirectory
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$projectPath = Join-Path $repoRoot 'Tools/Contracts.Tests/Contracts.Tests.csproj'
$report = [ordered]@{
    schemaVersion = 1
    contractVersion = 'C0-v1'
    fixtureVersion = 'specification-v1.1'
    sourceCommit = $sourceCommit
    sourceDirty = [bool](& git -C $repoRoot status --porcelain)
    sourceTreeSha256 = $null
    fixtureTreeSha256 = $null
    dotnetSdk = $null
    toolchainLockHash = $null
    offlineRestore = [bool]$Offline
    nugetAdvisoryCheck = $(if ($Offline) { 'PendingNetwork: offline restore disables advisory lookup' } else { 'RequestedDuringRestore' })
    result = 'FAIL'
    scope = 'Pure C# contract build and conformance tests. No Unity, gameplay, build or hardware acceptance.'
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
    Invoke-ShooterCheckedProcess -Name $Name -Executable $dotnet -ProcessArguments $ProcessArguments -ReportDirectory $reportDirectory -Report $report -WorkingDirectory $repoRoot -TimeoutSeconds $TimeoutSeconds
}
try {
    Write-Host "Contracts report: $reportDirectory"
    & python (Join-Path $PSScriptRoot 'verify_contract_lock.py') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Frozen C0 identity check failed.' }
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
    $null = Invoke-CheckedProcess 'tests' @('test', $projectPath, '--no-restore', '--configuration', 'Release', '--logger', 'trx;LogFileName=contracts.trx', '--results-directory', $reportDirectory)
    $trxPath = Join-Path $reportDirectory 'contracts.trx'
    if (!(Test-Path -LiteralPath $trxPath -PathType Leaf)) { throw 'Fresh test XML was not produced.' }
    $summary = Get-StrictTrxSummary -Path $trxPath -SuiteName 'Contracts'
    $results = $summary.Results
    # Normalize fresh TRX to portable JUnit XML, preserving every individual result.
    $xmlSettings = [Xml.XmlWriterSettings]::new()
    $xmlSettings.Indent = $true
    $writer = [Xml.XmlWriter]::Create((Join-Path $reportDirectory 'test-results.xml'), $xmlSettings)
    try {
        $writer.WriteStartElement('testsuite')
        $writer.WriteAttributeString('name', 'Shooter.Contracts')
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
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reportDirectory 'run.json') -Encoding utf8NoBOM
}
if ($report.result -ne 'PASS') { exit 1 }
Complete-ShooterReportRun $reportDirectory 'test-contracts'
Write-Host "PASS: $($report.testCount) pure contract tests. Evidence: $reportDirectory"
