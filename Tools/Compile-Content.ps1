[CmdletBinding()]
param([string]$RecipeSet = 'box-minimal-v1', [ValidateSet('Low','Balanced','High')][string]$Profile = 'Low',
    [ValidateRange(30,3600)][int]$TimeoutSeconds = 900)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$run = New-ShooterRun 'compile-content' $unity
try {
    $resultPath = Join-Path $run.Path 'content-manifest.json'
    Invoke-ShooterEditor $unity $run @('-quit', '-executeMethod', 'ShooterBuild.CompileContent', '-shooterRecipeSet', $RecipeSet, '-shooterProfile', $Profile, '-shooterReport', $resultPath) $TimeoutSeconds
    if (!(Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw 'Content compile produced no fresh manifest.' }
    $manifest = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if ($manifest.result -cne 'Verified' -or $manifest.toolchainHash -cne $unity.LockHash -or $manifest.recipeSet -cne $RecipeSet -or $manifest.profile -cne $Profile) { throw 'Content manifest verification mismatch.' }
    $run.Report.result = 'PASS'
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
