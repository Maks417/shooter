[CmdletBinding()]
param(
    # Show this many of the largest Artifacts areas.
    [ValidateRange(1, 200)][int]$Top = 15
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Read-only disk report (ADR-076). It prints free space, the repository's heavy folders and the per-user caches outside
# the repository that Unity, NuGet and pip fill. It never deletes anything; see Clean-Artifacts.ps1 for reclaiming.
$repoRoot = Split-Path -Parent $PSScriptRoot

function Get-Bytes([string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) { return $null }
    $item = Get-Item -LiteralPath $Path -Force
    if (!$item.PSIsContainer) { return $item.Length }
    $options = [IO.EnumerationOptions]::new()
    $options.RecurseSubdirectories = $true; $options.IgnoreInaccessible = $true
    $options.AttributesToSkip = [IO.FileAttributes]::ReparsePoint
    $sum = [long]0
    foreach ($file in ([IO.DirectoryInfo]$item.FullName).EnumerateFiles('*', $options)) { $sum += $file.Length }
    $sum
}

function Format-Size($Bytes) {
    if ($null -eq $Bytes) { return 'absent' }
    if ($Bytes -ge 1GB) { return ('{0:N1} GB' -f ($Bytes / 1GB)) }
    if ($Bytes -ge 1MB) { return ('{0:N0} MB' -f ($Bytes / 1MB)) }
    '{0:N0} KB' -f ($Bytes / 1KB)
}

function Write-Rows([string]$Title, $Rows) {
    Write-Host ''
    Write-Host $Title
    foreach ($row in $Rows) { Write-Host ('  {0,10}  {1}' -f (Format-Size $row.Bytes), $row.Name) }
}

$drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($repoRoot))
Write-Host ('Drive {0}: {1} free of {2}' -f $drive.Name, (Format-Size $drive.AvailableFreeSpace), (Format-Size $drive.TotalSize))

$repoRows = foreach ($relative in @('Artifacts', '.local/Unity', '.local', 'UnityProject/Library', 'UnityProject/Library/Bee', 'UnityProject/Temp', 'UnityProject/Logs', 'Tools', '.git')) {
    [pscustomobject]@{ Name = $relative; Bytes = Get-Bytes (Join-Path $repoRoot $relative) }
}
Write-Rows 'Repository' $repoRows

$artifacts = Join-Path $repoRoot 'Artifacts'
if (Test-Path -LiteralPath $artifacts) {
    $areaRows = Get-ChildItem -LiteralPath $artifacts -Force | ForEach-Object {
        [pscustomobject]@{ Name = 'Artifacts/' + $_.Name; Bytes = Get-Bytes $_.FullName }
    } | Sort-Object Bytes -Descending | Select-Object -First $Top
    Write-Rows "Largest Artifacts areas (top $Top)" $areaRows
}

$userCaches = @(
    @{ Name = 'Unity global cache (%LOCALAPPDATA%\Unity\cache)'; Path = Join-Path $env:LOCALAPPDATA 'Unity\cache' },
    @{ Name = 'Unity Asset Store downloads'; Path = Join-Path $env:APPDATA 'Unity\Asset Store-5.x' },
    @{ Name = 'Unity temp (%TEMP%\Unity)'; Path = Join-Path $env:TEMP 'Unity' },
    @{ Name = 'Unity Hub downloads'; Path = Join-Path $env:APPDATA 'UnityHub\downloads' },
    @{ Name = 'NuGet packages (~/.nuget/packages)'; Path = Join-Path $env:USERPROFILE '.nuget\packages' },
    @{ Name = 'NuGet HTTP cache'; Path = Join-Path $env:LOCALAPPDATA 'NuGet\v3-cache' },
    @{ Name = 'pip cache'; Path = Join-Path $env:LOCALAPPDATA 'pip\cache' },
    @{ Name = 'User temp (%TEMP%)'; Path = $env:TEMP }
)
$cacheRows = foreach ($cache in $userCaches) { [pscustomobject]@{ Name = $cache.Name; Bytes = Get-Bytes $cache.Path } }
Write-Rows 'Per-user caches outside the repository' $cacheRows

Write-Host ''
Write-Host 'Reclaim: ./Tools/Clean-Artifacts.ps1 (dry run first). Per-user caches are regenerable; clear them with their own tools (e.g. dotnet nuget locals all --clear).'
