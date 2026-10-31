$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'verify.ps1')
$manifest = [xml](Get-Content -LiteralPath (Join-Path $root 'src/app.manifest') -Raw)
$version = ([version]$manifest.assembly.assemblyIdentity.version).ToString(3)
$name = "GptQuotaBar-v$version-windows-x64"
$release = Join-Path $root 'artifacts/releases'
$stage = Join-Path $root ('artifacts/package-' + [guid]::NewGuid().ToString('N'))
$folder = Join-Path $stage $name
New-Item -ItemType Directory -Path $folder -Force | Out-Null
New-Item -ItemType Directory -Path $release -Force | Out-Null
# Explicit allowlist: no settings, diagnostics, credentials or live screenshots.
Copy-Item -LiteralPath (Join-Path $root 'artifacts/bin/GptQuotaBar.exe') -Destination $folder
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $folder
Copy-Item -LiteralPath (Join-Path $root 'docs/QUICKSTART.md') -Destination (Join-Path $folder 'README.md')
foreach ($command in @('Install', 'Uninstall', 'Start', 'Stop')) {
    Copy-Item -LiteralPath (Join-Path $root "scripts/$command.cmd") -Destination $folder
}
$zip = Join-Path $release "$name.zip"
Compress-Archive -LiteralPath $folder -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS.txt'), "$hash  $name.zip`n", (New-Object Text.UTF8Encoding($false)))
Copy-Item -LiteralPath (Join-Path $root 'docs/RELEASE_NOTES.md') -Destination (Join-Path $release 'release-notes.md')
# Clean only the uniquely created staging directory inside this repository.
$resolvedStage = [IO.Path]::GetFullPath($stage)
$allowedPrefix = [IO.Path]::GetFullPath((Join-Path $root 'artifacts')).TrimEnd('\') + '\'
if (!$resolvedStage.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe staging path.' }
Remove-Item -LiteralPath $resolvedStage -Recurse -Force
Write-Host "Release package ready: $name.zip"
