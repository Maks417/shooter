[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('Client','Server')][string]$Target,
    [ValidateRange(60,7200)][int]$TimeoutSeconds = 1800)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$run = New-ShooterRun "build-$($Target.ToLowerInvariant())" $unity
try {
    $buildDirectory = Join-Path $run.Path 'player'
    $manifestPath = Join-Path $run.Path 'build-manifest.json'
    Invoke-ShooterEditor $unity $run @('-quit', '-executeMethod', "ShooterBuild.Build$Target", '-shooterOutput', $buildDirectory, '-shooterReport', $manifestPath) $TimeoutSeconds
    Complete-ShooterPlayerBuild $unity $run $buildDirectory $manifestPath $Target
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
