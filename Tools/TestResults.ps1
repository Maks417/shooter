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

# Shared process runner for the pure SDK wrappers (Test-Contracts/Game/Generation/Simulation, Measure-*). It records the
# command in $Report.commands when that key exists, writes <ReportDirectory>/<Name>.log, echoes the output and throws on
# a nonzero exit or timeout. Returns the combined stdout+stderr.
function Invoke-ShooterCheckedProcess {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Executable,
        [string[]]$ProcessArguments = @(),
        [Parameter(Mandatory)][string]$ReportDirectory,
        [Collections.IDictionary]$Report = $null,
        [string]$WorkingDirectory = (Split-Path -Parent $PSScriptRoot),
        [ValidateRange(1, 7200)][int]$TimeoutSeconds = 180
    )
    $logPath = Join-Path $ReportDirectory "$Name.log"
    if ($null -ne $Report -and $Report.Contains('commands')) {
        $Report.commands += [ordered]@{ executable = $Executable; arguments = $ProcessArguments; log = $logPath }
    }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $ProcessArguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (!$process.Start()) { throw "Cannot start $Name" }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            [IO.File]::WriteAllText($logPath, $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult())
            throw "$Name timed out after $TimeoutSeconds seconds. See $logPath"
        }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText($logPath, $output)
        Write-Host $output
        if ($process.ExitCode -ne 0) { throw "$Name exited $($process.ExitCode). See $logPath" }
        return $output
    }
    finally { $process.Dispose() }
}

# Locked-mode restore arguments. -Offline restores from the local NuGet cache only and disables the advisory lookup.
function Get-ShooterRestoreArguments([string]$ProjectPath, [bool]$Offline) {
    $arguments = @('restore', $ProjectPath, '--locked-mode')
    if ($Offline) {
        $packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
        if (!(Test-Path -LiteralPath $packageCache -PathType Container)) { throw "Missing package cache: $packageCache" }
        return $arguments + @('--source', $packageCache, '-p:NuGetAudit=false')
    }
    $arguments + @('-p:NuGetAudit=true', '-p:NuGetAuditMode=all')
}
