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
    $summary = $results.DocumentElement
    if ($summary.LocalName -cne 'test-run' -or $summary.result -cne 'Passed') { throw 'Unity tests did not produce a passing test-run.' }
    $counts = @{}
    foreach ($attribute in @('total', 'passed', 'failed', 'skipped', 'inconclusive', 'testcasecount')) {
        $value = $summary.GetAttribute($attribute)
        $count = 0
        if ($value -cnotmatch '^[0-9]+$' -or ![int]::TryParse($value, [ref]$count)) { throw "Unity test-run has an invalid $attribute count." }
        $counts[$attribute] = $count
    }
    if ($counts.total -le 0 -or $counts.failed -ne 0 -or $counts.skipped -ne 0 -or $counts.inconclusive -ne 0 -or
        $counts.passed -ne $counts.total -or $counts.testcasecount -ne $counts.total) { throw 'Unity tests failed, skipped, were inconclusive or did not run.' }
    # A passing aggregate alone cannot certify the individual test results.
    $cases = @($summary.SelectNodes('.//test-case'))
    if ($cases.Count -ne $counts.total) { throw 'Unity test-case count differs from the test-run summary.' }
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($case in $cases) {
        $id = $case.GetAttribute('id')
        if ([string]::IsNullOrWhiteSpace($id) -or !$ids.Add($id)) { throw 'Unity test-case has a missing or duplicate ID.' }
        if ($case.GetAttribute('result') -cne 'Passed') { throw "Unity test-case did not pass: $id" }
    }
    foreach ($suite in $summary.SelectNodes('.//test-suite')) {
        if ($suite.GetAttribute('result') -cne 'Passed') { throw 'Unity test-suite did not pass.' }
    }
    $run.Report['testCount'] = $counts.total
    $run.Report.result = 'PASS'
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }
