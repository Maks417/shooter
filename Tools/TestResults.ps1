Set-StrictMode -Version Latest

function Get-StrictTrxSummary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$SuiteName
    )

    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$SuiteName did not produce fresh TRX results: $Path"
    }

    [xml]$trx = Get-Content -LiteralPath $Path -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    if ($null -eq $counters) {
        throw "$SuiteName TRX has no result counters."
    }

    $total = [int]$counters.total
    $executed = [int]$counters.executed
    $passed = [int]$counters.passed
    $failed = [int]$counters.failed
    $results = @($trx.TestRun.Results.UnitTestResult)
    $nonPassed = @($results | Where-Object { [string]$_.outcome -cne 'Passed' })

    if ($total -le 0) {
        throw "$SuiteName selected zero tests."
    }
    if ($executed -ne $total -or $passed -ne $total -or $failed -ne 0 -or $results.Count -ne $total -or $nonPassed.Count -ne 0) {
        throw "$SuiteName failed, skipped, aborted, or did not execute every selected test (total=$total executed=$executed passed=$passed failed=$failed resultRows=$($results.Count))."
    }

    return [pscustomobject]@{
        Total = $total
        Executed = $executed
        Passed = $passed
        Failed = $failed
        Skipped = $total - $executed
        Results = $results
    }
}
