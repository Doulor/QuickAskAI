// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

internal sealed partial class GitHubDeviceLoginPage : ListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly CopilotAuthService _authService;
    private readonly string _providerId;
    private readonly Action? _onChanged;
    private GitHubDeviceCodeResult? _deviceCode;
    private string _statusMessage = "正在准备 GitHub 登录...";
    private bool _isBusy;

    public GitHubDeviceLoginPage(SettingsManager settingsManager, string providerId, Action? onChanged = null)
    {
        _settingsManager = settingsManager;
        _authService = new CopilotAuthService();
        _providerId = providerId;
        _onChanged = onChanged;

        Icon = new IconInfo("");
        Title = "连接 GitHub";
        Name = "登录";
        ShowDetails = true;

        RestoreOrStartLogin();
    }

    private ProviderProfile? Provider => _settingsManager.GetProvider(_providerId);

    public override IListItem[] GetItems()
    {
        var provider = Provider;
        if (provider is null)
        {
            return [new ListItem(new NoOpCommand()) { Title = "提供商不存在", Icon = new IconInfo("") }];
        }

        if (string.IsNullOrWhiteSpace(provider.GitHubClientId))
        {
            return [CreateMissingClientIdItem(provider)];
        }

        if (_deviceCode is null)
        {
            return [CreateStatusItem(provider), CreateRestartItem()];
        }

        return [
            CreateCodeItem(provider),
            CreateContinueItem(),
            CreateCopyCodeItem(),
            CreateRestartItem(),
        ];
    }

    private void RestoreOrStartLogin()
    {
        var provider = Provider;
        if (provider is null || string.IsNullOrWhiteSpace(provider.GitHubClientId))
        {
            IsLoading = false;
            return;
        }

        _deviceCode = GitHubDeviceLoginStore.TryGet(provider.Id, provider.GitHubClientId);
        if (_deviceCode is not null)
        {
            _statusMessage = "继续使用上次未过期的 GitHub 登录验证码。";
            IsLoading = false;
            return;
        }

        StartLogin(resetExisting: false);
    }

    private void StartLogin(bool resetExisting)
    {
        var provider = Provider;
        if (provider is null || string.IsNullOrWhiteSpace(provider.GitHubClientId))
        {
            IsLoading = false;
            return;
        }

        if (resetExisting)
        {
            GitHubDeviceLoginStore.Clear(provider.Id, provider.GitHubClientId);
            _deviceCode = null;
        }

        _isBusy = true;
        IsLoading = true;
        _statusMessage = "正在向 GitHub 获取登录验证码...";
        RaiseItemsChanged();

        _ = Task.Run(async () =>
        {
            try
            {
                _deviceCode = await _authService.StartDeviceLoginAsync(provider.GitHubClientId).ConfigureAwait(false);
                GitHubDeviceLoginStore.Save(provider.Id, provider.GitHubClientId, _deviceCode);
                _statusMessage = "请在浏览器打开 GitHub 验证页面并输入验证码。";
            }
            catch (Exception ex)
            {
                _statusMessage = ex.Message;
                _deviceCode = null;
            }
            finally
            {
                _isBusy = false;
                IsLoading = false;
                RaiseItemsChanged();
            }
        });
    }

    private void CompleteLogin()
    {
        var provider = Provider;
        if (provider is null || _deviceCode is null || _isBusy)
        {
            return;
        }

        _isBusy = true;
        IsLoading = true;
        _statusMessage = "正在等待 GitHub 完成授权...";
        RaiseItemsChanged();

        _ = Task.Run(async () =>
        {
            try
            {
                var token = await _authService.PollForTokenAsync(provider.GitHubClientId, _deviceCode).ConfigureAwait(false);
                var login = await _authService.GetUserLoginAsync(token.AccessToken).ConfigureAwait(false);
                _settingsManager.SaveCopilotToken(provider.Id, token.AccessToken, login, ClassifyToken(token.AccessToken));
                GitHubDeviceLoginStore.Clear(provider.Id, provider.GitHubClientId);
                _statusMessage = string.IsNullOrWhiteSpace(login)
                    ? "GitHub 已连接。"
                    : $"GitHub 已连接：{login}";
                _onChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _statusMessage = ex.Message;
            }
            finally
            {
                _isBusy = false;
                IsLoading = false;
                RaiseItemsChanged();
            }
        });
    }

    private ListItem CreateMissingClientIdItem(ProviderProfile provider) => new(new CopilotProviderEditorPage(_settingsManager, provider, () => RaiseItemsChanged()))
    {
        Title = "GitHub 登录配置异常",
        Subtitle = "这个版本缺少内置 Client ID，请进入配置页填写",
        Icon = new IconInfo(""),
        Details = CreateDetails("缺少 GitHub OAuth Client ID", "普通用户通常不需要处理这个值。这个版本应当内置 Client ID；如果你 fork 了项目，也可以在配置页替换为自己的 GitHub OAuth App Client ID。"),
    };

    private ListItem CreateStatusItem(ProviderProfile provider) => new(new NoOpCommand())
    {
        Title = _statusMessage,
        Subtitle = provider.Name,
        Icon = new IconInfo(_isBusy ? "" : ""),
        Details = CreateDetails("连接 GitHub", _statusMessage),
    };

    private ListItem CreateCodeItem(ProviderProfile provider)
    {
        var deviceCode = _deviceCode!;
        return new ListItem(new CopyTextCommand(deviceCode.UserCode))
        {
            Title = $"验证码：{deviceCode.UserCode}",
            Subtitle = $"打开 {deviceCode.VerificationUri}，输入验证码后返回这里继续",
            Icon = new IconInfo(""),
            Details = CreateDetails("连接 GitHub", BuildLoginMarkdown(provider, deviceCode)),
            MoreCommands = [new CommandContextItem(new CopyTextCommand(deviceCode.UserCode)) { Title = "复制验证码" }],
        };
    }

    private ListItem CreateContinueItem() => new(new CompleteLoginCommand(this))
    {
        Title = _isBusy ? "正在确认授权..." : "我已完成授权，继续",
        Subtitle = _isBusy ? "正在等待 GitHub 返回登录结果" : "浏览器中授权完成后点击这里",
        Icon = new IconInfo(""),
        Details = CreateDetails("登录状态", _statusMessage),
    };

    private ListItem CreateCopyCodeItem() => new(new CopyTextCommand(_deviceCode?.UserCode ?? string.Empty))
    {
        Title = "复制验证码",
        Subtitle = _deviceCode?.UserCode ?? string.Empty,
        Icon = new IconInfo(""),
    };

    private ListItem CreateRestartItem() => new(new RestartLoginCommand(this))
    {
        Title = "重新获取验证码",
        Subtitle = "只有验证码过期或你想换一个验证码时使用",
        Icon = new IconInfo(""),
    };

    private static Details CreateDetails(string title, string body) => new()
    {
        Title = title,
        Body = body,
        Size = ContentSize.Large,
    };

    private static string ClassifyToken(string token)
    {
        if (token.StartsWith("gho_", StringComparison.Ordinal))
        {
            return "oauth";
        }

        if (token.StartsWith("ghu_", StringComparison.Ordinal))
        {
            return "github-app-user";
        }

        if (token.StartsWith("github_pat_", StringComparison.Ordinal))
        {
            return "fine-grained";
        }

        if (token.StartsWith("ghp_", StringComparison.Ordinal))
        {
            return "classic-pat-unsupported";
        }

        return "unknown";
    }

    private static string BuildLoginMarkdown(ProviderProfile provider, GitHubDeviceCodeResult deviceCode)
    {
        var builder = new StringBuilder();
        builder.AppendLine("### 浏览器登录 GitHub");
        builder.AppendLine();
        builder.Append("验证页面：").AppendLine(deviceCode.VerificationUri);
        builder.AppendLine();
        builder.Append("验证码：").AppendLine(deviceCode.UserCode);
        builder.AppendLine();
        builder.Append("提供商：").AppendLine(provider.Name);
        builder.Append("过期时间：").AppendLine(deviceCode.ExpiresAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        builder.AppendLine();
        builder.AppendLine("浏览器里授权完成后，回到这里选择“我已完成授权，继续”。关闭 Command Palette 后再回来也会继续显示这个未过期验证码。");
        return builder.ToString();
    }

    private sealed partial class CompleteLoginCommand : InvokableCommand
    {
        private readonly GitHubDeviceLoginPage _page;

        public CompleteLoginCommand(GitHubDeviceLoginPage page)
        {
            _page = page;
            Name = "继续";
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke()
        {
            _page.CompleteLogin();
            return CommandResult.KeepOpen();
        }
    }

    private sealed partial class RestartLoginCommand : InvokableCommand
    {
        private readonly GitHubDeviceLoginPage _page;

        public RestartLoginCommand(GitHubDeviceLoginPage page)
        {
            _page = page;
            Name = "重新获取验证码";
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke()
        {
            _page.StartLogin(resetExisting: true);
            return CommandResult.KeepOpen();
        }
    }
}
