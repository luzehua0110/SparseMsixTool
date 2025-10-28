@echo off
echo Building MSIX Tool...

REM 检查是否安装了 .NET SDK
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: .NET SDK not found. Please install .NET 6.0 or later.
    echo Download from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

REM 构建项目
echo Building project...
dotnet build -c Release
if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

REM 发布单文件可执行程序
echo Publishing single-file executable...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
if %errorlevel% neq 0 (
    echo Publish failed!
    pause
    exit /b 1
)

echo.
echo Build completed successfully!
echo Executable location: bin\Release\net6.0\win-x64\publish\msixtool.exe
echo.
echo To add to PATH:
echo 1. Copy the full path: %~dp0bin\Release\net6.0\win-x64\publish
echo 2. Add to System PATH environment variable
echo 3. Restart command prompt
echo.
pause