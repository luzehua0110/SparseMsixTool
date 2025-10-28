# ================================
# MsixTool 无依赖发布脚本
# ================================
# 此脚本会：
# 1. 使用Native AOT编译生成独立可执行文件
# 2. 从本地Windows SDK复制必需的工具
# 3. 打包所有文件到一个可移植的发布目录

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = ".\publish-standalone"
)

$ErrorActionPreference = "Stop"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  MsixTool 无依赖发布脚本" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# ================================
# 1. 检查.NET SDK
# ================================
Write-Host "[1/6] 检查 .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "  ✓ 找到 .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "  ✗ 未找到 .NET SDK" -ForegroundColor Red
    Write-Host "  请安装 .NET 8.0 SDK: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# ================================
# 2. 查找Windows SDK工具
# ================================
Write-Host "[2/6] 查找 Windows SDK 工具..." -ForegroundColor Yellow

# 查找Windows SDK安装路径
$sdkPaths = @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
    "${env:ProgramFiles}\Windows Kits\10\bin"
)

$sdkBinPath = $null
$sdkVersion = $null

foreach ($basePath in $sdkPaths) {
    if (Test-Path $basePath) {
        # 查找最新版本的SDK
        $versions = Get-ChildItem $basePath -Directory | Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } | Sort-Object Name -Descending
        if ($versions.Count -gt 0) {
            $sdkVersion = $versions[0].Name
            $sdkBinPath = Join-Path $basePath "$sdkVersion\x64"
            break
        }
    }
}

if (-not $sdkBinPath -or -not (Test-Path $sdkBinPath)) {
    Write-Host "  ✗ 未找到 Windows SDK" -ForegroundColor Red
    Write-Host "  请安装 Windows SDK 10: https://developer.microsoft.com/windows/downloads/windows-sdk/" -ForegroundColor Yellow
    exit 1
}

Write-Host "  ✓ 找到 Windows SDK: $sdkVersion" -ForegroundColor Green
Write-Host "    路径: $sdkBinPath" -ForegroundColor Gray

# 检查必需的工具
$requiredTools = @("makeappx.exe", "signtool.exe", "makecert.exe", "pvk2pfx.exe")
$missingTools = @()

foreach ($tool in $requiredTools) {
    $toolPath = Join-Path $sdkBinPath $tool
    if (Test-Path $toolPath) {
        Write-Host "  ✓ $tool" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $tool (未找到)" -ForegroundColor Red
        $missingTools += $tool
    }
}

if ($missingTools.Count -gt 0) {
    Write-Host ""
    Write-Host "  错误: 缺少以下工具: $($missingTools -join ', ')" -ForegroundColor Red
    Write-Host "  请重新安装 Windows SDK 并确保选择了签名工具组件" -ForegroundColor Yellow
    exit 1
}

# ================================
# 3. 清理并创建tools目录
# ================================
Write-Host "[3/6] 准备工具目录..." -ForegroundColor Yellow

$toolsDir = ".\tools\sdk"
if (Test-Path $toolsDir) {
    Remove-Item $toolsDir -Recurse -Force
}
New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null

# 复制SDK工具
foreach ($tool in $requiredTools) {
    $sourcePath = Join-Path $sdkBinPath $tool
    $destPath = Join-Path $toolsDir $tool
    Copy-Item $sourcePath $destPath -Force
    Write-Host "  ✓ 复制 $tool" -ForegroundColor Green
}

