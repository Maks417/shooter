[CmdletBinding()]
param([ValidateSet('box-collision-v1')][string]$RecipeSet = 'box-collision-v1', [ValidateSet('Low','Balanced','High')][string]$Profile = 'Low',
    [string]$CacheRoot = '', [switch]$Force, [switch]$DryRun,
    [ValidateRange(30,3600)][int]$TimeoutSeconds = 900)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$run = New-ShooterRun 'compile-content' $unity
try {
    if (!$CacheRoot) { $CacheRoot = Join-Path $run.Path 'cache' }
    $CacheRoot = [IO.Path]::GetFullPath($CacheRoot)
    $artifactsRoot = [IO.Path]::GetFullPath((Join-Path (Get-ShooterRoot) 'Artifacts')) + [IO.Path]::DirectorySeparatorChar
    if (!$CacheRoot.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'CacheRoot must stay under repository Artifacts.' }
    $resultPath = Join-Path $run.Path 'content-manifest.json'
    Invoke-ShooterEditor $unity $run @('-quit', '-executeMethod', 'ShooterBuild.CompileContent', '-shooterRecipeSet', $RecipeSet, '-shooterProfile', $Profile, '-shooterCacheRoot', $CacheRoot, '-shooterForce', ([bool]$Force).ToString().ToLowerInvariant(), '-shooterDryRun', ([bool]$DryRun).ToString().ToLowerInvariant(), '-shooterReport', $resultPath) $TimeoutSeconds
    if (!(Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw 'Content compile produced no fresh manifest.' }
    $manifest = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if ($manifest.result -cne 'Verified' -or $manifest.toolchainHash -cne $unity.LockHash -or $manifest.recipeSet -cne $RecipeSet -or $manifest.profile -cne $Profile -or
        $manifest.contentProfile -cne 'gameplay' -or $manifest.contentId -cnotmatch '^[0-9a-f]{64}$' -or $manifest.dryRun -ne [bool]$DryRun -or $manifest.force -ne [bool]$Force) { throw 'Content compile proof verification mismatch.' }
    if (!$DryRun) {
        $cacheManifest = Join-Path $CacheRoot ($manifest.contentId.Substring(0, 2) + '/' + $manifest.contentId + '/manifest.json')
        if (!(Test-Path -LiteralPath $cacheManifest -PathType Leaf) -or (Get-ShooterHash $cacheManifest) -cne $manifest.manifestSha256) { throw 'Published content manifest hash mismatch.' }
    }
    $run.Report['cacheRoot'] = $CacheRoot
    $run.Report['contentId'] = $manifest.contentId
    $run.Report['dryRun'] = [bool]$DryRun
    $run.Report['reused'] = $manifest.reused
    $run.Report.result = 'PASS'
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
