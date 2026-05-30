// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

internal sealed partial class ProviderManagementPage : ListPage
{
    private readonly SettingsManager _settingsManager;

    public ProviderManagementPage(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        Icon = new IconInfo("");
        Title = "模型提供商";
        Name = "管理";
        ShowDetails = false;
    }

    public override IListItem[] GetItems()
    {
        var items = new System.Collections.Generic.List<IListItem>();
        foreach (var profile in _settingsManager.Profiles)
        {
            items.Add(CreateProviderItem(profile));
            items.Add(CreateEditProviderItem(profile));
        }

        items.Add(CreateAddProviderItem());
        return [.. items];
    }

    private ListItem CreateProviderItem(ProviderProfile profile)
    {
        var isActive = profile.Id == _settingsManager.ActiveProvider.Id;
        return new ListItem(new SelectProviderCommand(this, profile.Id))
        {
            Title = isActive ? $"已选择：{profile.Name}" : $"选择：{profile.Name}",
            Subtitle = $"{profile.Model} · {MaskBaseUrl(profile.BaseUrl)}",
            Icon = new IconInfo(isActive ? "" : ""),
        };
    }

    private ListItem CreateEditProviderItem(ProviderProfile profile) => new(new ProviderEditorPage(_settingsManager, profile, () => RaiseItemsChanged()))
    {
        Title = $"编辑：{profile.Name}",
        Subtitle = "修改 Base URL、API Key、模型名和系统提示词",
        Icon = new IconInfo(""),
    };

    private ListItem CreateAddProviderItem() => new(new ProviderEditorPage(_settingsManager, _settingsManager.CreateEmptyProvider(), () => RaiseItemsChanged()))
    {
        Title = "添加模型提供商",
        Subtitle = "添加新的 Base URL、API Key 和模型名",
        Icon = new IconInfo(""),
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
        private readonly ProviderManagementPage _page;
        private readonly string _providerId;

        public SelectProviderCommand(ProviderManagementPage page, string providerId)
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
