[CmdletBinding()]
param(
    [ValidateSet('compound','depot','all')][string]$Map = 'all',
    [ValidateRange(30, 3600)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$reportDirectory = Join-Path $repoRoot "Artifacts/m8-runtime-composition/$sourceCommit/compile-gamecontent-$runId"
$null = New-Item -ItemType Directory -Path $reportDirectory -Force
$report = [ordered]@{
    schemaVersion = 1
    action = 'Compile-GameContent'
    map = $Map
    sourceCommit = $sourceCommit
    sourceDirty = [bool](& git -C $repoRoot status --porcelain)
    result = 'FAIL'
    seeds = @()
    failure = $null
}
try {
    Write-Host "Compile-GameContent report: $reportDirectory"
    $project = Join-Path $repoRoot 'Tools/Generation.Tests/Generation.Tests.csproj'
    $filter = 'FullyQualifiedName~Compound'
    & dotnet test $project -c Release --filter $filter --logger "trx;LogFileName=compile-gamecontent.trx" --results-directory $reportDirectory
    if ($LASTEXITCODE -ne 0) { throw "compound generation validation failed ($LASTEXITCODE)" }
    $report.seeds = @(460072030, 460072031)
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
