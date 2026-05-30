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

要使用 Copilot provider，需要 GitHub OAuth App 的 Client ID。Client ID 是公开标识，不是密码。

创建方法：

1. 打开 https://github.com/settings/developers
2. 进入 `OAuth Apps`。
3. 点击 `New OAuth App`。
4. 填写：
   - Application name: `快速询问AI`
   - Homepage URL: 你的项目 GitHub 地址，例如 `https://github.com/<owner>/<repo>`
   - Authorization callback URL: `http://localhost`
5. 创建后进入 App 设置，启用 `Device Flow`。
6. 复制 `Client ID`。
7. 在扩展中添加 GitHub Copilot 提供商并填写 Client ID。
8. 选择 `连接 GitHub`，按提示网页登录。

不要把 Client Secret 填入扩展。这个扩展使用 Device Flow，不需要 Client Secret。

## 本地数据

- 模型提供商配置: `%USERPROFILE%\Documents\QuickAskAI\providers.json`
- 会话记录: `%USERPROFILE%\Documents\QuickAskAI\conversations.json`
- Copilot token: Windows Credential Manager / PasswordVault，资源名 `QuickAskAI.GitHubCopilot`
