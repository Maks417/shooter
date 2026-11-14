[CmdletBinding()]
param(
    [ValidateSet('compound','depot','all')][string]$Map = 'all',
    [ValidateSet('all','gameplay','server','low','balanced','high')][string]$Profile = 'all',
    [string]$CacheRoot = '',
    [switch]$Force,
    # T067/C01: publish through the locked Unity editor with the pinned native NavMesh bake (ShooterBuild.CompileNativeGameContent).
    # Default (no switch) stays the pure MapContent software NVB1 path, always labelled software-walkable-grid / Pending.
    [switch]$Native,
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
if (!$CacheRoot) { $CacheRoot = Join-Path $repoRoot $(if ($Native) { 'Artifacts/map-packages-native' } else { 'Artifacts/map-packages' }) }
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
    native = [bool]$Native
    navigationSource = $(if ($Native) { 'unity-native-navmesh-pending-verification' } else { 'software-walkable-grid' })
    navBakeProvenance = @()
    nativeToolIdentity = $null
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
    $unity = $null
    if ($Native) {
        . (Join-Path $PSScriptRoot 'Unity.Common.ps1')
        $unity = Get-ShooterUnity
        $nativeRun = [pscustomobject]@{ Path = $reportDirectory; Report = [ordered]@{ commands = @() } }
        $editorArgs = @('-quit', '-executeMethod', 'ShooterBuild.CompileNativeGameContent',
            '-shooterMap', $Map, '-shooterProfile', $Profile, '-shooterCacheRoot', $CacheRoot,
            '-shooterForce', ([bool]$Force).ToString().ToLowerInvariant(), '-shooterReport', $proofPath)
        Invoke-ShooterEditor $unity $nativeRun $editorArgs $TimeoutSeconds
        $report.editorCommands = $nativeRun.Report.commands
        if (!(Test-Path -LiteralPath $proofPath -PathType Leaf)) { throw 'Native compile produced no compile-proof.json' }
    }
    $toolProject = Join-Path $PSScriptRoot 'MapContent/MapContent.csproj'
    $logPath = Join-Path $reportDirectory 'process.log'
    $dotnetArgs = @(
        'run', '--project', $toolProject, '--configuration', 'Release', '--',
        $repoRoot, $CacheRoot, $proofPath, $sourceCommit, $Map, ([bool]$Force).ToString().ToLowerInvariant(), $Profile
    )
    if (!$Native) {
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
    }
    if (!(Test-Path -LiteralPath $proofPath -PathType Leaf)) { throw 'Missing compile-proof.json' }
    $proof = Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    if ($proof.result -cne 'Verified' -or $proof.sourceCommit -cne $sourceCommit) { throw 'Map package proof incomplete.' }
    $nativeProvenance = 'unity-ai-navigation-2.0.0;pinned-physx-collision-bake'
    $softwareProvenance = 'software-walkable-grid;unity-native-bake-pending'
    if ($Native) {
        if ($proof.toolchainHash -cne $unity.LockHash) { throw 'Native proof toolchain hash differs from the locked editor identity.' }
        if ($proof.editorVersion -cne $unity.Lock.editorVersion -or $proof.editorRevision -cne $unity.Lock.editorRevision) { throw 'Native proof editor identity differs from the lock.' }
        if ($proof.nativeUnityNavBake -cne 'PASS' -or -not $proof.allNative -or -not $proof.sidecarValidated) { throw 'Native proof does not claim a verified native bake.' }
        foreach ($pkg in $proof.packages) {
            if ($pkg.navBakeProvenance -cne $nativeProvenance -or $pkg.navBakeProvenance -match 'software-walkable-grid|pending') { throw "Package $($pkg.mapId)/$($pkg.visualProfile) is not native: $($pkg.navBakeProvenance)" }
            if (-not $pkg.pathSpawnToObjective -or $pkg.polygonCount -lt 1) { throw "Package $($pkg.mapId)/$($pkg.visualProfile) lacks a usable native navmesh." }
        }
    }
    else {
        foreach ($pkg in $proof.packages) {
            if ($pkg.navBakeProvenance -cne $softwareProvenance) { throw "Pure publication must stay software-labelled; got $($pkg.navBakeProvenance)" }
        }
    }
    if ($proof.packages.Count -lt 1) { throw 'No packages published.' }
    foreach ($pkg in $proof.packages) {
        $packageDir = Join-Path $CacheRoot ($pkg.packageRootSha256.Substring(0, 2) + '/' + $pkg.packageRootSha256)
        $packageJson = Join-Path $packageDir 'package.json'
        if (!(Test-Path -LiteralPath $packageJson -PathType Leaf)) { throw "Missing package root: $packageJson" }
        $actual = (Get-FileHash -LiteralPath $packageJson -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -cne $pkg.packageRootSha256) { throw "Package root hash mismatch for $($pkg.mapId)/$($pkg.visualProfile)" }
    }
    if ($Native) {
        # Independent sidecar re-check from published bytes: every package native, navigation hash matches the stored file.
        $sidecarPath = Join-Path $CacheRoot 'nav-provenance.json'
        if (!(Test-Path -LiteralPath $sidecarPath -PathType Leaf)) { throw 'Missing nav-provenance.json sidecar.' }
        $sidecar = Get-Content -LiteralPath $sidecarPath -Raw | ConvertFrom-Json
        if ($sidecar.provenanceSchema -cne 'shooter.nav-bake-provenance.v1' -or -not $sidecar.allNative -or [string]::IsNullOrWhiteSpace($sidecar.nativeToolIdentity)) { throw 'nav-provenance sidecar does not record an all-native bake with tool identity.' }
        $sidecarByRoot = @{}
        foreach ($entry in @($sidecar.packages)) { $sidecarByRoot[[string]$entry.packageRootSha256] = $entry }
        foreach ($pkg in $proof.packages) {
            $entry = $sidecarByRoot[[string]$pkg.packageRootSha256]
            if ($null -eq $entry -or -not $entry.native -or $entry.navBakeProvenance -cne $nativeProvenance) { throw "Sidecar missing/non-native entry for $($pkg.mapId)/$($pkg.visualProfile)" }
            $navFile = Join-Path $CacheRoot ($pkg.packageRootSha256.Substring(0, 2) + '/' + $pkg.packageRootSha256 + '/navigation-gameplay.bin')
            $navHash = (Get-FileHash -LiteralPath $navFile -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($navHash -cne $entry.navigationSha256 -or $navHash -cne $pkg.navigationSha256) { throw "Navigation hash mismatch for $($pkg.mapId)/$($pkg.visualProfile)" }
        }
        $report.nativeToolIdentity = [string]$sidecar.nativeToolIdentity
    }
    # Generation tests covering compound/depot package builders (not a substitute for publication).
    $generationProject = Join-Path $repoRoot 'Tools/Generation.Tests/Generation.Tests.csproj'
    & dotnet test $generationProject -c Release --filter 'FullyQualifiedName~MapPackage|FullyQualifiedName~Compound|FullyQualifiedName~Depot' --logger "trx;LogFileName=compile-gamecontent-tests.trx" --results-directory $reportDirectory
    if ($LASTEXITCODE -ne 0) { throw "map package generation validation failed ($LASTEXITCODE)" }

    $report.packageCount = [int]$proof.packages.Count
    $report.seeds = @(460072030, 460072031)
    $report.navBakeProvenance = @($proof.packages | ForEach-Object { [ordered]@{ mapId = $_.mapId; visualProfile = $_.visualProfile; navBakeProvenance = $_.navBakeProvenance } })
    $report.navigationSource = $(if ($Native) { 'unity-native-navmesh' } else { 'software-walkable-grid' })
    # PASS only when the editor native provider produced and re-verified every package; pure tooling is always Pending.
    $report.nativeUnityNavBake = $(if ($Native) { 'PASS' } else { 'Pending' })
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
