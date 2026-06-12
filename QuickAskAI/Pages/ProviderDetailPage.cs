// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickAskAI;

internal sealed partial class ProviderDetailPage : ListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly string _providerId;

    public ProviderDetailPage(SettingsManager settingsManager, string providerId)
    {
        _settingsManager = settingsManager;
        _providerId = providerId;
        Icon = new IconInfo("");
        Title = Provider?.Name ?? ResourceHelper.GetString("ProvDetail_TitleFallback");
        Name = ResourceHelper.GetString("ProvDetail_Name");
        ShowDetails = false;
    }

    private ProviderProfile? Provider => _settingsManager.GetProvider(_providerId);

    public override IListItem[] GetItems()
    {
        var provider = Provider;
        if (provider is null)
        {
            return [new ListItem(new NoOpCommand()) { Title = ResourceHelper.GetString("ProvDetail_NotFound"), Icon = new IconInfo("") }];
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
        Title = provider.Id == _settingsManager.ActiveProvider.Id ? ResourceHelper.GetString("ProvDetail_AlreadySelected") : ResourceHelper.GetString("ProvDetail_SelectThis"),
        Subtitle = ResourceHelper.GetString("ProvDetail_SelectSubtitle"),
        Icon = new IconInfo(""),
    };

    private ListItem CreateEditItem(ProviderProfile provider)
    {
        var page = provider.ProviderType.Equals("copilot", System.StringComparison.OrdinalIgnoreCase)
            ? (ICommand)new CopilotProviderPage(_settingsManager, provider.Id)
            : new ProviderEditorPage(_settingsManager, provider, () => RaiseItemsChanged());
        return new ListItem(page)
        {
            Title = provider.ProviderType.Equals("copilot", System.StringComparison.OrdinalIgnoreCase) ? ResourceHelper.GetString("ProvDetail_ManageCopilot") : ResourceHelper.GetString("ProvDetail_EditProvider"),
            Subtitle = provider.ProviderType.Equals("copilot", System.StringComparison.OrdinalIgnoreCase)
                ? ResourceHelper.GetString("ProvDetail_ManageCopilotSubtitle")
                : ResourceHelper.GetString("ProvDetail_EditProviderSubtitle"),
            Icon = new IconInfo(""),
        };
    }

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

        return string.IsNullOrWhiteSpace(value) ? ResourceHelper.GetString("ProvDetail_BaseUrlNotConfigured") : value;
    }

    private sealed partial class SelectProviderCommand : InvokableCommand
    {
        private readonly ProviderDetailPage _page;
        private readonly string _providerId;

        public SelectProviderCommand(ProviderDetailPage page, string providerId)
        {
            _page = page;
            _providerId = providerId;
            Name = ResourceHelper.GetString("ProvDetail_SelectCommand_Name");
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke()
        {
            _page.SelectProvider(_providerId);
            return CommandResult.KeepOpen();
        }
    }
}
