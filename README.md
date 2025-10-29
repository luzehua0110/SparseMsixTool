# MsixTool - MSIX 稀疏包工具

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)

MsixTool 是一个强大的命令行工具,用于创建、签名和安装 MSIX 稀疏包。支持自签名证书管理、自动依赖检查、配置记忆等功能,让 MSIX 包开发变得简单高效。

## ✨ 核心特性

- 🎯 **一键打包**: 快速创建 MSIX 稀疏包
- ✍️ **证书管理**: 自动生成和管理自签名证书
- 📦 **智能安装**: 自动检测和安装依赖,支持自动卸载旧版本
- 🔍 **包查询**: 快速查找和管理已安装的 MSIX 包
- 💾 **配置记忆**: 自动保存常用路径,提升工作效率
- 🎨 **彩色输出**: 红色错误、黄色警告,信息一目了然
- 💿 **单文件发布**: Native AOT 编译,无需 .NET 运行时

## 📥 快速开始

### 安装

从 [Releases](https://github.com/luzehua0110/msixtool/releases) 下载最新版本,解压即用。

### 基本使用

```powershell
# 创建 MSIX 包
msixtool pack --source "C:\MyApp" --manifest "AppxManifest.xml"

# 创建自签名证书
msixtool makecert --publisher "CN=MyCompany" --password "MyPassword123"

# 签名 MSIX 包
msixtool sign --package "MyApp.msix" --cert "MyCert.pfx" --password "MyPassword123"

# 一键打包并安装
msixtool packandinstall --source "C:\MyApp" --manifest "AppxManifest.xml"

# 查找已安装的包
msixtool find --name "MyApp"

# 卸载包
msixtool uninstall --name "MyApp_1.0.0.0_x64__8wekyb3d8bbwe"
```

## 📋 命令列表

| 命令 | 说明 | 示例 |
|------|------|------|
| `pack` | 创建 MSIX 包 | `msixtool pack --source . --manifest manifest.xml` |
| `sign` | 签名 MSIX 包 | `msixtool sign --package app.msix --cert cert.pfx` |
| `install` | 安装 MSIX 包 | `msixtool install --package app.msix` |
| `uninstall` | 卸载已安装的包 | `msixtool uninstall --name PackageName` |
| `makecert` | 创建自签名证书 | `msixtool makecert --publisher "CN=Company"` |
| `packandinstall` | 打包并安装 | `msixtool packandinstall --source . --manifest manifest.xml` |
| `--no-auto-uninstall`(选项) | 禁用自动卸载旧版本 | `msixtool install --src app.msix --no-auto-uninstall` |
| `packandsign` | 打包并签名 | `msixtool packandsign --source . --manifest manifest.xml` |
| `find` | 查找已安装的包 | `msixtool find --name MyApp` |
| `findcert` | 查找证书 | `msixtool findcert --subject "CN=Company"` |
| `delcert` | 删除证书 | `msixtool delcert --thumbprint ABC123...` |
| `deps` | 检查依赖 | `msixtool deps` |

## 🔧 系统要求

- **操作系统**: Windows 10/11 (1809 或更高版本)
- **依赖**: Windows SDK (自动检测并提示安装)
- **架构**: x64

## 📖 详细文档

- [快速入门](快速入门.md) - 5分钟上手指南
- [用户使用手册](用户使用手册.md) - 完整功能说明
- [开发者文档](开发者文档.md) - 开发和构建指南
- [命令参考](命令参考.md) - 所有命令的详细参数
- [故障排查指南](故障排查指南.md) - 常见问题解决方案
- [文档索引](文档索引.md) - 完整文档列表

## 🎯 使用场景

### 场景 1: 开发测试
快速打包应用进行本地测试,无需发布到应用商店。

```powershell
msixtool packandinstall --source "C:\MyApp\bin\Release" --manifest "AppxManifest.xml"
```

### 场景 2: 企业内部分发
创建签名的 MSIX 包,通过内网分发给员工。

```powershell
# 1. 创建企业证书
msixtool makecert --publisher "CN=MyCompany" --password "SecurePass123"

# 2. 打包并签名
msixtool packandsign --source "C:\MyApp" --manifest "manifest.xml" --cert "cert.pfx" --password "SecurePass123"
```

### 场景 3: 版本升级
自动卸载旧版本并安装新版本。

```powershell
msixtool packandinstall --source "C:\MyApp" --manifest "AppxManifest.xml"
# 自动检测并卸载旧版本,安装新版本
```

## 🚀 高级功能

### 配置记忆
工具会自动记住最后使用的路径,下次使用时可以省略参数:

```powershell
# 第一次使用
msixtool pack --source "C:\MyApp" --manifest "AppxManifest.xml"

# 之后可以直接使用
msixtool pack
```

### 自动资源复制
打包时自动包含 `Assets` 目录下的资源文件,无需手动复制。

### 文件备份
在覆盖现有 MSIX 文件前自动创建带时间戳的备份:
```
MyApp_1.0.0.0.msix -> MyApp_1.0.0.0_2025-10-28_18-30-00.msix
```

### 自动卸载与禁用
默认行为是安装前自动卸载同名已安装包，避免“注册失败”或版本冲突。
如需保留旧版本以便对比，可使用 `--no-auto-uninstall`：
```powershell
msixtool install --src MyApp_1.0.0.0.msix --no-auto-uninstall
```

在一键打包安装命令中也可使用：
```powershell
msixtool packandinstall --src . --dest C:\MyApp\MyApp.exe --no-auto-uninstall
```

## 🛠️ 构建

```powershell
# 克隆仓库
git clone https://github.com/luzehua0110/msixtool.git
cd msixtool

# 编译 (Debug)
dotnet build -c Debug

# 发布 (Release with Native AOT)
dotnet publish -c Release -r win-x64 --self-contained

# 或使用提供的脚本
.\build.ps1
```

## ✅ 测试

项目包含基础单元测试，覆盖以下功能：
- 备份时间戳格式 (BackupConstants)
- 清单解析 (ManifestHelper)
- 资源引用与 Assets 自动包含 (ResourceHelper)

运行测试：
```powershell
dotnet test
```

## 🤖 CI 持续集成

已配置 GitHub Actions (`.github/workflows/ci.yml`)：
- Push / PR 到 main 分支自动触发
- 步骤：Checkout -> Restore -> Build -> Test -> Publish (Native AOT) -> 上传构建产物

你可以在 GitHub 的 Actions 选项卡查看执行状态与历史记录。

## 📝 版本历史

### v2.1.4 (2025-10-28)
- ✨ 新增文件备份功能,覆盖前自动创建时间戳备份
- 🎨 彩色控制台输出,错误红色、警告黄色
- 🐛 Bug 修复和性能优化

查看完整 [更新日志](更新日志.md)

## 🤝 贡献

欢迎提交 Issue 和 Pull Request!

## 📄 许可证

本项目采用 [MIT 许可证](LICENSE)。

## 👨‍💻 作者

**luzehua** - [luzehua0110@foxmail.com](mailto:luzehua0110@foxmail.com)

## 🔗 相关链接

- [MSIX 官方文档](https://docs.microsoft.com/zh-cn/windows/msix/)
- [Windows 应用 SDK](https://docs.microsoft.com/zh-cn/windows/apps/windows-app-sdk/)
- [MSIX 打包工具](https://docs.microsoft.com/zh-cn/windows/msix/packaging-tool/tool-overview)

---

⭐ 如果这个项目对您有帮助,请给个 Star!
