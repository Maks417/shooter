[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$policy = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'provenance-allowlist.json') -Raw | ConvertFrom-Json
$assetRoot = Join-Path $repoRoot 'UnityProject/Assets'
$failures = [Collections.Generic.List[string]]::new()
$guids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$assetFiles = @(Get-ChildItem -LiteralPath $assetRoot -Recurse -Force)
foreach ($item in $assetFiles) {
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { $failures.Add("Unreviewed reparse point: $($item.FullName)") }
    if (!$item.PSIsContainer -and $item.Extension -notin $policy.assetSourceExtensions) { $failures.Add("Unapproved asset source: $($item.FullName)") }
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
