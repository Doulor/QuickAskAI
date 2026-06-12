// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickAskAI;

internal sealed partial class ConversationManagementPage : ListPage
{
    private readonly ConversationStore _conversationStore;
    private readonly System.Action _onChanged;

    public ConversationManagementPage(ConversationStore conversationStore, System.Action onChanged)
    {
        _conversationStore = conversationStore;
        _onChanged = onChanged;
        Icon = new IconInfo("");
        Title = ResourceHelper.GetString("ConvManage_Title");
        Name = ResourceHelper.GetString("ConvManage_Name");
        ShowDetails = false;
    }

    public override IListItem[] GetItems()
    {
        var unusedSession = _conversationStore.Sessions.FirstOrDefault(session => session.Messages.Count == 0);
        var newSessionTitle = unusedSession is null ? ResourceHelper.GetString("ConvManage_StartNew") : ResourceHelper.GetString("ConvManage_SwitchToEmpty");
        var newSessionSubtitle = unusedSession is null
            ? ResourceHelper.GetString("ConvManage_NewSubtitle1")
            : ResourceHelper.GetString("ConvManage_NewSubtitle2");

        var items = new System.Collections.Generic.List<IListItem>
        {
            new ListItem(new NewConversationCommand(this))
            {
                Title = newSessionTitle,
                Subtitle = newSessionSubtitle,
                Icon = new IconInfo(""),
            },
        };

        foreach (var session in _conversationStore.Sessions)
        {
            var isActive = session.Id == _conversationStore.ActiveSession.Id;
            items.Add(new ListItem(new ConversationDetailPage(_conversationStore, session.Id, _onChanged))
            {
                Title = isActive ? ResourceHelper.GetString("ConvManage_CurrentPrefix") + session.Title : session.Title,
                Subtitle = $"{session.Messages.Count} {ResourceHelper.GetString("ConvManage_MessagesLabel")}{session.UpdatedAt:MM-dd HH:mm}",
                Icon = new IconInfo(isActive ? "" : ""),
                MoreCommands = [
                    new CommandContextItem(new DeleteConversationCommand(this, session.Id)),
                ],
            });
        }

        return [.. items];
    }

    private void StartNewConversation()
    {
        _conversationStore.StartNewSession();
        _onChanged();
        RaiseItemsChanged();
    }

    private sealed partial class NewConversationCommand : InvokableCommand
    {
        private readonly ConversationManagementPage _page;

        public NewConversationCommand(ConversationManagementPage page)
        {
            _page = page;
            Name = ResourceHelper.GetString("ConvManage_NewCommand_Name");
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke()
        {
            _page.StartNewConversation();
            return CommandResult.KeepOpen();
        }
    }

    private sealed partial class DeleteConversationCommand : InvokableCommand
    {
        private readonly ConversationManagementPage _page;
        private readonly string _sessionId;

        public DeleteConversationCommand(ConversationManagementPage page, string sessionId)
        {
            _page = page;
            _sessionId = sessionId;
            Name = ResourceHelper.GetString("ConvManage_DeleteCommand_Name");
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke()
        {
            _page.DeleteConversation(_sessionId);
            return CommandResult.KeepOpen();
        }
    }

    private void DeleteConversation(string sessionId)
    {
        _conversationStore.DeleteSession(sessionId);
        _onChanged();
        RaiseItemsChanged();
    }
}
