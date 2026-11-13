[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ClientBuildRunDirectory,
    [ValidateRange(30, 3600)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$outDir = Join-Path $repoRoot "Artifacts/m11-playable-release/$sourceCommit/package-$runId"
$null = New-Item -ItemType Directory -Path $outDir -Force
$report = [ordered]@{
    schemaVersion = 1
    action = 'Package-Release'
    sourceCommit = $sourceCommit
    sourceDirty = [bool](& git -C $repoRoot status --porcelain)
    result = 'FAIL'
    packageDirectory = $outDir
    failure = $null
}
try {
    $player = Join-Path $ClientBuildRunDirectory 'player'
    if (!(Test-Path -LiteralPath $player)) { throw "Missing player directory: $player" }
    $dest = Join-Path $outDir 'Shooter-Offline'
    Copy-Item -LiteralPath $player -Destination $dest -Recurse -Force
    @"
Shooter offline team-deathmatch (local package)
Source: $sourceCommit
Controls: WASD move, mouse look, LMB fire, RMB aim, R reload, Esc pause
Maps: -shooterMap training-compound|depot-yard
Team size: -shooterTeamSize 5|6
Diagnostic smoke: -shooterSmokeSeconds 2
"@ | Set-Content (Join-Path $dest 'CONTROLS.txt') -Encoding UTF8
    $hashes = Get-ChildItem -LiteralPath $dest -Recurse -File | ForEach-Object {
        $sha = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        [pscustomobject]@{ path = $_.FullName.Substring($dest.Length + 1).Replace('\','/'); sha256 = $sha }
    }
    $hashes | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $outDir 'inventory.json') -Encoding UTF8
    $report.result = 'PASS'
}
catch {
    $report.failure = "$_"
    $report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $outDir 'validation.json') -Encoding UTF8
    throw
}
$report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $outDir 'validation.json') -Encoding UTF8
Write-Host ($report | ConvertTo-Json -Depth 6)
exit 0
