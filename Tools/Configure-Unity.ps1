[CmdletBinding()]
param([Parameter(Mandatory)][string]$EditorPath, [ValidateRange(30,3600)][int]$TimeoutSeconds = 900)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$root = Get-ShooterRoot
$resolvedEditor = (Resolve-Path -LiteralPath $EditorPath).Path
& python (Join-Path $PSScriptRoot 'prepare_unity_packages.py') --editor $resolvedEditor --quiet
if ($LASTEXITCODE -ne 0) { throw 'Pinned engine-package preparation failed.' }
$lockPath = Join-Path $root 'toolchain-lock.json'
if (!(Test-Path -LiteralPath $lockPath -PathType Leaf)) { throw 'Create the verified portable toolchain lock before configuring a local editor.' }
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$unity = [pscustomobject]@{ Editor = $resolvedEditor; Lock = $lock; LockHash = Get-ShooterHash $lockPath; Project = Join-Path $root 'UnityProject' }
$run = New-ShooterRun 'configure-unity' $unity
try {
    $proofPath = Join-Path $run.Path 'editor-proof.json'
    Invoke-ShooterEditor $unity $run @('-quit', '-executeMethod', 'ShooterBuild.VerifyToolchain', '-shooterReport', $proofPath) $TimeoutSeconds
    if (!(Test-Path -LiteralPath $proofPath -PathType Leaf)) { throw 'Editor did not produce a fresh toolchain verification report.' }
    $proof = Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    if ($proof.result -cne 'Verified' -or $proof.editorVersion -cne $lock.editorVersion -or $proof.editorRevision -cne $lock.editorRevision -or $proof.toolchainHash -cne $unity.LockHash) { throw 'Selected editor failed exact version/revision/package verification.' }
    $localPath = Join-Path $root '.local/unity.json'
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $localPath) -Force
    [ordered]@{ schemaVersion = 1; editorPath = $resolvedEditor; editorVersion = $lock.editorVersion; editorRevision = $lock.editorRevision; editorExecutableSha256 = Get-ShooterHash $resolvedEditor } |
        ConvertTo-Json | Set-Content -LiteralPath $localPath -Encoding utf8NoBOM
    $run.Report.result = 'PASS'
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
