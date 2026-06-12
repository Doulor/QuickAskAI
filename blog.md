---
title: "快速询问AI：把 PowerToys 命令面板变成 AI 提问入口"
published: 2026-05-30
description: "快速询问AI是一个 PowerToys Command Palette 插件，支持 GitHub Copilot 登录和 OpenAI 兼容接口。1.2.0 起 GitHub Copilot provider 改为直接调用 Copilot HTTP API，不再依赖本地 copilot.exe，包体更小，也减少了本机兼容性问题。"
category: "wiki"
tags: ["Powertoys", "工具", "AI"]
draft: false
pinned: false
image: "/images/posts/AIExtension.png"
encrypted: false
sourceLink: "https://github.com/Doulor/AIExtension-for-Powertoys-CMDPalette"
---
# 快速询问AI：把 PowerToys Command Palette 变成随手可用的 AI 提问入口

> 快速询问AI是一个面向 PowerToys Command Palette 的 AI 提问插件。它的目标很简单：让你不用切换窗口、不用打开浏览器，就能在 Windows 命令面板里快速向 AI 提问，并在右侧详情区域直接阅读回答。

如果你已经习惯使用 PowerToys Command Palette 打开应用、搜索命令、执行工具，那么这个插件会把同一个入口扩展成一个轻量的 AI 问答界面。你可以接入 GitHub Copilot，也可以接入 OpenAI 兼容接口，例如 OpenAI、Azure OpenAI、本地大模型网关或其他支持 Chat Completions 协议的服务。

---
介绍宣传视频
---
<iframe width="100%" height="584" src="https://pub-a425f506bc5e491696a5cf9be896049c.r2.dev/post/AIExtension-for-Powertoys/quickaskai-intro-no-voiceover.mp4" title="介绍宣传视频" frameborder="0" allowfullscreen></iframe>


## 目录

