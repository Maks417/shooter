[CmdletBinding()]
param([ValidateRange(30,3600)][int]$TimeoutSeconds = 900)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
& python (Join-Path $PSScriptRoot 'verify_feature_registry.py') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Feature source registry failed.' }
$unity = Get-ShooterUnity
$run = New-ShooterRun 'measure-material-compression' $unity
try {
    $proofPath = Join-Path $run.Path 'compression-proof.json'
    Invoke-ShooterEditor $unity $run @('-buildTarget','StandaloneWindows64','-standaloneBuildSubtarget','Player','-quit','-executeMethod','ShooterMaterialCompression.Measure','-shooterReport',$proofPath) $TimeoutSeconds
    $proof = Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    if ($proof.result -cne 'Verified' -or $proof.toolchainHash -cne $unity.LockHash -or $proof.planes.Count -ne 21) { throw 'Compression proof incomplete.' }
    foreach ($plane in $proof.planes) {
        if ($plane.compressedBytes -le 0 -or $plane.compressedBytes -ge $plane.rgbaBytes -or $plane.mips.Count -ne 9) { throw 'Invalid compressed plane inventory.' }
        if ((Get-ShooterHash $plane.compressedPath) -cne $plane.compressedSha256) { throw 'Compressed evidence hash mismatch.' }
    }
    $run.Report['scope'] = $proof.scope
    $run.Report.result = 'PASS'
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
