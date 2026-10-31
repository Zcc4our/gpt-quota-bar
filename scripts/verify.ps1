$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1') {
    $tokens = $null; $errors = $null
    [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count) { throw "PowerShell syntax errors in $($file.Name)." }
}
& (Join-Path $PSScriptRoot 'check-public.ps1')
$manifest = [xml](Get-Content -LiteralPath (Join-Path $root 'src/app.manifest') -Raw)
$version = ([version]$manifest.assembly.assemblyIdentity.version).ToString(3)
$source = Get-Content -LiteralPath (Join-Path $root 'src/GptQuotaBar.cs') -Raw -Encoding UTF8
if (!$source.Contains('version = "' + $version + '"')) { throw 'Manifest and app-server client versions differ.' }
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -cne "v$version") { throw 'Release tag must match the manifest version.' }
& (Join-Path $PSScriptRoot 'build.ps1')
$binary = Join-Path $root 'artifacts/bin/GptQuotaBar.exe'
$resultPath = Join-Path $root 'artifacts/bin/test-results.json'
if (Test-Path -LiteralPath $resultPath) { Remove-Item -LiteralPath $resultPath }
$process = Start-Process -FilePath $binary -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $resultPath)) { throw 'Offline self-test failed.' }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if ($result.failed -ne 0 -or $result.passed -lt 25) { throw 'Unexpected self-test result.' }
Write-Host "Verified v$version : $($result.passed) offline assertions passed."
