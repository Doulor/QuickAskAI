# 快速询问AI Release 安装说明

这是 PowerToys Command Palette 扩展的 layout 测试包。

## 安装

1. 解压整个 zip，不要只解压单个文件。
2. 右键 `install.ps1`，选择 `使用 PowerShell 运行`。
3. 如果 PowerShell 阻止脚本运行，在当前目录打开 PowerShell 后运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

4. 打开 PowerToys Command Palette，Reload 扩展或重启 PowerToys。
5. 搜索 `快速询问AI`。

## 卸载

在解压目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

## GitHub Copilot 登录

Release 版本已经内置默认 GitHub OAuth Client ID。普通用户不需要自己创建 OAuth App，也不需要填写 API key。

1. 在扩展中添加 GitHub Copilot 提供商。
2. 选择 `连接 GitHub`。
3. 按提示打开 GitHub 验证页面并输入验证码。
4. 授权完成后回到扩展，选择 `我已完成授权，继续`。

用户登录的是自己的 GitHub 账号，使用的是自己的 GitHub Copilot 权益。

如果你 fork 了项目，或想发布自己的版本，可以在 GitHub 创建自己的 OAuth App，启用 Device Flow，然后在 Copilot 提供商配置页替换 Client ID。不要把 Client Secret 填入扩展；这个扩展使用 Device Flow，不需要 Client Secret。

## 本地数据

- 模型提供商配置: `%USERPROFILE%\Documents\QuickAskAI\providers.json`
- 会话记录: `%USERPROFILE%\Documents\QuickAskAI\conversations.json`
- Copilot token: Windows Credential Manager / PasswordVault，资源名 `QuickAskAI.GitHubCopilot`
