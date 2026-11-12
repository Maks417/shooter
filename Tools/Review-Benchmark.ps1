[CmdletBinding()]
param([Parameter(Mandatory)][string]$CaptureRunDirectory,
    [Parameter(Mandatory)][string]$BuildRunDirectory,
    [Parameter(Mandatory)][string]$HardwareEvidence,
    [ValidateRange(1,3)][int]$RunIndex = 1)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$capture = (Resolve-Path -LiteralPath $CaptureRunDirectory).Path
$build = (Resolve-Path -LiteralPath $BuildRunDirectory).Path
$evidence = (Resolve-Path -LiteralPath $HardwareEvidence).Path
$run = New-ShooterRun 'benchmark-evidence-review' $unity
try {
    $argsForAnalysis = @((Join-Path $PSScriptRoot 'analyze_benchmark.py'), '--capture', $capture,
        '--build-manifest', (Join-Path $build 'build-manifest.json'), '--hardware', (Join-Path $capture 'hardware.json'),
        '--run-index', $RunIndex, '--hardware-evidence', $evidence, '--evidence-application', (Join-Path $run.Path 'evidence-application.json'),
        '--output', (Join-Path $run.Path 'benchmark-result.json'), '--render-application', (Join-Path $run.Path 'render-budget-proof.json'))
    if (Test-Path -LiteralPath (Join-Path $capture 'external-gpu.json')) { $argsForAnalysis += @('--external-gpu-manifest', (Join-Path $capture 'external-gpu.json'), '--gpu-application', (Join-Path $run.Path 'gpu-application.json')) }
    $run.Report.commands += [ordered]@{ executable='python'; arguments=$argsForAnalysis }
    & python @argsForAnalysis
    if ($LASTEXITCODE -ne 0) { throw 'Evidence validation or analysis failed.' }
    $result = Get-Content -LiteralPath (Join-Path $run.Path 'benchmark-result.json') -Raw | ConvertFrom-Json
    $run.Report['gateStatus'] = $result.gateStatus
    $run.Report.result = if ($result.gateStatus -ceq 'ComputedPass') { 'PASS' } else { $result.gateStatus }
    $run.Report['scope'] = 'Fresh evidence review only; immutable raw capture and prior reports unchanged'
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
