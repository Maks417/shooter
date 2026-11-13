[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildRunDirectory, [Parameter(Mandatory)][string]$CacheRoot,
    [ValidateRange(30,120)][int]$TimeoutSeconds = 45)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$buildRoot = (Resolve-Path -LiteralPath $BuildRunDirectory).Path
$manifest = Get-Content -LiteralPath (Join-Path $buildRoot 'build-manifest.json') -Raw | ConvertFrom-Json
if ($manifest.result -cne 'Succeeded' -or $manifest.target -cne 'Client' -or $manifest.toolchainHash -cne $unity.LockHash) { throw 'Expected a locked release client build.' }
Assert-ShooterPackedProvenance $manifest
$CacheRoot = (Resolve-Path -LiteralPath $CacheRoot).Path
$run = New-ShooterRun 'smoke-gallery' $unity
try {
    $player = Join-Path $buildRoot 'player/Shooter.exe'
    $log = Join-Path $run.Path 'player.log'
    $arguments = @('-batchmode','-force-d3d11','-logFile',$log,'-shooterSmokeSeconds','2','-shooterProfile','Low','-shooterContentGallery',$CacheRoot,'-shooterContentToolchainHash',$unity.LockHash)
    Invoke-ShooterProcess -Executable $player -ProcessArguments $arguments -WorkingDirectory (Split-Path -Parent $player) -LogPath (Join-Path $run.Path 'process.log') -TimeoutSeconds $TimeoutSeconds
    $lines = @(Get-Content -LiteralPath $log)
    if (@($lines | Where-Object { $_.Contains('SHOOTER_FAILURE') }).Count -ne 0) { throw 'Gallery player reported failure.' }
    $loaded = @($lines | Where-Object { $_ -cmatch '^SHOOTER_CONTENT_GALLERY_READY textures=([0-9]+) meshBytes=([0-9]+) trianglesAllLods=([0-9]+) textureFormat=RGBA32$' })
    $ready = @($lines | Where-Object { $_ -ceq 'SHOOTER_READY_CLIENT' })
    $complete = @($lines | Where-Object { $_ -cmatch '^SHOOTER_SMOKE_PASS mode=Client ticks=([0-9]+)$' })
    if ($loaded.Count -ne 1 -or $ready.Count -ne 1 -or $complete.Count -ne 1) { throw 'Missing or repeated gallery/authority lifecycle marker.' }
    if ($complete[0] -cnotmatch 'ticks=([0-9]+)$' -or [uint64]$Matches[1] -lt 60) { throw 'Gallery smoke did not complete sixty authority ticks.' }
    $run.Report['completedTicks'] = [uint64]$Matches[1]
    $run.Report['workload'] = 'M1-content-gallery-v1'
    $run.Report['scope'] = 'Release cache/mesh/texture/shader loading and foundation authority smoke; no FPS, compression or full visual acceptance'
    $run.Report['galleryPlanSha256'] = Get-ShooterHash (Join-Path $CacheRoot 'gallery.json')
    $run.Report.result = 'PASS'
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
