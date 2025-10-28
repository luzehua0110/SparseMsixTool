# MSIX Tool 示例

本目录包含了使用 MSIX Tool 的示例。

## 示例应用结构

### 基本的 MSIX 稀疏包结构
```
ExampleApp/
├── AppxManifest.xml          # 必需的清单文件
├── MyApp.exe                 # 主程序
├── Assets/                   # 资源文件
│   ├── Square44x44Logo.png
│   ├── Square150x150Logo.png
│   └── StoreLogo.png
└── Dependencies/             # 依赖文件（可选）
    └── vcruntime140.dll
```

## AppxManifest.xml 模板

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         IgnorableNamespaces="uap mp">
    
    <Identity Name="MyCompany.MyApp"
              Publisher="CN=MyCompany"
              Version="1.0.0.0" />
              
    <Properties>
        <DisplayName>My Example App</DisplayName>
        <PublisherDisplayName>My Company</PublisherDisplayName>
        <Logo>Assets\StoreLogo.png</Logo>
        <Description>An example MSIX sparse package</Description>
    </Properties>
    
    <Dependencies>
        <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.22000.0" />
    </Dependencies>
    
    <Capabilities>
        <Capability Name="runFullTrust" />
    </Capabilities>
    
    <Applications>
        <Application Id="MyApp" Executable="MyApp.exe" EntryPoint="Windows.FullTrustApplication">
            <uap:VisualElements
                DisplayName="My Example App"
                Description="An example application"
                Square150x150Logo="Assets\Square150x150Logo.png"
                Square44x44Logo="Assets\Square44x44Logo.png"
                BackgroundColor="transparent">
            </uap:VisualElements>
        </Application>
    </Applications>
</Package>
```

## 使用步骤

1. **创建示例应用目录**
   ```powershell
   mkdir ExampleApp
   cd ExampleApp
   ```

2. **创建 AppxManifest.xml**
   复制上面的模板到 `AppxManifest.xml` 文件

3. **添加应用程序**
   将您的 .exe 文件和资源文件放入相应目录

4. **创建证书**
   ```powershell
   msixtool makecert --src .
   ```

5. **打包应用**
   ```powershell
   msixtool pack --src .
   ```

6. **安装应用**
   ```powershell
   msixtool install --name MyCompany.MyApp
   ```

## 注意事项

- 确保 `Publisher` 字段与您的证书主体匹配
- 所有在清单中引用的文件都必须存在
- 图标文件建议使用 PNG 格式
- 版本号必须是四位数字格式 (major.minor.build.revision)