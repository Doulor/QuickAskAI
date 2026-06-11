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
        Title = ResourceHelper.GetString("ProvManage_Title");
        Name = ResourceHelper.GetString("ProvManage_Name");
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
            Title = isActive ? ResourceHelper.GetString("ProvManage_CurrentPrefix") + profile.Name : profile.Name,
            Subtitle = BuildProviderSubtitle(profile),
            Icon = new IconInfo(isActive ? "" : profile.ProviderType.Equals("copilot", System.StringComparison.OrdinalIgnoreCase) ? "" : ""),
        };
    }

    private ListItem CreateAddOpenAiProviderItem() => new(new ProviderEditorPage(_settingsManager, _settingsManager.CreateEmptyProvider(), () => RaiseItemsChanged()))
    {
        Title = ResourceHelper.GetString("ProvManage_AddOpenAI"),
        Subtitle = ResourceHelper.GetString("ProvManage_AddOpenAISubtitle"),
        Icon = new IconInfo(""),
    };

    private ListItem CreateAddCopilotProviderItem() => new(new CopilotProviderEditorPage(_settingsManager, SettingsManager.CreateCopilotProvider(), () => RaiseItemsChanged()))
    {
        Title = ResourceHelper.GetString("ProvManage_AddCopilot"),
        Subtitle = ResourceHelper.GetString("ProvManage_AddCopilotSubtitle"),
        Icon = new IconInfo(""),
    };

    private static string BuildProviderSubtitle(ProviderProfile profile)
    {
        if (profile.ProviderType.Equals("copilot", System.StringComparison.OrdinalIgnoreCase))
        {
            var authState = SettingsManager.HasCopilotToken(profile)
                ? string.IsNullOrWhiteSpace(profile.GitHubLogin) ? ResourceHelper.GetString("ProvManage_GitHubConnected") : $"GitHub: {profile.GitHubLogin}"
                : ResourceHelper.GetString("ProvManage_GitHubNotConnected");
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

        return string.IsNullOrWhiteSpace(value) ? ResourceHelper.GetString("ProvManage_BaseUrlNotConfigured") : value;
    }
}
