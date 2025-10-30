# MsixTool 发布版本

此目录包含 MsixTool 的正式发布版本。

## 最新版本

### v2.1.0 (2025-10-30)

**文件名**: `MsixTool-v2.1.0-20251030.zip`

**主要特性**:
- ✅ 稀疏包安装前自动卸载同名包
- ✅ 外部程序路径自动保存和回退
- ✅ 图标缓存刷新优化（卸载后自动清理）
- ✅ 命令参数优化（位置参数、cert+password校验）
- ✅ Native AOT 编译（~14.8MB，无需 .NET 运行时）

**系统要求**:
- Windows 10/11
- Windows SDK（makeappx.exe, signtool.exe）

**使用方法**:
1. 解压 ZIP 文件到任意目录
2. 将解压目录添加到系统 PATH 环境变量（可选）
3. 运行 `msixtool --help` 查看帮助

**更新日志**: 查看根目录的 `更新日志.md`

---

## 命名规范

发布包命名格式：`MsixTool-v{版本号}-{编译日期}.zip`

示例：
- `MsixTool-v2.1.0-20251030.zip` - 版本 2.1.0，编译于 2025年10月30日
- `MsixTool-v2.0.0-20251025.zip` - 版本 2.0.0，编译于 2025年10月25日

---

## 下载链接

- **GitHub Releases**: https://github.com/luzehua0110/msixtool/releases
- **直接下载**: 从此目录获取最新 ZIP 文件

---

## 验证文件

下载后建议验证文件完整性：

```powershell
# 查看文件大小
Get-Item MsixTool-v2.1.0-20251030.zip | Select-Object Name, Length

# 计算 SHA256 哈希（可选）
Get-FileHash MsixTool-v2.1.0-20251030.zip -Algorithm SHA256
```

---

## 支持

如有问题或建议，请：
- 提交 Issue: https://github.com/luzehua0110/msixtool/issues
- 查看文档: 项目根目录的 `README.md`、`快速入门.md`、`命令参考.md`
