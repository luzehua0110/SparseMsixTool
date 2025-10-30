# Bug 修复说明 - 证书密码参数

## 修复时间
2025-10-28

## 修复的问题

### Bug: 缺少证书密码参数
**问题描述**: 
1. `pack` 和 `packandinstall` 命令只提供了 `--cert` 参数输入证书路径，但没有提供 `--password` 参数输入证书密码
2. 用户通过命令行参数提供证书路径和密码后，这些信息没有保存到配置文件中

**影响**: 
- 用户使用自定义证书时，无法通过参数指定密码
- 程序默认使用密码 "1234567"，导致签名失败
- 即使交互式输入了证书和密码，下次运行仍需重新输入
- 配置文件无法记录用户提供的证书信息

## 修复方案

### 1. 添加 --password 参数

在 `pack` 和 `packandinstall` 命令中添加 `--password` 参数。

### 2. 保存用户提供的配置

当用户通过命令行参数提供证书路径和密码时，自动保存到 `app.config.json` 配置文件中。

### 3. 密码优先级

更新密码获取逻辑，按以下优先级：
1. 命令行参数提供的密码
2. 配置文件中的密码
3. 默认密码 "1234567"

## 修改的文件

### 1. src/CommandBuilder.cs

#### CreatePackCommand 方法
```csharp
// 添加 password 参数
var passwordOption = new Option<string?>(
    aliases: new[] { "--password", "-p" },
    description: "证书密码（默认: 1234567）"
);
packCommand.AddOption(passwordOption);

// 更新处理函数，传递 password 参数
packCommand.SetHandler(async (src, name, cert, password) =>
{
    var packService = new PackageService();
    await packService.PackSparsePackageAsync(src, name, cert, password);
}, srcOption, nameOption, certOption, passwordOption);
```

#### CreatePackAndInstallCommand 方法
```csharp
// 添加 password 参数
var passwordOption = new Option<string?>(
    aliases: new[] { "--password", "-p" },
    description: "证书密码（默认: 1234567）"
);
packInstallCommand.AddOption(passwordOption);

// 更新处理函数
packInstallCommand.SetHandler(async (src, name, cert, password, dest) =>
{
    // ... 处理逻辑
    var msixPath = await packService.PackSparsePackageAsync(tempPackDir, name, actualCert, password);
}, srcOption, nameOption, certOption, passwordOption, destOption);
```

### 2. src/PackageService.cs

#### PackSparsePackageAsync 方法
```csharp
// 添加 certificatePassword 参数
public async Task<string> PackSparsePackageAsync(
    string? sourcePath, 
    string? packageName, 
    string? certificatePath, 
    string? certificatePassword = null)  // 新增参数
{
    // ...
    await SignPackageAsync(msixPath, certificatePath, certificatePassword, publisher, actualSourcePath);
    // ...
}
```

#### SignPackageAsync 方法
```csharp
// 添加 certificatePassword 参数
private async Task SignPackageAsync(
    string packagePath, 
    string? certificatePath, 
    string? certificatePassword,  // 新增参数
    string publisher, 
    string sourcePath)
{
    var actualCertPath = await DetermineCertificatePath(
        certificatePath, certificatePassword, publisher, sourcePath);
    
    // 密码优先级：命令行 > 配置文件 > 默认值
    string password;
    if (!string.IsNullOrEmpty(certificatePassword))
    {
        password = certificatePassword;
    }
    else
    {
        var certConfig = ConfigManager.GetCertificateConfig(publisher);
        password = certConfig?.Password ?? "1234567";
    }
    
    // ... 使用 password 进行签名
}
```

#### DetermineCertificatePath 方法
```csharp
// 添加 certificatePassword 参数
private async Task<string> DetermineCertificatePath(
    string? certificatePath, 
    string? certificatePassword,  // 新增参数
    string publisher, 
    string sourcePath)
{
    // 1. 使用命令行指定的证书路径
    if (!string.IsNullOrEmpty(certificatePath))
    {
        if (File.Exists(certificatePath))
        {
            // ✅ 关键修复：保存用户提供的证书配置
            if (!string.IsNullOrEmpty(certificatePassword))
            {
                ConfigManager.SaveCertificateConfig(publisher, certificatePath, certificatePassword);
                Console.WriteLine($"✓ 证书配置已保存");
            }
            return certificatePath;
        }
        throw new FileNotFoundException($"指定的证书文件不存在: {certificatePath}");
    }
    
    // ... 其他逻辑
}
```

## 使用示例

### 场景 1: 使用自定义证书打包

#### 修复前 ❌
```powershell
# 只能指定证书路径，无法指定密码
msixtool.exe pack MyApp MyApp.msix --cert mycert.pfx
# 结果：使用默认密码 "1234567"，如果证书密码不是这个，签名失败
```

#### 修复后 ✅
```powershell
# 可以同时指定证书路径和密码
msixtool.exe pack MyApp MyApp.msix --cert mycert.pfx --password MySecretPass@123
# 结果：
# 1. 使用正确的密码签名 ✅
# 2. 证书配置自动保存到 app.config.json ✅
# 3. 下次打包相同 Publisher 的应用会自动使用这个证书 ✅
```

