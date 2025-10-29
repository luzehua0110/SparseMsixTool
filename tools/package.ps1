param(
    [string]$Runtime = "win-x64",
    [switch]$Aot,
    [switch]$NoTimestamp,
    [switch]$LatestAlias
)

$ErrorActionPreference = 'Stop'

Write-Host "==> 发布配置" -ForegroundColor Cyan
Write-Host " Runtime: $Runtime"
Write-Host " AOT: $($Aot.IsPresent)"

$publishDir = Join-Path $PSScriptRoot "..\publish"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

$pubArgs = @('publish', '-c', 'Release', '-r', $Runtime, '--self-contained', 'true', '-o', $publishDir)
if ($Aot) { $pubArgs += '/p:PublishAot=true' }

Write-Host "==> 执行 dotnet $($pubArgs -join ' ')" -ForegroundColor Yellow
& dotnet @pubArgs
if ($LASTEXITCODE -ne 0) { throw '发布失败' }

$exe = Join-Path $publishDir 'msixtool.exe'
if (!(Test-Path $exe)) { throw "未找到生成的 msixtool.exe: $exe" }

$timestamp = Get-Date -Format 'yyyyMMddHHmm'
$zipBase = "MsixTool"
$zipName = if ($NoTimestamp) { "$zipBase.zip" } else { "$zipBase-$timestamp.zip" }
$zipPath = Join-Path (Get-Location) $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "==> 打包压缩: $zipName" -ForegroundColor Yellow
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath

if ($LatestAlias -and -not $NoTimestamp) {
    $latest = Join-Path (Get-Location) "$zipBase-latest.zip"
    Copy-Item $zipPath $latest -Force
    Write-Host "==> 生成别名: $(Split-Path -Leaf $latest)" -ForegroundColor Green
}

$sizeMB = [math]::Round((Get-Item $zipPath).Length/1MB,2)
Write-Host "生成成功: $zipName ($sizeMB MB)" -ForegroundColor Green

Write-Host "\n用法示例:" -ForegroundColor Cyan
Write-Host "  powershell -ExecutionPolicy Bypass -File tools/package.ps1 -Aot -LatestAlias" -ForegroundColor Gray
Write-Host "  powershell -File tools/package.ps1 -Runtime win-x86" -ForegroundColor Gray
