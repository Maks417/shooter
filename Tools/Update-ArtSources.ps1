[CmdletBinding()]
param()
# ADR-124: rewrites the file list of Tools/art-sources.json from what is on disk under UnityProject/Assets/Shooter/Art.
# Pack entries (author, source URL, license, attribution) are reviewed by hand and never generated here.
# Layout: Assets/Shooter/Art/<Category>/<pack-id>/..., and <pack-id> must already be a listed pack.
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$manifestPath = Join-Path $PSScriptRoot 'art-sources.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$packs = @{}
foreach ($pack in @($manifest.packs)) { $packs[[string]$pack.id] = $true }
$artRoot = Join-Path $repoRoot 'UnityProject/Assets/Shooter/Art'
$files = [Collections.Generic.List[object]]::new()
if (Test-Path -LiteralPath $artRoot) {
    foreach ($item in Get-ChildItem -LiteralPath $artRoot -Recurse -File | Where-Object { $_.Extension -in @('.fbx', '.png', '.tga') } | Sort-Object FullName) {
        $relative = $item.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
        $parts = $relative.Split('/')
        # UnityProject/Assets/Shooter/Art/<Category>/<pack-id>/...
        if ($parts.Count -lt 7) { throw "Art file is not inside a pack folder: $relative" }
        $packId = $parts[5]
        if (!$packs.ContainsKey($packId)) { throw "Art file belongs to an unlisted pack '$packId': $relative" }
        $files.Add([ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); pack = $packId })
    }
}
$out = [ordered]@{ schemaVersion = 1; policy = $manifest.policy; packs = @($manifest.packs); files = @($files) }
[IO.File]::WriteAllText($manifestPath, (($out | ConvertTo-Json -Depth 6) -replace "`r`n", "`n") + "`n")
Write-Host "art-sources.json: $($files.Count) files in $($packs.Count) packs"
