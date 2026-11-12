[CmdletBinding()]
param([string]$CacheRoot = '', [ValidateSet('Low','Balanced','High')][string]$Profile = 'Low',
    [switch]$Force, [switch]$Capture, [ValidateRange(30,3600)][int]$TimeoutSeconds = 900)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
& python (Join-Path $PSScriptRoot 'verify_content_format.py') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Frozen P02 format verification failed.' }
& python (Join-Path $PSScriptRoot 'verify_feature_registry.py') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Feature source registry check failed.' }
$unity = Get-ShooterUnity
$run = New-ShooterRun 'compile-gallery' $unity
try {
    if (!$CacheRoot) { $CacheRoot = Join-Path $run.Path 'cache' }
    $CacheRoot = [IO.Path]::GetFullPath($CacheRoot)
    $artifacts = [IO.Path]::GetFullPath((Join-Path (Get-ShooterRoot) 'Artifacts')) + [IO.Path]::DirectorySeparatorChar
    if (!$CacheRoot.StartsWith($artifacts, [StringComparison]::OrdinalIgnoreCase)) { throw 'CacheRoot must stay under repository Artifacts.' }
    $proofPath = Join-Path $run.Path 'compile-proof.json'
    Invoke-ShooterEditor $unity $run @('-quit','-executeMethod','ShooterFeatureBuild.Compile','-shooterCacheRoot',$CacheRoot,'-shooterProfile',$Profile,'-shooterForce',([bool]$Force).ToString().ToLowerInvariant(),'-shooterReport',$proofPath) $TimeoutSeconds
    $proof = Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    if ($proof.result -cne 'Verified' -or $proof.toolchainHash -cne $unity.LockHash -or $proof.profile -cne $Profile -or $proof.content.Count -ne 11 -or $proof.weapons.Count -ne 2) { throw 'Feature compile proof mismatch.' }
    foreach ($content in $proof.content) {
        if ($content.contentId -cnotmatch '^[0-9a-f]{64}$' -or $content.manifestSha256 -cnotmatch '^[0-9a-f]{64}$') { throw 'Malformed content identity.' }
        $path = Join-Path $CacheRoot ($content.contentId.Substring(0,2) + '/' + $content.contentId + '/manifest.json')
        if ((Get-ShooterHash $path) -cne $content.manifestSha256) { throw 'Published feature manifest hash mismatch.' }
    }
    if ((Get-ShooterHash (Join-Path $CacheRoot 'gallery.json')) -cne $proof.galleryPlanSha256) { throw 'Gallery plan hash mismatch.' }
    if ($Capture) {
        $captureRoot = Join-Path $run.Path 'captures'
        $captureProof = Join-Path $run.Path 'capture-proof.json'
        Invoke-ShooterEditor $unity $run @('-quit','-executeMethod','ShooterFeatureBuild.Capture','-shooterCacheRoot',$CacheRoot,'-shooterCaptureRoot',$captureRoot,'-shooterReport',$captureProof) $TimeoutSeconds
        $captureProofData = Get-Content -LiteralPath $captureProof -Raw | ConvertFrom-Json
        if ($captureProofData.result -cne 'Verified' -or $captureProofData.workload -cne 'M1-content-gallery-v1' -or $captureProofData.captures.Count -ne 22) { throw 'Gallery capture proof mismatch.' }
        foreach ($path in $captureProofData.captures) {
            if (![IO.Path]::GetFullPath($path).StartsWith($captureRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'Capture missing or outside run directory.' }
        }
        $run.Report['textureBytes'] = $captureProofData.textureBytes
        $run.Report['meshBytes'] = $captureProofData.meshBytes
    }
    $run.Report['cacheRoot'] = $CacheRoot
    $run.Report['galleryPlanSha256'] = $proof.galleryPlanSha256
    $run.Report['contentCount'] = $proof.content.Count
    $run.Report['profile'] = $Profile
    $run.Report['captured'] = [bool]$Capture
    $run.Report.result = 'PASS'
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
