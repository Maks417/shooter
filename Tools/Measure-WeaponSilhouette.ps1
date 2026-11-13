[CmdletBinding()]
param(
    [switch]$DryRun,
    [switch]$Capture,
    [string]$CaptureRoot = '',
    [string]$CacheRoot = '',
    [switch]$NoMidLods,
    [ValidateRange(30, 3600)][int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$python = (Get-Command python -ErrorAction Stop).Source
$module = Join-Path $PSScriptRoot 'weapon_silhouette.py'
$compileGallery = Join-Path $PSScriptRoot 'Compile-Gallery.ps1'
if (!(Test-Path -LiteralPath $module -PathType Leaf)) { throw "Missing $module" }
if (!(Test-Path -LiteralPath $compileGallery -PathType Leaf)) { throw "Missing $compileGallery" }

$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
$sourceDirty = [bool](& git -C $repoRoot status --porcelain)
$utc = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$packageRoot = Join-Path $repoRoot "Artifacts/m1-procedural-feasibility/$sourceCommit/weapon-silhouette-$utc"
$null = New-Item -ItemType Directory -Path $packageRoot -Force

$toolchainNote = 'Packages Compile-Gallery.ps1 -Capture weapon PNGs for human silhouette/ADS review. Never auto-passes visual judgment.'
$run = [ordered]@{
    schemaVersion = 1
    action = 'measure-weapon-silhouette'
    scope = 'M1 P03 formal weapon silhouette/ADS capture review package. Checklist remains pending.'
    sourceCommit = $sourceCommit
    sourceDirty = $sourceDirty
    packageRoot = $packageRoot
    packageStatus = 'PendingCapture'
    disposition = 'PendingReview'
    visualResult = 'pending'
    visualRealismPass = $false
    inventoryComplete = $false
    missing = @()
    reviewPath = $null
    captureRoot = $null
    galleryRun = $null
    toolchainNote = $toolchainNote
    result = 'FAIL'
    failure = $null
    commands = @()
    note = 'Packaging ReadyForHumanReview/PASS is not visual acceptance; disposition stays PendingReview until human review.'
}

function Write-RunReport {
    $run | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $packageRoot 'run.json') -Encoding utf8NoBOM
}

function Invoke-PythonPackage {
    param([string[]]$ExtraArgs, [switch]$AllowIncomplete)
    $argList = @(
        $module,
        '--output-dir', $packageRoot,
        '--source-commit', $sourceCommit,
        '--source-dirty', ($(if ($sourceDirty) { 'true' } else { 'false' })),
        '--toolchain-note', $toolchainNote
    )
    if ($NoMidLods) { $argList += '--no-mid-lods' }
    $argList += $ExtraArgs
    $logPath = Join-Path $packageRoot 'weapon_silhouette.log'
    $run.commands += [ordered]@{ executable = $python; arguments = $argList; log = $logPath }
    & $python @argList 2>&1 | Tee-Object -FilePath $logPath | Out-Host
    $code = $LASTEXITCODE
    if ($code -ne 0 -and -not $AllowIncomplete) {
        throw "weapon_silhouette.py failed (exit $code). See $logPath"
    }
    return $code
}

function Find-LatestGalleryCaptures([string]$Commit) {
    $parent = Join-Path $repoRoot "Artifacts/m0-foundation/$Commit"
    if (!(Test-Path -LiteralPath $parent -PathType Container)) { return $null }
    $candidates = @(Get-ChildItem -LiteralPath $parent -Directory -Filter 'compile-gallery-*' |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'captures') -PathType Container } |
        Sort-Object LastWriteTimeUtc -Descending)
    if ($candidates.Count -lt 1) { return $null }
    $top = $candidates[0]
    return [pscustomobject]@{
        GalleryRun = $top.FullName
        CaptureRoot = (Join-Path $top.FullName 'captures')
    }
}

