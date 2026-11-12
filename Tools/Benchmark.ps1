[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildRunDirectory,
    [ValidateSet('B01-minimal-v1')][string]$Workload = 'B01-minimal-v1',
    [ValidateSet('Low')][string]$Profile = 'Low', [ValidateRange(1,3)][int]$RunIndex = 1, [switch]$Diagnostic,
    [string]$HardwareEvidence = '', [switch]$DiagnosticAllocate, [switch]$PresentMon)
if ($DiagnosticAllocate -and !$Diagnostic) { throw 'Allocation injection is diagnostic only.' }
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$buildRoot = (Resolve-Path -LiteralPath $BuildRunDirectory).Path
$buildManifest = Join-Path $buildRoot 'build-manifest.json'
$build = Get-Content -LiteralPath $buildManifest -Raw | ConvertFrom-Json
if ($build.result -cne 'Succeeded' -or $build.target -cne 'Client' -or $build.backend -cne 'IL2CPP' -or $build.development -or $build.toolchainHash -cne $unity.LockHash) { throw 'A matching release IL2CPP client build is required.' }
if ((Get-ShooterHash (Join-Path $buildRoot 'harness-manifest.json')) -cne $build.contentManifestHash) { throw 'Harness content manifest hash mismatch.' }
$run = New-ShooterRun $(if ($Diagnostic) { 'benchmark-diagnostic' } else { "benchmark-$RunIndex" }) $unity
$trace = $null; $launch = [ordered]@{}
try {
    Assert-ShooterPackedProvenance $build
    $cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name.Trim()
    $ram = @(Get-CimInstance Win32_PhysicalMemory)
    $video = @(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion)
    $power = (powercfg /getactivescheme | Out-String).Trim()
    $battery = @(Get-CimInstance -Namespace root/WMI -ClassName BatteryStatus -ErrorAction SilentlyContinue)
    $systemDisk = Get-Partition -DriveLetter C -ErrorAction SilentlyContinue | Get-Disk -ErrorAction SilentlyContinue
    $hardware = [ordered]@{ cpu = $cpu; gpuAdapter = $null; ramBytes = [long](($ram | Measure-Object Capacity -Sum).Sum); ramSpeed = [int]$ram[0].ConfiguredClockSpeed;
        channelMode = $null; os = [Environment]::OSVersion.VersionString; driver = ($video | Where-Object Name -match 'AMD.*Radeon' | Select-Object -First 1).DriverVersion;
        powerPlan = $power; acPower = $(if ($battery.Count) { [bool]$battery[0].PowerOnline } else { $null }); thermalStabilized = $false; nvmeVerified = [bool]($systemDisk -and $systemDisk.BusType -eq 'NVMe');
        memorySlots = @($ram | Select-Object BankLabel,DeviceLocator,Capacity,ConfiguredClockSpeed); adapters = $video }
    $hardwarePath = Join-Path $run.Path 'hardware.json'; $hardware | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $hardwarePath -Encoding utf8NoBOM
    $player = Join-Path $buildRoot 'player/Shooter.exe'; $log = Join-Path $run.Path 'player.log'
    $arguments = @('-force-d3d11','-logFile',$log,'-shooterProfile',$Profile,'-shooterBenchmark',$Workload,'-shooterBenchmarkOutput',$run.Path,'-shooterBenchmarkDiagnostic',([bool]$Diagnostic).ToString().ToLowerInvariant())
    if ($DiagnosticAllocate) { $arguments += @('-shooterDiagnosticAllocate', 'true') }
    $run.Report.commands += [ordered]@{ executable=$player; arguments=$arguments }
    if ($PresentMon) {
        $traceLock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'presentmon-lock.json') -Raw | ConvertFrom-Json
        $traceExe = Join-Path (Get-ShooterRoot) $traceLock.relativeExecutable
        if (!(Test-Path -LiteralPath $traceExe) -or (Get-ShooterHash $traceExe) -cne $traceLock.sha256) { throw 'Missing or changed pinned PresentMon; run Tools/Configure-PresentMon.ps1.' }
        $traceInfo = [Diagnostics.ProcessStartInfo]::new(); $traceInfo.FileName=$traceExe
        $traceInfo.UseShellExecute=$false; $traceInfo.CreateNoWindow=$true; $traceInfo.RedirectStandardOutput=$true; $traceInfo.RedirectStandardError=$true
        $traceSession = 'Shooter-' + [Guid]::NewGuid().ToString('N')
        $traceArgs = @('--process_name','Shooter.exe','--output_file',(Join-Path $run.Path 'presentmon.csv'),'--qpc_time','--no_console_stats','--no_track_input','--set_circular_buffer_size','65536','--session_name',$traceSession,'--timed',$(if ($Diagnostic) { '15' } else { '250' }),'--terminate_after_timed')
        foreach ($a in $traceArgs) { $traceInfo.ArgumentList.Add($a) }
        $run.Report.commands += [ordered]@{ executable=$traceExe; arguments=$traceArgs }
        $trace=[Diagnostics.Process]::new(); $trace.StartInfo=$traceInfo; $null=$trace.Start()
        $traceOut=$trace.StandardOutput.ReadToEndAsync(); $traceError=$trace.StandardError.ReadToEndAsync()
        Start-Sleep -Milliseconds 700
        if ($trace.HasExited) { throw 'External GPU trace exited during startup.' }
    }
    Invoke-ShooterProcess -Executable $player -ProcessArguments $arguments -WorkingDirectory (Split-Path -Parent $player) -LogPath (Join-Path $run.Path 'process.log') -MemorySamplePath (Join-Path $run.Path 'memory.csv') -TimeoutSeconds $(if ($Diagnostic) { 60 } else { 330 }) -ProcessRecord $launch
    $launch | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run.Path 'player-launch.json') -Encoding utf8NoBOM
    if ($PresentMon) {
        if (!$trace.WaitForExit(30000)) { throw 'External GPU trace did not finish within its bounded timer.' }
        $traceLog = Join-Path $run.Path 'presentmon.log'
        [IO.File]::WriteAllText($traceLog, $traceOut.GetAwaiter().GetResult() + $traceError.GetAwaiter().GetResult())
        [ordered]@{ schemaVersion=1; toolVersion=$traceLock.version; toolSha256=$traceLock.sha256; processId=$launch.processId;
            playerSha256=$launch.executableSha256; traceExitCode=$trace.ExitCode; captureSha256=Get-ShooterHash (Join-Path $run.Path 'capture.json');
            csvSha256=Get-ShooterHash (Join-Path $run.Path 'presentmon.csv'); logSha256=Get-ShooterHash $traceLog } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run.Path 'external-gpu.json') -Encoding utf8NoBOM
    }
    if (!(Test-Path -LiteralPath $log) -or !(Select-String -LiteralPath $log -SimpleMatch 'SHOOTER_BENCHMARK_COMPLETE' -Quiet) -or (Select-String -LiteralPath $log -SimpleMatch 'SHOOTER_FAILURE' -Quiet)) { throw 'Player capture failed or did not finish.' }
    $output = Join-Path $run.Path $(if ($Diagnostic) { 'diagnostic-result.json' } else { 'benchmark-result.json' })
    $analysisArgs = @((Join-Path $PSScriptRoot 'analyze_benchmark.py'), '--capture', $run.Path, '--build-manifest', $buildManifest, '--hardware', $hardwarePath, '--run-index', $RunIndex, '--output', $output)
    if ($HardwareEvidence) { $analysisArgs += @('--hardware-evidence', (Resolve-Path -LiteralPath $HardwareEvidence).Path, '--evidence-application', (Join-Path $run.Path 'evidence-application.json')) }
    if ($PresentMon) { $analysisArgs += @('--external-gpu-manifest', (Join-Path $run.Path 'external-gpu.json'), '--gpu-application', (Join-Path $run.Path 'gpu-application.json')) }
    $run.Report.commands += [ordered]@{ executable='python'; arguments=$analysisArgs }
    & python @analysisArgs
    if ($LASTEXITCODE -ne 0) { throw 'Capture analysis failed.' }
    $result = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
    $run.Report['gateStatus'] = $result.gateStatus
    $run.Report['scope'] = $(if ($Diagnostic) { 'Short diagnostic only; not benchmark acceptance' } else { 'Real B01 capture; hardware/reviewer gate remains explicit in result' })
    $run.Report.result = 'PASS'
    if (!$Diagnostic -and $result.gateStatus -ne 'ComputedPass') { $run.Report.result = $result.gateStatus }
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally {
    if ($null -ne $trace) {
        if (!$trace.HasExited) {
            # Stop only this wrapper's uniquely named session; never another monitor's ETW session.
            & $traceExe --session_name $traceSession --terminate_existing_session 2>&1 | Out-Null
            if (!$trace.WaitForExit(5000)) { $trace.Kill($true); $trace.WaitForExit() }
        }
        if (!(Test-Path -LiteralPath (Join-Path $run.Path 'presentmon.log'))) { [IO.File]::WriteAllText((Join-Path $run.Path 'presentmon.log'),$traceOut.GetAwaiter().GetResult()+$traceError.GetAwaiter().GetResult()) }
        $trace.Dispose()
    }
    if ($launch.Count) { $launch | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run.Path 'player-launch.json') -Encoding utf8NoBOM }
    Save-ShooterRun $run
}
if ($run.Report.result -ne 'PASS') { exit 1 }
