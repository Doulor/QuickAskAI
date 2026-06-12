// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickAskAI;

internal sealed partial class AskAiPage : DynamicListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly ConversationStore _conversationStore;
    private readonly AiChatService _chatService;
    private int _requestVersion;
    private string _lastPrompt = string.Empty;
    private AiChatResponse? _lastResponse;
    private bool _isBusy;

    public AskAiPage(SettingsManager settingsManager, ConversationStore conversationStore, AiChatService chatService)
    {
        _settingsManager = settingsManager;
        _conversationStore = conversationStore;
        _chatService = chatService;
        _settingsManager.ProvidersChanged += OnProvidersChanged;
        _conversationStore.EnsureUnusedSessionActive();

        Icon = IconHelpers.FromRelativePath("Assets\\ICON.png");
        Title = ResourceHelper.GetString("AskAi_Title");
        Name = ResourceHelper.GetString("AskAi_Name");
        PlaceholderText = ResourceHelper.GetString("AskAi_Placeholder");
        ShowDetails = true;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        var query = SearchText.Trim();
        if (!string.IsNullOrWhiteSpace(query) && !_isBusy)
        {
            return BuildQueryItems(query);
        }

        return BuildHomeItems();
    }

    internal ICommandResult SubmitPrompt(string prompt)
    {
        prompt = prompt.Trim();
        var messages = _conversationStore.ActiveSession.Messages
            .Concat([new ChatMessage { Role = "user", Content = prompt }])
            .ToArray();
        var request = _settingsManager.CreateRequest(prompt, messages);
        var validationError = AiChatService.Validate(request);

        if (validationError is not null)
        {
            ShowCompletedResult(prompt, AiChatResponse.Failure(validationError, request.Model));
            return CommandResult.KeepOpen();
        }

        var requestVersion = Interlocked.Increment(ref _requestVersion);
        _conversationStore.AddUserMessage(prompt);
        _lastPrompt = prompt;
        _lastResponse = null;
        _isBusy = true;
        IsLoading = true;
        RaiseItemsChanged();

        var status = new StatusMessage
        {
            Message = ResourceHelper.GetString("AskAi_StatusMessage"),
            State = MessageState.Info,
            Progress = new ProgressState { IsIndeterminate = true },
        };

        ExtensionHost.ShowStatus(status, StatusContext.Page);

        _ = Task.Run(async () =>
        {
            try
            {
                var response = await _chatService.AskAsync(request).ConfigureAwait(false);
                ShowCompletedResult(requestVersion, prompt, response);
            }
            catch (Exception ex)
            {
                ShowCompletedResult(
                    requestVersion,
                    prompt,
                    AiChatResponse.Failure(ResourceHelper.GetString("AskAi_RequestFailed") + ex.Message, request.Model));
            }
            finally
            {
                ExtensionHost.HideStatus(status);
            }
        });

        return CommandResult.KeepOpen();
    }

    private IListItem[] BuildQueryItems(string query) =>
    [
        new ListItem(new SubmitPromptCommand(this, query))
        {
            Title = ResourceHelper.GetString("AskAi_AskTitle") + Preview(query, 80),
            Subtitle = ResourceHelper.GetString("AskAi_UsingProvider") + _settingsManager.ActiveProvider.Name + " / " + _settingsManager.ActiveProvider.Model,
            Icon = new IconInfo(""),
        },
    ];

    private IListItem[] BuildHomeItems()
    {
        var items = new System.Collections.Generic.List<IListItem>();

        if (_isBusy)
        {
            items.Add(CreateBusyItem());
        }
        else if (_lastResponse is not null)
        {
            items.Add(CreateResultFocusItem());
        }
        else
        {
            items.Add(CreateHelpItem());
        }

        items.Add(CreateConversationEntryItem());
        items.Add(CreateProviderManagementItem());

        return [.. items];
    }


    private ListItem CreateConversationEntryItem()
    {
        var session = _conversationStore.ActiveSession;
        return new ListItem(new ConversationManagementPage(_conversationStore, RefreshFromActiveSession))
        {
            Title = ResourceHelper.GetString("AskAi_ConversationTitle") + session.Title,
            Subtitle = $"{session.Messages.Count} {ResourceHelper.GetString("AskAi_ConversationSubtitle")}",
            Icon = new IconInfo(""),
        };
    }

    private ListItem CreateProviderManagementItem()
    {
        var provider = _settingsManager.ActiveProvider;
        return new ListItem(new ProviderManagementPage(_settingsManager))
        {
            Title = ResourceHelper.GetString("AskAi_ProviderTitle") + provider.Name,
            Subtitle = BuildProviderSubtitle(provider),
            Icon = new IconInfo(""),
        };
    }

    private ListItem CreateBusyItem() => new(new NoOpCommand())
    {
        Title = ResourceHelper.GetString("AskAi_BusyTitle"),
        Subtitle = Preview(_lastPrompt, 100),
        Icon = new IconInfo(""),
    };

    private ListItem CreateResultFocusItem()
    {
        if (_lastResponse is null)
        {
            return CreateHelpItem();
        }

        var response = _lastResponse;
        var body = response.IsSuccess
            ? response.Content + "\n\n---\n\n" + ResourceHelper.GetString("AskAi_ModelLabel") + EscapeInline(response.Model) + "\n\n" + ResourceHelper.GetString("AskAi_ServiceLabel") + EscapeInline(response.Endpoint)
            : CodeBlock(response.ErrorMessage);

        return new ListItem(response.IsSuccess ? new CopyTextCommand(response.Content) : new NoOpCommand())
        {
            Title = response.IsSuccess ? ResourceHelper.GetString("AskAi_AnswerTitle") : ResourceHelper.GetString("AskAi_RequestFailedTitle"),
            Subtitle = response.IsSuccess ? ResourceHelper.GetString("AskAi_AnswerSubtitle") : ResourceHelper.GetString("AskAi_ErrorSubtitle"),
            Icon = new IconInfo(response.IsSuccess ? "" : ""),
            Details = new Details
            {
                Title = response.IsSuccess ? ResourceHelper.GetString("AskAi_AnswerTitle") : ResourceHelper.GetString("AskAi_RequestFailedTitle"),
                Body = body,
                Size = ContentSize.Large,
            },
            MoreCommands = response.IsSuccess
                ? [new CommandContextItem(new CopyTextCommand(response.Content)) { Title = ResourceHelper.GetString("AskAi_CopyAnswer") }]
                : [],
        };
    }

    private static string EscapeInline(string value) => string.IsNullOrWhiteSpace(value)
        ? ResourceHelper.GetString("AskAi_NotProvided")
        : value.Replace("`", "\\`");

    private static string CodeBlock(string value)
    {
        var safeValue = string.IsNullOrWhiteSpace(value) ? ResourceHelper.GetString("AskAi_UnknownError") : value.Replace("```", "` ` `");
        return $"```text\n{safeValue}\n```";
    }

    private static ListItem CreateHelpItem() => new(new NoOpCommand())
    {
        Title = ResourceHelper.GetString("AskAi_HelpTitle"),
        Subtitle = ResourceHelper.GetString("AskAi_HelpSubtitle"),
        Icon = new IconInfo(""),
    };

    private void ShowCompletedResult(string prompt, AiChatResponse response)
    {
        Interlocked.Increment(ref _requestVersion);
        ApplyCompletedResult(prompt, response);
    }

    private void ShowCompletedResult(int requestVersion, string prompt, AiChatResponse response)
    {
        if (requestVersion != _requestVersion)
        {
            return;
        }

        ApplyCompletedResult(prompt, response);
    }

    private void ApplyCompletedResult(string prompt, AiChatResponse response)
    {
        _lastPrompt = prompt;
        _lastResponse = response;
        if (response.IsSuccess)
        {
            _conversationStore.AddAssistantMessage(response.Content);
        }
        else
        {
            _conversationStore.AddErrorMessage(response.ErrorMessage);
        }

        _isBusy = false;
        IsLoading = false;
        if (SearchText.Trim().Equals(prompt, StringComparison.Ordinal))
        {
            SetSearchNoUpdate(string.Empty);
            OnPropertyChanged(nameof(SearchText));
        }

        RaiseItemsChanged();
    }

    private void RefreshFromActiveSession()
    {
        var lastAssistantMessage = _conversationStore.ActiveSession.Messages.LastOrDefault(message => message.Role == "assistant");
        _lastPrompt = _conversationStore.ActiveSession.Messages.LastOrDefault(message => message.Role == "user")?.Content ?? string.Empty;
        _lastResponse = lastAssistantMessage is null
            ? null
            : AiChatResponse.Success(lastAssistantMessage.Content, _settingsManager.Model, _settingsManager.ActiveProvider.Name);
        _isBusy = false;
        IsLoading = false;
        RaiseItemsChanged();
    }

    private void OnProvidersChanged(object? sender, EventArgs e)
    {
        RaiseItemsChanged();
    }

    private static string Preview(string value, int maxLength) => value.Length <= maxLength
        ? value
        : value[..maxLength] + "...";

    private static string BuildProviderSubtitle(ProviderProfile provider)
    {
        if (provider.ProviderType.Equals("copilot", StringComparison.OrdinalIgnoreCase))
        {
            var authState = SettingsManager.HasCopilotToken(provider)
                ? string.IsNullOrWhiteSpace(provider.GitHubLogin) ? ResourceHelper.GetString("AskAi_GitHubConnected") : string.Format(ResourceHelper.GetString("AskAi_GitHubWithLogin"), provider.GitHubLogin)
                : ResourceHelper.GetString("AskAi_GitHubNotConnected");
            return $"{provider.Model} · {authState} · {ResourceHelper.GetString("AskAi_ProviderEnterToAdd")}";
        }

        return $"{provider.Model} · {MaskBaseUrl(provider.BaseUrl)} · {ResourceHelper.GetString("AskAi_ProviderEnterToManage")}";
    }

    private static string MaskBaseUrl(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return string.IsNullOrWhiteSpace(value) ? ResourceHelper.GetString("AskAi_BaseUrlNotConfigured") : value;
    }

    private sealed partial class SubmitPromptCommand : InvokableCommand
    {
        private readonly AskAiPage _page;
        private readonly string _prompt;

        public SubmitPromptCommand(AskAiPage page, string prompt)
        {
            _page = page;
            _prompt = prompt;
            Name = ResourceHelper.GetString("AskAi_SubmitCommand_Name");
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke() => _page.SubmitPrompt(_prompt);
    }
}
