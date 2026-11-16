[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$policy = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'provenance-allowlist.json') -Raw | ConvertFrom-Json
$assetRoot = Join-Path $repoRoot 'UnityProject/Assets'
$failures = [Collections.Generic.List[string]]::new()
$guids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$generated = @{}
$manifestPath = Join-Path $repoRoot $policy.generatedAssetsManifest
# Narrow, hash-pinned native source allowance: exactly the named paths, never an extension wildcard.
$namedNative = @{}
foreach ($native in @($policy.namedNativeSources | Where-Object { $null -ne $_ })) {
    if ($native.path -isnot [string] -or $native.path -cnotmatch '^UnityProject/Assets/[A-Za-z0-9_./-]+\.cpp$' -or $native.path.Contains('..') -or $native.sha256 -cnotmatch '^[0-9a-f]{64}$') { throw 'Malformed namedNativeSources entry' }
    $namedNative[[IO.Path]::GetFullPath((Join-Path $repoRoot $native.path))] = $native.sha256
}
# ADR-066: hash-pinned UI typeface bank (.ttf only under Presentation/Fonts).
$namedFonts = @{}
foreach ($font in @($policy.namedFontSources | Where-Object { $null -ne $_ })) {
    if ($font.path -isnot [string] -or $font.path -cnotmatch '^UnityProject/Assets/Shooter/Presentation/Fonts/[A-Za-z0-9_./-]+\.ttf$' -or $font.path.Contains('..') -or $font.sha256 -cnotmatch '^[0-9a-f]{64}$') { throw 'Malformed namedFontSources entry' }
    $namedFonts[[IO.Path]::GetFullPath((Join-Path $repoRoot $font.path))] = $font.sha256
}
# ADR-075: vendored third-party C# lives only under a ThirdParty/ folder and is hash-pinned with its upstream identity.
# ADR-075: prebuilt native binaries are hash-pinned per file, Win64 Plugins only. Malformed entries are reported, not thrown.
$namedThirdParty = @{}
$namedBinaries = @{}
function Test-UpstreamFields($Entry) {
    foreach ($field in @('upstream', 'tag', 'license')) {
        if ($Entry.$field -isnot [string] -or [string]::IsNullOrWhiteSpace($Entry.$field)) { return $false }
    }
    return $Entry.upstream -cmatch '^https://' -and $Entry.sha256 -is [string] -and $Entry.sha256 -cmatch '^[0-9a-f]{64}$'
}
foreach ($code in @($policy.PSObject.Properties['namedThirdPartyCodeSources'] | ForEach-Object { $_.Value } | Where-Object { $null -ne $_ })) {
    if ($code.path -isnot [string] -or $code.path -cnotmatch '^UnityProject/Assets/Shooter/[A-Za-z0-9_./-]+/ThirdParty/[A-Za-z0-9_.-]+\.cs$' -or $code.path.Contains('..') -or
        !(Test-UpstreamFields $code) -or $code.upstreamSha256 -isnot [string] -or $code.upstreamSha256 -cnotmatch '^[0-9a-f]{64}$') { $failures.Add('Malformed namedThirdPartyCodeSources entry'); continue }
    $namedThirdParty[[IO.Path]::GetFullPath((Join-Path $repoRoot $code.path))] = $code.sha256
}
foreach ($binary in @($policy.PSObject.Properties['namedNativeBinaries'] | ForEach-Object { $_.Value } | Where-Object { $null -ne $_ })) {
    if ($binary.path -isnot [string] -or $binary.path -cnotmatch '^UnityProject/Assets/Plugins/[A-Za-z0-9_./-]+\.dll$' -or $binary.path.Contains('..') -or
        !(Test-UpstreamFields $binary) -or $binary.platform -cne 'Win64') { $failures.Add('Malformed namedNativeBinaries entry'); continue }
    $namedBinaries[[IO.Path]::GetFullPath((Join-Path $repoRoot $binary.path))] = $binary.sha256
}
function Test-ProofFile($Proof, [string]$Kind) {
    if ($Proof -isnot [pscustomobject] -or $Proof.path -isnot [string] -or [string]::IsNullOrWhiteSpace($Proof.path) -or
        $Proof.sha256 -isnot [string] -or $Proof.sha256 -cnotmatch '^[0-9a-f]{64}$') { throw "Malformed $Kind provenance proof" }
    if ([IO.Path]::IsPathRooted($Proof.path) -or $Proof.path.Contains(':')) { throw 'Provenance paths must be relative' }
    $path = [IO.Path]::GetFullPath((Join-Path $repoRoot $Proof.path))
    if (!$path.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Escaping provenance path' }
    # Windows aliases must not turn one file into distinct proof identities.
    foreach ($part in ($Proof.path -split '[/\\]')) {
        if ($part -notin @('.', '..') -and ($part.EndsWith('.') -or $part.EndsWith(' '))) { throw 'Ambiguous provenance path' }
    }
    if ($path.Equals($manifestPath, [StringComparison]::OrdinalIgnoreCase)) { throw 'Self-referencing provenance proof' }
    if (!$proofPaths.Add($path)) { throw "Duplicate or overlapping provenance proof: $path" }
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $Proof.sha256) { throw "Provenance hash mismatch: $path" }
    if ($Kind -eq 'artifact') {
        # shooter.foundation.settings v1.0.0 emits only numeric Settings assets/materials and its code-owned scene.
        $settings = Join-Path $assetRoot 'Shooter/Settings'
        $scene = Join-Path $assetRoot 'Shooter/Bootstrap/Scenes/Bootstrap.unity'
        if (!($path.Equals($scene, [StringComparison]::OrdinalIgnoreCase) -or
            ([IO.Path]::GetDirectoryName($path).Equals($settings, [StringComparison]::OrdinalIgnoreCase) -and
             [IO.Path]::GetExtension($path) -cin @('.asset', '.mat')))) { throw "Unexpected settings generator artifact: $path" }
    }
    return $path
}
if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($manifest.schemaVersion -ne 1 -or $manifest.generatorId -cne 'shooter.foundation.settings' -or $manifest.generatorVersion -cne '1.0.0') { throw 'Unknown settings generator/version' }
        if ($manifest.toolchainHash -isnot [string] -or $manifest.toolchainHash -cnotmatch '^[0-9a-f]{64}$' -or
            (Get-FileHash -LiteralPath (Join-Path $repoRoot 'toolchain-lock.json') -Algorithm SHA256).Hash.ToLowerInvariant() -cne $manifest.toolchainHash) { throw 'Generated settings toolchain mismatch' }
        foreach ($kind in @('sources', 'artifacts')) {
            if ($manifest.$kind -isnot [array] -or $manifest.$kind.Count -eq 0) { throw "Missing or malformed provenance $kind collection" }
        }
        $proofPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($source in $manifest.sources) { $null = Test-ProofFile $source 'source' }
        $artifactPaths = foreach ($artifact in $manifest.artifacts) { Test-ProofFile $artifact 'artifact' }
        # No proof may whitelist an asset until the entire manifest has validated.
        foreach ($path in $artifactPaths) { $generated[$path] = $true }
    }
    catch { $failures.Add($_.Exception.Message) }
}
else { $failures.Add('Generated settings provenance is missing') }
$assetFiles = @(Get-ChildItem -LiteralPath $assetRoot -Recurse -Force)
foreach ($item in $assetFiles) {
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { $failures.Add("Unreviewed reparse point: $($item.FullName)") }
    if (!$item.PSIsContainer -and $item.Extension -notin $policy.assetSourceExtensions -and !$generated.ContainsKey($item.FullName) -and !$namedNative.ContainsKey($item.FullName) -and !$namedFonts.ContainsKey($item.FullName) -and !$namedBinaries.ContainsKey($item.FullName)) { $failures.Add("Unapproved asset source: $($item.FullName)") }
    if (!$item.PSIsContainer -and $item.Extension -ne '.meta' -and $item.FullName -match '[\\/]ThirdParty[\\/]' -and !$namedThirdParty.ContainsKey($item.FullName)) { $failures.Add("Unlisted third-party source: $($item.FullName)") }
    if ($item.Extension -eq '.meta') {
        $target = $item.FullName.Substring(0, $item.FullName.Length - 5)
        if (!(Test-Path -LiteralPath $target)) { $failures.Add("Orphan metadata: $($item.FullName)") }
        $guidLine = @(Get-Content -LiteralPath $item.FullName | Where-Object { $_ -match '^guid: ' })
        if ($guidLine.Count -ne 1 -or $guidLine[0] -cnotmatch '^guid: [0-9a-f]{32}$' -or !$guids.Add($guidLine[0])) { $failures.Add("Invalid/duplicate GUID: $($item.FullName)") }
    }
    elseif (!(Test-Path -LiteralPath ($item.FullName + '.meta') -PathType Leaf)) { $failures.Add("Missing metadata: $($item.FullName)") }
}
foreach ($fixture in $policy.nonShippingFixtures) {
    $path = [IO.Path]::GetFullPath((Join-Path $repoRoot $fixture.path))
    if (!$path.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Escaping allowlist path' }
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { $failures.Add("Missing fixture: $path"); continue }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $fixture.sha256) { $failures.Add("Fixture hash mismatch: $path") }
}
foreach ($native in $namedNative.GetEnumerator()) {
    if (!(Test-Path -LiteralPath $native.Key -PathType Leaf)) { $failures.Add("Missing named native source: $($native.Key)"); continue }
    if ((Get-FileHash -LiteralPath $native.Key -Algorithm SHA256).Hash.ToLowerInvariant() -cne $native.Value) { $failures.Add("Named native source hash mismatch: $($native.Key)") }
}
foreach ($font in $namedFonts.GetEnumerator()) {
    if (!(Test-Path -LiteralPath $font.Key -PathType Leaf)) { $failures.Add("Missing named font source: $($font.Key)"); continue }
    if ((Get-FileHash -LiteralPath $font.Key -Algorithm SHA256).Hash.ToLowerInvariant() -cne $font.Value) { $failures.Add("Named font source hash mismatch: $($font.Key)") }
}
foreach ($code in $namedThirdParty.GetEnumerator()) {
    if (!(Test-Path -LiteralPath $code.Key -PathType Leaf)) { $failures.Add("Missing third-party code source: $($code.Key)"); continue }
    if ((Get-FileHash -LiteralPath $code.Key -Algorithm SHA256).Hash.ToLowerInvariant() -cne $code.Value) { $failures.Add("Third-party code hash mismatch: $($code.Key)") }
}
foreach ($binary in $namedBinaries.GetEnumerator()) {
    if (!(Test-Path -LiteralPath $binary.Key -PathType Leaf)) { $failures.Add("Missing native binary: $($binary.Key)"); continue }
    if ((Get-FileHash -LiteralPath $binary.Key -Algorithm SHA256).Hash.ToLowerInvariant() -cne $binary.Value) { $failures.Add("Native binary hash mismatch: $($binary.Key)") }
}
$reportDirectory = Join-Path $repoRoot 'Artifacts/source-provenance'
$null = New-Item -ItemType Directory -Path $reportDirectory -Force
$report = [ordered]@{
    schemaVersion = 1
    result = $(if ($failures.Count) { 'FAIL' } else { 'PASS' })
    checkedAssetEntries = $assetFiles.Count
    sourceGuids = $guids.Count
    errors = @($failures)
    scope = $policy.scope
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $reportDirectory 'result.json') -Encoding utf8NoBOM
Write-Host ($report | ConvertTo-Json -Depth 5)
if ($failures.Count) { exit 1 }
