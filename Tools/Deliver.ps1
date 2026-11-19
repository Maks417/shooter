[CmdletBinding()]
param(
    # n.n.n release number for Build-Installer. Without it the chain stops after Package-Release.
    [string]$ReleaseVersion = '',
    [string]$PreviousInstallerReport = '',
    [string]$PreviousPackageDirectory = '',
    # Pass -Force to Compile-GameContent (republish every package; the runbook default for deliveries).
    [switch]$ForceContent,
    [switch]$Zip
)
# The AGENTS.md delivery sequence with no manual run-folder copying:
#   Compile-GameContent -Native -AndBuildClient  (one editor session: nav bake + IL2CPP client build)
#   Smoke -> Package-Release -RequireNativeNav -> Build-Installer (only with -ReleaseVersion)
# Every step is the normal wrapper with its own evidence folder and checks; this script only passes paths along and
# writes Artifacts/m11-playable-release/<commit>/deliver-<id>/deliver.json with each step's folder, result and duration.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$repoRoot = Get-ShooterRoot
if ([bool](& git -C $repoRoot status --porcelain)) { throw 'Deliver needs a clean tree: Package-Release rejects dirty builds. Commit first.' }
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$deliverDir = Join-Path $repoRoot "Artifacts/m11-playable-release/$commit/deliver-$runId"
$null = New-Item -ItemType Directory -Path $deliverDir -Force
$report = [ordered]@{ schemaVersion = 1; action = 'Deliver'; sourceCommit = $commit; result = 'FAIL'; steps = @(); failure = $null
    scope = 'Builder chain only. Builder, smoke and package PASS are not gameplay or release acceptance.' }

function Invoke-Step([string]$Name, [string]$Script, [string[]]$Arguments) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    & (Join-Path $PSScriptRoot $Script) @Arguments
    $code = $LASTEXITCODE
    $report.steps += [ordered]@{ step = $Name; arguments = $Arguments; exitCode = $code; seconds = [math]::Round($clock.Elapsed.TotalSeconds, 1) }
    if ($code -ne 0) { throw "$Name failed with exit code $code." }
}

function Get-NewestRun([string]$Area, [string]$Prefix) {
    $dir = Get-ChildItem -LiteralPath (Join-Path $repoRoot "Artifacts/$Area/$commit") -Directory -Filter "$Prefix-*" -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -First 1
    if (!$dir) { throw "No $Prefix run found under Artifacts/$Area/$commit." }
    $dir.FullName
}

try {
    $contentArgs = @('-Native', '-AndBuildClient')
    if ($ForceContent) { $contentArgs += '-Force' }
    Invoke-Step 'content+build' 'Compile-GameContent.ps1' $contentArgs
    $contentRun = Get-NewestRun 'm8-runtime-composition' 'compile-gamecontent'
    $buildRun = [string](Get-Content -LiteralPath (Join-Path $contentRun 'validation.json') -Raw | ConvertFrom-Json).buildRunDirectory
    if (!$buildRun -or !(Test-Path -LiteralPath (Join-Path $buildRun 'build-manifest.json'))) { throw 'Compile-GameContent recorded no client build run.' }
    $report.contentRunDirectory = $contentRun
    $report.buildRunDirectory = $buildRun

    Invoke-Step 'smoke' 'Smoke.ps1' @('-BuildRunDirectory', $buildRun)

    $packageArgs = @('-ClientBuildRunDirectory', $buildRun, '-MapCacheRoot', (Join-Path $repoRoot 'Artifacts/map-packages-native'), '-RequireNativeNav')
    if ($PreviousPackageDirectory) { $packageArgs += @('-PreviousPackageDirectory', $PreviousPackageDirectory) }
    if ($Zip) { $packageArgs += '-Zip' }
    Invoke-Step 'package' 'Package-Release.ps1' $packageArgs
    $report.packageDirectory = Get-NewestRun 'm11-playable-release' 'package'

    if ($ReleaseVersion) {
        $installerArgs = @('-PackageDirectory', $report.packageDirectory, '-ReleaseVersion', $ReleaseVersion)
        if ($PreviousInstallerReport) { $installerArgs += @('-PreviousInstallerReport', $PreviousInstallerReport) }
        Invoke-Step 'installer' 'Build-Installer.ps1' $installerArgs
        $report.installerDirectory = Get-NewestRun 'm11-playable-release' 'installer'
    }
    $report.result = 'PASS'
}
catch { $report.failure = $_.Exception.Message; Write-Warning $report.failure }
finally { $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $deliverDir 'deliver.json') -Encoding utf8NoBOM }
Write-Host "$($report.result): Deliver; steps: $(($report.steps | ForEach-Object { "$($_.step)=$($_.seconds)s" }) -join ', '); report: $deliverDir"
if ($report.result -ne 'PASS') { exit 1 }
Remove-ShooterStaleRuns (Split-Path -Parent (Split-Path -Parent $deliverDir)) 'deliver' $deliverDir
