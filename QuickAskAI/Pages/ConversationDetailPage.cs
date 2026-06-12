// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickAskAI;

internal sealed partial class ConversationDetailPage : ListPage
{
    private readonly ConversationStore _conversationStore;
    private readonly string _sessionId;
    private readonly System.Action _onChanged;

    public ConversationDetailPage(ConversationStore conversationStore, string sessionId, System.Action onChanged)
    {
        _conversationStore = conversationStore;
        _sessionId = sessionId;
        _onChanged = onChanged;
        Icon = new IconInfo("");
        Title = Session?.Title ?? ResourceHelper.GetString("ConvDetail_TitleFallback");
        Name = ResourceHelper.GetString("ConvDetail_Name");
        ShowDetails = true;
    }

    private ConversationSession? Session => _conversationStore.GetSession(_sessionId);

    public override IListItem[] GetItems()
    {
        var session = Session;
        if (session is null)
        {
            return [new ListItem(new NoOpCommand()) { Title = ResourceHelper.GetString("ConvDetail_NotFound"), Icon = new IconInfo("") }];
        }

        return [
            CreateHistoryItem(session),
            CreateSelectItem(session),
        ];
    }

    private ListItem CreateHistoryItem(ConversationSession session) => new(new NoOpCommand())
    {
        Title = session.Title,
        Subtitle = $"{session.Messages.Count} {ResourceHelper.GetString("ConvDetail_MessagesLabel")}{session.UpdatedAt:MM-dd HH:mm}",
        Icon = new IconInfo(session.Id == _conversationStore.ActiveSession.Id ? "" : ""),
        Details = new Details
        {
            Title = session.Title,
            Body = BuildHistoryMarkdown(session),
            Size = ContentSize.Large,
        },
    };

    private ListItem CreateSelectItem(ConversationSession session) => new(new SelectConversationCommand(this, session.Id))
    {
        Title = session.Id == _conversationStore.ActiveSession.Id ? ResourceHelper.GetString("ConvDetail_AlreadySelected") : ResourceHelper.GetString("ConvDetail_SelectThis"),
        Subtitle = ResourceHelper.GetString("ConvDetail_SelectSubtitle"),
        Icon = new IconInfo(""),
    };

    private void SelectConversation(string sessionId)
    {
        _conversationStore.SelectSession(sessionId);
        _onChanged();
        RaiseItemsChanged();
    }

    private static string BuildHistoryMarkdown(ConversationSession session)
    {
        if (session.Messages.Count == 0)
        {
            return ResourceHelper.GetString("ConvDetail_NoMessages");
        }

        var builder = new StringBuilder();
        foreach (var message in session.Messages)
        {
            var label = message.Role switch
            {
                "user" => ResourceHelper.GetString("ConvDetail_RoleYou"),
                "assistant" => "AI",
                "system" => ResourceHelper.GetString("ConvDetail_RoleSystem"),
                _ => message.Role,
            };

            builder.Append("### ").Append(label).AppendLine();
            builder.AppendLine();
            builder.AppendLine(message.Content);
            builder.AppendLine();
            builder.AppendLine("---");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private sealed partial class SelectConversationCommand : InvokableCommand
    {
        private readonly ConversationDetailPage _page;
        private readonly string _sessionId;

        public SelectConversationCommand(ConversationDetailPage page, string sessionId)
        {
            _page = page;
            _sessionId = sessionId;
            Name = ResourceHelper.GetString("ConvDetail_SelectCommand_Name");
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke()
        {
            _page.SelectConversation(_sessionId);
            return CommandResult.KeepOpen();
        }
    }
}
