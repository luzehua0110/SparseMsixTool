# Bug 修复说明

## 修复时间
2025-10-28

## 修复的问题

### Bug 1: 证书污染源目录
**问题描述**: 当没有提供证书路径，自动生成证书时，证书被放到了清单文件所在的目录，污染了源代码目录。

**影响**: 
- 开发环境中会在应用源码目录生成 .pfx 证书文件
- 可能被误提交到版本控制系统
- 多个应用在同一目录下会混淆证书文件

**修复方案**: 
将自动生成的证书统一保存到程序运行目录下的 `data` 目录：
```
<程序运行目录>/data/<Publisher>.pfx
```

### Bug 2: 配置文件未更新
**问题描述**: 自动生成证书并设置信任之后，`app.config.json` 配置文件中没有增加对应的证书配置项。

**影响**:
- 下次运行时无法自动找到证书
- 需要重新输入证书路径和密码
- 无法实现证书配置的持久化

**修复方案**:
在自动生成证书后，立即调用 `ConfigManager.SaveCertificateConfig()` 保存配置。

## 修改的文件

### 1. src/CertificateService.cs

#### 修改 CreateCertificateForPublisherAsync 方法
```csharp
// 修改前：接收 string outputDirectory
public async Task<string> CreateCertificateForPublisherAsync(
    string publisher, 
    string outputDirectory,  // 必需参数
    string password = "1234567")

// 修改后：可选的 outputDirectory，默认使用 data 目录
public async Task<string> CreateCertificateForPublisherAsync(
    string publisher, 
    string? outputDirectory,  // 可选参数
    string password = "1234567")
{
    // 如果没有指定输出目录，使用程序运行目录下的data目录
    string actualOutputDir;
    if (string.IsNullOrEmpty(outputDirectory))
    {
        var appDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        actualOutputDir = Path.Combine(appDir, "data");
    }
    else
    {
        actualOutputDir = outputDirectory;
    }
    
    // 确保目录存在
    if (!Directory.Exists(actualOutputDir))
    {
        Directory.CreateDirectory(actualOutputDir);
        Console.WriteLine($"创建证书目录: {actualOutputDir}");
    }
    
    // ... 继续处理
}
```

#### 修改 CreateCertificateAsync 方法
```csharp
// 不再使用清单文件所在目录或当前目录
// 统一使用 null，让 CreateCertificateForPublisherAsync 处理
string? outputPath = null;  // 使用默认的 data 目录

// 添加配置保存提示
ConfigManager.SaveCertificateConfig(publisher, certPath, certPassword);
Console.WriteLine($"✓ 证书配置已保存到配置文件");
```

#### 修改 GetOrCreateCertificateAsync 方法
```csharp
// 修改签名，outputDirectory 变为可选
public async Task<string> GetOrCreateCertificateAsync(
    string publisher, 
    string? outputDirectory = null,  // 可选参数
    string? password = null)

// 添加配置保存逻辑
var createdCert = await CreateCertificateForPublisherAsync(publisher, actualOutputDir, certPassword);
Console.WriteLine($"✓ 已自动创建并安装证书: {createdCert}");

// 保存到配置
ConfigManager.SaveCertificateConfig(publisher, createdCert, certPassword);
Console.WriteLine($"✓ 证书配置已保存到配置文件");
```

### 2. src/PackageService.cs

#### 修改 DetermineCertificatePath 方法
```csharp
// 在自动生成证书时，不传递 sourcePath
var certService = new CertificateService();
// 传递 null 让证书保存到程序运行目录的data目录
var generatedCertPath = await certService.CreateCertificateForPublisherAsync(
    publisher, 
    null,      // 不传递 sourcePath
    password);

// 确保配置已保存
ConfigManager.SaveCertificateConfig(publisher, generatedCertPath, password);
Console.WriteLine($"✓ 证书配置已保存到配置文件");
```

## 测试验证

