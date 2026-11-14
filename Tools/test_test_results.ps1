[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'TestResults.ps1')

$evidence = Join-Path ([IO.Path]::GetTempPath()) ('shooter-trx-guard-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $evidence

function Write-TrxFixture {
    param(
        [string]$Path,
        [int]$Total,
        [int]$Executed,
        [int]$Passed,
        [int]$Failed,
        [string[]]$Outcomes
    )
    $rows = for ($index = 0; $index -lt $Outcomes.Count; $index++) {
        "<UnitTestResult testName=`"case-$index`" outcome=`"$($Outcomes[$index])`" duration=`"00:00:00.0010000`" />"
    }
    @"
<TestRun>
  <Results>$($rows -join '')</Results>
  <ResultSummary>
    <Counters total="$Total" executed="$Executed" passed="$Passed" failed="$Failed" />
  </ResultSummary>
</TestRun>
"@ | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

function Assert-Rejected {
    param([string]$Name, [scriptblock]$Action)
    try {
        & $Action
        throw "$Name was accepted."
    }
    catch {
        if ($_.Exception.Message -ceq "$Name was accepted.") { throw }
    }
}

try {
    $passing = Join-Path $evidence 'passing.trx'
    Write-TrxFixture $passing 2 2 2 0 @('Passed', 'Passed')
    $summary = Get-StrictTrxSummary -Path $passing -SuiteName 'passing'
    if ($summary.Total -ne 2 -or $summary.Skipped -ne 0) { throw 'Passing summary was incorrect.' }

    $zero = Join-Path $evidence 'zero.trx'
    Write-TrxFixture $zero 0 0 0 0 @()
    Assert-Rejected 'zero selected tests' { Get-StrictTrxSummary -Path $zero -SuiteName 'zero' }

    $skipped = Join-Path $evidence 'skipped.trx'
    Write-TrxFixture $skipped 2 1 1 0 @('Passed', 'NotExecuted')
    Assert-Rejected 'skipped test' { Get-StrictTrxSummary -Path $skipped -SuiteName 'skipped' }

    $failed = Join-Path $evidence 'failed.trx'
    Write-TrxFixture $failed 2 2 1 1 @('Passed', 'Failed')
    Assert-Rejected 'failed test' { Get-StrictTrxSummary -Path $failed -SuiteName 'failed' }

    $missing = Join-Path $evidence 'missing.trx'
    Assert-Rejected 'missing result' { Get-StrictTrxSummary -Path $missing -SuiteName 'missing' }
}
finally {
    Remove-Item -LiteralPath $evidence -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host 'PASS: strict TRX guard rejects zero, skipped, failed, and missing results.'
