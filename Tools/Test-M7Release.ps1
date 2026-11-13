[CmdletBinding()]
param(
    [ValidateRange(30, 7200)][int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$reportDirectory = Join-Path $repoRoot "Artifacts/m7-hardening-stretch/$sourceCommit/$runId"
$null = New-Item -ItemType Directory -Path $reportDirectory -Force
$python = (Get-Command python -ErrorAction Stop).Source
Write-Host "M7 release hardening report: $reportDirectory"
& $python (Join-Path $repoRoot 'Tools/ReleaseHardening/regression_matrix.py') --repo $repoRoot --out $reportDirectory
if ($LASTEXITCODE -ne 0) { throw "regression_matrix failed" }
& $python (Join-Path $repoRoot 'Tools/ReleaseHardening/stretch_60fps_report.py') --out $reportDirectory
if ($LASTEXITCODE -ne 0) { throw "stretch_60fps_report failed" }
Copy-Item (Join-Path $repoRoot 'Tools/ReleaseHardening/deferred_scope_ledger.json') (Join-Path $reportDirectory 'deferred_scope_ledger.json')
$summary = [ordered]@{
    schemaVersion = 1
    milestone = 'M7'
    sourceCommit = $sourceCommit
    result = 'PASS_WITH_PENDING'
    evidence = $reportDirectory
    stretch = 'PendingHardware'
    mandatory30FpsBaselinesRelaxed = $false
}
$summary | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $reportDirectory 'validation.json')
Write-Host ($summary | ConvertTo-Json)
