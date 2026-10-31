$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$prefix = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$files = @(Get-ChildItem -LiteralPath $root -Recurse -File -Force | Where-Object {
    $relative = $_.FullName.Substring($prefix.Length).Replace('\', '/')
    $relative -notmatch '^(\.git|artifacts)/'
})

$tracked = @()
# Also inspect staged/tracked files, even when an ignore rule would hide them.
if ((Get-Command git -ErrorAction SilentlyContinue) -and (Test-Path -LiteralPath (Join-Path $root '.git'))) {
    $tracked = @(& git -C $root ls-files --cached)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate the Git index.' }
    foreach ($relative in $tracked) {
        if ($relative -match '^(\.git|artifacts)/') { throw "Generated/private file tracked: $relative" }
        $path = Join-Path $root $relative
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Tracked file missing: $relative" }
    }
}

$checks = [ordered]@{
    'personal Windows path' = '(?i)[A-Z]:[\\/](?:Users|Documents and Settings)[\\/][^\\/<>"\r\n]+'
    'personal Unix path' = '/(?:Users|home)/[a-zA-Z0-9_.-]+/'
    'email address' = '\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b'
    'OpenAI-style key' = '\bsk-(?:proj-|svcacct-)?[A-Za-z0-9_-]{20,}'
    'GitHub token' = '\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})'
    'private key' = '-----BEGIN (?:[A-Z]+ )?PRIVATE KEY-----'
    'JWT' = '\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}'
    'literal credential' = '(?i)\b(?:api[_-]?key|access[_-]?token|refresh[_-]?token|password)\s*[:=]\s*["''][^"''\r\n]{8,}["'']'
}
$findings = @()
foreach ($file in $files) {
    $relative = $file.FullName.Substring($prefix.Length).Replace('\', '/')
    if ($relative -match '(?i)(^|/)(?:\.env(?:\..*)?|auth\.json|credentials\.json|settings\.json|installation\.json|error\.json|diagnostics\.json|quota-probe\.json|live-state\.json|live-bar\.png|test-results\.json|ui-test-results\.json|ui-test-preview\.png|fixture-state\.json|\.codex)(/|$)' -or $file.Extension -match '(?i)^\.(exe|dll|pdb|zip|7z|log|pem|key)$') {
        $findings += "$relative : forbidden public file"
        continue
    }
    if ($relative -eq 'assets/preview.png') { continue }
    if ($file.Extension -notin @('', '.cs', '.manifest', '.ps1', '.cmd', '.md', '.yml', '.yaml') -and $file.Name -notin @('.gitignore', '.gitattributes', '.editorconfig')) {
        $findings += "$relative : unexpected file type; review before publishing"
        continue
    }
    $variants = @(@{ Kind = 'worktree'; Lines = [IO.File]::ReadAllLines($file.FullName) })
    if ($tracked -contains $relative) {
        $indexLines = @(& git -C $root show ":$relative")
        if ($LASTEXITCODE -ne 0) { throw "Cannot inspect staged file: $relative" }
        $variants += @{ Kind = 'index'; Lines = $indexLines }
    }
    foreach ($variant in $variants) {
        $lines = $variant.Lines
        for ($i = 0; $i -lt $lines.Length; $i++) {
            foreach ($check in $checks.GetEnumerator()) {
                if ($lines[$i] -match $check.Value) { $findings += "${relative}:$($i + 1) ($($variant.Kind)) : $($check.Key)" }
            }
        }
    }
}
if ($findings.Count) {
    # Report locations and categories, never echo a matched credential.
    $findings | ForEach-Object { Write-Host $_ }
    throw "Public-file check failed ($($findings.Count) findings)."
}
Write-Host "Public-file check passed ($($files.Count) files). Pattern checks still require human review."
