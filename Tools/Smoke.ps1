[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildRunDirectory, [ValidateRange(30,120)][int]$TimeoutSeconds = 45)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$buildRoot = (Resolve-Path -LiteralPath $BuildRunDirectory).Path
$manifest = Get-Content -LiteralPath (Join-Path $buildRoot 'build-manifest.json') -Raw | ConvertFrom-Json
if ($manifest.result -cne 'Succeeded' -or $manifest.toolchainHash -cne $unity.LockHash) { throw 'Build manifest does not match the locked toolchain.' }
$run = New-ShooterRun "smoke-$($manifest.target.ToLowerInvariant())" $unity
try {
    Assert-ShooterPackedProvenance $manifest
    $player = Join-Path $buildRoot 'player/Shooter.exe'
    $log = Join-Path $run.Path 'player.log'
    $arguments = @('-batchmode','-logFile',$log,'-shooterSmokeSeconds','2','-shooterProfile','Low')
    if ($manifest.target -eq 'Server') { $arguments += '-nographics' } else { $arguments += '-force-d3d11' }
    Invoke-ShooterProcess -Executable $player -ProcessArguments $arguments -WorkingDirectory (Split-Path -Parent $player) -LogPath (Join-Path $run.Path 'process.log') -TimeoutSeconds $TimeoutSeconds
    if (!(Test-Path -LiteralPath $log) -or !(Select-String -LiteralPath $log -SimpleMatch "SHOOTER_SMOKE_PASS mode=$($manifest.target)" -Quiet) -or (Select-String -LiteralPath $log -SimpleMatch 'SHOOTER_FAILURE' -Quiet)) { throw 'Player did not produce a fresh successful smoke marker.' }
    if ($manifest.target -eq 'Server') {
        foreach ($assembly in @('Shooter.Presentation.dll','Shooter.Bootstrap.Client.dll')) {
            if (Test-Path -LiteralPath (Join-Path $buildRoot "player/Shooter_Data/Managed/$assembly")) { throw "Server includes client assembly: $assembly" }
        }
        if (@($manifest.graphicsSourceAssets).Count) { throw 'Server build manifest contains graphics payloads.' }
    }
    $run.Report['buildManifestSha256'] = Get-ShooterHash (Join-Path $buildRoot 'build-manifest.json')
    $run.Report.result = 'PASS'
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
