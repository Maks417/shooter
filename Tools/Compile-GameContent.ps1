[CmdletBinding()]
param(
    [ValidateSet('compound','depot','oil','all')][string]$Map = 'all',
    [ValidateSet('all','gameplay','server','low','balanced','high')][string]$Profile = 'all',
    [string]$CacheRoot = '',
    [switch]$Force,
    # T067/C01 two-stage native publication: (1) pure .NET MapContent publishes every payload into Artifacts/map-packages;
    # (2) the locked Unity editor ONLY bakes navigation from that authoritative collision (ShooterBuild.CompileNativeGameContent)
    # into a stage directory; (3) pure .NET MapContent republishes into the native cache with the staged NVB1 bakes injected.
    # A hard guard then proves every non-navigation file equals the pure package. Default (no switch) stays the pure
    # MapContent software NVB1 path, always labelled software-walkable-grid / Pending.
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
    nonNavByteIdentityGuard = $null
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
    $logPath = Join-Path $reportDirectory 'process.log'
    $unity = $null
    $toolProject = Join-Path $PSScriptRoot 'MapContent/MapContent.csproj'
    $forceArg = ([bool]$Force).ToString().ToLowerInvariant()
    function Invoke-MapContentPublish([string]$cache, [string]$proof, [string]$logFile, [string]$navStage) {
        $dotnetArgs = @(
            'run', '--project', $toolProject, '--configuration', 'Release', '--',
            $repoRoot, $cache, $proof, $sourceCommit, $Map, $forceArg, $Profile
        )
        if ($navStage) { $dotnetArgs += $navStage }
        $previous = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $output = & dotnet @dotnetArgs 2>&1 | Out-String
            $code = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previous
        }
        [IO.File]::WriteAllText($logFile, $output)
        if ($code -ne 0) { throw "MapContent publish tool failed; see $logFile." }
    }
    $purePackageCache = $null
    $pureProof = $null
    if ($Native) {
        . (Join-Path $PSScriptRoot 'Unity.Common.ps1')
        $unity = Get-ShooterUnity
        # Stage 1: pure .NET publication of ALL content (the editor never regenerates content; Mono rounds floats differently).
        $purePackageCache = [IO.Path]::GetFullPath((Join-Path $repoRoot 'Artifacts/map-packages'))
        if ($purePackageCache -ieq $CacheRoot) { throw 'Native CacheRoot must differ from the pure Artifacts/map-packages cache.' }
        $null = New-Item -ItemType Directory -Path $purePackageCache -Force
        $pureProofPath = Join-Path $reportDirectory 'pure-compile-proof.json'
        Invoke-MapContentPublish $purePackageCache $pureProofPath (Join-Path $reportDirectory 'pure-process.log') ''
        $pureProof = Get-Content -LiteralPath $pureProofPath -Raw | ConvertFrom-Json
        if ($pureProof.result -cne 'Verified' -or $pureProof.sourceCommit -cne $sourceCommit) { throw 'Pure map package proof incomplete.' }
        foreach ($pkg in $pureProof.packages) {
            if ($pkg.navBakeProvenance -cne 'software-walkable-grid;unity-native-bake-pending') { throw "Pure stage must stay software-labelled; got $($pkg.navBakeProvenance)" }
        }
        # Stage 2: editor bakes navigation ONLY, from the .NET-published authoritative collision (one source package per map).
        $sourceByMap = [ordered]@{}
        foreach ($pkg in $pureProof.packages) { if (-not $sourceByMap.Contains([string]$pkg.mapId)) { $sourceByMap[[string]$pkg.mapId] = [string]$pkg.packageRootSha256 } }
        $sourceArg = (($sourceByMap.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ',')
        $navStageDir = Join-Path $reportDirectory 'nav-stage'
        $stageProofPath = Join-Path $reportDirectory 'nav-stage-proof.json'
        $nativeRun = [pscustomobject]@{ Path = $reportDirectory; Report = [ordered]@{ commands = @() } }
        $editorArgs = @('-quit', '-executeMethod', 'ShooterBuild.CompileNativeGameContent',
            '-shooterPackageCache', $purePackageCache, '-shooterPackages', $sourceArg,
            '-shooterNavStage', $navStageDir, '-shooterReport', $stageProofPath)
        Invoke-ShooterEditor $unity $nativeRun $editorArgs $TimeoutSeconds
        $report.editorCommands = $nativeRun.Report.commands
        if (!(Test-Path -LiteralPath $stageProofPath -PathType Leaf)) { throw 'Native nav bake produced no nav-stage-proof.json' }
        $stageProof = Get-Content -LiteralPath $stageProofPath -Raw | ConvertFrom-Json
        if ($stageProof.result -cne 'Staged' -or $stageProof.toolchainHash -cne $unity.LockHash) { throw 'Nav stage proof does not match the locked toolchain.' }
        if ($stageProof.editorVersion -cne $unity.Lock.editorVersion -or $stageProof.editorRevision -cne $unity.Lock.editorRevision) { throw 'Nav stage editor identity differs from the lock.' }
        foreach ($m in $stageProof.maps) {
            if (-not $m.repeatIdentical -or $m.polygonCount -lt 1) { throw "Nav stage for $($m.mapId) is not a reproducible native bake." }
            $src = Join-Path $purePackageCache ($m.sourcePackageRootSha256.Substring(0, 2) + '/' + $m.sourcePackageRootSha256 + '/collision-gameplay.bin')
            if ((Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash.ToLowerInvariant() -cne $m.collisionSha256) { throw "Nav stage collision hash for $($m.mapId) differs from the .NET-published collision." }
        }
        # Stage 3: pure .NET publication of the final native packages with the staged NVB1 bakes injected.
        Invoke-MapContentPublish $CacheRoot $proofPath (Join-Path $reportDirectory 'process.log') $navStageDir
    }
    else {
        Invoke-MapContentPublish $CacheRoot $proofPath $logPath ''
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
        # HARD GUARD (two-stage native publication): every non-navigation payload of every native package must be byte-identical
        # to the pure-.NET-published package of the same map/profile; gameplay collision included. The package root may differ only
        # in the navigation artifact hash/dependency entry.
        $pureByKey = @{}
        foreach ($pp in $pureProof.packages) { $pureByKey["$($pp.mapId)/$($pp.visualProfile)"] = $pp }
        $guardRows = @()
        foreach ($pkg in $proof.packages) {
            $key = "$($pkg.mapId)/$($pkg.visualProfile)"
            $pure = $pureByKey[$key]
            if ($null -eq $pure) { throw "Non-nav guard: no pure .NET package for $key." }
            $nativeDir = Join-Path $CacheRoot ($pkg.packageRootSha256.Substring(0, 2) + '/' + $pkg.packageRootSha256)
            $pureDir = Join-Path $purePackageCache ($pure.packageRootSha256.Substring(0, 2) + '/' + $pure.packageRootSha256)
            $nativeFiles = @(Get-ChildItem -LiteralPath $nativeDir -File | ForEach-Object { $_.Name } | Sort-Object)
            $pureFiles = @(Get-ChildItem -LiteralPath $pureDir -File | ForEach-Object { $_.Name } | Sort-Object)
            if (($nativeFiles -join '|') -cne ($pureFiles -join '|')) { throw "Non-nav guard FAIL ${key}: file sets differ (native: $($nativeFiles -join ',') pure: $($pureFiles -join ','))." }
            $compared = 0
            foreach ($name in $nativeFiles) {
                if ($name -ceq 'navigation-gameplay.bin' -or $name -ceq 'package.json') { continue }
                $nh = (Get-FileHash -LiteralPath (Join-Path $nativeDir $name) -Algorithm SHA256).Hash
                $ph = (Get-FileHash -LiteralPath (Join-Path $pureDir $name) -Algorithm SHA256).Hash
                if ($nh -cne $ph) { throw "Non-nav guard FAIL ${key}: $name differs between native ($nh) and pure .NET ($ph)." }
                $compared++
            }
            if (-not (Test-Path -LiteralPath (Join-Path $nativeDir 'collision-gameplay.bin'))) { throw "Non-nav guard FAIL ${key}: native package lacks collision-gameplay.bin." }
            # package.json: identical apart from the navigation hash + its dependency entry.
            $nj = Get-Content -LiteralPath (Join-Path $nativeDir 'package.json') -Raw | ConvertFrom-Json
            $pj = Get-Content -LiteralPath (Join-Path $pureDir 'package.json') -Raw | ConvertFrom-Json
            foreach ($doc in @($nj, $pj)) {
                $doc.PSObject.Properties.Remove('navigationArtifactSha256')
                $doc.dependencies = @($doc.dependencies | Where-Object { $_.role -cne 'navigation' })
            }
            if (($nj | ConvertTo-Json -Depth 10 -Compress) -cne ($pj | ConvertTo-Json -Depth 10 -Compress)) { throw "Non-nav guard FAIL ${key}: package.json differs beyond the navigation entry." }
            if ($pkg.navigationSha256 -ceq $pure.navigationSha256) { throw "Non-nav guard FAIL ${key}: native navigation equals the software grid; native bake was not injected." }
            $guardRows += [ordered]@{ mapId = $pkg.mapId; visualProfile = $pkg.visualProfile; nonNavFilesCompared = $compared; nativeNavigationSha256 = $pkg.navigationSha256; pureNavigationSha256 = $pure.navigationSha256 }
        }
        # ADR-078: the shared weapon package carries no navigation, so native and pure caches must hold identical bytes.
        $weaponGuard = $null
        $nativeWeaponIndex = Join-Path $CacheRoot 'weapons.json'
        $pureWeaponIndex = Join-Path $purePackageCache 'weapons.json'
        if ((Test-Path -LiteralPath $nativeWeaponIndex) -or (Test-Path -LiteralPath $pureWeaponIndex)) {
            if (!(Test-Path -LiteralPath $nativeWeaponIndex) -or !(Test-Path -LiteralPath $pureWeaponIndex)) { throw 'Non-nav guard FAIL weapons: weapons.json present in only one cache.' }
            $nw = Get-Content -LiteralPath $nativeWeaponIndex -Raw | ConvertFrom-Json
            $pw = Get-Content -LiteralPath $pureWeaponIndex -Raw | ConvertFrom-Json
            if ([string]$nw.packageRootSha256 -cne [string]$pw.packageRootSha256) { throw 'Non-nav guard FAIL weapons: weapon package roots differ between native and pure caches.' }
            $wRel = ([string]$nw.packageRootSha256).Substring(0, 2) + '/' + [string]$nw.packageRootSha256
            $nativeWeaponDir = Join-Path $CacheRoot $wRel
            $pureWeaponDir = Join-Path $purePackageCache $wRel
            $weaponFiles = @(Get-ChildItem -LiteralPath $pureWeaponDir -File | ForEach-Object { $_.Name } | Sort-Object)
            foreach ($name in $weaponFiles) {
                $nh = (Get-FileHash -LiteralPath (Join-Path $nativeWeaponDir $name) -Algorithm SHA256).Hash
                $ph = (Get-FileHash -LiteralPath (Join-Path $pureWeaponDir $name) -Algorithm SHA256).Hash
                if ($nh -cne $ph) { throw "Non-nav guard FAIL weapons: $name differs between native and pure caches." }
            }
            $weaponGuard = [ordered]@{ packageRootSha256 = [string]$nw.packageRootSha256; filesCompared = $weaponFiles.Count }
        }
        $report.nonNavByteIdentityGuard = [ordered]@{ result = 'PASS'; purePackageCache = $purePackageCache; packages = $guardRows; weaponPackage = $weaponGuard }
        Write-Host "Non-navigation byte-identity guard PASS for $($guardRows.Count) native packages against $purePackageCache"
    }
    # Generation tests covering compound/depot package builders (not a substitute for publication).
    $generationProject = Join-Path $repoRoot 'Tools/Generation.Tests/Generation.Tests.csproj'
    & dotnet test $generationProject -c Release --filter 'FullyQualifiedName~MapPackage|FullyQualifiedName~Compound|FullyQualifiedName~Depot' --logger "trx;LogFileName=compile-gamecontent-tests.trx" --results-directory $reportDirectory
    if ($LASTEXITCODE -ne 0) { throw "map package generation validation failed ($LASTEXITCODE)" }

    $report.packageCount = [int]$proof.packages.Count
    $report.seeds = @(460072030, 460072031, 460072032)
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
