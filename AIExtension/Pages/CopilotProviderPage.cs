// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

internal sealed partial class CopilotProviderPage : ListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly string _providerId;

    public CopilotProviderPage(SettingsManager settingsManager, string providerId)
    {
        _settingsManager = settingsManager;
        _providerId = providerId;
        Icon = new IconInfo("");
        Title = Provider?.Name ?? "GitHub Copilot";
        Name = "Copilot";
        ShowDetails = false;
    }

    private ProviderProfile? Provider => _settingsManager.GetProvider(_providerId);

    public override IListItem[] GetItems()
    {
        var provider = Provider;
        if (provider is null)
        {
            return [new ListItem(new NoOpCommand()) { Title = "提供商不存在", Icon = new IconInfo("") }];
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
        Title = provider.Id == _settingsManager.ActiveProvider.Id ? "当前已选择" : "选择这个提供商",
        Subtitle = "后续询问将使用这个 GitHub Copilot 提供商",
        Icon = new IconInfo(""),
    };

    private ListItem CreateLoginItem(ProviderProfile provider)
    {
        var isConnected = SettingsManager.HasCopilotToken(provider);
        return new ListItem(new GitHubDeviceLoginPage(_settingsManager, provider.Id, () => RaiseItemsChanged()))
        {
            Title = isConnected ? "重新连接 GitHub" : "连接 GitHub",
            Subtitle = isConnected ? "当前授权失效或需要换号时使用" : "网页登录后即可使用 GitHub Copilot",
            Icon = new IconInfo(""),
        };
    }

    private ListItem CreateEditItem(ProviderProfile provider) => new(new CopilotProviderEditorPage(_settingsManager, provider, () => RaiseItemsChanged()))
    {
        Title = "编辑 Copilot 配置",
        Subtitle = "修改名称、Client ID、模型名和系统提示词",
        Icon = new IconInfo(""),
    };

    private ListItem CreateDisconnectItem(ProviderProfile provider) => new(new DisconnectCommand(this, provider.Id))
    {
        Title = "断开 GitHub 连接",
        Subtitle = SettingsManager.HasCopilotToken(provider) ? "删除本机保存的 GitHub 授权" : "当前未保存 GitHub 授权",
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
            ? string.IsNullOrWhiteSpace(provider.GitHubLogin) ? "GitHub 已连接" : $"GitHub: {provider.GitHubLogin}"
            : "未连接 GitHub";
        var clientState = string.IsNullOrWhiteSpace(provider.GitHubClientId) ? "未填写 Client ID" : "已填写 Client ID";
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
            Name = "选择提供商";
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
            Name = "断开连接";
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke()
        {
            _page.Disconnect(_providerId);
            return CommandResult.KeepOpen();
        }
    }
}
