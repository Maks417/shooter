[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
$evidence = Join-Path $repoRoot ('Artifacts/tooling-tests/source-provenance-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$null = New-Item -ItemType Directory -Path $evidence
$results = [Collections.Generic.List[object]]::new()
function Write-TestFile([string]$Root, [string]$Relative, [string]$Text) {
    $path = Join-Path $Root $Relative
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force
    [IO.File]::WriteAllText($path, $Text)
}
function Get-TestProof([string]$Root, [string]$Relative) {
    @{ path = $Relative; sha256 = (Get-FileHash -LiteralPath (Join-Path $Root $Relative) -Algorithm SHA256).Hash.ToLowerInvariant() }
}
function Invoke-ProvenanceCase([string]$Name, [scriptblock]$Mutate, [string]$ExpectedError = '', [string]$OutputAsset = 'UnityProject/Assets/Shooter/Settings/Test.asset') {
    $root = Join-Path $evidence $Name
    $null = New-Item -ItemType Directory -Path (Join-Path $root 'Tools') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Test-SourceProvenance.ps1') -Destination (Join-Path $root 'Tools/Test-SourceProvenance.ps1')
    Write-TestFile $root 'toolchain-lock.json' '{"syntheticTestOnly":true}'
    Write-TestFile $root 'UnityProject/Assets/Shooter/Build.cs' '// synthetic numeric generator test source'
    Write-TestFile $root $OutputAsset 'synthetic generated numeric settings'
    $manifestRelative = 'UnityProject/Assets/Shooter/Settings/provenance.json'
    Write-TestFile $root $manifestRelative '{}'
    $policy = @{ scope = 'Synthetic wrapper regression only; no art or hardware acceptance'; assetSourceExtensions = @('.cs', '.asmdef', '.meta', '.json'); generatedAssetsManifest = $manifestRelative; nonShippingFixtures = @() }
    Write-TestFile $root 'Tools/provenance-allowlist.json' ($policy | ConvertTo-Json -Depth 8)
    $manifest = @{ schemaVersion = 1; generatorId = 'shooter.foundation.settings'; generatorVersion = '1.0.0'; toolchainHash = (Get-TestProof $root 'toolchain-lock.json').sha256; sources = @((Get-TestProof $root 'UnityProject/Assets/Shooter/Build.cs')); artifacts = @((Get-TestProof $root $OutputAsset)) }
    & $Mutate $manifest $root
    Write-TestFile $root $manifestRelative ($manifest | ConvertTo-Json -Depth 12)
    $i = 0
    foreach ($item in @(Get-ChildItem -LiteralPath (Join-Path $root 'UnityProject/Assets') -Recurse -Force)) {
        if ($item.Extension -eq '.meta') { continue }
        $i++
        [IO.File]::WriteAllText($item.FullName + '.meta', ('fileFormatVersion: 2' + "`nguid: " + $i.ToString('x32') + "`n"))
    }
    $logPath = Join-Path $root 'wrapper.log'
    & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File (Join-Path $root 'Tools/Test-SourceProvenance.ps1') *> $logPath
    $code = $LASTEXITCODE
    $reportPath = Join-Path $root 'Artifacts/source-provenance/result.json'
    if (!(Test-Path -LiteralPath $reportPath -PathType Leaf)) { throw "$Name produced no report; see $logPath" }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($ExpectedError) {
        if ($code -eq 0 -or $report.result -cne 'FAIL' -or !(($report.errors -join "`n").Contains($ExpectedError))) { throw "$Name did not reject with '$ExpectedError'; see $logPath" }
    }
    elseif ($code -ne 0 -or $report.result -cne 'PASS' -or $report.errors.Count -ne 0) { throw "$Name did not pass; see $logPath" }
    $results.Add([ordered]@{ name = $Name; result = 'PASS'; wrapperExitCode = $code; expectedError = $ExpectedError; report = $reportPath; log = $logPath })
}
try {
    Invoke-ProvenanceCase 'valid-asset' {}
    Invoke-ProvenanceCase 'valid-material' {} '' 'UnityProject/Assets/Shooter/Settings/Test.mat'
    Invoke-ProvenanceCase 'valid-code-owned-scene' {} '' 'UnityProject/Assets/Shooter/Bootstrap/Scenes/Bootstrap.unity'
    Invoke-ProvenanceCase 'empty-sources' { param($m, $r) $m.sources = @() } 'sources collection'
    Invoke-ProvenanceCase 'missing-sources' { param($m, $r) $m.Remove('sources') } 'sources collection'
    Invoke-ProvenanceCase 'object-sources' { param($m, $r) $m.sources = $m.sources[0] } 'sources collection'
    Invoke-ProvenanceCase 'empty-artifacts' { param($m, $r) $m.artifacts = @() } 'artifacts collection'
    Invoke-ProvenanceCase 'missing-artifacts' { param($m, $r) $m.Remove('artifacts') } 'artifacts collection'
    Invoke-ProvenanceCase 'object-artifacts' { param($m, $r) $m.artifacts = $m.artifacts[0] } 'artifacts collection'
    Invoke-ProvenanceCase 'null-proof' { param($m, $r) $m.sources = @($null) } 'Malformed source provenance proof'
    Invoke-ProvenanceCase 'string-proof' { param($m, $r) $m.sources = @('bad') } 'Malformed source provenance proof'
    Invoke-ProvenanceCase 'empty-path' { param($m, $r) $m.sources[0].path = '' } 'Malformed source provenance proof'
    Invoke-ProvenanceCase 'numeric-path' { param($m, $r) $m.sources[0].path = 42 } 'Malformed source provenance proof'
    Invoke-ProvenanceCase 'uppercase-hash' { param($m, $r) $m.sources[0].sha256 = $m.sources[0].sha256.ToUpperInvariant() } 'Malformed source provenance proof'
    Invoke-ProvenanceCase 'short-hash' { param($m, $r) $m.sources[0].sha256 = 'abc' } 'Malformed source provenance proof'
    Invoke-ProvenanceCase 'numeric-hash' { param($m, $r) $m.sources[0].sha256 = 42 } 'Malformed source provenance proof'
    Invoke-ProvenanceCase 'rooted-path' { param($m, $r) $m.sources[0].path = Join-Path $r $m.sources[0].path } 'Provenance paths must be relative'
    Invoke-ProvenanceCase 'drive-relative-path' { param($m, $r) $m.sources[0].path = 'C:source.cs' } 'Provenance paths must be relative'
    Invoke-ProvenanceCase 'escaping-path' { param($m, $r) $m.sources[0].path = '../outside.cs' } 'Escaping provenance path'
    Invoke-ProvenanceCase 'trailing-dot-alias' { param($m, $r) $m.sources[0].path += '.' } 'Ambiguous provenance path'
    Invoke-ProvenanceCase 'duplicate-source-case' { param($m, $r) $p = @{ path = $m.sources[0].path.ToUpperInvariant(); sha256 = $m.sources[0].sha256 }; $m.sources += $p } 'Duplicate or overlapping provenance proof'
    Invoke-ProvenanceCase 'duplicate-artifact-alias' { param($m, $r) $p = @{ path = 'UnityProject/Assets/Shooter/Settings/../Settings/Test.asset'; sha256 = $m.artifacts[0].sha256 }; $m.artifacts += $p } 'Duplicate or overlapping provenance proof'
    Invoke-ProvenanceCase 'source-artifact-overlap' { param($m, $r) $m.artifacts = @($m.sources[0]) } 'Duplicate or overlapping provenance proof'
    Invoke-ProvenanceCase 'self-proof' { param($m, $r) $m.sources[0].path = 'UnityProject/Assets/Shooter/Settings/provenance.json' } 'Self-referencing provenance proof'
    Invoke-ProvenanceCase 'hash-mismatch' { param($m, $r) $m.artifacts[0].sha256 = '0' * 64 } 'Provenance hash mismatch'
    Invoke-ProvenanceCase 'missing-proof-file' { param($m, $r) $m.sources[0].path = 'missing.cs' } 'Provenance hash mismatch'
    Invoke-ProvenanceCase 'fake-image-proof' {} 'Unexpected settings generator artifact' 'UnityProject/Assets/Shooter/Settings/Imported.png'
    $imageReport = Get-Content -LiteralPath (Join-Path $evidence 'fake-image-proof/Artifacts/source-provenance/result.json') -Raw | ConvertFrom-Json
    if (!(($imageReport.errors -join "`n").Contains('Unapproved asset source:'))) { throw 'Rejected image proof still whitelisted its image' }
    Invoke-ProvenanceCase 'invalid-second-artifact' { param($m, $r) $m.artifacts += @{ path = ''; sha256 = '0' * 64 } } 'Malformed artifact provenance proof'
    $invalidReport = Get-Content -LiteralPath (Join-Path $evidence 'invalid-second-artifact/Artifacts/source-provenance/result.json') -Raw | ConvertFrom-Json
    if (!(($invalidReport.errors -join "`n").Contains('Unapproved asset source:'))) { throw 'Partially invalid manifest still whitelisted its first artifact' }
    $summary = [ordered]@{ result = 'PASS'; testCount = $results.Count; scope = 'Synthetic standalone provenance wrapper regressions only'; cases = @($results) }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'results.json') -Encoding utf8NoBOM
    Write-Host "PASS: $($results.Count) provenance wrapper cases; evidence: $evidence"
}
catch {
    [ordered]@{ result = 'FAIL'; testCount = $results.Count; error = $_.Exception.Message; cases = @($results) } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'results.json') -Encoding utf8NoBOM
    throw
}
