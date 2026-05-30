// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

internal sealed partial class ProviderDetailPage : ListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly string _providerId;

    public ProviderDetailPage(SettingsManager settingsManager, string providerId)
    {
        _settingsManager = settingsManager;
        _providerId = providerId;
        Icon = new IconInfo("");
        Title = Provider?.Name ?? "模型提供商";
        Name = "提供商";
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
            CreateEditItem(provider),
        ];
    }

    private ListItem CreateSummaryItem(ProviderProfile provider) => new(new NoOpCommand())
    {
        Title = provider.Name,
        Subtitle = $"{provider.Model} · {MaskBaseUrl(provider.BaseUrl)}",
        Icon = new IconInfo(provider.Id == _settingsManager.ActiveProvider.Id ? "" : ""),
    };

    private ListItem CreateSelectItem(ProviderProfile provider) => new(new SelectProviderCommand(this, provider.Id))
    {
        Title = provider.Id == _settingsManager.ActiveProvider.Id ? "当前已选择" : "选择这个提供商",
        Subtitle = "后续询问将使用这个提供商",
        Icon = new IconInfo(""),
    };

    private ListItem CreateEditItem(ProviderProfile provider) => new(new ProviderEditorPage(_settingsManager, provider, () => RaiseItemsChanged()))
    {
        Title = "编辑提供商",
        Subtitle = "修改 Base URL、API Key、模型名和系统提示词",
        Icon = new IconInfo(""),
    };

    private void SelectProvider(string providerId)
    {
        _settingsManager.SelectProvider(providerId);
        RaiseItemsChanged();
    }

    private static string MaskBaseUrl(string value)
    {
        if (System.Uri.TryCreate(value, System.UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return string.IsNullOrWhiteSpace(value) ? "未配置 Base URL" : value;
    }

    private sealed partial class SelectProviderCommand : InvokableCommand
    {
        private readonly ProviderDetailPage _page;
        private readonly string _providerId;

        public SelectProviderCommand(ProviderDetailPage page, string providerId)
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
}
