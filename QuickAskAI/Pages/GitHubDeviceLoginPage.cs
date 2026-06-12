// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickAskAI;

internal sealed partial class GitHubDeviceLoginPage : ListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly CopilotAuthService _authService;
    private readonly string _providerId;
    private readonly Action? _onChanged;
    private GitHubDeviceCodeResult? _deviceCode;
    private string _statusMessage = ResourceHelper.GetString("GitHubLogin_Preparing");
    private bool _isBusy;

    public GitHubDeviceLoginPage(SettingsManager settingsManager, string providerId, Action? onChanged = null)
    {
        _settingsManager = settingsManager;
        _authService = new CopilotAuthService();
        _providerId = providerId;
        _onChanged = onChanged;

        Icon = new IconInfo("");
        Title = ResourceHelper.GetString("GitHubLogin_Title");
        Name = ResourceHelper.GetString("GitHubLogin_Name");
        ShowDetails = true;

        RestoreOrStartLogin();
    }

    private ProviderProfile? Provider => _settingsManager.GetProvider(_providerId);

    public override IListItem[] GetItems()
    {
        var provider = Provider;
        if (provider is null)
        {
            return [new ListItem(new NoOpCommand()) { Title = ResourceHelper.GetString("GitHubLogin_NotFound"), Icon = new IconInfo("\uE713") }];
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
            _statusMessage = ResourceHelper.GetString("GitHubLogin_ContinuePrevious");
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
        _statusMessage = ResourceHelper.GetString("GitHubLogin_FetchingCode");
        RaiseItemsChanged();

        _ = Task.Run(async () =>
        {
            try
            {
                _deviceCode = await _authService.StartDeviceLoginAsync(provider.GitHubClientId).ConfigureAwait(false);
                GitHubDeviceLoginStore.Save(provider.Id, provider.GitHubClientId, _deviceCode);
                _statusMessage = ResourceHelper.GetString("GitHubLogin_EnterCode");
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
        _statusMessage = ResourceHelper.GetString("GitHubLogin_WaitingAuth");
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
                    ? ResourceHelper.GetString("GitHubLogin_Connected")
                    : ResourceHelper.GetString("GitHubLogin_ConnectedWith") + login;
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
        Title = ResourceHelper.GetString("GitHubLogin_ConfigError"),
        Subtitle = ResourceHelper.GetString("GitHubLogin_ConfigErrorSubtitle"),
        Icon = new IconInfo(""),
        Details = CreateDetails(ResourceHelper.GetString("GitHubLogin_MissingClientId"), ResourceHelper.GetString("GitHubLogin_MissingClientIdDetail")),
    };

    private ListItem CreateStatusItem(ProviderProfile provider) => new(new NoOpCommand())
    {
        Title = _statusMessage,
        Subtitle = provider.Name,
        Icon = new IconInfo(_isBusy ? "" : ""),
        Details = CreateDetails(ResourceHelper.GetString("GitHubLogin_Title"), _statusMessage),
    };

    private ListItem CreateCodeItem(ProviderProfile provider)
    {
        var deviceCode = _deviceCode!;
        return new ListItem(new CopyTextCommand(deviceCode.UserCode))
        {
            Title = ResourceHelper.GetString("GitHubLogin_VerificationCode") + deviceCode.UserCode,
            Subtitle = ResourceHelper.GetString("GitHubLogin_CodeSubtitle") + deviceCode.VerificationUri + ResourceHelper.GetString("GitHubLogin_CodeSubtitleSuffix"),
            Icon = new IconInfo(""),
            Details = CreateDetails(ResourceHelper.GetString("GitHubLogin_Title"), BuildLoginMarkdown(provider, deviceCode)),
            MoreCommands = [new CommandContextItem(new CopyTextCommand(deviceCode.UserCode)) { Title = ResourceHelper.GetString("GitHubLogin_CopyCode") }],
        };
    }

    private ListItem CreateContinueItem() => new(new CompleteLoginCommand(this))
    {
        Title = _isBusy ? ResourceHelper.GetString("GitHubLogin_ConfirmingAuth") : ResourceHelper.GetString("GitHubLogin_IHaveCompleted"),
        Subtitle = _isBusy ? ResourceHelper.GetString("GitHubLogin_WaitingForResult") : ResourceHelper.GetString("GitHubLogin_AuthCompletedClickHere"),
        Icon = new IconInfo(""),
        Details = CreateDetails(ResourceHelper.GetString("GitHubLogin_LoginStatus"), _statusMessage),
    };

    private ListItem CreateCopyCodeItem() => new(new CopyTextCommand(_deviceCode?.UserCode ?? string.Empty))
    {
        Title = ResourceHelper.GetString("GitHubLogin_CopyCode"),
        Subtitle = _deviceCode?.UserCode ?? string.Empty,
        Icon = new IconInfo(""),
    };

    private ListItem CreateRestartItem() => new(new RestartLoginCommand(this))
    {
        Title = ResourceHelper.GetString("GitHubLogin_RestartCode"),
        Subtitle = ResourceHelper.GetString("GitHubLogin_RestartCodeSubtitle"),
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
        builder.AppendLine(ResourceHelper.GetString("GitHubLogin_BrowserLoginGitHub"));
        builder.AppendLine();
        builder.Append(ResourceHelper.GetString("GitHubLogin_VerificationPage")).AppendLine(deviceCode.VerificationUri);
        builder.AppendLine();
        builder.Append(ResourceHelper.GetString("GitHubLogin_VerificationCodeLabel")).AppendLine(deviceCode.UserCode);
        builder.AppendLine();
        builder.Append(ResourceHelper.GetString("GitHubLogin_ProviderLabel")).AppendLine(provider.Name);
        builder.Append(ResourceHelper.GetString("GitHubLogin_ExpiresAt")).AppendLine(deviceCode.ExpiresAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        builder.AppendLine();
        builder.AppendLine(ResourceHelper.GetString("GitHubLogin_ReturnAfterAuth"));
        return builder.ToString();
    }

    private sealed partial class CompleteLoginCommand : InvokableCommand
    {
        private readonly GitHubDeviceLoginPage _page;

        public CompleteLoginCommand(GitHubDeviceLoginPage page)
        {
            _page = page;
            Name = ResourceHelper.GetString("GitHubLogin_ContinueCommand_Name");
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
            Name = ResourceHelper.GetString("GitHubLogin_RestartCommand_Name");
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke()
        {
            _page.StartLogin(resetExisting: true);
            return CommandResult.KeepOpen();
        }
    }
}