# 复制必需的DLL依赖
$requiredDlls = @()
$dllFiles = Get-ChildItem $sdkBinPath -Filter "*.dll"
foreach ($dll in $dllFiles) {
    # 只复制这些工具可能需要的DLL
    if ($dll.Name -match "^(mssign32|wintrust|crypt32|msvcr|msvcp|vcruntime)") {
        Copy-Item $dll.FullName (Join-Path $toolsDir $dll.Name) -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "  ✓ SDK工具已准备就绪" -ForegroundColor Green

# ================================
# 4. 执行Native AOT发布
# ================================
Write-Host "[4/6] 执行 Native AOT 发布..." -ForegroundColor Yellow
Write-Host "  配置: $Configuration" -ForegroundColor Gray
Write-Host "  目标: win-x64" -ForegroundColor Gray
Write-Host ""

$publishCmd = "dotnet publish -c $Configuration -r win-x64 --self-contained true -o `"$OutputDir`""
Write-Host "  命令: $publishCmd" -ForegroundColor Gray
Write-Host ""

try {
    Invoke-Expression $publishCmd
    if ($LASTEXITCODE -ne 0) {
        throw "发布失败"
    }
    Write-Host ""
    Write-Host "  ✓ 发布成功" -ForegroundColor Green
} catch {
    Write-Host ""
    Write-Host "  ✗ 发布失败: $_" -ForegroundColor Red
    exit 1
}

# ================================
# 5. 创建README
# ================================
Write-Host "[5/6] 创建说明文件..." -ForegroundColor Yellow

$readmeContent = @"
# MsixTool - 独立发布包

## 版本信息
- 编译时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
- .NET版本: $dotnetVersion
- Windows SDK: $sdkVersion
- 编译模式: Native AOT (无需.NET运行时)

## 文件说明
- msixtool.exe - 主程序 (Native AOT编译，无需.NET运行时)
- tools/sdk/ - Windows SDK工具 (makeappx, signtool, makecert, pvk2pfx)

## 使用方法

### 快速开始
```powershell
# 查看帮助
.\msixtool.exe --help

# 检查依赖(首次运行)
.\msixtool.exe deps
```

### 基本命令
```powershell
# 打包MSIX
.\msixtool.exe pack <源目录> <输出.msix> [--manifest <清单文件>]

# 安装MSIX
.\msixtool.exe install <msix文件>

# 卸载应用
.\msixtool.exe uninstall <包名称>

# 创建证书
.\msixtool.exe makecert <证书文件.pfx> [--password <密码>]

# 打包并安装
.\msixtool.exe pack-install <源目录> <输出.msix>
```

## 系统要求
- Windows 10 (Build 19041+) 或 Windows 11
- PowerShell 5.1+ (系统自带)
- 管理员权限(用于安装证书和部署包)

## 无依赖说明
此发布包已包含所有必需组件：
✓ 使用Native AOT编译，无需安装.NET运行时
✓ 内置Windows SDK工具，无需单独安装SDK
✓ 可直接复制到任何Windows 10+机器运行

## 部署到其他机器
1. 将整个目录复制到目标机器
2. 确保目标机器是Windows 10 (19041+)或Windows 11
3. 以管理员身份运行命令提示符或PowerShell
4. 执行 msixtool.exe 命令

## 技术细节
- 编译技术: .NET 8 Native AOT
- 目标平台: Windows 10.0.19041.0
- 架构: x64
- 包含的SDK工具来自: Windows SDK $sdkVersion

## 许可证
详见 LICENSE 文件

## 更多信息
访问项目主页获取完整文档
"@

$readmePath = Join-Path $OutputDir "README.txt"
$readmeContent | Out-File -FilePath $readmePath -Encoding UTF8
Write-Host "  ✓ 创建 README.txt" -ForegroundColor Green

# ================================
# 6. 验证发布包
# ================================
Write-Host "[6/6] 验证发布包..." -ForegroundColor Yellow

$exePath = Join-Path $OutputDir "msixtool.exe"
$toolsSdkPath = Join-Path $OutputDir "tools\sdk"

if (Test-Path $exePath) {
    $fileSize = (Get-Item $exePath).Length / 1MB
    Write-Host "  ✓ msixtool.exe ($([math]::Round($fileSize, 2)) MB)" -ForegroundColor Green
} else {
    Write-Host "  ✗ 主程序未找到" -ForegroundColor Red
    exit 1
}

if (Test-Path $toolsSdkPath) {
    $toolCount = (Get-ChildItem $toolsSdkPath -Filter "*.exe").Count
    Write-Host "  ✓ SDK工具目录 ($toolCount 个工具)" -ForegroundColor Green
} else {
    Write-Host "  ✗ SDK工具目录未找到" -ForegroundColor Red
    exit 1
}

# ================================
# 完成
# ================================
Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "  发布完成!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "发布位置: $OutputDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "部署说明:" -ForegroundColor Yellow
Write-Host "  1. 将整个 '$OutputDir' 目录复制到目标机器" -ForegroundColor White
Write-Host "  2. 在目标机器上以管理员身份运行 PowerShell" -ForegroundColor White
Write-Host "  3. 执行: .\msixtool.exe --help" -ForegroundColor White
Write-Host ""
Write-Host "测试命令:" -ForegroundColor Yellow
Write-Host "  cd $OutputDir" -ForegroundColor White
Write-Host "  .\msixtool.exe deps" -ForegroundColor White
Write-Host ""

# 询问是否立即测试
$testNow = Read-Host "是否立即测试发布包? (Y/N)"
if ($testNow -eq 'Y' -or $testNow -eq 'y') {
    Write-Host ""
    Write-Host "运行测试..." -ForegroundColor Yellow
    Push-Location $OutputDir
    try {
        & ".\msixtool.exe" deps
    } finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "按任意键退出..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
