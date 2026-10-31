$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'build.ps1')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$output = Join-Path $root 'artifacts/tests'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$test = [IO.Path]::GetFullPath((Join-Path $output 'InputRegression.exe')).Replace('/', '\')
$source = [IO.Path]::GetFullPath((Join-Path $root 'tests/InputRegression.cs')).Replace('/', '\')
$manifest = [IO.Path]::GetFullPath((Join-Path $root 'src/app.manifest')).Replace('/', '\')
& $compiler /nologo /target:winexe /platform:x64 /codepage:65001 /utf8output "/out:$test" "/win32manifest:$manifest" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $source
if ($LASTEXITCODE -ne 0) { throw 'Interaction test compilation failed.' }
$binary = [IO.Path]::GetFullPath((Join-Path $root 'artifacts/bin/GptQuotaBar.exe'))
$report = [IO.Path]::GetFullPath((Join-Path $output 'results.json'))
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
Write-Host 'Testing a separate fixture window. Keep the foreground application unchanged until completion.'
$process = Start-Process -FilePath $test -ArgumentList ('"' + $binary + '" "' + $report + '"') -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $report)) { throw 'Interaction test failed; inspect artifacts/tests/results.json locally.' }
if (!(Get-Content -LiteralPath $report -Raw | ConvertFrom-Json).passed) { throw 'Interaction checks failed.' }
Write-Host 'Separate-process interaction regression passed.'
