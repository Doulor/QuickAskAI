// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
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
        foreach (var profile in _settingsManager.Profiles
            .OrderByDescending(profile => profile.Id == _settingsManager.ActiveProvider.Id)
            .ThenBy(profile => profile.Name))
        {
            items.Add(CreateProviderItem(profile));
        }

        items.Add(CreateAddOpenAiProviderItem());
        items.Add(CreateAddCopilotProviderItem());
        return [.. items];
    }

    private ListItem CreateProviderItem(ProviderProfile profile)
    {
        var isActive = profile.Id == _settingsManager.ActiveProvider.Id;
        var page = profile.ProviderType.Equals("copilot", System.StringComparison.OrdinalIgnoreCase)
            ? (ICommand)new CopilotProviderPage(_settingsManager, profile.Id)
            : new ProviderDetailPage(_settingsManager, profile.Id);
        return new ListItem(page)
        {
            Title = isActive ? $"当前：{profile.Name}" : profile.Name,
            Subtitle = BuildProviderSubtitle(profile),
            Icon = new IconInfo(isActive ? "" : profile.ProviderType.Equals("copilot", System.StringComparison.OrdinalIgnoreCase) ? "" : ""),
        };
    }

    private ListItem CreateAddOpenAiProviderItem() => new(new ProviderEditorPage(_settingsManager, _settingsManager.CreateEmptyProvider(), () => RaiseItemsChanged()))
    {
        Title = "添加 OpenAI 兼容提供商",
        Subtitle = "添加新的 Base URL、API Key 和模型名",
        Icon = new IconInfo(""),
    };

    private ListItem CreateAddCopilotProviderItem() => new(new CopilotProviderEditorPage(_settingsManager, SettingsManager.CreateCopilotProvider(), () => RaiseItemsChanged()))
    {
        Title = "添加 GitHub Copilot 提供商",
        Subtitle = "使用 GitHub 网页登录连接 Copilot",
        Icon = new IconInfo(""),
    };

    private static string BuildProviderSubtitle(ProviderProfile profile)
    {
        if (profile.ProviderType.Equals("copilot", System.StringComparison.OrdinalIgnoreCase))
        {
            var authState = SettingsManager.HasCopilotToken(profile)
                ? string.IsNullOrWhiteSpace(profile.GitHubLogin) ? "GitHub 已连接" : $"GitHub: {profile.GitHubLogin}"
                : "未连接 GitHub";
            return $"{profile.Model} · {authState}";
        }

        return $"{profile.Model} · {MaskBaseUrl(profile.BaseUrl)}";
    }

    private static string MaskBaseUrl(string value)
    {
        if (System.Uri.TryCreate(value, System.UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return string.IsNullOrWhiteSpace(value) ? "未配置 Base URL" : value;
    }
}
