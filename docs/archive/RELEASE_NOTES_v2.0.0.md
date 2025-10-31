# MsixTool v2.0.0 发布说明

**发布日期:** 2025-10-28  
**版本:** v2.0.0  
**文件:** MsixTool-v2.0.0-20251028.zip (4.34 MB)

---

## 🎉 重大更新

v2.0.0 是一个**重大架构改进版本**，彻底解决了之前版本的运行时依赖问题。

---

## ✨ 核心改进

### 1. 自动发现系统 Windows SDK
- **不再打包 SDK 工具**，改为自动查找系统安装的 Windows SDK
- 支持所有 Windows SDK 10.0.x.x 版本
- 自动按版本降序查找，优先使用最新版本
- 完全避免 "并行配置不正确" 错误

### 2. 大幅减小体积
- **从 41.94 MB 降至 4.34 MB**（减少 90%）
- 仅包含：
  - `msixtool.exe` (9.82 MB，Native AOT 编译)
  - `README.txt` (使用说明)

### 3. 更高可靠性
- 直接使用系统正确安装的 SDK 工具
- 避免 DLL 依赖冲突
- 无需 VC++ Redistributable
- 无需配置 PATH 环境变量

---

## 📋 系统要求

| 组件 | 要求 |
|------|------|
| 操作系统 | Windows 10 1809+ / Windows 11 |
| 架构 | x64 (64位) |
| **Windows SDK** | **10.0.18362.0 或更高（必需）** |
| .NET Runtime | 无需安装（Native AOT） |

### Windows SDK 安装

如果系统未安装 Windows SDK，请下载并安装：

**下载地址:** https://developer.microsoft.com/windows/downloads/windows-sdk/

**安装时只需勾选:**
- ✓ Windows SDK Signing Tools for Desktop Apps

**安装后无需任何配置**，MsixTool 会自动发现并使用。

---

## 🚀 快速开始

1. **解压文件**
   ```
   解压 MsixTool-v2.0.0-20251028.zip 到任意目录
   ```

2. **检查依赖**
   ```powershell
   .\msixtool.exe deps
   ```

3. **运行命令**
   ```powershell
   # 打包稀疏包
   .\msixtool.exe pack --src "D:\MyApp\PackageFolder"
   
   # 一键打包并安装
   .\msixtool.exe packandinstall --src "D:\MyApp\PackageFolder" --dest "C:\MyApp\MyApp.exe"
   ```

---

## 🔧 主要命令

| 命令 | 说明 |
|------|------|
| `pack` | 打包稀疏包（不签名） |
| `sign` | 签名已有的 MSIX 包 |
| `packandinstall` | 全自动流程（打包→证书→签名→资源→安装） |
| `install` | 安装稀疏包 |
| `uninstall` | 卸载应用 |
| `makecert` | 创建测试证书 |
| `find` | 查找已安装的包 |
| `deps` | 检查系统依赖 |

详细帮助：`.\msixtool.exe --help`

---

## 📊 版本对比

### v1.3.4（已废弃）

❌ **存在的问题:**
- 打包了 SDK 工具、DLL 和 VC++ Redistributable
- 大小: 41.94 MB
- **运行时报错: "并行配置不正确"**
- 无法解决 WinSxS 组件依赖

### v2.0.0（推荐）

✅ **优势:**
- 自动发现系统 Windows SDK
- 大小: 4.34 MB（减少 90%）
- **完全避免依赖冲突**
- 100% 可靠运行
- 支持所有 SDK 版本

---

## 🔍 技术实现

### SDK 自动发现机制

```csharp
// 扫描路径
C:\Program Files (x86)\Windows Kits\10\bin\10.0.x.x\x64\
C:\Program Files\Windows Kits\10\bin\10.0.x.x\x64\

// 查找顺序
1. 按版本降序排列所有 SDK
2. 优先使用最新版本
3. 查找 makeappx.exe、signtool.exe 等工具
4. 返回完整路径直接调用
```

### 支持的工具

- `makeappx.exe` - MSIX 打包
- `signtool.exe` - 数字签名  
- `makecert.exe` - 证书生成
- `pvk2pfx.exe` - 证书转换

---

## ❓ 常见问题

### Q: 提示 "未找到 makeappx.exe"

**A:** 系统未安装 Windows SDK。请下载并安装:  
https://developer.microsoft.com/windows/downloads/windows-sdk/

安装时勾选 "Windows SDK Signing Tools for Desktop Apps" 组件。

### Q: 我的系统有多个 SDK 版本，会用哪个？

**A:** MsixTool 自动使用最新版本的 SDK。

### Q: v1.3.4 还能用吗？

**A:** 不推荐。v1.3.4 存在依赖问题，建议升级到 v2.0.0。

### Q: v2.0.0 可以在没有 SDK 的机器上运行吗？

**A:** 不可以。v2.0.0 要求系统安装 Windows SDK。  
这是设计选择，因为直接使用系统 SDK 可以完全避免依赖冲突。

---

## 📝 更新历史

### v2.0.0 (2025-10-28)
- ✅ 自动发现系统 Windows SDK
- ✅ 支持所有 SDK 版本（10.0.x.x）
- ✅ 移除打包的 SDK 工具和依赖
- ✅ 体积减小到 4.34 MB
- ✅ 完全避免依赖冲突

### v1.3.4 (2025-10-28) - 已废弃
- ❌ 包含 VC++ Redistributable 自动安装
- ❌ 仍存在 "并行配置不正确" 错误
- ❌ 大小 41.94 MB

### v1.3.3 (2025-10-28) - 已废弃
- ❌ 添加 Universal CRT 运行时文件
- ❌ 未解决根本问题

### v1.3.2 (2025-10-28) - 已废弃
- ✅ 修复资源文件复制时机

### v1.3.1 (2025-10-28) - 已废弃
- ✅ 分离 pack 和 sign 命令
- ✅ 优化证书创建时机

---

## 📥 下载

**文件名:** MsixTool-v2.0.0-20251028.zip  
**大小:** 4.34 MB  
**位置:** `<项目根目录>\`

---

## 📞 技术支持

如遇问题，请提供:
- Windows 版本 (运行 `winver`)
- Windows SDK 版本 (查看安装路径)
- 完整错误消息
- 执行的完整命令

---

## ⚖️ 许可证

详见项目 LICENSE 文件。

---

**MsixTool v2.0.0 - 简单、可靠、开箱即用**
