// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

public partial class AIExtensionCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settingsManager = new();
    private readonly ConversationStore _conversationStore = new();
    private readonly AiChatService _chatService = new();
    private readonly ICommandItem[] _commands;

    public AIExtensionCommandsProvider()
    {
        DisplayName = ResourceHelper.GetString("CommandsProvider_DisplayName");
        Icon = IconHelpers.FromRelativePath("Assets\\ICON.png");
        Settings = _settingsManager.Settings;
        _commands = [
            new CommandItem(new AskAiPage(_settingsManager, _conversationStore, _chatService))
            {
                Title = DisplayName,
                Icon = IconHelpers.FromRelativePath("Assets\\ICON.png"),
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

}
