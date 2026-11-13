[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# Synthetic wrapper/regression only; does not claim visual or hardware acceptance.
$repoRoot = Split-Path -Parent $PSScriptRoot
$evidence = Join-Path $repoRoot ('Artifacts/tooling-tests/weapon-silhouette-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$null = New-Item -ItemType Directory -Path $evidence -Force
$python = (Get-Command python -ErrorAction Stop).Source
$measure = Join-Path $PSScriptRoot 'Measure-WeaponSilhouette.ps1'
$unit = Join-Path $PSScriptRoot 'test_weapon_silhouette.py'
$results = [Collections.Generic.List[object]]::new()

try {
    $unitLog = Join-Path $evidence 'unit.log'
    & $python $unit *> $unitLog
    if ($LASTEXITCODE -ne 0) { throw "test_weapon_silhouette.py failed; see $unitLog" }
    $results.Add([ordered]@{ name = 'unit-python'; result = 'PASS'; log = $unitLog })

    $dryLog = Join-Path $evidence 'measure-dry-run.log'
    & $measure -DryRun *> $dryLog
    if ($LASTEXITCODE -ne 0) { throw "Measure dry-run failed; see $dryLog" }
    $commit = (& git -C $repoRoot rev-parse HEAD).Trim()
    $runParent = Join-Path $repoRoot "Artifacts/m1-procedural-feasibility/$commit"
    $runDir = Get-ChildItem -LiteralPath $runParent -Directory -Filter 'weapon-silhouette-*' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if (-not $runDir) { throw 'Dry-run wrote no weapon-silhouette package directory' }
    $reviewPath = Join-Path $runDir.FullName 'silhouette-review.json'
    if (!(Test-Path -LiteralPath $reviewPath -PathType Leaf)) { $reviewPath = Join-Path $runDir.FullName 'review-manifest.json' }
    $manifest = Get-Content -LiteralPath $reviewPath -Raw | ConvertFrom-Json
    $pending = Get-Content -LiteralPath (Join-Path $runDir.FullName 'PendingCapture.json') -Raw | ConvertFrom-Json
    $run = Get-Content -LiteralPath (Join-Path $runDir.FullName 'run.json') -Raw | ConvertFrom-Json
    if ($manifest.visualResult -cne 'pending' -or $manifest.packageStatus -cne 'PendingCapture') {
        throw 'Dry-run manifest must be PendingCapture with visualResult pending'
    }
    if ($manifest.disposition -cne 'PendingCapture' -or $manifest.visualRealismPass -eq $true) {
        throw 'Dry-run must keep PendingCapture disposition and visualRealismPass=false'
    }
    foreach ($field in @('scalePlausible', 'silhouetteIntact', 'adsAlignment', 'lowerLodReadable', 'prohibitedArtAbsent')) {
        if ($manifest.checklist.$field -cne 'pending') { throw "Checklist $field must be pending" }
    }
    if ($pending.packageStatus -cne 'PendingCapture') { throw 'PendingCapture.json missing or wrong status' }
    if ($run.result -cne 'PendingCapture') { throw 'run.json must record PendingCapture' }
    if (Test-Path -LiteralPath (Join-Path $runDir.FullName 'images') -PathType Container) {
        throw 'Dry-run must not fabricate an images directory'
    }
    $results.Add([ordered]@{ name = 'measure-dry-run'; result = 'PASS'; package = $runDir.FullName })

    $captureRoot = Join-Path $evidence 'synthetic-captures'
    $null = New-Item -ItemType Directory -Path $captureRoot -Force
    foreach ($weapon in @('rifle', 'pistol')) {
        foreach ($angle in @('front', 'side', 'three-quarter', 'ads')) {
            [IO.File]::WriteAllBytes((Join-Path $captureRoot "$weapon-$angle.png"), [Text.Encoding]::UTF8.GetBytes("$weapon-$angle"))
        }
        foreach ($lod in 1..3) {
            [IO.File]::WriteAllBytes((Join-Path $captureRoot "$weapon-lod$lod.png"), [Text.Encoding]::UTF8.GetBytes("$weapon-lod$lod"))
        }
    }
    $pkgLog = Join-Path $evidence 'measure-capture-root.log'
    & $measure -CaptureRoot $captureRoot *> $pkgLog
    if ($LASTEXITCODE -ne 0) { throw "Measure -CaptureRoot failed; see $pkgLog" }
    $pkgDir = Get-ChildItem -LiteralPath $runParent -Directory -Filter 'weapon-silhouette-*' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    $pkgReview = Join-Path $pkgDir.FullName 'silhouette-review.json'
    if (!(Test-Path -LiteralPath $pkgReview -PathType Leaf)) { throw 'Complete package missing silhouette-review.json' }
    $pkgManifest = Get-Content -LiteralPath $pkgReview -Raw | ConvertFrom-Json
    $pkgRun = Get-Content -LiteralPath (Join-Path $pkgDir.FullName 'run.json') -Raw | ConvertFrom-Json
    if ($pkgManifest.packageStatus -cne 'ReadyForHumanReview' -or $pkgManifest.visualResult -cne 'pending') {
        throw 'CaptureRoot package must be ReadyForHumanReview with visualResult pending'
    }
    if ($pkgManifest.disposition -cne 'PendingReview' -or $pkgManifest.visualRealismPass -eq $true) {
        throw 'Complete package must keep disposition PendingReview and visualRealismPass=false'
    }
    if ($pkgRun.result -cne 'PASS') { throw 'Complete Measure run.json must PASS packaging' }
    if (!(Test-Path -LiteralPath (Join-Path $pkgDir.FullName 'images/rifle-ads.png') -PathType Leaf)) {
        throw 'Packaged rifle-ads.png missing'
    }
    $expected = (Get-FileHash -LiteralPath (Join-Path $captureRoot 'rifle-ads.png') -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($pkgManifest.images.'rifle-ads.png'.sha256 -cne $expected) { throw 'Packaged sha256 mismatch for rifle-ads.png' }
    $results.Add([ordered]@{ name = 'measure-complete-set'; result = 'PASS'; package = $pkgDir.FullName; disposition = $pkgManifest.disposition })

    # Missing capture set: packaging FAIL, silhouette-review.json lists missing, no visual PASS.
    $missingRoot = Join-Path $evidence 'missing-captures'
    $null = New-Item -ItemType Directory -Path $missingRoot -Force
    [IO.File]::WriteAllBytes((Join-Path $missingRoot 'rifle-front.png'), [Text.Encoding]::UTF8.GetBytes('rifle-front'))
    $missingLog = Join-Path $evidence 'measure-missing.log'
    & $measure -CaptureRoot $missingRoot *> $missingLog
    if ($LASTEXITCODE -eq 0) { throw 'Measure missing set should fail packaging' }
    $missingDir = Get-ChildItem -LiteralPath $runParent -Directory -Filter 'weapon-silhouette-*' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    $missingReviewPath = Join-Path $missingDir.FullName 'silhouette-review.json'
    if (!(Test-Path -LiteralPath $missingReviewPath -PathType Leaf)) { throw 'Missing package wrote no silhouette-review.json' }
    $missingManifest = Get-Content -LiteralPath $missingReviewPath -Raw | ConvertFrom-Json
    $missingRun = Get-Content -LiteralPath (Join-Path $missingDir.FullName 'run.json') -Raw | ConvertFrom-Json
    if ($missingRun.result -ceq 'PASS') { throw 'Missing packaging must not PASS' }
    if ($missingManifest.inventoryComplete) { throw 'Missing review must not claim inventoryComplete' }
    if (@($missingManifest.missing).Count -lt 7) { throw 'Missing review should list nearly all required views' }
    if ($missingManifest.disposition -cne 'PendingCapture' -and $missingManifest.disposition -cne 'PendingReview') {
        throw "Unexpected missing disposition: $($missingManifest.disposition)"
    }
    if ($missingManifest.visualRealismPass -eq $true) { throw 'Missing review must keep visualRealismPass=false' }
    $results.Add([ordered]@{
        name = 'measure-missing-set'
        result = 'PASS'
        package = $missingDir.FullName
        missingCount = @($missingManifest.missing).Count
        disposition = $missingManifest.disposition
    })

    $summaryPath = Join-Path $evidence 'summary.json'
    [ordered]@{
        result = 'PASS'
        caseCount = $results.Count
        cases = $results
        note = 'Harness regression only; formal silhouette/ADS visual review remains pending.'
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8NoBOM
    Write-Host "PASS: $($results.Count) weapon silhouette harness cases. Evidence: $evidence"
}
catch {
    Write-Warning $_.Exception.Message
    [ordered]@{ result = 'FAIL'; error = $_.Exception.Message; cases = $results } |
        ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $evidence 'summary.json') -Encoding utf8NoBOM
    exit 1
}