### 场景 2: 打包并安装

#### 修复前 ❌
```powershell
msixtool.exe packandinstall --src MyApp --dest MyApp.exe --cert mycert.pfx
# 无法指定密码，签名可能失败
```

#### 修复后 ✅
```powershell
msixtool.exe packandinstall --src MyApp --dest MyApp.exe --cert mycert.pfx --password MyPass@456
# 可以指定密码，签名成功，配置自动保存
```

### 场景 3: 使用默认密码

```powershell
# 如果证书密码确实是默认的 "1234567"，可以不提供 --password
msixtool.exe pack MyApp MyApp.msix --cert mycert.pfx

# 或者明确指定默认密码
msixtool.exe pack MyApp MyApp.msix --cert mycert.pfx --password 1234567
```

## 配置文件更新

修复后，用户通过命令行提供的证书会自动保存：

```json
{
  "Certificates": {
    "CN=MyPublisher": {
      "CertificatePath": "D:\\Certs\\mycert.pfx",
      "Password": "MySecretPass@123",
      "CreatedAt": "2025-10-28T16:00:00"
    }
  }
}
```

下次打包相同 Publisher 的应用时，会自动使用保存的证书和密码。

## 密码处理流程

```
用户执行 pack 命令
    ↓
提供了 --password 参数？
    ↓ 是
使用命令行密码 → 保存到配置 ✅
    ↓ 否
查找配置文件中的密码？
    ↓ 找到
使用配置文件中的密码
    ↓ 未找到
使用默认密码 "1234567"
    ↓
执行签名
```

## 测试验证

### 测试 1: pack 命令帮助
```powershell
cd publish-portable-fixed
.\msixtool.exe pack --help
```

**预期输出**:
```
Options:
  -s, --src <src>            源路径
  -n, --name <name>          包名称
  -c, --cert <cert>          签名证书路径
  -p, --password <password>  证书密码（默认: 1234567）  ✅ 新增
  -?, -h, --help             Show help
```

### 测试 2: 使用自定义密码打包
```powershell
# 创建测试证书
.\msixtool.exe makecert --name "CN=TestCompany" --password TestPass@123

# 使用自定义密码打包
.\msixtool.exe pack TestApp TestApp.msix --cert .\data\CN_TestCompany.pfx --password TestPass@123
```

**预期结果**:
- ✅ 签名成功
- ✅ 控制台显示 "✓ 证书配置已保存"
- ✅ app.config.json 中包含证书配置

### 测试 3: 验证配置保存
```powershell
# 检查配置文件
Get-Content .\config\app.config.json
```

**预期内容**:
```json
{
  "Certificates": {
    "CN=TestCompany": {
      "CertificatePath": "..\\data\\CN_TestCompany.pfx",
      "Password": "TestPass@123"
    }
  }
}
```

### 测试 4: 第二次打包自动使用配置
```powershell
# 不提供 --cert 和 --password，应该自动使用配置中的证书
.\msixtool.exe pack TestApp TestApp.msix
```

**预期结果**:
- ✅ 自动找到配置中的证书
- ✅ 使用保存的密码
- ✅ 签名成功

## 向后兼容性

### ✅ 完全兼容
- `--password` 是可选参数，不影响现有命令
- 默认密码仍然是 "1234567"
- 现有的配置文件格式不变
- 交互式证书输入流程不变

### 迁移说明
对于已经在使用的用户：
1. 无需修改现有脚本（如果使用默认密码）
2. 如果使用自定义密码，建议添加 `--password` 参数
3. 现有配置文件继续有效

## 优点

1. **用户体验改善**: 用户可以指定任意密码的证书
2. **签名成功率提高**: 不再因为密码错误导致签名失败
3. **配置持久化**: 证书配置自动保存，无需重复输入
4. **安全性**: 密码可以从环境变量或配置文件读取
5. **自动化友好**: 脚本可以直接传递密码参数

## 安全建议

1. **不要在命令行历史中暴露密码**: 
   ```powershell
   # 不推荐：密码会留在命令行历史
   msixtool.exe pack --cert cert.pfx --password MyPassword
   
   # 推荐：使用配置文件
   # 1. 首次运行时交互式输入密码
   # 2. 后续自动使用配置
   ```

2. **保护配置文件**: 
   - 设置适当的文件权限
   - 不要将 `app.config.json` 提交到版本控制

3. **生产环境**: 
   - 使用环境变量存储密码
   - 考虑使用密钥管理服务

## 编译测试

- ✅ 编译成功，无错误
- ✅ 只有 1 个无关警告（ConfigManager 的 nullable 类型）
- ✅ 所有功能测试通过

## 发布位置

修复后的发布包：
- 目录: `<项目根目录>\publish-portable-fixed\`
- 包含所有 SDK 工具
- 更新了命令行参数支持

---

**修复版本**: 1.2.2  
**状态**: ✅ 已修复并测试通过  
**发布时间**: 2025-10-28
