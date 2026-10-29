# SparseMsixTool 发布版本（原 MsixTool）

本目录用于记录 SparseMsixTool（原 MsixTool，保留临时别名 msixtool）的版本发布信息。
编译产物（exe/zip）不直接入库，请从 GitHub Releases 页面下载。

## 版本记录

### v1.0.0 (2025-10-31) - 首个正式版本（更名后）

**主要特性**:
- 🎯 **一键操作**: packandinstall 完成打包+签名+安装全流程
- 📦 **智能配置**: 自动保存路径和证书信息，支持历史配置回退
- ✍️ **证书管理**: makecert/sign 支持位置参数，简化命令行
- 🔄 **自动升级**: 安装前自动卸载同名旧版本包
- 🖼️ **图标刷新**: 卸载后自动清理图标缓存，避免残留
- 💿 **Native AOT**: 单文件，无需 .NET 运行时
- ✨ **参数校验**: cert+password 联合校验，提升安全性

**系统要求**:
- Windows 10 1809+ / Windows 11
- Windows SDK（makeappx.exe, signtool.exe）

**使用方法**:
1. 从 GitHub Releases 下载 ZIP 并解压到任意目录
2. 将解压目录添加到系统 PATH 环境变量（可选）
3. 运行 `sparsemsix --help` 查看帮助（旧别名：`msixtool --help`）

**更新日志**: 查看根目录的 `更新日志.md`

---

**注**: v2.x 版本为内部开发版本，已归档。v1.0.0 为首个正式发布版本。

---

## 命名规范

发布包命名格式：`SparseMsixTool-v{版本号}-{编译日期}.zip`

---

## 下载链接

- **GitHub Releases**: https://github.com/luzehua0110/SparseMsixTool/releases

---

## 验证文件

下载后建议验证文件完整性：

```powershell
# 查看文件大小
Get-Item SparseMsixTool-v*.zip | Select-Object Name, Length

# 计算 SHA256 哈希（可选）
Get-FileHash SparseMsixTool-v*.zip -Algorithm SHA256
```

---

## 支持

如有问题或建议，请：
- 提交 Issue: https://github.com/luzehua0110/SparseMsixTool/issues
- 查看文档: 项目根目录的 `README.md`、`快速入门.md`、`命令参考.md`
