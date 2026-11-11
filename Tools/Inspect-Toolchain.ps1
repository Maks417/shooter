[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $repoRoot 'Artifacts/toolchain-discovery'
$null = New-Item -ItemType Directory -Path $outputDirectory -Force
$candidatePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$hubConfigs = @(
    (Join-Path $env:APPDATA 'UnityHub/editors.json'),
    (Join-Path $env:APPDATA 'UnityHub/secondaryInstallPath.json'),
    (Join-Path $env:LOCALAPPDATA 'UnityHub/editors.json'),
    (Join-Path $env:LOCALAPPDATA 'UnityHub/secondaryInstallPath.json')
)
$editorRoots = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$null = $editorRoots.Add((Join-Path $env:ProgramFiles 'Unity/Hub/Editor'))
$null = $candidatePaths.Add((Join-Path $env:ProgramFiles 'Unity/Editor/Unity.exe'))
function Add-ConfigPaths($Value) {
    if ($Value -is [string]) {
        if ([IO.Path]::IsPathRooted($Value)) {
            if ([IO.Path]::GetFileName($Value) -eq 'Unity.exe') { $null = $candidatePaths.Add($Value) }
            else { $null = $editorRoots.Add($Value) }
        }
    }
    elseif ($Value -is [array]) { foreach ($item in $Value) { Add-ConfigPaths $item } }
    elseif ($Value -is [pscustomobject]) { foreach ($property in $Value.PSObject.Properties) { Add-ConfigPaths $property.Value } }
}
foreach ($config in $hubConfigs) {
    if (Test-Path -LiteralPath $config -PathType Leaf) { Add-ConfigPaths (Get-Content -LiteralPath $config -Raw | ConvertFrom-Json) }
}
$uninstallKeys = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
)
$registeredUnity = @(Get-ItemProperty $uninstallKeys -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match '\bUnity\b' } | Select-Object DisplayName, DisplayVersion, InstallLocation)
foreach ($app in $registeredUnity) { if ($app.InstallLocation) { $null = $editorRoots.Add($app.InstallLocation.Trim('"')) } }
foreach ($registryPath in @('HKCU:\Software\Unity Technologies\Installer\Unity', 'HKLM:\SOFTWARE\Unity Technologies\Installer\Unity')) {
    if (Test-Path -LiteralPath $registryPath) {
        $registryValue = Get-Item -LiteralPath $registryPath
        foreach ($name in $registryValue.GetValueNames()) { Add-ConfigPaths $registryValue.GetValue($name) }
    }
}
$pathEditor = Get-Command Unity.exe -ErrorAction SilentlyContinue
if ($pathEditor) { $null = $candidatePaths.Add($pathEditor.Source) }
foreach ($root in $editorRoots) {
    $null = $candidatePaths.Add((Join-Path $root 'Editor/Unity.exe'))
    $null = $candidatePaths.Add((Join-Path $root 'Unity.exe'))
    if (Test-Path -LiteralPath $root -PathType Container) {
        foreach ($directory in Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue) { $null = $candidatePaths.Add((Join-Path $directory.FullName 'Editor/Unity.exe')) }
    }
}
$editors = @(foreach ($path in $candidatePaths) {
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        $file = Get-Item -LiteralPath $path
        [ordered]@{ path = $file.FullName; productVersion = $file.VersionInfo.ProductVersion; fileVersion = $file.VersionInfo.FileVersion; selected = $false }
    }
})
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$sdkVersion = $null; $sdkInfoExitCode = $null
if ($dotnet) {
    $sdkVersion = (& $dotnet.Source --version).Trim()
    & $dotnet.Source --info 2>&1 | Out-File -LiteralPath (Join-Path $outputDirectory 'dotnet-info.txt') -Encoding utf8
    $sdkInfoExitCode = $LASTEXITCODE
}
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$cppInstallations = @()
if (Test-Path -LiteralPath $vswhere) { $cppInstallations = @(& $vswhere -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath) }
$sdkIncludes = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/Include'
$windowsSdks = @(Get-ChildItem -LiteralPath $sdkIncludes -Directory -ErrorAction SilentlyContinue | ForEach-Object Name)
$report = [ordered]@{
    schemaVersion = 1
    observedAtUtc = [DateTime]::UtcNow.ToString('o')
    sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
    status = $(if ($editors.Count -eq 0) { 'PendingUnityInstallation' } else { 'PendingEditorSelectionAndBuildVerification' })
    editorCandidates = $editors
    hubConfigsChecked = $hubConfigs
    registeredUnity = $registeredUnity
    editorCandidatePathsChecked = @($candidatePaths | Sort-Object)
    dotnetSdk = $sdkVersion
    dotnetInfoExitCode = $sdkInfoExitCode
    cppInstallations = $cppInstallations
    windowsSdkIncludeVersions = $windowsSdks
    unityLicense = 'Unverified: requires selected editor and real builds; no credential files inspected'
    il2cppClientBuild = 'Pending'
    dedicatedServerBuild = 'Pending'
    packageVersions = 'Unselected: no toolchain lock may be invented'
    scope = 'Known-path, PATH, registry and Hub-config discovery; not an exhaustive filesystem search or build verification.'
}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $outputDirectory 'discovery.json') -Encoding utf8NoBOM
Write-Host ($report | ConvertTo-Json -Depth 10)
if ($editors.Count -eq 0) { exit 2 }
