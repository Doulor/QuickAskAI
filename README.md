# 快速询问AI

[English README](README.en.md)

快速询问AI是一个 PowerToys Command Palette 扩展。它把命令面板顶部输入框变成提问框，按 Enter 即可向配置好的 AI 提问，并在右侧详情区域显示回答。

## 功能

- OpenAI-compatible Chat Completions 调用，支持自定义 Base URL、API Key、模型名、系统提示词和 temperature。
- 多模型提供商管理，可以添加、选择和编辑不同提供商。
- GitHub Copilot provider，支持 GitHub 网页设备码登录，不需要用户手动准备 API key。
- 会话管理，默认保留上下文，并支持新建会话、切换历史会话和查看聊天记录。
- 回答支持复制，错误信息会尽量保留可诊断内容，同时避免显示 API key 或 GitHub token。

## 普通用户安装

你需要先安装 Microsoft PowerToys，并启用 PowerToys 里的 Command Palette。

1. 打开本仓库的 GitHub Releases 页面。
2. 下载 release assets 里的 `QuickAskAI-<版本>-x64.zip`，不要下载 `Source code.zip`，那个是源码包。
3. 把整个 zip 解压到普通文件夹，例如“下载\\QuickAskAI”。不要直接在压缩包预览窗口里运行脚本。
4. 进入解压后的文件夹，右键 `install.ps1`，选择“使用 PowerShell 运行”。
5. 如果 PowerShell 阻止脚本运行，在这个文件夹空白处打开 PowerShell，然后运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

6. 打开 PowerToys Command Palette，输入 `Reload`，选择 `Reload Command Palette extensions`。也可以直接重启 PowerToys。
7. 在 Command Palette 里搜索 `快速询问AI`。

`install.ps1` 会把解压目录里的扩展 layout 注册到 Windows。注册完成后，PowerToys Command Palette 才能发现并加载这个扩展。

## 数据存储

用户配置和聊天记录不会存放在 Git 仓库里。

| 数据 | 路径 |
| --- | --- |
| 模型提供商配置 | `%USERPROFILE%\\Documents\\QuickAskAI\\providers.json` |
| 会话聊天记录 | `%USERPROFILE%\\Documents\\QuickAskAI\\conversations.json` |
| GitHub Copilot token | Windows Credential Manager / PasswordVault，资源名 `QuickAskAI.GitHubCopilot` |
| Copilot CLI 本机缓存 | `%LOCALAPPDATA%\\copilot\\pkg\\win32-x64` |

`providers.json` 可能包含 OpenAI-compatible provider 的 API Key，请不要提交到 GitHub。GitHub Copilot 网页登录得到的 token 不写入 `providers.json`，而是保存到 Windows 凭据存储。

## GitHub Copilot 登录

Copilot provider 使用 GitHub OAuth Device Flow。Release 版本已经内置默认 GitHub OAuth Client ID，普通用户不需要自己创建 OAuth App：

1. 在扩展中添加 `GitHub Copilot` 提供商。
2. 选择 `连接 GitHub`。
3. 打开页面提示的 GitHub 验证网址并输入验证码。
4. 授权完成后回到扩展，选择 `我已完成授权，继续`。

用户登录的是自己的 GitHub 账号，使用的是自己的 GitHub Copilot 权益。

### 维护者：创建自己的 GitHub OAuth App

如果你 fork 了项目，或想发布自己的版本，可以创建自己的 OAuth App 并替换默认 Client ID：

1. 打开 <https://github.com/settings/developers>。
2. 进入 `OAuth Apps`。
3. 点击 `New OAuth App`。
4. 填写：
   - Application name: `快速询问AI`
   - Homepage URL: 你的项目 GitHub 地址，例如 `https://github.com/<owner>/<repo>`
   - Authorization callback URL: `http://localhost`
5. 创建后进入 App 设置，启用 `Device Flow`。
6. 复制 `Client ID`。

Client ID 是公开标识，不是密码，也不是 API key。这个扩展使用 Device Flow，不需要 Client Secret。不要把 Client Secret 填入扩展或提交到仓库。

Copilot SDK 第一次启动时会解压它自带的 Copilot CLI。如果遇到 `EXDEV: cross-device link not permitted` 这类首次解压错误，扩展会自动运行一次随包带的 `copilot.exe --help` 预热缓存后重试。

## 构建

需要：

- Windows 10 19041 或更高版本。
- .NET 9 SDK。
- Windows 11 SDK 10.0.26100。
- PowerToys Command Palette。

构建 x64 Debug：

```powershell
$env:DOTNET_ROOT = Join-Path $env:USERPROFILE ".dotnet"
& "$env:DOTNET_ROOT\dotnet.exe" build .\AIExtension.sln -p:Platform=x64
```

如果 `dotnet` 已在 PATH 中，也可以直接运行：

```powershell
dotnet build .\AIExtension.sln -p:Platform=x64
```

## 本地注册测试

构建后，可以注册 Debug appx layout：

```powershell
Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force
Add-AppxPackage -Register .\AIExtension\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml -ForceApplicationShutdown -ForceUpdateFromAnyVersion
```

然后重新打开或 Reload PowerToys Command Palette。

## GitHub Release 测试包

可以用 `build-release.ps1` 生成适合上传到 GitHub Release 的 zip：

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Configuration Release -Platform x64 -Version v1.1.0-beta.1
```

生成文件位于：

```text
release\artifacts\QuickAskAI-v1.1.0-beta.1-x64.zip
```

这个 zip 是 appx layout 测试包，包含：

- 扩展运行文件。
- `AppxManifest.xml`。
- `install.ps1`。
- `uninstall.ps1`。
- 安装和 OAuth App 说明。

用户下载后解压整个 zip，运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

## Git 仓库说明

仓库只应提交源码、项目文件、资源文件和文档。不要提交：

- `AIExtension/bin/`
- `AIExtension/obj/`
- `.appx` / `.msix` 包输出
- `providers.json`
- `conversations.json`
- API Key、GitHub token、Client Secret 或证书私钥

这些内容已经写入 `.gitignore`。
