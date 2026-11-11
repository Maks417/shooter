Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ShooterRoot { Split-Path -Parent $PSScriptRoot }

function Get-ShooterHash([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-ShooterPackedProvenance($Manifest) {
    # These engine-generated entries have no image extension, so an extension scan misses them.
    $unexpected = @($Manifest.sourceAssetPaths | Where-Object {
        $_ -cmatch '^Built-in Sprite:' -or $_ -ceq 'Built-in Texture2D: Splash Screen Unity Logo'
    })
    if ($unexpected.Count) { throw ('Unapproved built-in visual payload: ' + ($unexpected -join ', ')) }
}

function Get-ShooterUnity {
    $root = Get-ShooterRoot
    $lockPath = Join-Path $root 'toolchain-lock.json'
    if (!(Test-Path -LiteralPath $lockPath -PathType Leaf)) { throw 'F01 pending: toolchain-lock.json is missing. Install/select the editor before running Unity checks.' }
    $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
    if ($lock.schemaVersion -ne 1) { throw 'Unsupported toolchain lock schema.' }
    $localPath = Join-Path $root '.local/unity.json'
    if (!(Test-Path -LiteralPath $localPath -PathType Leaf)) { throw 'Missing .local/unity.json; use Tools/Configure-Unity.ps1 -EditorPath <locked Unity.exe>.' }
    $local = Get-Content -LiteralPath $localPath -Raw | ConvertFrom-Json
    $editorPath = [IO.Path]::GetFullPath($local.editorPath)
    if (!(Test-Path -LiteralPath $editorPath -PathType Leaf)) { throw "Missing editor: $editorPath" }
    if ((Get-Item -LiteralPath $editorPath).VersionInfo.ProductVersion -cne ($lock.editorVersion + '_' + $lock.editorRevision)) { throw 'Editor executable version/revision differs from the lock.' }
    if ($local.editorVersion -cne $lock.editorVersion -or $local.editorRevision -cne $lock.editorRevision) { throw 'Local editor registration does not match the portable toolchain lock.' }
    if ((Get-ShooterHash $editorPath) -cne $local.editorExecutableSha256) { throw 'Editor executable changed; verify and reconfigure the locked installation.' }
    & python (Join-Path $PSScriptRoot 'prepare_unity_packages.py') --editor $editorPath --quiet | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Pinned Core RP source curation does not match its lock.' }
    $version = Get-Content -LiteralPath (Join-Path $root 'UnityProject/ProjectSettings/ProjectVersion.txt') -Raw
    if ($version -notmatch "(?m)^m_EditorVersion: $([regex]::Escape($lock.editorVersion))\r?$" -or
        $version -notmatch "(?m)^m_EditorVersionWithRevision: $([regex]::Escape($lock.editorVersion)) \($([regex]::Escape($lock.editorRevision))\)\r?$") { throw 'ProjectVersion.txt differs from toolchain-lock.json.' }
    $manifest = Get-Content -LiteralPath (Join-Path $root 'UnityProject/Packages/manifest.json') -Raw | ConvertFrom-Json
    $packageLock = Get-Content -LiteralPath (Join-Path $root 'UnityProject/Packages/packages-lock.json') -Raw | ConvertFrom-Json
    $expectedPackages = @{
        'com.unity.render-pipelines.universal' = $lock.urpVersion
        'com.unity.inputsystem' = $lock.inputSystemVersion
        'com.unity.test-framework' = $lock.testFrameworkVersion
        'com.unity.ai.navigation' = $lock.aiNavigationVersion
    }
    foreach ($package in $expectedPackages.Keys) {
        if ($manifest.dependencies.$package -cne $expectedPackages[$package] -or $packageLock.dependencies.$package.version -cne $expectedPackages[$package]) { throw "Package differs from toolchain lock: $package" }
    }
    Write-Host "Locked Unity: $($lock.editorVersion) ($($lock.editorRevision)) at $editorPath"
    [pscustomobject]@{ Editor = $editorPath; Lock = $lock; LockHash = Get-ShooterHash $lockPath; Project = Join-Path $root 'UnityProject' }
}

function New-ShooterRun([string]$Action, $Unity) {
    $root = Get-ShooterRoot
    $commit = (& git -C $root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
    $id = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $path = Join-Path $root "Artifacts/m0-foundation/$commit/$Action-$id"
    $null = New-Item -ItemType Directory -Path $path
    [pscustomobject]@{
        Path = $path
        Report = [ordered]@{
            schemaVersion = 1; action = $Action; sourceCommit = $commit
            sourceDirty = [bool](& git -C $root status --porcelain)
            toolchainHash = $Unity.LockHash; editorVersion = $Unity.Lock.editorVersion; editorRevision = $Unity.Lock.editorRevision
            result = 'FAIL'; error = $null; commands = @()
        }
    }
}

function Invoke-ShooterProcess {
    param([string]$Executable, [string[]]$ProcessArguments, [string]$WorkingDirectory,
        [string]$LogPath, [ValidateRange(1, 7200)][int]$TimeoutSeconds = 1200, [string]$MemorySamplePath = '')
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable; $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in $ProcessArguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
    $memoryWriter = $null
    try {
        if (!$process.Start()) { throw "Cannot start $Executable" }
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        $elapsed = [Diagnostics.Stopwatch]::StartNew()
        if ($MemorySamplePath) {
            $memoryWriter = [IO.StreamWriter]::new($MemorySamplePath)
            $memoryWriter.WriteLine('utc,privateBytes,rssBytes')
        }
        while (!$process.WaitForExit(1000) -and $elapsed.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
            if ($memoryWriter) {
                $process.Refresh()
                if (!$process.HasExited) { $memoryWriter.WriteLine([DateTime]::UtcNow.ToString('o') + ',' + $process.PrivateMemorySize64 + ',' + $process.WorkingSet64) }
            }
        }
        if (!$process.HasExited) {
            $process.Kill($true); $process.WaitForExit()
            [IO.File]::WriteAllText($LogPath, $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult())
            throw "Process timed out after $TimeoutSeconds seconds: $Executable; see $LogPath"
        }
        [IO.File]::WriteAllText($LogPath, $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult())
        if ($process.ExitCode -ne 0) { throw "Process exited $($process.ExitCode): $Executable; see $LogPath" }
    }
    finally { if ($memoryWriter) { $memoryWriter.Dispose() }; $process.Dispose() }
}

function Invoke-ShooterEditor($Unity, $Run, [string[]]$EditorArguments, [int]$TimeoutSeconds = 1200) {
    $argsForEditor = @('-batchmode', '-projectPath', $Unity.Project, '-logFile', (Join-Path $Run.Path 'editor.log')) + $EditorArguments
    $Run.Report.commands += [ordered]@{ executable = $Unity.Editor; arguments = $argsForEditor }
    Invoke-ShooterProcess -Executable $Unity.Editor -ProcessArguments $argsForEditor -WorkingDirectory (Get-ShooterRoot) -LogPath (Join-Path $Run.Path 'process.log') -TimeoutSeconds $TimeoutSeconds
    $log = Join-Path $Run.Path 'editor.log'
    if (!(Test-Path -LiteralPath $log -PathType Leaf)) { throw 'Unity produced no fresh editor log.' }
    if (Select-String -LiteralPath $log -Pattern 'error CS\d+|Aborting batchmode due to failure|SHOOTER_FAILURE' -Quiet) { throw "Unity reported a failure: $log" }
}

function Save-ShooterRun($Run) {
    $Run.Report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $Run.Path 'run.json') -Encoding utf8NoBOM
    Write-Host "$($Run.Report.result): $($Run.Report.action); evidence: $($Run.Path)"
}
