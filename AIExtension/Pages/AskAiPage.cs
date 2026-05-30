// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

internal sealed partial class AskAiPage : ContentPage
{
    private readonly SettingsManager _settingsManager;
    private readonly AiChatService _chatService;
    private readonly AskAiForm _form;
    private int _requestVersion;
    private AiChatResponse? _lastResponse;
    private bool _isBusy;

    public AskAiPage(SettingsManager settingsManager, AiChatService chatService)
    {
        _settingsManager = settingsManager;
        _chatService = chatService;
        _form = new AskAiForm(this);

        Icon = new IconInfo("");
        Title = "快速询问AI";
        Name = "询问";
    }

    public override IContent[] GetContent() =>
    [
        new MarkdownContent(BuildStatusMarkdown()),
        _form,
    ];

    internal ICommandResult SubmitPrompt(string prompt)
    {
        prompt = prompt.Trim();
        var request = _settingsManager.CreateRequest(prompt);
        var validationError = AiChatService.Validate(request);

        if (validationError is not null)
        {
            ShowCompletedResult(AiChatResponse.Failure(validationError, request.Model));
            return CommandResult.KeepOpen();
        }

        var requestVersion = Interlocked.Increment(ref _requestVersion);
        _lastResponse = null;
        _isBusy = true;
        IsLoading = true;
        Commands = [];
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
                ShowCompletedResult(requestVersion, response);
            }
            catch (Exception ex)
            {
                ShowCompletedResult(
                    requestVersion,
                    AiChatResponse.Failure($"请求处理失败：{ex.Message}", request.Model));
            }
            finally
            {
                ExtensionHost.HideStatus(status);
            }
        });

        return CommandResult.KeepOpen();
    }

    private void ShowCompletedResult(AiChatResponse response)
    {
        Interlocked.Increment(ref _requestVersion);
        ApplyCompletedResult(response);
    }

    private void ShowCompletedResult(int requestVersion, AiChatResponse response)
    {
        if (requestVersion != _requestVersion)
        {
            return;
        }

        ApplyCompletedResult(response);
    }

    private void ApplyCompletedResult(AiChatResponse response)
    {
        _lastResponse = response;
        _isBusy = false;
        IsLoading = false;
        Commands = response.IsSuccess
            ? [new CommandContextItem(new CopyTextCommand(response.Content)) { Title = "复制回答" }]
            : [];
        RaiseItemsChanged();
    }

    private string BuildStatusMarkdown()
    {
        if (_isBusy)
        {
            return "## 正在询问AI\n\n请稍候，回答生成后会自动显示在这里。";
        }

        if (_lastResponse is null)
        {
            return "## 快速询问AI\n\n在下方输入问题后提交。";
        }

        if (_lastResponse.IsSuccess)
        {
            return $"## 回答\n\n{_lastResponse.Content}\n\n---\n\n模型：{EscapeInline(_lastResponse.Model)}\n\n服务：{EscapeInline(_lastResponse.Endpoint)}";
        }

        return $"## 请求失败\n\n{CodeBlock(_lastResponse.ErrorMessage)}";
    }

    private static string EscapeInline(string value) => string.IsNullOrWhiteSpace(value)
        ? "未提供"
        : value.Replace("`", "\\`");

    private static string CodeBlock(string value)
    {
        var safeValue = string.IsNullOrWhiteSpace(value) ? "未知错误。" : value.Replace("```", "` ` `");
        return $"```text\n{safeValue}\n```";
    }

    private sealed partial class AskAiForm : FormContent
    {
        private readonly AskAiPage _page;

        public AskAiForm(AskAiPage page)
        {
            _page = page;
            TemplateJson = """
            {
              "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
              "type": "AdaptiveCard",
              "version": "1.5",
              "body": [
                {
                  "type": "Input.Text",
                  "id": "Prompt",
                  "label": "问题",
                  "placeholder": "输入你想询问的内容",
                  "isRequired": true,
                  "errorMessage": "请输入问题",
                  "isMultiline": true
                }
              ],
              "actions": [
                {
                  "type": "Action.Submit",
                  "title": "询问"
                }
              ]
            }
            """;
        }

        public override ICommandResult SubmitForm(string inputs)
        {
            try
            {
                var formInput = JsonNode.Parse(inputs)?.AsObject();
                var prompt = formInput?["Prompt"]?.ToString() ?? string.Empty;
                return _page.SubmitPrompt(prompt);
            }
            catch (JsonException)
            {
                return CommandResult.ShowToast("无法读取表单内容，请重试。");
            }
        }
    }
}
