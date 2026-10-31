$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Windows x64 .NET Framework C# compiler is required.' }
$output = Join-Path $root 'artifacts/bin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$source = [IO.Path]::GetFullPath((Join-Path $root 'src/GptQuotaBar.cs')).Replace('/', '\')
$manifest = [IO.Path]::GetFullPath((Join-Path $root 'src/app.manifest')).Replace('/', '\')
$binary = [IO.Path]::GetFullPath((Join-Path $output 'GptQuotaBar.exe')).Replace('/', '\')
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /utf8output "/out:$binary" "/win32manifest:$manifest" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Core.dll /reference:Microsoft.CSharp.dll $source
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host 'Built artifacts/bin/GptQuotaBar.exe'
