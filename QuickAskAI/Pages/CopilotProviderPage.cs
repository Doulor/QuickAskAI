// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickAskAI;

internal sealed partial class CopilotProviderPage : ListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly string _providerId;

    public CopilotProviderPage(SettingsManager settingsManager, string providerId)
    {
        _settingsManager = settingsManager;
        _providerId = providerId;
        Icon = new IconInfo("");
        Title = Provider?.Name ?? ResourceHelper.GetString("CopilotPage_TitleFallback");
        Name = ResourceHelper.GetString("CopilotPage_Name");
        ShowDetails = false;
    }

    private ProviderProfile? Provider => _settingsManager.GetProvider(_providerId);

    public override IListItem[] GetItems()
    {
        var provider = Provider;
        if (provider is null)
        {
            return [new ListItem(new NoOpCommand()) { Title = ResourceHelper.GetString("CopilotPage_NotFound"), Icon = new IconInfo("\uE713") }];
        }

        return [
            CreateSummaryItem(provider),
            CreateSelectItem(provider),
            CreateLoginItem(provider),
            CreateEditItem(provider),
            CreateDisconnectItem(provider),
        ];
    }

    private ListItem CreateSummaryItem(ProviderProfile provider) => new(new NoOpCommand())
    {
        Title = provider.Name,
        Subtitle = BuildSubtitle(provider),
        Icon = new IconInfo(provider.Id == _settingsManager.ActiveProvider.Id ? "" : ""),
    };

    private ListItem CreateSelectItem(ProviderProfile provider) => new(new SelectProviderCommand(this, provider.Id))
    {
        Title = provider.Id == _settingsManager.ActiveProvider.Id ? ResourceHelper.GetString("CopilotPage_AlreadySelected") : ResourceHelper.GetString("CopilotPage_SelectThis"),
        Subtitle = ResourceHelper.GetString("CopilotPage_SelectSubtitle"),
        Icon = new IconInfo(""),
    };

    private ListItem CreateLoginItem(ProviderProfile provider)
    {
        var isConnected = SettingsManager.HasCopilotToken(provider);
        return new ListItem(new GitHubDeviceLoginPage(_settingsManager, provider.Id, () => RaiseItemsChanged()))
        {
            Title = isConnected ? ResourceHelper.GetString("CopilotPage_ReconnectGitHub") : ResourceHelper.GetString("CopilotPage_ConnectGitHub"),
            Subtitle = isConnected ? ResourceHelper.GetString("CopilotPage_ReconnectSubtitle") : ResourceHelper.GetString("CopilotPage_ConnectSubtitle"),
            Icon = new IconInfo(""),
        };
    }

    private ListItem CreateEditItem(ProviderProfile provider) => new(new CopilotProviderEditorPage(_settingsManager, provider, () => RaiseItemsChanged()))
    {
        Title = ResourceHelper.GetString("CopilotPage_EditCopilot"),
        Subtitle = ResourceHelper.GetString("CopilotPage_EditCopilotSubtitle"),
        Icon = new IconInfo(""),
    };

    private ListItem CreateDisconnectItem(ProviderProfile provider) => new(new DisconnectCommand(this, provider.Id))
    {
        Title = ResourceHelper.GetString("CopilotPage_DisconnectGitHub"),
        Subtitle = SettingsManager.HasCopilotToken(provider) ? ResourceHelper.GetString("CopilotPage_DisconnectSubtitleHasToken") : ResourceHelper.GetString("CopilotPage_DisconnectSubtitleNoToken"),
        Icon = new IconInfo(""),
    };

    private void SelectProvider(string providerId)
    {
        _settingsManager.SelectProvider(providerId);
        RaiseItemsChanged();
    }

    private void Disconnect(string providerId)
    {
        _settingsManager.RemoveCopilotToken(providerId);
        RaiseItemsChanged();
    }

    private static string BuildSubtitle(ProviderProfile provider)
    {
        var authState = SettingsManager.HasCopilotToken(provider)
            ? string.IsNullOrWhiteSpace(provider.GitHubLogin) ? ResourceHelper.GetString("CopilotPage_GitHubConnected") : $"GitHub: {provider.GitHubLogin}"
            : ResourceHelper.GetString("CopilotPage_GitHubNotConnected");
        var clientState = string.IsNullOrWhiteSpace(provider.GitHubClientId) ? ResourceHelper.GetString("CopilotPage_ClientIdNotSet") : ResourceHelper.GetString("CopilotPage_ClientIdSet");
        return $"{provider.Model} · {authState} · {clientState}";
    }

    private sealed partial class SelectProviderCommand : InvokableCommand
    {
        private readonly CopilotProviderPage _page;
        private readonly string _providerId;

        public SelectProviderCommand(CopilotProviderPage page, string providerId)
        {
            _page = page;
            _providerId = providerId;
            Name = ResourceHelper.GetString("CopilotPage_SelectCommand_Name");
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke()
        {
            _page.SelectProvider(_providerId);
            return CommandResult.KeepOpen();
        }
    }

    private sealed partial class DisconnectCommand : InvokableCommand
    {
        private readonly CopilotProviderPage _page;
        private readonly string _providerId;

        public DisconnectCommand(CopilotProviderPage page, string providerId)
        {
            _page = page;
            _providerId = providerId;
            Name = ResourceHelper.GetString("CopilotPage_DisconnectCommand_Name");
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke()
        {
            _page.Disconnect(_providerId);
            return CommandResult.KeepOpen();
        }
    }
}
