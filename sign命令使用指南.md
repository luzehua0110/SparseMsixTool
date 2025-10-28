# sign 命令使用指南

## 命令格式

```powershell
.\msixtool.exe sign --src <MSIX文件路径> [--cert <证书路径>] [--password <密码>]
```

## 参数说明

| 参数 | 别名 | 必需 | 说明 |
|------|------|------|------|
| `--src` | `-s` | ✓ | 要签名的 MSIX 包文件路径 |
| `--cert` | `-c` | ✗ | 签名证书路径（.pfx 文件）<br>如果不提供，工具会自动查找或创建证书 |
| `--password` | `-p` | ✗ | 证书密码<br>默认: 1234567 |

## 使用示例

### 1. 使用指定证书签名

```powershell
.\msixtool.exe sign --src "C:\MyApp\MyApp.msix" --cert "C:\Certs\mycert.pfx" --password "mypassword"
```

**注意事项：**
- ✅ 使用**双引号**包裹路径和密码
- ❌ 不要使用单引号（PowerShell 会原样传递单引号）

### 2. 使用短参数

```powershell
.\msixtool.exe sign -s "C:\MyApp\MyApp.msix" -c "C:\Certs\mycert.pfx" -p "mypassword"
```

### 3. 自动查找或创建证书

如果不提供 `--cert` 参数，工具会：
1. 解包 MSIX 读取发布者信息
2. 检查是否已为该发布者创建过证书
3. 如果没有，自动创建测试证书并安装

```powershell
.\msixtool.exe sign --src "C:\MyApp\MyApp.msix"
```

## 工作流程

```
1. 检查 MSIX 文件是否存在
   ↓
2. 尝试直接读取 MSIX Zip 内的 AppxManifest.xml （支持稀疏包）
   ↓
   若直接读取失败 → 回退使用 makeappx unpack（仅适用于完整包）
   ↓
3. 从清单获取发布者 (Publisher) 信息
   ↓
4. 确定证书路径
   - 如果指定了 --cert，使用指定证书
   - 否则查找配置文件中的证书
   - 如果没有，自动创建测试证书
   ↓
5. 安装证书到系统受信任根存储（如需要）
   ↓
6. 使用 signtool.exe 签名
   ↓
7. 清理临时文件
```

## 图标缓存刷新机制

安装或卸载（包括稀疏包安装/注册）完成后，工具会自动执行轻量图标缓存刷新，从而避免开始菜单或任务栏仍显示旧图标。

执行步骤：
- 调用 `ie4uinit.exe -show` 触发图标更新
- 发送 `SHChangeNotify` 通知刷新 shell 关联
- 如需更彻底清理，可在未来版本加入 `--aggressive-icon-refresh` 选项调用 `ie4uinit.exe -clear`

无需额外操作，默认即可避免残留图标。

## 常见问题

### Q: 提示 "无法读取包信息"

**可能原因：**
1. MSIX 文件路径不正确或不存在
2. MSIX 文件已损坏
3. makeappx.exe 解包失败

**解决方法：**
```powershell
# 1. 检查文件是否存在
Test-Path "C:\MyApp\MyApp.msix"

# 2. 使用完整的更新版本（包含详细错误信息）
.\msixtool.exe sign -s "C:\MyApp\MyApp.msix" -c "C:\Certs\cert.pfx" -p "1234567"

# 3. 查看详细的解包命令输出
```

新版本会显示：
```
执行解包命令: makeappx.exe unpack /p "C:\MyApp\MyApp.msix" /d "临时目录" /o
```

### Q: 提示 "未找到要签名的包文件"

**原因：** 文件路径不正确

**解决：** 使用绝对路径或正确的相对路径
```powershell
# 正确 ✓
.\msixtool.exe sign -s "C:\Users\用户名\Downloads\MyApp.msix"

# 错误 ✗ （拼写错误或路径不存在）
.\msixtool.exe sign -s "C:\Users\用户名\Downlaods\MyApp.msix"
```

### Q: PowerShell 单引号问题

**错误用法：**
```powershell
# ❌ 单引号会被原样传递，密码变成 '1234567' （包括引号）
.\msixtool.exe sign -s "file.msix" -p '1234567'
```

**正确用法：**
```powershell
# ✅ 使用双引号
.\msixtool.exe sign -s "file.msix" -p "1234567"

# ✅ 或者不使用引号（如果密码不包含空格）
.\msixtool.exe sign -s "file.msix" -p 1234567
```

### Q: 需要管理员权限吗？

**签名本身不需要管理员权限**，但以下操作需要：
- ✓ 签名 MSIX 文件 - **不需要**
- ✗ 安装证书到受信任根存储 - **需要**

如果工具需要安装证书，会弹出 UAC 提示请求管理员权限。

### Q: 如何验证签名是否成功？

签名成功后，会显示：
```
✓ 签名成功
  证书: C:\path\to\cert.pfx
  时间戳: http://timestamp.digicert.com
```

可以使用 Windows 资源管理器验证：
1. 右键点击 .msix 文件
2. 选择 "属性"
3. 切换到 "数字签名" 选项卡
4. 应该能看到签名信息

## 与 pack 命令的区别

| 特性 | pack | sign |
|------|------|------|
| 功能 | 打包稀疏包 | 签名已有包 |
| 输入 | 包含清单的目录 | .msix 文件 |
| 输出 | .msix 文件（未签名） | .msix 文件（已签名） |
| 是否签名 | ✗ | ✓ |

**工作流程：**
```
pack 命令:  目录 → .msix (未签名)
sign 命令:  .msix (未签名) → .msix (已签名)
```

**一步完成（推荐）：**
```powershell
# 使用 packandinstall 一次完成打包、签名、安装
.\msixtool.exe packandinstall --src "D:\MyApp\Package" --dest "C:\MyApp\MyApp.exe"
```

## 技术细节

### 使用的工具

- **makeappx.exe** - 解包 MSIX 读取清单
- **signtool.exe** - 数字签名

### 时间戳服务器

签名时会使用时间戳服务器：
- 默认: http://timestamp.digicert.com
- 作用: 即使证书过期，签名仍然有效

### 证书存储

工具自动创建的证书保存在：
```
<程序目录>\data\<Publisher>\certificate.pfx
```

配置信息保存在：
```
<程序目录>\config\app.config.json
```

## 完整示例

```powershell
# 场景：为已打包的 MSIX 文件签名

# 步骤 1: 准备 MSIX 文件（从 pack 命令生成或已有）
$msixFile = "C:\Users\<用户名>\Downloads\PhotoStoreDemo\PhotoStoreDemo_1.0.0.1.msix"

# 步骤 2: 准备证书（可选）
$certFile = "C:\TempSpace\HelloSparsePackage\Certificate\HelloSparsePackage-SelfSignedCertificate-001.pfx"
$certPassword = "1234567"

# 步骤 3: 执行签名
.\msixtool.exe sign --src $msixFile --cert $certFile --password $certPassword

# 或者简化版（自动查找/创建证书）
.\msixtool.exe sign --src $msixFile
```

## 相关命令

- `pack` - 打包稀疏包（不签名）
- `packandinstall` - 打包、签名、安装一体化
- `makecert` - 创建测试证书
- `install` - 安装 MSIX 包

查看命令帮助：
```powershell
.\msixtool.exe sign --help
```
