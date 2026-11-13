[CmdletBinding()]
param([string]$CacheRoot='', [switch]$Force, [switch]$Capture, [ValidateRange(30,3600)][int]$TimeoutSeconds=900)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
& python (Join-Path $PSScriptRoot 'verify_feature_registry.py') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Feature source registry failed.' }
$unity=Get-ShooterUnity
$run=New-ShooterRun 'compile-soldier' $unity
try {
    if (!$CacheRoot) { $CacheRoot=Join-Path $run.Path 'cache' }
    $CacheRoot=[IO.Path]::GetFullPath($CacheRoot)
    $proofPath=Join-Path $run.Path 'compile-proof.json'
    Invoke-ShooterEditor $unity $run @('-buildTarget','StandaloneWindows64','-standaloneBuildSubtarget','Player','-quit','-executeMethod','ShooterSoldierBuild.Compile','-shooterCacheRoot',$CacheRoot,'-shooterForce',([bool]$Force).ToString().ToLowerInvariant(),'-shooterReport',$proofPath) $TimeoutSeconds
    $proof=Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    if ($proof.result -cne 'Verified' -or $proof.content.Count -ne 2 -or $proof.semanticJoints -ne 23 -or $proof.toolchainHash -cne $unity.LockHash) { throw 'Soldier compilation proof mismatch.' }
    foreach ($item in $proof.content) {
        $manifest=Join-Path $CacheRoot ($item.contentId.Substring(0,2)+'/'+$item.contentId+'/manifest.json')
        if ((Get-ShooterHash $manifest) -cne $item.manifestSha256) { throw 'Soldier manifest hash mismatch.' }
    }
    if ((Get-ShooterHash (Join-Path $CacheRoot 'soldier-gallery.json')) -cne $proof.planSha256) { throw 'Soldier plan hash mismatch.' }
    if ($Capture) {
        $captureRoot=Join-Path $run.Path 'captures';$captureProof=Join-Path $run.Path 'capture-proof.json'
        Invoke-ShooterEditor $unity $run @('-buildTarget','StandaloneWindows64','-standaloneBuildSubtarget','Player','-quit','-executeMethod','ShooterSoldierBuild.Capture','-shooterCacheRoot',$CacheRoot,'-shooterCaptureRoot',$captureRoot,'-shooterReport',$captureProof) $TimeoutSeconds
        $captured=Get-Content -LiteralPath $captureProof -Raw | ConvertFrom-Json
        if ($captured.result -cne 'Verified' -or $captured.captures.Count -ne 35) { throw 'Incomplete soldier image diagnostic.' }
        foreach ($path in $captured.captures) { if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'Soldier capture missing.' } }
    }
    $run.Report['cacheRoot']=$CacheRoot;$run.Report['scope']='M1 soldier regeneration/skinning capture diagnostic; V05 acceptance remains separate'
    $run.Report.result='PASS'
}
catch { $run.Report.error=$_.Exception.Message;Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
