[CmdletBinding()]
param([Parameter(Mandatory)][string]$CacheRoot,[switch]$Force,[ValidateRange(30,3600)][int]$TimeoutSeconds=300)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repoRoot=Split-Path -Parent $PSScriptRoot
foreach ($guard in @('verify_contract_lock.py','verify_content_format.py','verify_feature_registry.py')) {
    & python (Join-Path $PSScriptRoot $guard) | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Source guard failed: $guard" }
}
$commit=(& git -C $repoRoot rev-parse HEAD).Trim()
$run=Join-Path $repoRoot ('Artifacts/m1-continuation/compile-set/'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
$null=New-Item -ItemType Directory -Path $run
$report=Join-Path $run 'compile-proof.json'
$CacheRoot=[IO.Path]::GetFullPath($CacheRoot)
$arguments=@('run','--project',(Join-Path $PSScriptRoot 'M1Content/M1Content.csproj'),'--configuration','Release','--',$repoRoot,$CacheRoot,$report,$commit,([bool]$Force).ToString().ToLowerInvariant())
$start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=(Get-Command dotnet).Source;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
$process=[Diagnostics.Process]::new();$process.StartInfo=$start
$proof=[ordered]@{result='FAIL';sourceCommit=$commit;sourceDirty=[bool](& git -C $repoRoot status --porcelain);arguments=$arguments;error=$null}
try {
    $null=$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    if (!$process.WaitForExit($TimeoutSeconds*1000)) { $process.Kill($true);$process.WaitForExit();throw 'M1 compile tool timeout.' }
    [IO.File]::WriteAllText((Join-Path $run 'process.log'),$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult())
    if ($process.ExitCode -ne 0) { throw 'M1 compile tool failed; see process.log.' }
    $compiled=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($compiled.result -cne 'Verified' -or $compiled.content.Count -ne 12 -or $compiled.sourceCommit -cne $commit) { throw 'M1 compile set proof incomplete.' }
    foreach ($item in $compiled.content) {
        $manifest=Join-Path $CacheRoot ($item.id.Substring(0,2)+'/'+$item.id+'/manifest.json')
        if ((Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant() -cne $item.manifestSha256) { throw 'M1 manifest hash mismatch.' }
    }
    $proof.result='PASS'
}
catch { $proof.error=$_.Exception.Message;Write-Warning $proof.error }
finally { $process.Dispose();$proof | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'run.json') -Encoding utf8NoBOM }
Write-Host "$($proof.result): M1 feasibility content set; $run"
if ($proof.result -ne 'PASS') { exit 1 }
