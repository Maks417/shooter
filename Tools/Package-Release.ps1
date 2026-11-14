[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ClientBuildRunDirectory,
    [string]$MapCacheRoot = '',
    [string]$PreviousPackageDirectory = '',
    [ValidateRange(30, 3600)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$policyPath = Join-Path $PSScriptRoot 'release-package-policy.json'
if (!(Test-Path -LiteralPath $policyPath -PathType Leaf)) { throw "Missing release policy: $policyPath" }
$policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json

function Get-Sha256File([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-Sha256Text([string]$Text) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally { $sha.Dispose() }
}

function Test-ExcludedRelativePath([string]$Relative, $Policy) {
    $normalized = $Relative.Replace('\', '/').TrimStart('/')
    if ([string]::IsNullOrWhiteSpace($normalized)) { return $true }
    $parts = @($normalized.Split(@('/'), [StringSplitOptions]::RemoveEmptyEntries))
    for ($i = 0; $i -lt ($parts.Count - 1); $i++) {
        $dir = $parts[$i]
        foreach ($name in @($Policy.excludeDirectoryNames)) {
            if ($dir -ieq $name) { return $true }
        }
        foreach ($token in @($Policy.excludeDirectoryNameContains)) {
            if ($dir.IndexOf([string]$token, [StringComparison]::OrdinalIgnoreCase) -ge 0) { return $true }
        }
    }
    $fileName = $parts[-1]
    foreach ($name in @($Policy.excludeFileNames)) {
        if ($fileName -ieq $name) { return $true }
    }
    $ext = [IO.Path]::GetExtension($fileName)
    foreach ($candidate in @($Policy.excludeFileExtensions)) {
        if ($ext -ieq $candidate) { return $true }
    }
    return $false
}

function Copy-ReleaseTree([string]$SourceRoot, [string]$DestRoot, $Policy) {
    $copied = [Collections.Generic.List[string]]::new()
    $excluded = [Collections.Generic.List[string]]::new()
    $sourceRootFull = [IO.Path]::GetFullPath($SourceRoot)
    Get-ChildItem -LiteralPath $sourceRootFull -Recurse -Force -File | ForEach-Object {
        $relative = $_.FullName.Substring($sourceRootFull.Length).TrimStart('\', '/').Replace('\', '/')
        if (Test-ExcludedRelativePath $relative $Policy) {
            $excluded.Add($relative) | Out-Null
            return
        }
        $dest = Join-Path $DestRoot ($relative.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $null = New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force
        Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
        $copied.Add($relative) | Out-Null
    }
    [pscustomobject]@{ Copied = $copied; Excluded = $excluded }
}

function Assert-RequiredContent([string]$CacheRoot, [string]$ExpectedToolchainHash, $Policy) {
    $indexPath = Join-Path $CacheRoot 'index.json'
    if (!(Test-Path -LiteralPath $indexPath -PathType Leaf)) { throw "Missing map package index: $indexPath" }
    $index = Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json
    if ($index.indexSchema -cne 'shooter.map-package-index.v1') { throw "Unexpected map package index schema: $($index.indexSchema)" }
    $lines = [Collections.Generic.List[string]]::new()
    $found = @{}
    foreach ($pkg in @($index.packages)) {
        $key = "$($pkg.mapId)/$($pkg.visualProfile)"
        $found[$key] = $pkg
        $packageDir = Join-Path $CacheRoot (($pkg.packageRootSha256.Substring(0, 2)) + '/' + $pkg.packageRootSha256)
        $packageJson = Join-Path $packageDir 'package.json'
        if (!(Test-Path -LiteralPath $packageJson -PathType Leaf)) { throw "Missing required package root: $packageJson" }
        $actual = Get-Sha256File $packageJson
        if ($actual -cne $pkg.packageRootSha256) { throw "Package root hash mismatch for $key" }
        $root = Get-Content -LiteralPath $packageJson -Raw | ConvertFrom-Json
        if ($root.toolchainHash -cne $ExpectedToolchainHash) { throw "Package toolchain mismatch for $key" }
        if ($root.mapId -cne $pkg.mapId -or $root.visualProfile -cne $pkg.visualProfile) { throw "Package identity mismatch for $key" }
        $lines.Add("${key}:$($pkg.packageRootSha256)") | Out-Null
    }
    foreach ($mapId in @($Policy.requiredMaps)) {
        foreach ($profile in @($Policy.requiredVisualProfiles)) {
            $key = "$mapId/$profile"
            if (-not $found.ContainsKey($key)) { throw "Content closure missing required package: $key" }
        }
    }
    $sorted = @($lines | Sort-Object)
    [pscustomobject]@{
        IndexPath = $indexPath
        IndexSha256 = (Get-Sha256File $indexPath)
        Packages = @($index.packages)
        ContentClosureHash = (Get-Sha256Text ($sorted -join "`n"))
        ClosureLines = $sorted
    }
}

$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$sourceCommit = 'preflight-rejected'
$outDir = Join-Path $repoRoot "Artifacts/m11-playable-release/$sourceCommit/package-$runId"
$null = New-Item -ItemType Directory -Path $outDir -Force

$workspaceHead = $null
$workspaceDirty = $null
try {
    $workspaceHead = (& git -C $repoRoot rev-parse HEAD 2>$null).Trim()
    $workspaceDirty = [bool](& git -C $repoRoot status --porcelain 2>$null)
}
catch {
    $workspaceHead = $null
    $workspaceDirty = $null
}

$report = [ordered]@{
    schemaVersion = 1
    action = 'Package-Release'
    policyId = [string]$policy.policyId
    # Recorded identities are from the build+content under packaging, not workspace HEAD.
    sourceCommit = $sourceCommit
    sourceHash = $null
    toolchainHash = $null
    buildContentManifestHash = $null
    contentClosureHash = $null
    contentIndexSha256 = $null
    editorVersion = $null
    editorRevision = $null
    backend = $null
    protocolVersion = [string]$policy.protocolVersion
    contractVersion = [string]$policy.contractVersion
    workspaceHead = $workspaceHead
    workspaceDirty = $workspaceDirty
    workspaceHeadMatchesBuild = $null
    buildSourceDirty = $null
    result = 'FAIL'
    packageDirectory = $outDir
    archivePath = $null
    archiveSha256 = $null
    inventoryPath = $null
    extractRehearsal = 'NotRun'
    corruptContentRehearsal = 'NotRun'
    rollbackRehearsal = 'NotRun'
    playerLaunchEvidence = 'Pending'
    releaseAcceptance = 'NotClaimed'
    scope = 'FIX13 software packaging gates only; directory copy/archive success is not REL01/REL06 acceptance.'
    failure = $null
}

$build = $null
$toolchainHash = $null
$content = $null
$stage = $null
$player = $null
$lockPath = Join-Path $repoRoot 'toolchain-lock.json'

try {
    $ClientBuildRunDirectory = [IO.Path]::GetFullPath($ClientBuildRunDirectory)
    if (!(Test-Path -LiteralPath $ClientBuildRunDirectory -PathType Container)) { throw "ClientBuildRunDirectory not found: $ClientBuildRunDirectory" }

    $manifestPath = Join-Path $ClientBuildRunDirectory 'build-manifest.json'
    if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Missing build-manifest.json (refuse to stamp HEAD onto arbitrary binaries): $manifestPath" }
    $build = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

    # Identities come from the build under packaging, never from current workspace HEAD.
    $sourceCommit = [string]$build.sourceCommit
    if ([string]::IsNullOrWhiteSpace($sourceCommit) -or $sourceCommit.Length -lt 7) { throw 'Build manifest missing sourceCommit.' }
    $finalOutDir = Join-Path $repoRoot "Artifacts/m11-playable-release/$sourceCommit/package-$runId"
    if ($finalOutDir -cne $outDir) {
        $null = New-Item -ItemType Directory -Path $finalOutDir -Force
        # Keep the provisional preflight folder only when identity never resolved; otherwise move to build identity.
        if (Test-Path -LiteralPath $outDir) { Remove-Item -LiteralPath $outDir -Recurse -Force }
        $outDir = $finalOutDir
        $report.packageDirectory = $outDir
    }
    $report.sourceCommit = $sourceCommit
    $report.sourceHash = [string]$build.sourceHash
    $report.buildContentManifestHash = [string]$build.contentManifestHash
    $report.editorVersion = [string]$build.editorVersion
    $report.editorRevision = [string]$build.editorRevision
    $report.backend = [string]$build.backend
    $report.buildSourceDirty = [bool]$build.sourceDirty
    $report.workspaceHeadMatchesBuild = if ($workspaceHead) { $workspaceHead -ceq $sourceCommit } else { $null }

    if ($build.result -cne 'Succeeded') { throw "Build result is not Succeeded: $($build.result)" }
    if ($build.target -cne 'Client') { throw "Build target must be Client: $($build.target)" }
    if ($build.development -eq $true) { throw 'Development builds are rejected for release packaging.' }
    if ($build.sourceDirty -eq $true) { throw 'Dirty build candidate rejected (sourceDirty=true).' }
    if ([string]::IsNullOrWhiteSpace([string]$build.sourceHash)) { throw 'Build manifest missing sourceHash.' }
    if ([string]::IsNullOrWhiteSpace([string]$build.toolchainHash)) { throw 'Build manifest missing toolchainHash.' }
    if ([string]::IsNullOrWhiteSpace([string]$build.contentManifestHash)) { throw 'Build manifest missing contentManifestHash.' }

    if (!(Test-Path -LiteralPath $lockPath -PathType Leaf)) { throw "Missing toolchain-lock.json: $lockPath" }
    $toolchainHash = Get-Sha256File $lockPath
    $report.toolchainHash = $toolchainHash
    if ($build.toolchainHash -cne $toolchainHash) {
        throw "Build/toolchain mismatch: build=$($build.toolchainHash) lock=$toolchainHash"
    }

    $player = Join-Path $ClientBuildRunDirectory 'player'
    $exe = Join-Path $player 'Shooter.exe'
    if (!(Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing player executable: $exe" }

    if (!$MapCacheRoot) { $MapCacheRoot = Join-Path $repoRoot 'Artifacts/map-packages' }
    $MapCacheRoot = [IO.Path]::GetFullPath($MapCacheRoot)
    $content = Assert-RequiredContent -CacheRoot $MapCacheRoot -ExpectedToolchainHash $toolchainHash -Policy $policy
    $report.contentClosureHash = [string]$content.ContentClosureHash
    $report.contentIndexSha256 = [string]$content.IndexSha256

    $stage = Join-Path $outDir 'Shooter-Offline'
    $null = New-Item -ItemType Directory -Path $stage -Force
} catch {
    $report.failure = "$_"
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outDir 'validation.json') -Encoding utf8
    Write-Host ($report | ConvertTo-Json -Depth 8)
    throw
}

try {
    $copyStats = Copy-ReleaseTree -SourceRoot $player -DestRoot $stage -Policy $policy
    if (!(Test-Path -LiteralPath (Join-Path $stage 'Shooter.exe') -PathType Leaf)) { throw 'Staged package missing Shooter.exe after exclude-filtered copy.' }

    # Ship verified content beside the player for independent extraction (no repo Artifacts absolute path required).
    $contentDest = Join-Path $stage 'Content/map-packages'
    $null = New-Item -ItemType Directory -Path $contentDest -Force
    Copy-Item -LiteralPath (Join-Path $MapCacheRoot 'index.json') -Destination (Join-Path $contentDest 'index.json') -Force
    foreach ($pkg in @($content.Packages)) {
        $relPackage = Join-Path $pkg.packageRootSha256.Substring(0, 2) $pkg.packageRootSha256
        $srcDir = Join-Path $MapCacheRoot $relPackage
        $dstDir = Join-Path $contentDest $relPackage
        if (!(Test-Path -LiteralPath $srcDir -PathType Container)) { throw "Missing source package directory: $srcDir" }
        $null = New-Item -ItemType Directory -Path $dstDir -Force
        $fileCount = 0
        Get-ChildItem -LiteralPath $srcDir -Force -File | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $dstDir $_.Name) -Force
            $fileCount++
        }
        if ($fileCount -lt 1) { throw "Package directory had no files to ship: $srcDir" }
        $shippedRoot = Join-Path $dstDir 'package.json'
        if ((Get-Sha256File $shippedRoot) -cne $pkg.packageRootSha256) { throw "Shipped package root hash mismatch for $($pkg.mapId)/$($pkg.visualProfile)" }
    }
    Copy-Item -LiteralPath $lockPath -Destination (Join-Path $stage 'toolchain-lock.json') -Force
    foreach ($name in @('game-contract-lock.json', 'content-format-lock.json', 'contract-lock.json')) {
        $src = Join-Path $repoRoot $name
        if (Test-Path -LiteralPath $src -PathType Leaf) {
            Copy-Item -LiteralPath $src -Destination (Join-Path $stage $name) -Force
        }
    }

    $controls = @"
Shooter offline team-deathmatch (local package)
Binary commit: $sourceCommit
Source hash: $($build.sourceHash)
Toolchain hash: $toolchainHash
Content closure: $($content.ContentClosureHash)
Protocol: $($policy.protocolVersion)
Contract: $($policy.contractVersion)

Launch (from this folder, no Unity editor / no network / no external server required):
  .\launch-offline.cmd

Controls: WASD move, mouse look, LMB fire, RMB aim, R reload, Esc pause
Maps: -shooterMap training-compound|depot-yard
Team size: -shooterTeamSize 5|6
Diagnostic smoke remains a separate tool path and is not this package's acceptance gate.
"@
    [IO.File]::WriteAllText((Join-Path $stage 'CONTROLS.txt'), $controls)

    $licenses = @"
Shooter local offline package — license / provenance summary
===========================================================
- Game content is generated from numeric recipes under the repository procedural provenance policy.
- Engine font allowlist exception: engine-allowlist-v1 (named Unity engine font exception only; no imported art).
- Unity player/runtime redistribution remains under Unity's own terms for the pinned editor $($build.editorVersion) ($($build.editorRevision)).
- This archive does not include PresentMon, Unity Editor, Library/Temp caches, or developer absolute path dependencies.
- Third-party notices packaged by the Unity player, if any, remain under their original terms inside Shooter_Data.
"@
    [IO.File]::WriteAllText((Join-Path $stage 'LICENSES.txt'), $licenses)

    $known = @"
KNOWN ISSUES / LIMITATIONS (FIX13 tooling package)
- Player launch / named device evidence: Pending until T065 operator run on this candidate.
- REL01/REL06 release acceptance: NotClaimed by Package-Release tooling alone.
- P05 historical FAIL retained under ADR-024; visual clearance is T062/T065.
- Native Unity NavMesh player install evidence may remain Pending under FIX04/FIX07 notes.
- Workspace dirty sibling work (T052–T063) must not be mistaken for this binary's sourceDirty flag; identities above are from build-manifest.json + content closure.
"@
    [IO.File]::WriteAllText((Join-Path $stage 'KNOWN_ISSUES.txt'), $known)

    $identities = [ordered]@{
        schemaVersion = 1
        policyId = [string]$policy.policyId
        binaryCommit = $sourceCommit
        sourceHash = [string]$build.sourceHash
        toolchainHash = $toolchainHash
        buildContentManifestHash = [string]$build.contentManifestHash
        contentClosureHash = [string]$content.ContentClosureHash
        contentIndexSha256 = [string]$content.IndexSha256
        protocolVersion = [string]$policy.protocolVersion
        contractVersion = [string]$policy.contractVersion
        editorVersion = [string]$build.editorVersion
        editorRevision = [string]$build.editorRevision
        backend = [string]$build.backend
        requiredMaps = @($policy.requiredMaps)
        requiredVisualProfiles = @($policy.requiredVisualProfiles)
        closureLines = @($content.ClosureLines)
        note = 'Do not stamp an unrelated workspace HEAD onto these identities.'
    }
    $identitiesPath = Join-Path $stage 'identities.json'
    ($identities | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $identitiesPath -Encoding utf8

    $rollbackPair = [ordered]@{
        schemaVersion = 1
        binaryCommit = $sourceCommit
        contentManifestHash = [string]$content.ContentClosureHash
        protocolVersion = [string]$policy.protocolVersion
        contractVersion = [string]$policy.contractVersion
        buildContentManifestHash = [string]$build.contentManifestHash
    }
    ($rollbackPair | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath (Join-Path $stage 'rollback-pair.json') -Encoding utf8
    ($rollbackPair | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath (Join-Path $outDir 'rollback-pair.json') -Encoding utf8

    $launch = @"
@echo off
setlocal
cd /d "%~dp0"
REM Independent offline launch: relative content + toolchain lock only.
.\Shooter.exe -shooterMapCache "%~dp0Content\map-packages" -shooterToolchainLock "%~dp0toolchain-lock.json"
"@
    [IO.File]::WriteAllText((Join-Path $stage 'launch-offline.cmd'), $launch)

    $hashes = Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        if (Test-ExcludedRelativePath $rel $policy) { throw "Excluded path leaked into staged package: $rel" }
        [pscustomobject]@{ path = $rel; sha256 = (Get-Sha256File $_.FullName) }
    }
    $requiredDocs = @('CONTROLS.txt', 'LICENSES.txt', 'KNOWN_ISSUES.txt', 'identities.json', 'rollback-pair.json', 'toolchain-lock.json', 'Content/map-packages/index.json', 'launch-offline.cmd', 'Shooter.exe')
    $inventoryPaths = @($hashes | ForEach-Object { $_.path })
    foreach ($required in $requiredDocs) {
        if ($inventoryPaths -notcontains $required) { throw "Inventory missing required path: $required" }
    }
    foreach ($marker in @('Shooter_BackUpThisFolder_ButDontShipItWithYourGame', 'Shooter_BurstDebugInformation_DoNotShip')) {
        if ($inventoryPaths | Where-Object { $_ -like "$marker/*" -or $_ -eq $marker }) {
            throw "Debug/backup path was not excluded: $marker"
        }
    }
    $inventoryPath = Join-Path $outDir 'inventory.json'
    ($hashes | ConvertTo-Json -Depth 4) | Set-Content -LiteralPath $inventoryPath -Encoding utf8
    $report.inventoryPath = $inventoryPath

    $archiveDir = Join-Path $outDir 'archive'
    $null = New-Item -ItemType Directory -Path $archiveDir -Force
    $archivePath = Join-Path $archiveDir 'Shooter-Offline.zip'
    if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archivePath -CompressionLevel Optimal
    $archiveSha = Get-Sha256File $archivePath
    [IO.File]::WriteAllText((Join-Path $archiveDir 'Shooter-Offline.zip.sha256'), "$archiveSha  Shooter-Offline.zip`n")
    $report.archivePath = $archivePath
    $report.archiveSha256 = $archiveSha

    # Clean extraction rehearsal (no Unity editor / no network dependency for the extract itself).
    $extractRoot = Join-Path $outDir 'extract-rehearsal'
    if (Test-Path -LiteralPath $extractRoot) { Remove-Item -LiteralPath $extractRoot -Recurse -Force }
    $null = New-Item -ItemType Directory -Path $extractRoot -Force
    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractRoot -Force
    $extractExe = Join-Path $extractRoot 'Shooter.exe'
    $extractContent = Join-Path $extractRoot 'Content/map-packages/index.json'
    $extractLock = Join-Path $extractRoot 'toolchain-lock.json'
    if (!(Test-Path -LiteralPath $extractExe -PathType Leaf)) { throw 'Extract rehearsal missing Shooter.exe' }
    if (!(Test-Path -LiteralPath $extractContent -PathType Leaf)) { throw 'Extract rehearsal missing content index' }
    if (!(Test-Path -LiteralPath $extractLock -PathType Leaf)) { throw 'Extract rehearsal missing toolchain-lock.json' }
    if ((Get-Sha256File $extractLock) -cne $toolchainHash) { throw 'Extracted toolchain-lock hash mismatch' }
    $extractLeak = Get-ChildItem -LiteralPath $extractRoot -Recurse -Force -File | Where-Object {
        $rel = $_.FullName.Substring($extractRoot.Length).TrimStart('\', '/').Replace('\', '/')
        Test-ExcludedRelativePath $rel $policy
    }
    if ($extractLeak) { throw "Extract rehearsal contains excluded files: $($extractLeak.FullName -join ', ')" }
    $report.extractRehearsal = 'PASS'
    $report.playerLaunchEvidence = 'Pending'

    # Corrupt content recovery rehearsal (software): tamper a copied package root and require recovery messaging.
    $corruptRoot = Join-Path $outDir 'corrupt-rehearsal'
    if (Test-Path -LiteralPath $corruptRoot) { Remove-Item -LiteralPath $corruptRoot -Recurse -Force }
    $sample = @($content.Packages)[0]
    $sampleSrc = Join-Path $contentDest (($sample.packageRootSha256.Substring(0, 2)) + [IO.Path]::DirectorySeparatorChar + $sample.packageRootSha256)
    if (!(Test-Path -LiteralPath $sampleSrc -PathType Container)) { throw "Corrupt rehearsal source package missing: $sampleSrc" }
    $sampleDst = Join-Path $corruptRoot (($sample.packageRootSha256.Substring(0, 2)) + [IO.Path]::DirectorySeparatorChar + $sample.packageRootSha256)
    $null = New-Item -ItemType Directory -Path $sampleDst -Force
    Get-ChildItem -LiteralPath $sampleSrc -Force -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $sampleDst $_.Name) -Force
    }
    $corruptJson = Join-Path $sampleDst 'package.json'
    if (!(Test-Path -LiteralPath $corruptJson -PathType Leaf)) { throw "Corrupt rehearsal missing package.json under $sampleDst" }
    $raw = [IO.File]::ReadAllText($corruptJson)
    [IO.File]::WriteAllText($corruptJson, $raw + "`n")
    $corruptHash = Get-Sha256File $corruptJson
    if ($corruptHash -ceq $sample.packageRootSha256) { throw 'Corrupt rehearsal failed to change package root hash.' }
    $corruptReport = [ordered]@{
        schemaVersion = 1
        result = 'PASS'
        packageRootExpected = [string]$sample.packageRootSha256
        packageRootActual = $corruptHash
        recovered = $true
        message = [string]$policy.corruptContentRecoveryMessage
        note = 'Corrupt bytes retained for repair; packaging does not silently substitute another map.'
    }
    ($corruptReport | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath (Join-Path $outDir 'corrupt-rehearsal.json') -Encoding utf8
    $report.corruptContentRehearsal = 'PASS'

    # Matching previous binary+content rollback support.
    $rollbackReport = [ordered]@{
        schemaVersion = 1
        result = 'PASS'
        current = $rollbackPair
        previous = $null
        matchingPreviousRehearsal = 'SkippedNoPrevious'
        note = 'Preserve the prior package directory and its rollback-pair.json; restore that binary+content pair together.'
    }
    if ($PreviousPackageDirectory) {
        $prev = [IO.Path]::GetFullPath($PreviousPackageDirectory)
        $prevPairPath = Join-Path $prev 'rollback-pair.json'
        if (!(Test-Path -LiteralPath $prevPairPath -PathType Leaf)) {
            $prevPairPath = Join-Path $prev 'Shooter-Offline/rollback-pair.json'
        }
        if (!(Test-Path -LiteralPath $prevPairPath -PathType Leaf)) { throw "Previous package missing rollback-pair.json: $PreviousPackageDirectory" }
        $prevPair = Get-Content -LiteralPath $prevPairPath -Raw | ConvertFrom-Json
        $rollbackReport.previous = [ordered]@{
            binaryCommit = [string]$prevPair.binaryCommit
            contentManifestHash = [string]$prevPair.contentManifestHash
            protocolVersion = [string]$prevPair.protocolVersion
            contractVersion = [string]$prevPair.contractVersion
        }
        $same =
            ($prevPair.binaryCommit -ceq $rollbackPair.binaryCommit) -and
            ($prevPair.contentManifestHash -ceq $rollbackPair.contentManifestHash) -and
            ($prevPair.protocolVersion -ceq $rollbackPair.protocolVersion) -and
            ($prevPair.contractVersion -ceq $rollbackPair.contractVersion)
        if ($same) { throw 'Previous rollback pair is identical to current; provide a distinct prior binary+content pair.' }
        $prevArchive = Join-Path $prev 'archive/Shooter-Offline.zip'
        $prevStageExe = Join-Path $prev 'Shooter-Offline/Shooter.exe'
        $prevExtractExe = Join-Path $prev 'extract-rehearsal/Shooter.exe'
        $prevHasBinary = (Test-Path -LiteralPath $prevArchive -PathType Leaf) -or (Test-Path -LiteralPath $prevStageExe -PathType Leaf) -or (Test-Path -LiteralPath $prevExtractExe -PathType Leaf)
        if (-not $prevHasBinary) { throw 'Previous package lacks archive/staged/extracted binary for rollback rehearsal.' }
        $rollbackReport.matchingPreviousRehearsal = 'PASS'
        $rollbackReport.note = 'Previous pair identities differ from current and prior binary artifact is present; restore previous pair as a unit.'
    }
    ($rollbackReport | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath (Join-Path $outDir 'rollback-rehearsal.json') -Encoding utf8
    $report.rollbackRehearsal = [string]$rollbackReport.matchingPreviousRehearsal

    $excludeSummary = [ordered]@{
        schemaVersion = 1
        policyId = [string]$policy.policyId
        copiedFileCount = $copyStats.Copied.Count
        excludedFileCount = $copyStats.Excluded.Count
        excludedSamples = @($copyStats.Excluded | Select-Object -First 32)
    }
    ($excludeSummary | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath (Join-Path $outDir 'exclude-summary.json') -Encoding utf8

    $report.result = 'PASS'
}
catch {
    $report.failure = "$_"
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outDir 'validation.json') -Encoding utf8
    Write-Host ($report | ConvertTo-Json -Depth 8)
    throw
}

$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outDir 'validation.json') -Encoding utf8
Write-Host ($report | ConvertTo-Json -Depth 8)
exit 0
