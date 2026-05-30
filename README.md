# 快速询问AI

[English](README.en.md)

快速询问AI是一个给 PowerToys Command Palette 使用的 AI 提问插件。安装后，你可以直接在命令面板里输入问题，选择这个插件，把问题发给 GitHub Copilot 或你配置的 OpenAI 兼容模型，然后在命令面板右侧查看回答。

## 适合谁使用

- 想在 Windows 命令面板里快速问 AI 的用户。
- 已经在使用 GitHub Copilot，希望不用另外准备 API key 的用户。
- 有自己的 OpenAI 兼容服务，希望在 PowerToys Command Palette 里调用它的用户。

## 主要功能

- 在 PowerToys Command Palette 中直接提问。
- 支持 GitHub Copilot 登录，使用 GitHub 设备码授权，不需要手动填写 Copilot API key。
- 支持 OpenAI 兼容 Chat Completions 接口，可以配置 Base URL、API Key、模型名、系统提示词和 temperature。
- 支持多个模型提供商，方便在不同 AI 服务之间切换。
- 保留会话上下文，支持新建会话、切换历史会话和查看聊天记录。
- 支持复制回答。

## 安装前准备

你需要一台 Windows 电脑，并安装 Microsoft PowerToys。安装后，请打开 PowerToys 设置，确认 Command Palette 已启用。

## 下载

请在 GitHub Releases 页面下载最新版：

<https://github.com/Doulor/AIExtension-for-Powertoys-CMDPanel/releases>

在 release 页面底部的 Assets 区域，下载类似下面名字的文件：

```text
QuickAskAI-v版本号-x64.zip
```

不要下载 `Source code.zip` 或 `Source code.tar.gz`，那是源码包，普通用户不能直接拿来安装。

## 安装和加载到 Command Palette

1. 把下载的 `QuickAskAI-...-x64.zip` 完整解压到一个普通文件夹，例如“下载\QuickAskAI”。
2. 进入解压后的文件夹。
3. 右键 `install.ps1`，选择“使用 PowerShell 运行”。
4. 如果 PowerShell 阻止脚本运行，在当前文件夹打开 PowerShell 后运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

5. 看到 `快速询问AI installed.` 后，打开 PowerToys Command Palette。
6. 在 Command Palette 中输入 `Reload`，选择 `Reload Command Palette extensions`。
7. 重新打开 Command Palette，搜索 `快速询问AI`。

如果没有搜到，重启 PowerToys 后再试一次。

## 第一次使用

### 使用 GitHub Copilot

1. 在 Command Palette 里打开 `快速询问AI`。
2. 添加或选择 GitHub Copilot 提供商。
3. 选择 `连接 GitHub`。
4. 按页面提示打开 GitHub 验证网址，并输入验证码。
5. 授权完成后回到扩展，选择 `我已完成授权，继续`。

你登录的是自己的 GitHub 账号，使用的是自己的 GitHub Copilot 权益。

### 使用 OpenAI 兼容服务

如果你有 OpenAI、Azure OpenAI、本地大模型网关或其他兼容 Chat Completions 的服务，可以添加一个自定义提供商，并填写：

- Base URL
- API Key
- 模型名
- 系统提示词
- temperature

## 卸载

如果你还保留着解压后的文件夹，可以在里面运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

卸载后，在 Command Palette 中执行 `Reload Command Palette extensions`，或重启 PowerToys。

## 本地数据保存在哪里

这个插件会把配置和聊天记录保存在你的电脑本地。

| 数据 | 路径 |
| --- | --- |
| 模型提供商配置 | `%USERPROFILE%\Documents\QuickAskAI\providers.json` |
| 会话聊天记录 | `%USERPROFILE%\Documents\QuickAskAI\conversations.json` |
| GitHub Copilot token | Windows Credential Manager / PasswordVault，资源名 `QuickAskAI.GitHubCopilot` |
| Copilot CLI 本机缓存 | `%LOCALAPPDATA%\copilot\pkg\win32-x64` |

`providers.json` 可能包含你的 OpenAI 兼容服务 API key，请不要公开分享这个文件。GitHub Copilot 登录得到的 token 不写入 `providers.json`，而是保存在 Windows 凭据存储中。

## 给开发者

如果你想从源码构建，需要 Windows 10 19041 或更高版本、.NET 9 SDK、Windows 11 SDK 10.0.26100，以及 PowerToys Command Palette。

构建 x64 Debug：

```powershell
dotnet build .\AIExtension.sln -p:Platform=x64
```

注册 Debug layout：

```powershell
Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force
Add-AppxPackage -Register .\AIExtension\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml -ForceApplicationShutdown -ForceUpdateFromAnyVersion
```

生成 GitHub Release 用的 zip：

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Configuration Release -Platform x64 -Version v1.1.0-beta.2
```
