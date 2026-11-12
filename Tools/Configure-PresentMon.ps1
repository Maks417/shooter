[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'presentmon-lock.json') -Raw | ConvertFrom-Json
$path = Join-Path $root $lock.relativeExecutable
$null = New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force
if (!(Test-Path -LiteralPath $path)) { Invoke-WebRequest -Uri $lock.url -OutFile $path }
if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $lock.sha256) { throw 'Pinned PresentMon hash mismatch; existing executable is not overwritten.' }
$license = Join-Path (Split-Path -Parent $path) 'LICENSE.txt'
if (!(Test-Path -LiteralPath $license)) { Invoke-WebRequest -Uri $lock.licenseUrl -OutFile $license }
if ((Get-FileHash -LiteralPath $license -Algorithm SHA256).Hash.ToLowerInvariant() -cne $lock.licenseSha256) { throw 'Pinned PresentMon license hash mismatch.' }
Write-Host "Verified standalone PresentMon $($lock.version): $path"