### 场景 1: 使用 makecert 命令创建证书
```powershell
cd <项目根目录>\publish-portable-fixed
.\msixtool.exe makecert --name "CN=TestPublisher" --password test123
```

**预期结果**:
- 证书保存在: `<程序目录>\data\CN_TestPublisher.pfx`
- 配置文件更新，包含证书路径和密码
- 不在当前工作目录创建证书文件

### 场景 2: 打包时自动生成证书
```powershell
cd <项目根目录>\publish-portable-fixed
.\msixtool.exe pack <应用目录> output.msix
# 选择 "2. 自动生成证书"
```

**预期结果**:
- 证书保存在: `<程序目录>\data\<Publisher>.pfx`
- 不在应用源码目录创建证书文件
- 配置文件更新
- 下次打包同一 Publisher 的应用时自动使用该证书

### 场景 3: 从清单文件创建证书
```powershell
.\msixtool.exe makecert --manifest <清单文件路径> --password test123
```

**预期结果**:
- 证书保存在: `<程序目录>\data\<Publisher>.pfx`
- 不在清单文件所在目录创建证书
- 配置文件更新

## 配置文件示例

修复后，`app.config.json` 会自动更新：

```json
{
  "Certificates": {
    "CN=TestPublisher": {
      "CertificatePath": "D:\\Tools\\MsixTool\\data\\CN_TestPublisher.pfx",
      "Password": "test123",
      "CreatedAt": "2025-10-28T15:00:00"
    },
    "CN=MyCompany": {
      "CertificatePath": "D:\\Tools\\MsixTool\\data\\CN_MyCompany.pfx",
      "Password": "1234567",
      "CreatedAt": "2025-10-28T15:05:00"
    }
  }
}
```

## 目录结构

修复后的目录结构：

```
<程序运行目录>/
├── msixtool.exe
├── tools/
│   └── sdk/
│       ├── makeappx.exe
│       ├── signtool.exe
│       ├── makecert.exe
│       └── pvk2pfx.exe
├── data/                           # 新增：证书存储目录
│   ├── CN_TestPublisher.pfx
│   ├── CN_MyCompany.pfx
│   └── ...
└── config/
    └── app.config.json             # 证书配置
```

## 向后兼容性

### 兼容性保证
✅ **完全兼容** - 现有功能不受影响：
- 手动指定证书路径时，仍然使用指定的路径
- 配置文件中已有的证书配置继续有效
- 命令行参数和行为保持不变

### 迁移说明
对于已经使用旧版本的用户：
1. 旧的证书文件仍然可以使用
2. 建议将现有证书文件移动到 `data` 目录
3. 或者在配置文件中手动指定现有证书路径

## 优点

1. **保持源码目录干净**: 不再在应用源码目录生成证书文件
2. **集中管理**: 所有自动生成的证书都在 `data` 目录
3. **持久化配置**: 自动保存证书配置，无需重复输入
4. **便携性**: 整个程序目录（包括 data 目录）可以一起移动
5. **安全性**: 证书和配置文件在程序目录下，便于权限管理

## 注意事项

1. **data 目录权限**: 确保程序有权限在运行目录创建 data 目录
2. **证书备份**: 建议定期备份 data 目录和 config/app.config.json
3. **密码安全**: 配置文件中的密码是明文存储，注意保护配置文件

## 测试清单

修复后需要测试的场景：

- [x] 使用 makecert --name 创建证书
- [x] 使用 makecert --manifest 创建证书
- [x] pack 时选择自动生成证书
- [x] 验证证书保存在 data 目录
- [x] 验证配置文件自动更新
- [x] 验证下次打包可以自动使用证书
- [x] 验证不在源码目录生成证书
- [x] 编译成功，无错误

## 发布包位置

修复后的发布包：
- 目录: `<项目根目录>\publish-portable-fixed\`
- 包含所有 SDK 工具
- 已通过编译测试

---

**修复版本**: 1.2.1  
**状态**: ✅ 已修复并编译通过  
**发布时间**: 2025-10-28
