// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

internal sealed partial class ConversationManagementPage : ListPage
{
    private readonly ConversationStore _conversationStore;
    private readonly System.Action _onChanged;

    public ConversationManagementPage(ConversationStore conversationStore, System.Action onChanged)
    {
        _conversationStore = conversationStore;
        _onChanged = onChanged;
        Icon = new IconInfo("");
        Title = "会话";
        Name = "会话";
        ShowDetails = false;
    }

    public override IListItem[] GetItems()
    {
        var unusedSession = _conversationStore.Sessions.FirstOrDefault(session => session.Messages.Count == 0);
        var newSessionTitle = unusedSession is null ? "开始新的会话" : "切换到空白新会话";
        var newSessionSubtitle = unusedSession is null
            ? "清空上下文，开启一轮新的连续对话"
            : "已经有一个未使用的新会话，不会重复创建";

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
                Title = isActive ? $"当前：{session.Title}" : session.Title,
                Subtitle = $"{session.Messages.Count} 条消息 · {session.UpdatedAt:MM-dd HH:mm}",
                Icon = new IconInfo(isActive ? "" : ""),
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
            Name = "开始新的会话";
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke()
        {
            _page.StartNewConversation();
            return CommandResult.KeepOpen();
        }
    }
}
