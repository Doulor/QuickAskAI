// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

internal sealed partial class AskAiPage : DynamicListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly AiChatService _chatService;
    private int _requestVersion;
    private string _lastPrompt = string.Empty;
    private AiChatResponse? _lastResponse;
    private bool _isBusy;

    public AskAiPage(SettingsManager settingsManager, AiChatService chatService)
    {
        _settingsManager = settingsManager;
        _chatService = chatService;

        Icon = new IconInfo("");
        Title = "快速询问AI";
        Name = "询问";
        PlaceholderText = "输入问题，按 Enter 询问 AI";
        ShowDetails = true;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        var query = SearchText.Trim();

        if (_isBusy)
        {
            return [CreateBusyItem()];
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            return [CreateAskItem(query)];
        }

        if (_lastResponse is not null)
        {
            return [CreateResultItem(_lastResponse)];
        }

        return [CreateHelpItem()];
    }

    internal ICommandResult SubmitPrompt(string prompt)
    {
        prompt = prompt.Trim();
        var request = _settingsManager.CreateRequest(prompt);
        var validationError = AiChatService.Validate(request);

        if (validationError is not null)
        {
            ShowCompletedResult(prompt, AiChatResponse.Failure(validationError, request.Model));
            return CommandResult.KeepOpen();
        }

        var requestVersion = Interlocked.Increment(ref _requestVersion);
        _lastPrompt = prompt;
        _lastResponse = null;
        _isBusy = true;
        IsLoading = true;
        RaiseItemsChanged();

        var status = new StatusMessage
        {
            Message = "正在询问 AI...",
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
                    AiChatResponse.Failure($"请求处理失败：{ex.Message}", request.Model));
            }
            finally
            {
                ExtensionHost.HideStatus(status);
            }
        });

        return CommandResult.KeepOpen();
    }

    private ListItem CreateAskItem(string query)
    {
        return new ListItem(new SubmitPromptCommand(this, query))
        {
            Title = $"询问：{Preview(query, 80)}",
            Subtitle = "按 Enter 发送",
            Icon = new IconInfo(""),
            Details = new Details
            {
                Title = "将发送的问题",
                Body = query,
                Size = ContentSize.Medium,
            },
        };
    }

    private ListItem CreateBusyItem()
    {
        return new ListItem(new NoOpCommand())
        {
            Title = "正在询问 AI...",
            Subtitle = Preview(_lastPrompt, 100),
            Icon = new IconInfo(""),
            Details = new Details
            {
                Title = "正在生成回答",
                Body = _lastPrompt,
                Size = ContentSize.Medium,
            },
        };
    }

    private static ListItem CreateResultItem(AiChatResponse response)
    {
        var title = response.IsSuccess ? "回答" : "请求失败";
        var body = response.IsSuccess
            ? $"{response.Content}\n\n---\n\n模型：{EscapeInline(response.Model)}\n\n服务：{EscapeInline(response.Endpoint)}"
            : CodeBlock(response.ErrorMessage);

        return new ListItem(response.IsSuccess ? new CopyTextCommand(response.Content) : new NoOpCommand())
        {
            Title = title,
            Subtitle = response.IsSuccess ? "输入新问题可继续询问；按 Enter 可复制回答" : "输入新问题后按 Enter 重试",
            Icon = new IconInfo(response.IsSuccess ? "" : ""),
            Details = new Details
            {
                Title = title,
                Body = body,
                Size = ContentSize.Large,
            },
            MoreCommands = response.IsSuccess
                ? [new CommandContextItem(new CopyTextCommand(response.Content)) { Title = "复制回答" }]
                : [],
        };
    }

    private static ListItem CreateHelpItem()
    {
        return new ListItem(new NoOpCommand())
        {
            Title = "直接输入问题",
            Subtitle = "上方输入框默认选中，输入后按 Enter 询问 AI",
            Icon = new IconInfo(""),
            Details = new Details
            {
                Title = "快速询问AI",
                Body = "在顶部输入框输入问题，然后按 Enter。首次使用前请在扩展设置里填写 Base URL、API Key 和模型名。",
                Size = ContentSize.Medium,
            },
        };
    }

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
        _isBusy = false;
        IsLoading = false;
        if (SearchText.Trim().Equals(prompt, StringComparison.Ordinal))
        {
            SetSearchNoUpdate(string.Empty);
            OnPropertyChanged(nameof(SearchText));
        }

        RaiseItemsChanged();
    }

    private static string Preview(string value, int maxLength) => value.Length <= maxLength
        ? value
        : string.Concat(value.AsSpan(0, maxLength), "...");

    private static string EscapeInline(string value) => string.IsNullOrWhiteSpace(value)
        ? "未提供"
        : value.Replace("`", "\\`");

    private static string CodeBlock(string value)
    {
        var safeValue = string.IsNullOrWhiteSpace(value) ? "未知错误。" : value.Replace("```", "` ` `");
        return $"```text\n{safeValue}\n```";
    }

    private sealed partial class SubmitPromptCommand : InvokableCommand
    {
        private readonly AskAiPage _page;
        private readonly string _prompt;

        public SubmitPromptCommand(AskAiPage page, string prompt)
        {
            _page = page;
            _prompt = prompt;
            Name = "询问";
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke() => _page.SubmitPrompt(_prompt);
    }
}
