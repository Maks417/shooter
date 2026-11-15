[CmdletBinding()]
param(
    # Without -Apply this is a dry run: it only prints and records what would be deleted.
    [switch]$Apply,
    # Also delete regenerable SDK caches: Tools/*/bin, Tools/*/obj and Tools/__pycache__.
    # Do not use while another session is running an SDK wrapper in this checkout.
    [switch]$IncludeBuildCaches,
    # Runs written within this many hours are kept (protects concurrent sessions and fresh evidence).
    [ValidateRange(0, 8760)][int]$KeepRecentHours = 6,
    # Newest per-commit runs kept in every Artifacts area, whether referenced or not.
    [ValidateRange(0, 100)][int]$KeepNewestPerArea = 1
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Reclaims disk space from ignored local evidence after a milestone/installer build.
# Scope: per-commit run folders Artifacts/<area>/<40-hex commit>/ only. A run is KEPT when any of these hold:
#   - its full commit hash, or its 7-character short hash, appears in any file tracked at HEAD
#     (handoffs, product-state audit, coordination.json, locks, ...) - i.e. it is cited evidence;
#   - it is the HEAD commit's run;
#   - it is among the newest -KeepNewestPerArea runs of its area, or was written within -KeepRecentHours;
#   - it holds the newest installer-report.json (Build-Installer -PreviousInstallerReport needs it).
# Never touched: .local (pinned editor, venvs, worktrees), UnityProject/Library, non-commit Artifacts folders.
# A cleanup report is always written to Artifacts/workspace-cleanup/<utc-run-id>/cleanup-report.json.

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot 'Artifacts'
if (!(Test-Path -LiteralPath $artifacts -PathType Container)) { Write-Host 'No Artifacts directory; nothing to clean.'; exit 0 }

$head = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $head -cnotmatch '^[0-9a-f]{40}$') { throw 'Cannot resolve git HEAD.' }

# Collect every 7+ hex token from tracked text files at HEAD (one git grep, no per-file reads).
$referenced = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$tokens = & git -C $repoRoot grep -I -h -o -E '[0-9a-f]{7,40}' HEAD -- . 2>$null
if ($LASTEXITCODE -gt 1) { throw 'git grep failed while collecting referenced commits.' }
foreach ($t in $tokens) {
    $v = ($t -replace '^HEAD:', '')
    $null = $referenced.Add($v)
    if ($v.Length -gt 7) { $null = $referenced.Add($v.Substring(0, 7)) }
}

function Get-DirectoryBytes([IO.DirectoryInfo]$Dir) {
    $opt = [IO.EnumerationOptions]::new(); $opt.RecurseSubdirectories = $true; $opt.IgnoreInaccessible = $true
    $opt.AttributesToSkip = [IO.FileAttributes]::ReparsePoint
    $sum = [long]0
    foreach ($f in $Dir.EnumerateFiles('*', $opt)) { $sum += $f.Length }
    $sum
}

function Remove-LongPathDirectory([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetFullPath($repoRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (!$full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw "Refusing to delete outside the repository: $full" }
    # \\?\ prefix: build/test runs nest deeper than MAX_PATH (package rehearsal trees).
    [IO.Directory]::Delete('\\?\' + $full, $true)
}

$cutoff = [DateTime]::Now.AddHours(-$KeepRecentHours)
$newestInstallerDir = Get-ChildItem -LiteralPath $artifacts -Directory |
    ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Directory -Filter 'installer-*' -Depth 1 -ErrorAction SilentlyContinue } |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'installer-report.json') } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
$installerKeep = if ($newestInstallerDir) { $newestInstallerDir.Parent.FullName } else { '' }