try {
    Write-Host "Weapon silhouette review package: $packageRoot"

    $modeDryRun = [bool]$DryRun
    if (-not $Capture -and -not $CaptureRoot -and -not $DryRun) {
        $modeDryRun = $true
    }
    if ($Capture -and $CaptureRoot) {
        throw 'Specify either -Capture or -CaptureRoot, not both.'
    }

    if ($modeDryRun -and -not $Capture -and -not $CaptureRoot) {
        $null = Invoke-PythonPackage -ExtraArgs @('--dry-run')
        $reviewPath = Join-Path $packageRoot 'silhouette-review.json'
        if (!(Test-Path -LiteralPath $reviewPath -PathType Leaf)) { $reviewPath = Join-Path $packageRoot 'review-manifest.json' }
        $manifest = Get-Content -LiteralPath $reviewPath -Raw | ConvertFrom-Json
        $run.reviewPath = $reviewPath
        $run.packageStatus = 'PendingCapture'
        $run.disposition = [string]$manifest.disposition
        if (-not $run.disposition) { $run.disposition = 'PendingCapture' }
        $run.visualResult = 'pending'
        $run.visualRealismPass = $false
        $run.inventoryComplete = $false
        $run.missing = @($manifest.missing)
        $run.result = 'PendingCapture'
        Write-Host "PendingCapture: dry-run review package (no images fabricated). Evidence: $packageRoot"
    }
    else {
        $resolvedCaptureRoot = $null
        $galleryRun = $null

        if ($Capture) {
            if (!(Test-Path -LiteralPath (Join-Path $repoRoot '.local/unity.json') -PathType Leaf)) {
                $msg = 'Locked Unity registration (.local/unity.json) missing; cannot invoke Compile-Gallery -Capture.'
                Invoke-PythonPackage -ExtraArgs @('--dry-run', '--failure', $msg)
                $run.packageStatus = 'Failed'
                $run.failure = $msg
                $run.result = 'FAIL'
                throw $msg
            }
            $galleryArgs = @('-NoProfile', '-File', $compileGallery, '-Capture', '-TimeoutSeconds', "$TimeoutSeconds")
            if ($CacheRoot) { $galleryArgs += @('-CacheRoot', $CacheRoot) }
            $galleryLog = Join-Path $packageRoot 'compile-gallery.log'
            $pwsh = Join-Path $PSHOME 'pwsh.exe'
            if (!(Test-Path -LiteralPath $pwsh -PathType Leaf)) { $pwsh = 'pwsh' }
            $run.commands += [ordered]@{ executable = $pwsh; arguments = $galleryArgs; log = $galleryLog }
            & $pwsh @galleryArgs *> $galleryLog
            if ($LASTEXITCODE -ne 0) {
                $msg = "Compile-Gallery.ps1 -Capture failed (exit $LASTEXITCODE). See $galleryLog"
                Invoke-PythonPackage -ExtraArgs @('--dry-run', '--failure', $msg)
                $run.packageStatus = 'Failed'
                $run.failure = $msg
                $run.result = 'FAIL'
                throw $msg
            }
            $found = Find-LatestGalleryCaptures $sourceCommit
            if (-not $found) {
                $msg = 'Compile-Gallery reported success but no captures directory was found under Artifacts/m0-foundation.'
                Invoke-PythonPackage -ExtraArgs @('--dry-run', '--failure', $msg)
                $run.packageStatus = 'Failed'
                $run.failure = $msg
                $run.result = 'FAIL'
                throw $msg
            }
            $resolvedCaptureRoot = $found.CaptureRoot
            $galleryRun = $found.GalleryRun
        }
        else {
            $resolvedCaptureRoot = [IO.Path]::GetFullPath($CaptureRoot)
            if (!(Test-Path -LiteralPath $resolvedCaptureRoot -PathType Container)) {
                throw "CaptureRoot not found: $resolvedCaptureRoot"
            }
            $nested = Join-Path $resolvedCaptureRoot 'captures'
            if (Test-Path -LiteralPath $nested -PathType Container) {
                $galleryRun = $resolvedCaptureRoot
                $resolvedCaptureRoot = $nested
            }
            else {
                $galleryRun = Split-Path -Parent $resolvedCaptureRoot
            }
        }

        $run.captureRoot = $resolvedCaptureRoot
        $run.galleryRun = $galleryRun
        $extra = @('--capture-root', $resolvedCaptureRoot)
        if ($galleryRun) { $extra += @('--gallery-run', $galleryRun) }
        $inventoryExit = Invoke-PythonPackage -ExtraArgs $extra -AllowIncomplete

        $manifestPath = Join-Path $packageRoot 'silhouette-review.json'
        if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
            $manifestPath = Join-Path $packageRoot 'review-manifest.json'
        }
        if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'silhouette-review.json was not written.' }
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $run.reviewPath = $manifestPath
        $run.missing = @($manifest.missing)
        $run.inventoryComplete = [bool]$manifest.inventoryComplete
        $run.disposition = [string]$manifest.disposition
        $run.visualRealismPass = $false
        if ($manifest.visualResult -cne 'pending') { throw 'Harness must not auto-pass visualResult.' }
        if ($manifest.visualRealismPass -eq $true) { throw 'Harness must not set visualRealismPass.' }
        foreach ($field in @('scalePlausible', 'silhouetteIntact', 'adsAlignment', 'lowerLodReadable', 'prohibitedArtAbsent')) {
            if ($manifest.checklist.$field -cne 'pending') { throw "Checklist field $field must remain pending." }
        }
        if ($manifest.packageStatus -ceq 'ReadyForHumanReview' -and $manifest.inventoryComplete -and $inventoryExit -eq 0) {
            if ($run.disposition -cne 'PendingReview') { throw 'Complete packaging must keep disposition PendingReview.' }
            $run.packageStatus = 'ReadyForHumanReview'
            $run.visualResult = 'pending'
            $run.result = 'PASS'
            Write-Host "PASS (packaging): inventory complete; disposition=PendingReview; visualRealismPass=false. Evidence: $packageRoot"
        }
        else {
            $run.packageStatus = [string]$manifest.packageStatus
            if (-not $run.disposition) { $run.disposition = 'PendingCapture' }
            $run.visualResult = 'pending'
            $run.result = 'FAIL'
            $run.failure = "Required silhouette captures missing: $($run.missing -join ', ')"
            Write-Warning $run.failure
        }
    }
}
catch {
    $run.failure = $_.Exception.Message
    if (-not $run.failure) { $run.failure = "$_" }
    if ($run.result -ne 'PendingCapture') { $run.result = 'FAIL' }
    Write-Warning $run.failure
}
finally {
    Write-RunReport
}
if ($run.result -eq 'FAIL') { exit 1 }
# PendingCapture and ReadyForHumanReview are successful harness outcomes; neither is a visual PASS.
exit 0
