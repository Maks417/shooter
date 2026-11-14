[CmdletBinding()]
param(
    [ValidateSet('compound','depot','all')][string]$Map = 'all',
    [ValidateSet('all','gameplay','server','low','balanced','high')][string]$Profile = 'all',
    [string]$CacheRoot = '',
    [switch]$Force,
    [ValidateRange(30, 3600)][int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
& python (Join-Path $PSScriptRoot 'verify_content_format.py') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Frozen content-format identity check failed.' }

$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$reportDirectory = Join-Path $repoRoot "Artifacts/m8-runtime-composition/$sourceCommit/compile-gamecontent-$runId"
$null = New-Item -ItemType Directory -Path $reportDirectory -Force
if (!$CacheRoot) { $CacheRoot = Join-Path $repoRoot 'Artifacts/map-packages' }
$CacheRoot = [IO.Path]::GetFullPath($CacheRoot)
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'Artifacts')) + [IO.Path]::DirectorySeparatorChar
if (!$CacheRoot.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'CacheRoot must stay under repository Artifacts.' }
$null = New-Item -ItemType Directory -Path $CacheRoot -Force

$report = [ordered]@{
    schemaVersion = 1
    action = 'Compile-GameContent'
    map = $Map
    profile = $Profile
    sourceCommit = $sourceCommit
    sourceDirty = [bool](& git -C $repoRoot status --porcelain)
    cacheRoot = $CacheRoot
    force = [bool]$Force
    result = 'FAIL'
    packageCount = 0
    seeds = @()
    nativeUnityNavBake = 'Pending'
    releasePlayerLoad = 'Pending'
    failure = $null
}
try {
    Write-Host "Compile-GameContent report: $reportDirectory"
    # Preserve diagnostic compilers as separate named tools; this wrapper publishes map packages only.
    $proofPath = Join-Path $reportDirectory 'compile-proof.json'
    $toolProject = Join-Path $PSScriptRoot 'MapContent/MapContent.csproj'
    $logPath = Join-Path $reportDirectory 'process.log'
    $dotnetArgs = @(
        'run', '--project', $toolProject, '--configuration', 'Release', '--',
        $repoRoot, $CacheRoot, $proofPath, $sourceCommit, $Map, ([bool]$Force).ToString().ToLowerInvariant(), $Profile
    )
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & dotnet @dotnetArgs 2>&1 | Out-String
    }
    finally {
        $ErrorActionPreference = $previous
    }
    [IO.File]::WriteAllText($logPath, $output)
    if ($LASTEXITCODE -ne 0) { throw 'MapContent publish tool failed; see process.log.' }
    if (!(Test-Path -LiteralPath $proofPath -PathType Leaf)) { throw 'Missing compile-proof.json' }
    $proof = Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    if ($proof.result -cne 'Verified' -or $proof.sourceCommit -cne $sourceCommit) { throw 'Map package proof incomplete.' }
    if ($proof.packages.Count -lt 1) { throw 'No packages published.' }
    foreach ($pkg in $proof.packages) {
        $packageDir = Join-Path $CacheRoot ($pkg.packageRootSha256.Substring(0, 2) + '/' + $pkg.packageRootSha256)
        $packageJson = Join-Path $packageDir 'package.json'
        if (!(Test-Path -LiteralPath $packageJson -PathType Leaf)) { throw "Missing package root: $packageJson" }
        $actual = (Get-FileHash -LiteralPath $packageJson -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -cne $pkg.packageRootSha256) { throw "Package root hash mismatch for $($pkg.mapId)/$($pkg.visualProfile)" }
    }
    # Generation tests covering compound/depot package builders (not a substitute for publication).
    $generationProject = Join-Path $repoRoot 'Tools/Generation.Tests/Generation.Tests.csproj'
    & dotnet test $generationProject -c Release --filter 'FullyQualifiedName~MapPackage|FullyQualifiedName~Compound|FullyQualifiedName~Depot' --logger "trx;LogFileName=compile-gamecontent-tests.trx" --results-directory $reportDirectory
    if ($LASTEXITCODE -ne 0) { throw "map package generation validation failed ($LASTEXITCODE)" }

    $report.packageCount = [int]$proof.packages.Count
    $report.seeds = @(460072030, 460072031)
    $report.nativeUnityNavBake = [string]$proof.nativeUnityNavBake
    $report.releasePlayerLoad = [string]$proof.releasePlayerLoad
    $report.result = 'PASS'
}
catch {
    $report.failure = "$_"
    $report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $reportDirectory 'validation.json') -Encoding UTF8
    throw
}
$report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $reportDirectory 'validation.json') -Encoding UTF8
Write-Host ($report | ConvertTo-Json -Depth 8)
exit 0
