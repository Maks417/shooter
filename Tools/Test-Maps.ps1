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
$reportDirectory = Join-Path $repoRoot "Artifacts/m8-runtime-composition/$sourceCommit/test-maps-$runId"
$null = New-Item -ItemType Directory -Path $reportDirectory -Force
$report = [ordered]@{
    schemaVersion = 1
    action = 'Test-Maps'
    sourceCommit = $sourceCommit
    sourceDirty = [bool](& git -C $repoRoot status --porcelain)
    offlineRestore = [bool]$Offline
    dotnetSdk = $null
    result = 'FAIL'
    scope = 'Pure catalog/spawn/path corner checks plus MapPackage publication/load tests. Software NVB1 provenance stays pending without Unity editor bridge; native bake/runtime install code is present but editor/player evidence remains Pending for FIX04.'
    testCount = 0
    nativeUnityNavBake = 'Pending'
    releasePlayerLoad = 'Pending'
    failure = $null
}
try {
    Write-Host "Test-Maps report: $reportDirectory"
    $project = Join-Path $repoRoot 'Tools/Game.Contracts.Tests/Game.Contracts.Tests.csproj'
    $generationProject = Join-Path $repoRoot 'Tools/Generation.Tests/Generation.Tests.csproj'
    $report.dotnetSdk = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'dotnet SDK query failed' }
    $expectedSdk = (Get-Content -LiteralPath (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
    if ($report.dotnetSdk -ne $expectedSdk) { throw "Expected SDK $expectedSdk; got $($report.dotnetSdk)" }
    $restoreArgs = @('restore', $project, '--locked-mode')
    $generationRestoreArgs = @('restore', $generationProject, '--locked-mode')
    if ($Offline) {
        $packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
        if (!(Test-Path -LiteralPath $packageCache -PathType Container)) { throw "Missing package cache: $packageCache" }
        $offlineArgs = @('--source', $packageCache, '-p:NuGetAudit=false')
        $restoreArgs += $offlineArgs
        $generationRestoreArgs += $offlineArgs
    }
    & dotnet @restoreArgs
    if ($LASTEXITCODE -ne 0) { throw 'restore failed' }
    & dotnet @generationRestoreArgs
    if ($LASTEXITCODE -ne 0) { throw 'generation restore failed' }
    & dotnet test $project -c Release --no-restore --filter 'FullyQualifiedName~Catalog|FullyQualifiedName~OfflineSession|FullyQualifiedName~MapId' --logger "trx;LogFileName=maps.trx" --results-directory $reportDirectory
    if ($LASTEXITCODE -ne 0) { throw 'map-related Game-v1 tests failed' }
    # Explicit connectivity + verified map-package suite (not a mislabeled content compiler).
    & dotnet test $generationProject -c Release --no-restore --filter 'FullyQualifiedName~Compound|FullyQualifiedName~MapPackage|FullyQualifiedName~Depot|FullyQualifiedName~MapTopology' --logger "trx;LogFileName=maps-compound.trx" --results-directory $reportDirectory
    if ($LASTEXITCODE -ne 0) { throw 'compound/map-package validation failed' }
    $mapSummary = Get-StrictTrxSummary -Path (Join-Path $reportDirectory 'maps.trx') -SuiteName 'Map Game-v1 filter'
    $compoundSummary = Get-StrictTrxSummary -Path (Join-Path $reportDirectory 'maps-compound.trx') -SuiteName 'Map compound/package filter'
    $report.testCount = $mapSummary.Total + $compoundSummary.Total
    $report.result = 'PASS'
}
catch {
    $report.failure = "$_"
    $report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $reportDirectory 'validation.json') -Encoding UTF8
    throw
}
$report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $reportDirectory 'validation.json') -Encoding UTF8
Write-Host ($report | ConvertTo-Json -Depth 6)
exit 0
