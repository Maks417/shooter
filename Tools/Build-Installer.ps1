[CmdletBinding()]
param(
    # A Package-Release output directory (contains validation.json, inventory.json and Shooter-Offline/).
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    # Monotonic release number supplied by the integrator, n.n.n (the installer's AppVersion).
    [Parameter(Mandatory = $true)][string]$ReleaseVersion,
    [string]$IsccPath = '',
    # Optional installer-report.json of the previous release; ReleaseVersion must be strictly greater.
    [string]$PreviousInstallerReport = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$policyPath = Join-Path $PSScriptRoot 'installer/installer-policy.json'
$issPath = Join-Path $PSScriptRoot 'installer/shooter.iss'
if (!(Test-Path -LiteralPath $policyPath -PathType Leaf)) { throw "Missing installer policy: $policyPath" }
if (!(Test-Path -LiteralPath $issPath -PathType Leaf)) { throw "Missing installer script: $issPath" }
$policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json

function Get-Sha256File([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$sourceCommit = 'preflight-rejected'
$outDir = $null
$shortOut = $null
$extraDir = $null
$report = [ordered]@{
    schemaVersion = 1
    action = 'Build-Installer'
    policyId = [string]$policy.policyId
    result = 'FAIL'
    installerTechnology = "$($policy.installerTechnology) $($policy.isccVersion)"
    isccPath = $null
    isccSha256 = $null
    appId = [string]$policy.appId
    installScope = [string]$policy.installScope
    releaseVersion = $ReleaseVersion
    sourceCommit = $null
    sourceHash = $null
    toolchainHash = $null
    buildContentManifestHash = $null
    contentClosureHash = $null
    contentIndexSha256 = $null
    packageDirectory = $null
    packageValidationResult = $null
    packageInventoryFiles = $null
    workspaceHead = $null
    workspaceDirty = $null
    installerPath = $null
    installerSha256 = $null
    installerBytes = $null
    releaseAcceptance = 'NotClaimed'
    scope = 'Installer artifact build only. INS01-INS06 and REL01/REL06 are separate evidence; nothing here is acceptance.'
    failure = $null
}

function Save-Report {
    if ($null -eq $outDir) { return }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outDir 'installer-report.json') -Encoding utf8
}

try {
    try {
        $report.workspaceHead = (& git -C $repoRoot rev-parse HEAD 2>$null).Trim()
        $report.workspaceDirty = [bool](& git -C $repoRoot status --porcelain 2>$null)
    } catch { }

    $PackageDirectory = [IO.Path]::GetFullPath($PackageDirectory)
    if (!(Test-Path -LiteralPath $PackageDirectory -PathType Container)) { throw "PackageDirectory not found: $PackageDirectory" }
    $report.packageDirectory = $PackageDirectory

    $validationPath = Join-Path $PackageDirectory 'validation.json'
    if (!(Test-Path -LiteralPath $validationPath -PathType Leaf)) { throw "Package validation.json missing (not a Package-Release output): $validationPath" }
    $validation = Get-Content -LiteralPath $validationPath -Raw | ConvertFrom-Json
    $commit = [string]$validation.sourceCommit
    if ($commit -notmatch '^[0-9a-f]{7,40}$') { throw 'Package validation has no usable sourceCommit.' }
    $sourceCommit = $commit
    $outDir = Join-Path $repoRoot "Artifacts/m11-playable-release/$sourceCommit/installer-$runId"
    $null = New-Item -ItemType Directory -Path $outDir -Force

    if ($ReleaseVersion -notmatch '^(0|[1-9]\d{0,4})\.(0|[1-9]\d{0,4})\.(0|[1-9]\d{0,4})$') { throw "ReleaseVersion must be n.n.n numeric (no leading zeros): $ReleaseVersion" }
    if ($PreviousInstallerReport) {
        $prevReport = Get-Content -LiteralPath ([IO.Path]::GetFullPath($PreviousInstallerReport)) -Raw | ConvertFrom-Json
        if ([version]$ReleaseVersion -le [version]([string]$prevReport.releaseVersion)) {
            throw "ReleaseVersion $ReleaseVersion is not greater than previous $($prevReport.releaseVersion) (monotonic release numbers only)."
        }
    }

    # ---- Pinned compiler ----
    if (!$IsccPath) { $IsccPath = Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe' }
    $IsccPath = [IO.Path]::GetFullPath($IsccPath)
    if (!(Test-Path -LiteralPath $IsccPath -PathType Leaf)) { throw "ISCC.exe not found: $IsccPath" }
    $isccHash = Get-Sha256File $IsccPath
    $report.isccPath = $IsccPath
    $report.isccSha256 = $isccHash
    if ($isccHash -cne ([string]$policy.isccSha256).ToLowerInvariant()) {
        throw "ISCC hash mismatch: found $isccHash, policy pins $($policy.isccSha256) (Inno Setup $($policy.isccVersion))."
    }

    # ---- Package validity / identities ----
    $report.packageValidationResult = [string]$validation.result
    if ($validation.action -cne 'Package-Release') { throw "validation.json action is not Package-Release: $($validation.action)" }
    if ($validation.result -cne 'PASS') { throw "Package validation result is not PASS: $($validation.result)" }
    if ($validation.buildSourceDirty -ne $false) { throw 'Dirty or unknown build candidate rejected (buildSourceDirty is not false).' }
    foreach ($field in @('sourceHash', 'toolchainHash', 'buildContentManifestHash', 'contentClosureHash', 'contentIndexSha256')) {
        if ([string]::IsNullOrWhiteSpace([string]$validation.$field)) { throw "Package validation missing $field." }
    }
    $report.sourceCommit = $sourceCommit
    $report.sourceHash = [string]$validation.sourceHash
    $report.toolchainHash = [string]$validation.toolchainHash
    $report.buildContentManifestHash = [string]$validation.buildContentManifestHash
    $report.contentClosureHash = [string]$validation.contentClosureHash
    $report.contentIndexSha256 = [string]$validation.contentIndexSha256

    $stage = Join-Path $PackageDirectory 'Shooter-Offline'
    if (!(Test-Path -LiteralPath $stage -PathType Container)) { throw "Staged package missing: $stage" }
    foreach ($rel in @($policy.requiredPackageFiles)) {
        if (!(Test-Path -LiteralPath (Join-Path $stage $rel) -PathType Leaf)) { throw "Package missing required file: $rel" }
    }

    $identities = Get-Content -LiteralPath (Join-Path $stage 'identities.json') -Raw | ConvertFrom-Json
    $pairs = @(
        @('binaryCommit', 'sourceCommit'), @('sourceHash', 'sourceHash'), @('toolchainHash', 'toolchainHash'),
        @('buildContentManifestHash', 'buildContentManifestHash'), @('contentClosureHash', 'contentClosureHash'),
        @('contentIndexSha256', 'contentIndexSha256'))
    foreach ($p in $pairs) {
        if ([string]$identities.($p[0]) -cne [string]$validation.($p[1])) { throw "Identity mismatch between package identities.json and validation.json: $($p[0])" }
    }
    if ((Get-Sha256File (Join-Path $stage 'toolchain-lock.json')) -cne [string]$validation.toolchainHash) { throw 'Packaged toolchain-lock.json does not match package toolchainHash.' }
    $rollback = Get-Content -LiteralPath (Join-Path $stage 'rollback-pair.json') -Raw | ConvertFrom-Json
    if ([string]$rollback.binaryCommit -cne $sourceCommit -or [string]$rollback.contentManifestHash -cne [string]$validation.contentClosureHash) {
        throw 'rollback-pair.json does not match package identities.'
    }

    # ---- Inventory: every file hashed, nothing extra, nothing missing ----
    $inventoryPath = Join-Path $PackageDirectory 'inventory.json'
    if (!(Test-Path -LiteralPath $inventoryPath -PathType Leaf)) { throw "Package inventory.json missing: $inventoryPath" }
    $inventory = @(Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json)
    if ($inventory.Count -lt 1) { throw 'Package inventory is empty.' }
    $stageFull = [IO.Path]::GetFullPath($stage)
    $actualFiles = @(Get-ChildItem -LiteralPath $stageFull -Recurse -Force -File | ForEach-Object {
        $_.FullName.Substring($stageFull.Length).TrimStart('\', '/').Replace('\', '/')
    })
    $inventoryPaths = @($inventory | ForEach-Object { [string]$_.path })
    $extra = @($actualFiles | Where-Object { $inventoryPaths -cnotcontains $_ })
    $missing = @($inventoryPaths | Where-Object { $actualFiles -cnotcontains $_ })
    if ($extra.Count -gt 0) { throw "Staged package has files absent from inventory (first: $($extra[0]))." }
    if ($missing.Count -gt 0) { throw "Inventory lists files missing from staged package (first: $($missing[0]))." }
    foreach ($entry in $inventory) {
        $actual = Get-Sha256File (Join-Path $stageFull ([string]$entry.path).Replace('/', '\'))
        if ($actual -cne [string]$entry.sha256) { throw "Inventory hash mismatch: $($entry.path)" }
    }
    $report.packageInventoryFiles = $inventory.Count

    # ---- Installer-side metadata ----
    # Built under a short temp path (ISCC mishandles long Artifacts paths) and copied to the evidence folder after the compile.
    $extraDir = Join-Path ([IO.Path]::GetTempPath()) ('sx' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    $null = New-Item -ItemType Directory -Path $extraDir -Force
    $payloadLines = @($inventoryPaths | Where-Object { $_ -ne 'Shooter.exe' } | ForEach-Object { $_.Replace('/', '\') } | Sort-Object)
    [IO.File]::WriteAllText((Join-Path $extraDir 'payload-files.txt'), (($payloadLines + @('Shooter.exe')) -join "`r`n") + "`r`n")
    $installerIdentity = [ordered]@{
        schemaVersion = 1
        appId = [string]$policy.appId
        releaseVersion = $ReleaseVersion
        installerTechnology = "$($policy.installerTechnology) $($policy.isccVersion)"
        binaryCommit = $sourceCommit
        sourceHash = [string]$validation.sourceHash
        toolchainHash = [string]$validation.toolchainHash
        buildContentManifestHash = [string]$validation.buildContentManifestHash
        contentClosureHash = [string]$validation.contentClosureHash
        contentIndexSha256 = [string]$validation.contentIndexSha256
        protocolVersion = [string]$validation.protocolVersion
        contractVersion = [string]$validation.contractVersion
        packageInventoryFiles = $inventory.Count
        note = 'Immutable build identities of the installed release pair. Written by Build-Installer.ps1; user settings are not stored here.'
    }
    ($installerIdentity | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath (Join-Path $extraDir 'installer-identity.json') -Encoding utf8

    # ---- Compile ----
    $outputName = "Shooter-Setup-$ReleaseVersion"
    $installerDir = Join-Path $outDir 'installer'
    $null = New-Item -ItemType Directory -Path $installerDir -Force
    # ISCC output goes to a short temp directory (long Artifacts paths break its temp-file copy), then is moved.
    $shortOut = Join-Path ([IO.Path]::GetTempPath()) ('so' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    $null = New-Item -ItemType Directory -Path $shortOut -Force
    # Packaged content paths can exceed MAX_PATH from deep Artifacts locations; compile through a short junction.
    $junction = Join-Path ([IO.Path]::GetTempPath()) ('si' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    $null = New-Item -ItemType Junction -Path $junction -Target $stageFull
    $isccArgs = @(
        '/Q',
        "/DAppId=$($policy.appId)",
        "/DAppVersion=$ReleaseVersion",
        "/DPublisher=$($policy.publisher)",
        "/DSourceDir=$junction",
        "/DExtraDir=$extraDir",
        "/DOutputDir=$shortOut",
        "/DOutputName=$outputName",
        $issPath)
    $isccLog = Join-Path $outDir 'iscc.log'
    try { & $IsccPath @isccArgs *> $isccLog; $isccExit = $LASTEXITCODE }
    finally { & cmd.exe /c rmdir "$junction" | Out-Null }
    if ($isccExit -ne 0) { throw "ISCC failed with exit code $isccExit; see $isccLog" }
    $builtPath = Join-Path $shortOut "$outputName.exe"
    if (!(Test-Path -LiteralPath $builtPath -PathType Leaf)) { throw "ISCC reported success but installer is missing: $builtPath" }
    $installerPath = Join-Path $installerDir "$outputName.exe"
    Move-Item -LiteralPath $builtPath -Destination $installerPath -Force
    Remove-Item -LiteralPath $shortOut -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item -LiteralPath $extraDir -Destination (Join-Path $outDir 'installer-extra') -Recurse -Force
    Remove-Item -LiteralPath $extraDir -Recurse -Force -ErrorAction SilentlyContinue
    $sha = Get-Sha256File $installerPath
    [IO.File]::WriteAllText("$installerPath.sha256", "$sha  $outputName.exe`n")
    $report.installerPath = $installerPath
    $report.installerSha256 = $sha
    $report.installerBytes = (Get-Item -LiteralPath $installerPath).Length
    $report.result = 'PASS'
}
catch {
    $report.failure = "$_"
    foreach ($tmp in @($shortOut, $extraDir)) { if ($tmp) { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue } }
    Save-Report
    Write-Host ($report | ConvertTo-Json -Depth 8)
    throw
}

Save-Report
Write-Host ($report | ConvertTo-Json -Depth 8)
exit 0
