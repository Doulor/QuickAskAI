# 快速询问AI Release 安装说明

这是 PowerToys Command Palette 扩展的 layout 测试包。你不需要 Visual Studio，也不需要自己编译；按下面步骤安装后，PowerToys Command Palette 就能加载这个扩展。

## 安装前准备

1. 安装 Microsoft PowerToys。
2. 打开 PowerToys 设置，确认 Command Palette 已启用。
3. 如果 PowerToys 正在运行，可以保持运行；安装脚本会处理旧的扩展进程。

## 安装

1. 解压整个 zip 到一个普通文件夹，例如“下载\\QuickAskAI”。不要只解压 `install.ps1`，也不要直接在压缩包预览窗口里运行文件。
2. 进入解压后的文件夹。
3. 右键 `install.ps1`，选择“使用 PowerShell 运行”。
4. 如果 PowerShell 阻止脚本运行，在当前文件夹打开 PowerShell 后运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

5. 看到 `快速询问AI installed.` 后，打开 PowerToys Command Palette。
6. 在 Command Palette 里输入 `Reload`，选择 `Reload Command Palette extensions`。
7. 重新打开 Command Palette，搜索 `快速询问AI`。

如果搜索不到，重启 PowerToys 后再试一次。

## 这一步做了什么

`install.ps1` 会把当前文件夹里的 `AppxManifest.xml` 注册到 Windows。Command Palette 扩展是通过 Windows app extension 机制发现的，所以必须先注册，不能只双击 `AIExtension.exe`。

## 卸载

在解压目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

卸载后，Reload Command Palette extensions 或重启 PowerToys。

## GitHub Copilot 登录

Release 版本已经内置默认 GitHub OAuth Client ID。普通用户不需要自己创建 OAuth App，也不需要填写 API key。

从 1.2.0 开始，GitHub Copilot provider 不再启动本地 `copilot.exe`，而是登录后直接调用 Copilot HTTP API。因此 release 包更小，也不会再遇到 `copilot.exe` 首次解压失败、`EXDEV` 或 Node SEA 初始化失败。

1. 在扩展中添加 GitHub Copilot 提供商。
2. 选择 `连接 GitHub`。
3. 按提示打开 GitHub 验证页面并输入验证码。
4. 授权完成后回到扩展，选择 `我已完成授权，继续`。

用户登录的是自己的 GitHub 账号，使用的是自己的 GitHub Copilot 权益。

如果你从旧版本升级后遇到 `HTTP 404: Not Found`，请先在扩展中断开 GitHub，然后重新选择 `连接 GitHub`。旧版本自建 OAuth App 登录得到的 token 不能用于新版的 Copilot HTTP API 兑换流程。

如果你 fork 了项目，或想发布自己的版本，请注意：普通 GitHub OAuth App 登录得到的 token 可能无法兑换 Copilot API token。新版默认使用 VS Code Copilot 的公开 client id。

## 本地数据

- 模型提供商配置: `%USERPROFILE%\Documents\QuickAskAI\providers.json`
- 会话记录: `%USERPROFILE%\Documents\QuickAskAI\conversations.json`
- Copilot token: Windows Credential Manager / PasswordVault，资源名 `QuickAskAI.GitHubCopilot`
- Copilot CLI 缓存: 1.2.0 起不再使用本地 `copilot.exe` 或 CLI 解压缓存
