// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

internal sealed partial class AIExtensionPage : ListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly AiChatService _chatService;

    public AIExtensionPage(SettingsManager settingsManager, AiChatService chatService)
    {
        _settingsManager = settingsManager;
        _chatService = chatService;

        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "快速询问AI";
        Name = "打开";
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new AskAiPage(_settingsManager, _chatService))
            {
                Title = "询问 AI",
                Subtitle = "使用设置中的 Base URL 和 API Key 调用 OpenAI-compatible 接口",
                Icon = new IconInfo(""),
            },
        ];
    }
}
