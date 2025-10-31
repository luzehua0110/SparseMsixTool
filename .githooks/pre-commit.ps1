#!/usr/bin/env pwsh
# Pre-commit hook: block accidental reintroduction of legacy alias 'msixtool'
# Excludes historical changelog file 更新日志.md
param()
$ErrorActionPreference = 'SilentlyContinue'
# Ensure running from repo root
Set-Location (git rev-parse --show-toplevel)
# Find forbidden references (case-insensitive)
$legacyRefs = git grep -I -n -i 'msixtool' -- ':!更新日志.md'
if ($LASTEXITCODE -eq 0 -and $legacyRefs) {
    Write-Host "[HOOK] ❌ Found legacy 'msixtool' references (excluding 更新日志.md). Please remove them:" -ForegroundColor Red
    $legacyRefs | ForEach-Object { Write-Host $_ }
    Write-Host "[HOOK] Commit aborted to maintain branding consistency." -ForegroundColor Yellow
    exit 1
}
Write-Host "[HOOK] ✅ No forbidden legacy alias references detected." -ForegroundColor Green
exit 0