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
$reportDirectory = Join-Path $repoRoot "Artifacts/m8-runtime-composition/$sourceCommit/test-maps-$runId"
$null = New-Item -ItemType Directory -Path $reportDirectory -Force
$report = [ordered]@{
    schemaVersion = 1
    action = 'Test-Maps'
    sourceCommit = $sourceCommit
    sourceDirty = [bool](& git -C $repoRoot status --porcelain)
    result = 'FAIL'
    scope = 'Pure catalog/spawn/path corner checks against CompoundPlanner. Unity PhysX install is covered by player/Build+Smoke product path.'
    testCount = 0
    failure = $null
}
try {
    Write-Host "Test-Maps report: $reportDirectory"
    $project = Join-Path $repoRoot 'Tools/Game.Contracts.Tests/Game.Contracts.Tests.csproj'
    $restoreArgs = @('restore', $project)
    if ($Offline) { $restoreArgs += '--locked-mode' }
    & dotnet @restoreArgs
    if ($LASTEXITCODE -ne 0) { throw 'restore failed' }
    & dotnet test $project -c Release --no-restore --filter 'FullyQualifiedName~Catalog|FullyQualifiedName~OfflineSession|FullyQualifiedName~MapId' --logger "trx;LogFileName=maps.trx" --results-directory $reportDirectory
    if ($LASTEXITCODE -ne 0) { throw 'map-related Game-v1 tests failed' }
    # Explicit connectivity probe via Generation.Tests compound suite
    & dotnet test (Join-Path $repoRoot 'Tools/Generation.Tests/Generation.Tests.csproj') -c Release --filter 'FullyQualifiedName~Compound' --logger "trx;LogFileName=maps-compound.trx" --results-directory $reportDirectory
    if ($LASTEXITCODE -ne 0) { throw 'compound connectivity failed' }
    $report.testCount = 1
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