- [它解决什么问题](#它解决什么问题)
- [适合哪些用户](#适合哪些用户)
- [核心功能概览](#核心功能概览)
- [普通用户安装指南](#普通用户安装指南)
- [第一次使用](#第一次使用)
- [GitHub Copilot 登录流程](#github-copilot-登录流程)
- [1.2.0：不再依赖本地 copilot.exe](#120不再依赖本地-copilotexe)
- [OpenAI 兼容服务配置](#openai-兼容服务配置)
- [会话与本地数据](#会话与本地数据)
- [使用建议](#使用建议)
- [给开发者：项目结构与实现思路](#给开发者项目结构与实现思路)
- [给开发者：本地构建和注册](#给开发者本地构建和注册)
- [给开发者：Release 打包](#给开发者release-打包)
- [常见问题](#常见问题)
- [总结](#总结)

---

## 它解决什么问题

现在很多 AI 工具都很好用，但使用入口往往比较分散：有的在浏览器里，有的在 IDE 里，有的在独立客户端里。对于一些很短、很即时的问题，比如：

- “帮我解释这个错误是什么意思。”
- “这个命令怎么写？”
- “把这句话润色一下。”
- “给我一个 PowerShell 命令示例。”
- “这个概念用一句话解释。”

每次都切到网页或另一个应用，会打断正在做的事。

快速询问AI的思路是把 AI 提问入口放到 PowerToys Command Palette 里。你唤起命令面板，输入问题，选择插件，就能在当前上下文里得到回答。它不是为了替代完整聊天应用，而是为了处理那些“我现在就想问一下”的轻量需求。

---

## 适合哪些用户

### 普通 Windows 用户

如果你已经安装 PowerToys，并且平时会用 Command Palette 打开应用或搜索命令，这个插件可以自然地融入你的使用习惯。你不需要理解插件内部如何工作，只需要下载 release 包、运行安装脚本、在 Command Palette 中 Reload 扩展即可。

### GitHub Copilot 用户

如果你有 GitHub Copilot 权益，这个插件支持通过 GitHub 设备码授权登录。你不需要手动准备 Copilot API key，也不需要把 token 写进配置文件。登录完成后，插件会使用你的 GitHub 账号和 Copilot 权益，并直接通过 Copilot HTTP API 获取回答。

### 使用 OpenAI 兼容服务的用户

如果你有 OpenAI、Azure OpenAI、本地大模型网关，或其他兼容 Chat Completions 的服务，也可以把它配置成一个提供商。你可以自定义 Base URL、API Key、模型名、系统提示词和 temperature。

### 想开发 Command Palette 扩展的人

这个项目也可以作为一个 PowerToys Command Palette 扩展的参考：它包含 out-of-process COM server、MSIX layout 注册、Command Palette 页面、设置管理、会话管理、GitHub Device Flow 登录和 release 打包脚本。

---

## 核心功能概览

| 功能 | 说明 |
| --- | --- |
| Command Palette 内提问 | 在 PowerToys Command Palette 中输入问题并查看回答 |
| GitHub Copilot provider | 使用 GitHub 设备码登录，不需要用户手动准备 Copilot API key |
| Copilot HTTP 直连 | 1.2.0 起不再启动本地 `copilot.exe`，直接请求 Copilot HTTP API |
| OpenAI-compatible provider | 支持自定义 Base URL、API Key、模型名、系统提示词和 temperature |
| 多提供商管理 | 可以添加、选择和编辑不同 AI 服务配置 |
| 会话上下文 | 默认保留上下文，适合连续追问 |
| 会话管理 | 支持新建会话、切换历史会话和查看聊天记录 |
| 回答复制 | 方便把 AI 输出复制到其他地方 |
| 本地存储 | 配置和聊天记录保存在用户电脑本地 |

---

## 普通用户安装指南

安装前请先确认：

1. 你使用的是 Windows 10 19041 或更高版本。
2. 已安装 Microsoft PowerToys。
3. PowerToys 设置中已经启用 Command Palette。

### 第一步：下载正确的文件

打开项目的 GitHub Releases 页面：

<https://github.com/Doulor/AIExtension-for-Powertoys-CMDPalette/releases>

在最新版 Release 页面底部的 Assets 区域，下载类似下面名字的文件：

```text
QuickAskAI-v版本号-x64.zip
```

请注意，不要下载：

- `Source code.zip`
- `Source code.tar.gz`

这两个是 GitHub 自动生成的源码包，不是普通用户安装用的插件包。

### 第二步：解压完整 zip

把下载到的 `QuickAskAI-...-x64.zip` 完整解压到一个普通文件夹，例如：

```text
下载\QuickAskAI
```

不要直接在压缩包预览窗口里运行脚本。插件的安装文件需要在同一个解压目录里。

### 第三步：运行安装脚本

进入解压后的文件夹，**右键 `install.bat`，选择”以管理员身份运行”**。在弹出的 UAC 窗口点”是”。

脚本会自动将签名证书安装到系统信任列表，然后安装 MSIX 包。看到下面提示后，说明安装完成：

```text
快速询问AI installed.
```

### 第四步：让 Command Palette 重新加载扩展

打开 PowerToys Command Palette，输入：

```text
Reload
```

选择：

```text
Reload Command Palette extensions
```

然后重新打开 Command Palette，搜索：

```text
快速询问AI
```

如果没有搜到，重启 PowerToys 后再试一次。

---

## 第一次使用

安装并加载成功后，在 Command Palette 中打开 `快速询问AI`。第一次使用时，你需要先选择或添加一个 AI 提供商。

你可以选择两条路线：

1. 使用 GitHub Copilot。
2. 使用 OpenAI 兼容服务。

如果你已经有 GitHub Copilot 权益，推荐先试 GitHub Copilot provider，因为它不需要你手动复制 API key。

---

## GitHub Copilot 登录流程

GitHub Copilot provider 使用 GitHub OAuth Device Flow，也就是设备码登录流程。大致步骤如下：

1. 在插件中添加或选择 `GitHub Copilot` 提供商。
2. 选择 `连接 GitHub`。
3. 插件会显示 GitHub 验证网址和设备码。
4. 在浏览器里打开验证网址。
5. 输入设备码并授权。
6. 回到插件，选择 `我已完成授权，继续`。

登录完成后，插件会使用你的 GitHub 账号和你的 GitHub Copilot 权益。

### 普通用户不需要创建 OAuth App

Release 版本已经内置默认 GitHub OAuth Client ID。普通用户不需要自己创建 GitHub OAuth App，也不需要填写 Client Secret。1.2.0 起默认使用 VS Code Copilot 的公开 client id，以便登录得到的 token 可以兑换 Copilot API token。

### token 保存在哪里

GitHub Copilot 登录得到的 token 不会写入普通配置文件，而是保存到 Windows Credential Manager / PasswordVault 中，资源名为：

```text
QuickAskAI.GitHubCopilot
```

---

## OpenAI 兼容服务配置

如果你使用的是 OpenAI 兼容接口，可以添加一个自定义 provider。通常需要填写这些字段：

| 字段 | 含义 |
| --- | --- |
| Base URL | API 的基础地址 |
| API Key | 你的服务密钥 |
| 模型名 | 例如 `gpt-4.1`、`gpt-4o-mini` 或服务商提供的模型 ID |
| 系统提示词 | 可选，用来定义 AI 的回答风格或角色 |
| temperature | 可选，用来控制输出随机性 |

只要服务兼容 Chat Completions 风格接口，就可以尝试接入。

### API Key 安全提醒

OpenAI 兼容 provider 的 API Key 会保存在本地配置文件中：

```text
%USERPROFILE%\Documents\QuickAskAI\providers.json
```

请不要把这个文件上传到 GitHub，也不要公开分享给别人。

---

## 会话与本地数据

快速询问AI支持会话上下文。也就是说，你可以围绕同一个主题连续追问，插件会保留当前会话的上下文。

本地数据保存位置如下：

| 数据 | 路径 |
| --- | --- |
| 模型提供商配置 | `%USERPROFILE%\Documents\QuickAskAI\providers.json` |
| 会话聊天记录 | `%USERPROFILE%\Documents\QuickAskAI\conversations.json` |
| GitHub Copilot token | Windows Credential Manager / PasswordVault，资源名 `QuickAskAI.GitHubCopilot` |
| GitHub Copilot HTTP 运行数据 | 1.2.0 起不再使用本地 `copilot.exe` 或 CLI 解压缓存 |

这些文件不会保存在 Git 仓库里。对于普通用户来说，它们只是本机配置和聊天记录；对于开发者来说，也应当避免把它们误提交到代码仓库。

---

## 使用建议

### 适合问什么

这个插件适合处理短平快的问题，例如：

- 命令行参数怎么写。
- 某个错误信息是什么意思。
- 一段文本怎么润色。
- 某个概念如何快速理解。
- 一段代码大概在做什么。
- 给一个脚本、正则或配置示例。

### 不适合什么

它不一定适合承载很长、很复杂的多轮深度任务。如果你需要长时间整理资料、处理大型代码库、生成长篇内容，完整聊天界面或 IDE 内 AI 工具可能会更舒服。

换句话说，快速询问AI更像是一个“随手问一下”的入口，而不是完整替代所有 AI 客户端。

---

## 给开发者：项目结构与实现思路

这个项目是一个 PowerToys Command Palette 扩展。Command Palette 扩展通常以独立进程形式运行，通过 COM server 和 Windows app extension 机制被 PowerToys 发现和加载。

项目中的关键文件和目录包括：

| 路径 | 作用 |
| --- | --- |
| `QuickAskAI/Program.cs` | 扩展进程入口，负责托管 COM server |
| `QuickAskAI/QuickAskAIExtension.cs` | 扩展对象入口 |
| `AIExtension/QuickAskAICommandsProvider.cs` | Command Palette 命令提供者 |
| `QuickAskAI/Services/CopilotChatService.cs` | GitHub Copilot HTTP API 调用逻辑 |
| `QuickAskAI/Pages/` | 主要页面实现，包括提问页、会话页、提供商管理页等 |
| `QuickAskAI/SettingsManager.cs` | 配置读取、保存和管理 |
| `QuickAskAI/Package.appxmanifest` | AppX/MSIX manifest，声明扩展身份、COM server 和 Command Palette app extension |
| `QuickAskAI/Assets/` | 应用图标和打包资源 |
| `build-release.ps1` | 生成 GitHub Release zip 的脚本 |
| `release/layout-scripts/` | release 包内附带的安装、卸载和说明脚本 |

### Command Palette 扩展加载机制

普通用户容易误解的一点是：这个插件不是双击 `QuickAskAI.exe` 启动的。

PowerToys Command Palette 需要通过 Windows app extension 机制发现扩展。因此 release 包中的 `install.ps1` 会调用类似下面的命令注册 layout：

```powershell
Add-AppxPackage -Register .\AppxManifest.xml -ForceApplicationShutdown -ForceUpdateFromAnyVersion
```

注册后，Windows 知道这个目录下存在一个 app extension。Command Palette Reload 扩展时，才能发现并加载它。

### Manifest 中的关键声明

`Package.appxmanifest` 里有两个重要部分：

1. COM server 声明。
2. `com.microsoft.commandpalette` app extension 声明。

前者让扩展可以作为 out-of-process COM server 被创建，后者让 Command Palette 知道这是一个可加载的命令面板扩展。

---

## 给开发者：本地构建和注册

开发环境要求：

- Windows 10 19041 或更高版本。
- .NET 9 SDK。
- Windows 11 SDK 10.0.26100。
- PowerToys Command Palette。

构建 x64 Debug：

```powershell
dotnet build .\QuickAskAI.sln -p:Platform=x64
```

如果你的 .NET SDK 安装在用户目录，也可以显式指定：

```powershell
$env:DOTNET_ROOT = Join-Path $env:USERPROFILE ".dotnet"
& "$env:DOTNET_ROOT\dotnet.exe" build .\QuickAskAI.sln -p:Platform=x64
```

构建完成后，注册 Debug layout：

```powershell
Get-Process QuickAskAI -ErrorAction SilentlyContinue | Stop-Process -Force
Add-AppxPackage -Register .\QuickAskAI\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml -ForceApplicationShutdown -ForceUpdateFromAnyVersion
```

然后在 Command Palette 中执行 `Reload Command Palette extensions`。

---

## 给开发者：Release 打包

项目提供了 `build-release.ps1` 用于生成适合上传到 GitHub Release 的 zip：

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Configuration Release -Platform x64 -Version v1.2.0
```

生成文件位于：

```text
release\artifacts\QuickAskAI-v1.2.0-x64.zip
```

这个 zip 包含：

- 扩展运行文件。
- `AppxManifest.xml`。
- `install.ps1`。
- `uninstall.ps1`。
- 面向用户的安装说明。

### 发布前检查清单

发布前建议检查：

- Release 构建是否成功。
- zip 中是否包含 `AppxManifest.xml`。
- zip 中是否包含 `install.ps1` 和 `uninstall.ps1`。
- `Package.appxmanifest` 中的版本号是否已经递增。
- GitHub Release 页面是否上传了 `QuickAskAI-...-x64.zip`，而不仅仅是源码包。
- README 是否明确告诉用户下载 Assets 里的 zip，而不是下载 Source code。

---

## 常见问题

### 为什么安装后还要 Reload Command Palette？

Command Palette 不一定会立刻重新扫描所有扩展。安装脚本完成注册后，需要在 Command Palette 中运行 `Reload Command Palette extensions`，让它重新发现新扩展。重启 PowerToys 也可以达到类似效果。

### 为什么不能直接运行 exe？

因为这是 Command Palette 扩展，不是普通桌面应用。它需要通过 Windows app extension 注册，再由 PowerToys Command Palette 加载。

### 下载 release 时应该选哪个文件？

请选择 Assets 中的：

```text
QuickAskAI-v版本号-x64.zip
```

不要选择 `Source code.zip`，那是源码包。

### GitHub Copilot 需要我自己准备 API Key 吗？

不需要。Release 版本使用 GitHub 设备码登录。你只需要用自己的 GitHub 账号授权，并确保账号拥有 GitHub Copilot 权益。

### OpenAI 兼容 provider 的 API Key 安全吗？

它会保存在你本机的 `%USERPROFILE%\Documents\QuickAskAI\providers.json` 中。请不要把这个文件公开或提交到仓库。

### 聊天记录保存在哪里？

聊天记录保存在：

```text
%USERPROFILE%\Documents\QuickAskAI\conversations.json
```

---

## 总结

快速询问AI不是一个庞大的 AI 客户端，而是一个尽量贴近 Windows 使用习惯的小入口。它把 PowerToys Command Palette 变成一个可以随手提问的地方：

- 对普通用户来说，下载 zip、运行安装脚本、Reload Command Palette，就能开始使用。
- 对 GitHub Copilot 用户来说，可以直接通过 GitHub 设备码登录。
- 对 OpenAI 兼容服务用户来说，可以配置自己的 Base URL、API Key 和模型。
- 对开发者来说，它也是一个完整的 Command Palette 扩展示例，覆盖构建、注册、打包和发布流程。

如果你经常在 Windows 上工作，并且已经把 PowerToys Command Palette 当成日常入口，那么这个插件会让“问一下 AI”这件事变得更自然、更轻量。
