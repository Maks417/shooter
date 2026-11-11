[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('EditMode','PlayMode')][string]$Mode,
    [ValidateRange(30,3600)][int]$TimeoutSeconds = 900)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$run = New-ShooterRun "tests-$($Mode.ToLowerInvariant())" $unity
try {
    $resultPath = Join-Path $run.Path 'test-results.xml'
    # Do not use -quit with -runTests: the test runner owns shutdown.
    # A preceding Dedicated Server build persists UNITY_SERVER in local editor state.
    # Select Player before project load so client-side test assemblies are discovered.
    Invoke-ShooterEditor $unity $run @('-buildTarget', 'StandaloneWindows64', '-standaloneBuildSubtarget', 'Player', '-runTests', '-testPlatform', $Mode, '-testResults', $resultPath) $TimeoutSeconds
    if (!(Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw 'Unity produced no fresh test XML.' }
    [xml]$results = Get-Content -LiteralPath $resultPath -Raw
    $summary = $results.'test-run'
    if ($summary.result -cne 'Passed' -or [int]$summary.total -le 0 -or [int]$summary.failed -ne 0 -or [int]$summary.passed -ne [int]$summary.total) { throw 'Unity tests failed, skipped, were inconclusive or did not run.' }
    $run.Report['testCount'] = [int]$summary.total
    $run.Report.result = 'PASS'
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
