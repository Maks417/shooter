# Standalone wrapper regressions. XML/editor behavior below is synthetic, test-only evidence.
[CmdletBinding()]
param([string[]]$ExistingResultPaths = @())
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$evidence = Join-Path $repoRoot "Artifacts/wrapper-tests/unity-$runId"
$null = New-Item -ItemType Directory -Path $evidence
$pwsh = (Get-Process -Id $PID).Path
$mock = @'
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Get-ShooterUnity { [pscustomobject]@{ LockHash = 'test-only' } }
function New-ShooterRun($Action, $Unity) {
    [pscustomobject]@{ Path = $PSScriptRoot; Report = [ordered]@{ result = 'FAIL'; error = $null; action = $Action } }
}
function Invoke-ShooterEditor($Unity, $Run, [string[]]$EditorArguments, [int]$TimeoutSeconds) {
    $expected = @('-buildTarget', 'StandaloneWindows64', '-standaloneBuildSubtarget', 'Player', '-runTests', '-testPlatform')
    for ($i = 0; $i -lt $expected.Count; $i++) {
        if ($EditorArguments[$i] -cne $expected[$i]) { throw 'Player target/test-run arguments changed.' }
    }
    if ($EditorArguments[6] -cnotin @('EditMode', 'PlayMode') -or $EditorArguments[7] -cne '-testResults' -or $EditorArguments.Count -ne 9 -or $TimeoutSeconds -ne 30) { throw 'Unexpected test arguments.' }
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'input.xml')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'input.xml') -Destination $EditorArguments[8]
    }
}
function Save-ShooterRun($Run) {
    $Run.Report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'run.json') -Encoding utf8NoBOM
}
'@
# This NUnit shape matches retained Unity 6000.3.28f1 EditMode/PlayMode XML.
$valid = '<test-run result="Passed" total="2" passed="2" failed="0" skipped="0" inconclusive="0" testcasecount="2"><test-suite result="Passed"><test-case id="1001" result="Passed"/><test-case id="1002" result="Passed"/></test-suite></test-run>'
$scenarios = [Collections.Generic.List[object]]::new()
function Add-Case([string]$Name, [AllowNull()][string]$Xml, [bool]$Pass, [string]$Mode = 'EditMode') {
    $scenarios.Add([pscustomobject]@{ Name = $Name; Xml = $Xml; Pass = $Pass; Mode = $Mode })
}
Add-Case 'passing-editmode' $valid $true
Add-Case 'passing-playmode' $valid $true 'PlayMode'
foreach ($result in @('Failed', 'Skipped', 'Inconclusive')) {
    Add-Case "$result-case-under-passing-summary" ($valid.Replace('id="1002" result="Passed"', "id=""1002"" result=""$result""")) $false
}
Add-Case 'missing-case-result' ($valid.Replace('id="1002" result="Passed"', 'id="1002"')) $false
Add-Case 'missing-case' ($valid.Replace('<test-case id="1002" result="Passed"/>', '')) $false
Add-Case 'no-cases' ($valid.Replace('<test-case id="1001" result="Passed"/><test-case id="1002" result="Passed"/>', '')) $false
Add-Case 'duplicate-case-id' ($valid.Replace('id="1002"', 'id="1001"')) $false
Add-Case 'missing-case-id' ($valid.Replace('id="1002" ', '')) $false
Add-Case 'blank-case-id' ($valid.Replace('id="1002"', 'id=" "')) $false
Add-Case 'failed-suite' ($valid.Replace('<test-suite result="Passed">', '<test-suite result="Failed">')) $false
Add-Case 'failed-summary' ($valid.Replace('<test-run result="Passed"', '<test-run result="Failed"')) $false
foreach ($attribute in @('skipped', 'inconclusive', 'failed')) {
    Add-Case "nonzero-$attribute-count" ($valid.Replace("$attribute=""0""", "$attribute=""1""")) $false
}
Add-Case 'zero-tests' '<test-run result="Passed" total="0" passed="0" failed="0" skipped="0" inconclusive="0" testcasecount="0"/>' $false
Add-Case 'unequal-passed-count' ($valid.Replace('passed="2"', 'passed="1"')) $false
Add-Case 'unequal-testcasecount' ($valid.Replace('testcasecount="2"', 'testcasecount="3"')) $false
foreach ($attribute in @('total', 'passed', 'failed', 'skipped', 'inconclusive', 'testcasecount')) {
    $value = if ($attribute -in @('total', 'passed', 'testcasecount')) { 2 } else { 0 }
    Add-Case "missing-$attribute-count" ($valid.Replace(" $attribute=""$value""", '')) $false
}
Add-Case 'negative-count' ($valid.Replace('total="2"', 'total="-2"')) $false
Add-Case 'fractional-count' ($valid.Replace('total="2"', 'total="2.0"')) $false
Add-Case 'overflowing-count' ($valid.Replace('total="2"', 'total="2147483648"')) $false
Add-Case 'wrong-root' ($valid.Replace('test-run', 'test-results')) $false
Add-Case 'malformed-xml' '<test-run' $false
Add-Case 'missing-xml' $null $false
foreach ($path in $ExistingResultPaths) {
    $resolved = (Resolve-Path -LiteralPath $path).Path
    Add-Case ('retained-' + $scenarios.Count) (Get-Content -LiteralPath $resolved -Raw) $true
}
$outcomes = [Collections.Generic.List[object]]::new()
foreach ($scenario in $scenarios) {
    $caseRoot = Join-Path $evidence $scenario.Name
    $null = New-Item -ItemType Directory -Path $caseRoot
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Test-Unity.ps1') -Destination $caseRoot
    Set-Content -LiteralPath (Join-Path $caseRoot 'Unity.Common.ps1') -Value $mock -Encoding utf8NoBOM
    if ($scenario.Xml) { Set-Content -LiteralPath (Join-Path $caseRoot 'input.xml') -Value $scenario.Xml -Encoding utf8NoBOM }
    $output = & $pwsh -NoProfile -File (Join-Path $caseRoot 'Test-Unity.ps1') -Mode $scenario.Mode -TimeoutSeconds 30 2>&1
    $exitCode = $LASTEXITCODE
    $output | Set-Content -LiteralPath (Join-Path $caseRoot 'process.log') -Encoding utf8NoBOM
    $report = Get-Content -LiteralPath (Join-Path $caseRoot 'run.json') -Raw | ConvertFrom-Json
    $expectedResult = if ($scenario.Pass) { 'PASS' } else { 'FAIL' }
    $ok = $report.result -ceq $expectedResult -and (($exitCode -eq 0) -eq $scenario.Pass)
    if ($scenario.Pass) {
        [xml]$inputXml = $scenario.Xml
        $ok = $ok -and $report.testCount -eq [int]$inputXml.DocumentElement.GetAttribute('total')
    }
    else { $ok = $ok -and ![string]::IsNullOrWhiteSpace($report.error) }
    $outcomes.Add([ordered]@{ name = $scenario.Name; result = $(if ($ok) { 'PASS' } else { 'FAIL' }); wrapperExitCode = $exitCode; wrapperResult = $report.result })
}
$failed = @($outcomes | Where-Object result -cne 'PASS')
$summary = [ordered]@{ schemaVersion = 1; result = $(if ($failed.Count) { 'FAIL' } else { 'PASS' }); tests = $outcomes.Count; failed = $failed.Count; scope = 'Synthetic wrapper validation only; optional retained XML replay does not run Unity or certify new test execution.'; cases = @($outcomes) }
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'result.json') -Encoding utf8NoBOM
if ($failed.Count) { throw "Unity wrapper regressions failed: $($failed.name -join ', '); evidence: $evidence" }
Write-Host "PASS: $($outcomes.Count) Unity wrapper regressions; evidence: $evidence"
exit 0
