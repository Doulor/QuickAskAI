# QuickAskAI

[中文说明](README.md)

QuickAskAI is a PowerToys Command Palette extension. It turns the Command Palette input box into a quick AI prompt box: type a question, press Enter, and read the response in the details pane.

## Features

- OpenAI-compatible Chat Completions provider with custom Base URL, API key, model name, system prompt, and temperature.
- Multiple provider profiles, so you can add, select, and edit different AI providers.
- GitHub Copilot provider with GitHub Device Flow login. Users do not need to manually prepare an API key for Copilot.
- Conversation history with an active session, session switching, and stored chat records.
- Copyable responses and diagnostic error messages that avoid exposing API keys or GitHub tokens.

## Install For Regular Users

You need Microsoft PowerToys with Command Palette enabled before installing this extension.

1. Install or update Microsoft PowerToys from the Microsoft Store or the official PowerToys GitHub releases.
2. Open PowerToys Settings and make sure Command Palette is enabled.
3. Go to this repository's GitHub Releases page.
4. Download `QuickAskAI-<version>-x64.zip` from the release assets. Do not download `Source code.zip`; that is only for developers.
5. Extract the entire zip file to a normal folder, such as `Downloads\QuickAskAI`. Do not run files directly from inside the zip preview.
6. In the extracted folder, right-click `install.ps1` and choose `Run with PowerShell`.
7. If Windows blocks the script, open PowerShell in the extracted folder and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

8. Open PowerToys Command Palette, run the `Reload` command, and choose `Reload Command Palette extensions`. Restarting PowerToys also works.
9. Search for `快速询问AI` or `QuickAskAI` in Command Palette.

The installer registers the extension locally with Windows. After registration, Command Palette can discover it as an out-of-process extension.

## First Use

You can use either an OpenAI-compatible provider or the built-in GitHub Copilot provider.

For GitHub Copilot:

1. Open the extension in Command Palette.
2. Add or select the `GitHub Copilot` provider.
3. Choose `Connect GitHub`.
4. Open the GitHub verification URL shown by the extension and enter the device code.
5. Return to the extension and choose the option that confirms authorization is complete.

You sign in with your own GitHub account and use your own GitHub Copilot entitlement.

For OpenAI-compatible providers, add a provider with your API Base URL, API key, and model name.

## Local Data

User settings and chat history are stored outside the Git repository.

| Data | Path |
| --- | --- |
| Provider settings | `%USERPROFILE%\Documents\QuickAskAI\providers.json` |
| Conversation history | `%USERPROFILE%\Documents\QuickAskAI\conversations.json` |
| GitHub Copilot token | Windows Credential Manager / PasswordVault, resource name `QuickAskAI.GitHubCopilot` |
| Copilot CLI cache | `%LOCALAPPDATA%\copilot\pkg\win32-x64` |

`providers.json` can contain API keys for OpenAI-compatible providers. Do not share or commit it. GitHub Copilot tokens are stored in Windows Credential Manager, not in `providers.json`.

## Build From Source

Requirements:

- Windows 10 19041 or later.
- .NET 9 SDK.
- Windows 11 SDK 10.0.26100.
- Microsoft PowerToys with Command Palette.

Build x64 Debug:

```powershell
$env:DOTNET_ROOT = Join-Path $env:USERPROFILE ".dotnet"
& "$env:DOTNET_ROOT\dotnet.exe" build .\AIExtension.sln -p:Platform=x64
```

If `dotnet` is already available in PATH:

```powershell
dotnet build .\AIExtension.sln -p:Platform=x64
```

Register the Debug appx layout locally:

```powershell
Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force
Add-AppxPackage -Register .\AIExtension\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml -ForceApplicationShutdown -ForceUpdateFromAnyVersion
```

Then reload Command Palette extensions or restart PowerToys.

## Build A Release Zip

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Configuration Release -Platform x64 -Version v1.1.0-beta.2
```

The generated zip is written to:

```text
release\artifacts\QuickAskAI-v1.1.0-beta.2-x64.zip
```

The zip contains the app layout, `AppxManifest.xml`, `install.ps1`, `uninstall.ps1`, and user-facing installation notes.
