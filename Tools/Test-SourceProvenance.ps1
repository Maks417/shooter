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
    if (!$item.PSIsContainer -and $item.Extension -notin $policy.assetSourceExtensions -and !$generated.ContainsKey($item.FullName)) { $failures.Add("Unapproved asset source: $($item.FullName)") }
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
