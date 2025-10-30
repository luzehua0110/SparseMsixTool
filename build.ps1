# Build script for MSIX Tool

Write-Host "Building MSIX Tool..." -ForegroundColor Green

# Check if .NET SDK is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "Found .NET SDK version: $dotnetVersion" -ForegroundColor Blue
} catch {
    Write-Host "Error: .NET SDK not found. Please install .NET 6.0 or later." -ForegroundColor Red
    Write-Host "Download from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# Build project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# Publish with Native AOT (不能与 PublishSingleFile 同时使用)
Write-Host "Publishing with Native AOT..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishAot=true
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host ""
Write-Host "Build completed successfully!" -ForegroundColor Green
$publishPath = Join-Path $PSScriptRoot "bin\Release\net8.0\win-x64\publish"
Write-Host "Executable location: $publishPath\msixtool.exe" -ForegroundColor Blue

Write-Host ""
Write-Host "To add to PATH:" -ForegroundColor Yellow
Write-Host "1. Copy the full path: $publishPath" -ForegroundColor White
Write-Host "2. Add to System PATH environment variable" -ForegroundColor White
Write-Host "3. Restart command prompt" -ForegroundColor White

# Offer to add to PATH automatically
Write-Host ""
$addToPath = Read-Host "Do you want to add to PATH automatically? (y/N)"
if ($addToPath -eq 'y' -or $addToPath -eq 'Y') {
    try {
        $currentPath = [Environment]::GetEnvironmentVariable("PATH", [EnvironmentVariableTarget]::User)
        if ($currentPath -notlike "*$publishPath*") {
            $newPath = $currentPath + ";" + $publishPath
            [Environment]::SetEnvironmentVariable("PATH", $newPath, [EnvironmentVariableTarget]::User)
            Write-Host "Added to user PATH successfully! Please restart your command prompt." -ForegroundColor Green
        } else {
            Write-Host "Path already exists in PATH." -ForegroundColor Blue
        }
    } catch {
        Write-Host "Failed to add to PATH automatically. Please add manually." -ForegroundColor Red
    }
}

Read-Host "Press Enter to exit"