$entries = [Collections.Generic.List[object]]::new()
foreach ($area in Get-ChildItem -LiteralPath $artifacts -Directory) {
    $runs = @(Get-ChildItem -LiteralPath $area.FullName -Directory | Where-Object Name -cmatch '^[0-9a-f]{40}$' |
        Sort-Object LastWriteTime -Descending)
    for ($i = 0; $i -lt $runs.Count; $i++) {
        $d = $runs[$i]
        $reason = if ($referenced.Contains($d.Name) -or $referenced.Contains($d.Name.Substring(0, 7))) { 'referenced-in-tracked-files' }
            elseif ($d.Name -ceq $head) { 'head-commit' }
            elseif ($i -lt $KeepNewestPerArea) { 'newest-in-area' }
            elseif ($d.LastWriteTime -gt $cutoff) { 'recent' }
            elseif ($d.FullName -eq $installerKeep) { 'newest-installer' }
            else { '' }
        $entries.Add([pscustomobject]@{
            path = $d.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
            bytes = (Get-DirectoryBytes $d)
            lastWrite = $d.LastWriteTime.ToString('o')
            action = if ($reason) { 'keep' } else { 'delete' }
            reason = if ($reason) { $reason } else { 'unreferenced-old-run' }
        })
    }
}

if ($IncludeBuildCaches) {
    $caches = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'Tools') -Directory | ForEach-Object {
        foreach ($n in 'bin', 'obj') { $p = Join-Path $_.FullName $n; if (Test-Path -LiteralPath $p) { Get-Item -LiteralPath $p } }
    })
    $py = Join-Path $repoRoot 'Tools/__pycache__'
    if (Test-Path -LiteralPath $py) { $caches += Get-Item -LiteralPath $py }
    foreach ($c in $caches) {
        $entries.Add([pscustomobject]@{
            path = $c.FullName.Substring($repoRoot.Length + 1).Replace('\', '/'); bytes = (Get-DirectoryBytes $c)
            lastWrite = $c.LastWriteTime.ToString('o'); action = 'delete'; reason = 'regenerable-build-cache'
        })
    }
}

$failures = [Collections.Generic.List[string]]::new()
$freed = [long]0
foreach ($e in $entries | Where-Object action -eq 'delete') {
    if (!$Apply) { continue }
    try { Remove-LongPathDirectory (Join-Path $repoRoot $e.path); $freed += $e.bytes; $e.action = 'deleted' }
    catch { $e.action = 'failed'; $failures.Add("$($e.path): $($_.Exception.Message)") }
}

$planned = ($entries | Where-Object { $_.action -in 'delete', 'deleted', 'failed' } | Measure-Object bytes -Sum).Sum
$kept = ($entries | Where-Object action -eq 'keep' | Measure-Object bytes -Sum).Sum
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$reportDir = Join-Path $artifacts "workspace-cleanup/$runId"
$null = New-Item -ItemType Directory -Path $reportDir -Force
[ordered]@{
    schemaVersion = 1; action = 'Clean-Artifacts'; mode = if ($Apply) { 'apply' } else { 'dry-run' }; head = $head
    keepRecentHours = $KeepRecentHours; keepNewestPerArea = $KeepNewestPerArea; includeBuildCaches = [bool]$IncludeBuildCaches
    candidateBytes = [long]$planned; freedBytes = $freed; keptBytes = [long]$kept; failures = $failures; entries = $entries
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $reportDir 'cleanup-report.json') -Encoding utf8

$entries | Where-Object action -ne 'keep' | Sort-Object bytes -Descending |
    Format-Table @{ n = 'MB'; e = { [math]::Round($_.bytes / 1MB) } }, action, reason, path -AutoSize | Out-Host
$gb = { param($b) [math]::Round($b / 1GB, 2) }
if ($Apply) { Write-Host "Freed $(& $gb $freed) GB; kept $(& $gb $kept) GB of cited/recent evidence." }
else { Write-Host "Dry run: $(& $gb $planned) GB would be freed; $(& $gb $kept) GB kept. Re-run with -Apply to delete." }
Write-Host "Report: $(Join-Path $reportDir 'cleanup-report.json')"
if ($failures.Count) { $failures | ForEach-Object { Write-Warning $_ }; exit 1 }
exit 0
