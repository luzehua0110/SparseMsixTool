param(
    [string]$Runtime = "win-x64",
    [switch]$Aot,
    [switch]$NoTimestamp,
    [switch]$LatestAlias
)

$ErrorActionPreference = "Stop"

Write-Host "==> Publishing Config" -ForegroundColor Cyan
Write-Host " Runtime: $Runtime"
Write-Host " AOT: $($Aot.IsPresent)"

$publishDir = Join-Path $PSScriptRoot "..\publish"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

$pubArgs = @("publish", "-c", "Release", "-r", $Runtime, "--self-contained", "true", "-o", $publishDir)
if ($Aot) { $pubArgs += "/p:PublishAot=true" }

Write-Host "==> Running dotnet $($pubArgs -join " ")" -ForegroundColor Yellow
& dotnet @pubArgs
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

$exe = Join-Path $publishDir "sparsemsix.exe"
if (!(Test-Path $exe)) { throw "sparsemsix.exe not found: $exe" }

Write-Host "(legacy alias msixtool.exe 已移除)" -ForegroundColor DarkGray

$csprojPath = Join-Path $PSScriptRoot "..\SparseMsixTool.csproj"
$csprojContent = Get-Content $csprojPath -Raw
if ($csprojContent -match "<Version>([\d\.]+)</Version>") {
    $version = $Matches[1]
} else {
    throw "Cannot read version from SparseMsixTool.csproj"
}

$timestamp = Get-Date -Format "yyyyMMdd"
$zipBase = "SparseMsixTool-v$version"
$zipName = if ($NoTimestamp) { "$zipBase.zip" } else { "$zipBase-$timestamp.zip" }
$zipPath = Join-Path (Get-Location) $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "==> Compressing: $zipName" -ForegroundColor Yellow
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

if ($LatestAlias -and -not $NoTimestamp) {
    $latest = Join-Path (Get-Location) "$zipBase-latest.zip"
    Copy-Item $zipPath $latest -Force
    Write-Host "==> Created alias: $(Split-Path -Leaf $latest)" -ForegroundColor Green
}

$sizeMB = [math]::Round((Get-Item $zipPath).Length/1MB,2)
Write-Host "Build successful: $zipName ($sizeMB MB)" -ForegroundColor Green
# No legacy alias included

Write-Host "`nUsage:" -ForegroundColor Cyan
Write-Host "  powershell -ExecutionPolicy Bypass -File tools/package.ps1 -Aot -LatestAlias" -ForegroundColor Gray
Write-Host "  powershell -File tools/package.ps1 -Runtime win-x86" -ForegroundColor Gray
