# QuickAskAI

[中文](README.md)

QuickAskAI is an AI prompt extension for Microsoft PowerToys Command Palette. After installation, you can type a question directly in Command Palette, send it to GitHub Copilot or an OpenAI-compatible model, and read the answer in the Command Palette details pane.

## Who This Is For

- Windows users who want a quick AI prompt box inside PowerToys Command Palette.
- GitHub Copilot users who want to sign in with GitHub instead of preparing an API key.
- Users with an OpenAI-compatible endpoint who want to call it from Command Palette.

## Features

- Ask AI questions directly from PowerToys Command Palette.
- GitHub Copilot sign-in through GitHub Device Flow. No manual Copilot API key is required.
- OpenAI-compatible Chat Completions providers with configurable Base URL, API key, model name, system prompt, and temperature.
- Multiple provider profiles, so you can switch between different AI services.
- Conversation context, new sessions, session switching, and chat history.
- Copyable answers.

## Before You Install

You need a Windows PC with Microsoft PowerToys installed. Open PowerToys Settings and make sure Command Palette is enabled.

The current release is an unsigned appx layout package. Windows requires Developer Mode before it can be registered locally. Open Windows Settings, go to System > For developers, and turn on Developer Mode. You can also run `start ms-settings:developers` to open that settings page directly.

## Download

Download the latest version from GitHub Releases:

<https://github.com/Doulor/AIExtension-for-Powertoys-CMDPalette/releases>

In the Assets section of the release page, download the file named like this:

```text
QuickAskAI-v<version>-x64.zip
```

Do not download `Source code.zip` or `Source code.tar.gz`. Those files are source archives for developers and cannot be installed directly by regular users.

## Install And Load It In Command Palette

1. Extract the entire `QuickAskAI-...-x64.zip` file to a normal folder, such as `Downloads\QuickAskAI`.
2. Open the extracted folder.
3. Right-click `install.ps1` and choose `Run with PowerShell`.
4. If Windows blocks the script, open PowerShell in the extracted folder and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

5. After the script prints `快速询问AI installed.`, open PowerToys Command Palette.
6. Type `Reload` in Command Palette and choose `Reload Command Palette extensions`.
7. Open Command Palette again and search for `快速询问AI`.

If the extension does not appear, restart PowerToys and try again.

## First Use

### Use GitHub Copilot

1. Open `快速询问AI` in Command Palette.
2. Add or select the GitHub Copilot provider.
3. Choose `连接 GitHub`.
4. Open the GitHub verification URL shown by the extension and enter the device code.
5. Return to the extension and choose the option that confirms authorization is complete.

You sign in with your own GitHub account and use your own GitHub Copilot entitlement.

### Use An OpenAI-Compatible Service

If you use OpenAI, Azure OpenAI, a local model gateway, or another Chat Completions-compatible service, add a custom provider and fill in:

- Base URL
- API key
- Model name
- System prompt
- Temperature

## Uninstall

If you still have the extracted folder, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

After uninstalling, run `Reload Command Palette extensions` in Command Palette or restart PowerToys.

## Local Data

QuickAskAI stores settings and chat history locally on your PC.

| Data | Path |
| --- | --- |
| Provider settings | `%USERPROFILE%\Documents\QuickAskAI\providers.json` |
| Conversation history | `%USERPROFILE%\Documents\QuickAskAI\conversations.json` |
| GitHub Copilot token | Windows Credential Manager / PasswordVault, resource name `QuickAskAI.GitHubCopilot` |
| Copilot CLI cache | `%LOCALAPPDATA%\copilot\pkg\win32-x64` |

`providers.json` may contain API keys for OpenAI-compatible providers. Do not share it publicly. GitHub Copilot tokens are not stored in `providers.json`; they are stored in Windows Credential Manager.

## For Developers

To build from source, you need Windows 10 19041 or later, .NET 9 SDK, Windows 11 SDK 10.0.26100, and PowerToys Command Palette.

Build x64 Debug:

```powershell
dotnet build .\AIExtension.sln -p:Platform=x64
```

Register the Debug layout:

```powershell
Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force
Add-AppxPackage -Register .\AIExtension\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml -ForceApplicationShutdown -ForceUpdateFromAnyVersion
```

Build a GitHub Release zip:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Configuration Release -Platform x64 -Version v1.1.0-beta.2
```
