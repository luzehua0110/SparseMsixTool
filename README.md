# MsixTool - MSIX 稀疏包工具

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)

强大的 MSIX 稀疏包命令行工具，支持打包、签名、安装、卸载和证书管理。Native AOT 编译，无需 .NET 运行时，开箱即用。

## ✨ 核心特性

- 🎯 **一键操作**: `packandinstall` 完成打包、签名、安装全流程
- ✍️ **智能证书**: 自动创建/查找/安装自签名证书
- 📦 **配置记忆**: 自动保存路径，下次使用无需重复输入
- 🔄 **自动升级**: 安装前自动卸载旧版本（可选 `--no-auto-uninstall`）
- 🎨 **友好输出**: 彩色提示（红色错误/黄色警告），清晰直观
- 💿 **Native AOT**: 单文件发布，14.8MB，无需 .NET 运行时
- 🖼️ **图标刷新**: 卸载后自动清理图标缓存

## 📥 快速开始

### 安装

从 [Releases](https://github.com/luzehua0110/msixtool/releases) 下载最新版本，解压即用。

### 5分钟上手

```powershell
# 1. 创建自签名证书（首次使用）
msixtool makecert --publisher "CN=MyCompany" --password "pass123"

# 2. 一键打包、签名并安装稀疏包
msixtool packandinstall --src "C:\MyApp" --dest "C:\MyApp"

# 3. 查找已安装的包
msixtool find MyApp

# 4. 卸载包
msixtool uninstall MyApp
```

## 📋 命令速查

### 核心命令

| 命令 | 说明 | 示例 |
|------|------|------|
| **packandinstall** | 打包+签名+安装（一步到位） | `msixtool packandinstall --dest C:\App` |
| **packandsign** | 打包+签名（不安装） | `msixtool packandsign --src . --dest C:\App` |
| **install** | 安装 MSIX 包 | `msixtool install --src app.msix --dest C:\App` |
| **uninstall** | 卸载包 | `msixtool uninstall MyApp` |
| **find** | 查找已安装的包 | `msixtool find MyApp` |

### 证书管理

| 命令 | 说明 | 示例 |
|------|------|------|
| **makecert** | 创建自签名证书 | `msixtool makecert --publisher "CN=Company" --password pass123` |
| **sign** | 为包签名 | `msixtool sign --src app.msix --cert cert.pfx --password pass123` |
| **findcert** | 查找证书 | `msixtool findcert MyCompany` |
| **delcert** | 删除证书 | `msixtool delcert C:\cert.pfx` |

### 其他工具

| 命令 | 说明 | 示例 |
|------|------|------|
| **pack** | 仅打包（不签名） | `msixtool pack --src .` |
| **deps** | 检查依赖 | `msixtool deps` |

## 🎯 典型场景

### 场景1：快速开发测试

```powershell
# 应用目录结构
MyApp/
├── AppxManifest.xml
├── MyApp.exe
└── Assets/
    └── logo.png

# 一键打包安装（--dest 指定外部程序所在目录）
cd MyApp
msixtool packandinstall --dest .

# 测试完成后卸载
msixtool uninstall MyApp
```

### 场景2：企业内部分发

```powershell
# 1. 创建企业证书（--dest 指定证书保存目录）
msixtool makecert --publisher "CN=MyCompany" --dest C:\Certs --password CompanyPass

# 2. 打包签名（不安装，--dest 指定外部程序所在目录）
msixtool packandsign --src C:\MyApp --dest C:\MyApp --cert C:\Certs\CN_MyCompany.pfx --password CompanyPass

# 3. 分发 MSIX 文件给用户
# 用户安装：msixtool install --src MyApp_1.0.0.0.msix --dest C:\MyApp
```

### 场景3：自动化CI/CD

```powershell
# 构建后自动打包（不安装，--dest 指定外部程序所在目录）
msixtool packandsign --src "$env:BUILD_DIR" --dest "$env:APP_DIR"

# 上传生成的 .msix 到发布平台
```

## 🔧 高级功能

### 配置记忆

工具会自动记住上次使用的外部程序路径（目录），下次可省略 `--dest`：

```powershell
# 第一次（--dest 指定外部程序所在目录）
msixtool packandinstall --dest C:\MyApp

# 之后直接用（自动使用历史路径）
msixtool packandinstall
```

### 证书复用

证书和密码会保存到配置文件（`config/app.config.json`），后续自动使用：

```powershell
# 第一次指定证书（--dest 指定外部程序所在目录）
msixtool packandinstall --cert cert.pfx --password pass123 --dest C:\MyApp

# 之后自动使用配置中的证书
msixtool packandinstall --dest C:\MyApp
```

### 文件自动备份

覆盖现有 MSIX 文件前自动备份（时间戳格式 `yyyyMMddHHmm`）：
```
MyApp.msix → MyApp_202510291630.msix
```

### 参数校验增强

- `--cert` 和 `--password` 必须配套使用
- `--dest` 可省略时自动从配置读取

## 🔧 系统要求

- **操作系统**: Windows 10/11 (1809+)
- **依赖**: Windows SDK（工具自动检测并提示安装位置）
- **架构**: x64
- **无需 .NET 运行时**

## 📖 详细文档

- [命令参考](命令参考.md) - 所有命令的详细参数
- [故障排查指南](故障排查指南.md) - 常见问题解决方案
- [开发者文档](开发者文档.md) - 开发和构建指南
- [项目架构](项目架构.md) - 技术架构说明
- [更新日志](更新日志.md) - 版本历史

## 🛠️ 本地构建

### 开发构建

```powershell
# 克隆仓库
git clone https://github.com/luzehua0110/msixtool.git
cd msixtool

# Debug 编译
dotnet build -c Debug

# 发布 (Native AOT)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishAot=true

# 或使用快捷脚本
.\build.ps1
```

### 打包发布

```powershell
# 生成带时间戳的 zip 和 latest 别名
powershell -ExecutionPolicy Bypass -File tools/package.ps1 -Aot -LatestAlias
```

### 运行测试

```powershell
dotnet test
```

## 📝 版本历史

### v1.0.0 (2025-10-31) - 首个正式版本
- 🎯 一键操作：packandinstall 完成打包+签名+安装
- 📦 智能配置：自动保存路径和证书，支持回退
- ✍️ 证书管理：简化参数，位置参数支持
- 🔄 自动升级：安装前自动卸载旧版本
- 🖼️ 图标刷新：卸载后自动清理图标缓存
- � Native AOT：14.8MB 单文件，无需 .NET 运行时
- ✨ 参数校验：cert+password 联合校验，提升安全性

查看完整 [更新日志](更新日志.md)

---

**注**: v2.x 版本为内部开发版本，已归档。v1.0.0 为首个正式发布版本。

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

[MIT License](LICENSE)

## 👨‍💻 作者

**luzehua** - luzehua0110@foxmail.com

## 🔗 相关资源

- [MSIX 官方文档](https://docs.microsoft.com/windows/msix/)
- [Windows 应用 SDK](https://docs.microsoft.com/windows/apps/windows-app-sdk/)
- [MSIX 打包工具](https://docs.microsoft.com/windows/msix/packaging-tool/tool-overview)

---

⭐ 如果这个项目对您有帮助，请给个 Star！
