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
        ShowDetails = false;
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

    private IListItem[] BuildQueryItems(string query) =>
    [
        new ListItem(new SubmitPromptCommand(this, query))
        {
            Title = $"询问：{Preview(query, 80)}",
            Subtitle = $"使用 {_settingsManager.ActiveProvider.Name} / {_settingsManager.ActiveProvider.Model}",
            Icon = new IconInfo(""),
        },
    ];

    private IListItem[] BuildHomeItems()
    {
        var items = new System.Collections.Generic.List<IListItem>
        {
            CreateProviderSummaryItem(),
        };

        foreach (var profile in _settingsManager.Profiles)
        {
            items.Add(CreateProviderItem(profile));
            items.Add(CreateEditProviderItem(profile));
        }

        items.Add(CreateAddProviderItem());

        if (_isBusy)
        {
            items.Add(CreateBusyItem());
        }
        else if (_lastResponse is not null)
        {
            items.Add(CreateResultItem(_lastResponse));
        }
        else
        {
            items.Add(CreateHelpItem());
        }

        return [.. items];
    }

    private ListItem CreateProviderSummaryItem()
    {
        var provider = _settingsManager.ActiveProvider;
        return new ListItem(new NoOpCommand())
        {
            Title = $"当前提供商：{provider.Name}",
            Subtitle = $"{provider.Model} · {MaskBaseUrl(provider.BaseUrl)}",
            Icon = new IconInfo(""),
        };
    }

    private ListItem CreateProviderItem(ProviderProfile profile)
    {
        var isActive = profile.Id == _settingsManager.ActiveProvider.Id;
        return new ListItem(new SelectProviderCommand(this, profile.Id))
        {
            Title = isActive ? $"已选择：{profile.Name}" : $"选择：{profile.Name}",
            Subtitle = $"{profile.Model} · {MaskBaseUrl(profile.BaseUrl)}",
            Icon = new IconInfo(isActive ? "" : ""),
            MoreCommands = [],
        };
    }

    private ListItem CreateEditProviderItem(ProviderProfile profile) => new(new ProviderEditorPage(_settingsManager, profile, () => RaiseItemsChanged()))
    {
        Title = $"编辑：{profile.Name}",
        Subtitle = "修改 Base URL、API Key、模型名和系统提示词",
        Icon = new IconInfo(""),
    };

    private ListItem CreateAddProviderItem() => new(new ProviderEditorPage(_settingsManager, _settingsManager.CreateEmptyProvider(), () => RaiseItemsChanged()))
    {
        Title = "添加模型提供商",
        Subtitle = "添加新的 Base URL、API Key 和模型名",
        Icon = new IconInfo(""),
    };

    private ListItem CreateBusyItem() => new(new NoOpCommand())
    {
        Title = "正在询问 AI...",
        Subtitle = Preview(_lastPrompt, 100),
        Icon = new IconInfo(""),
    };

    private static ListItem CreateResultItem(AiChatResponse response)
    {
        if (!response.IsSuccess)
        {
            return new ListItem(new NoOpCommand())
            {
                Title = "请求失败",
                Subtitle = Preview(response.ErrorMessage.ReplaceLineEndings(" "), 140),
                Icon = new IconInfo(""),
            };
        }

        return new ListItem(new CopyTextCommand(response.Content))
        {
            Title = Preview(response.Content.ReplaceLineEndings(" "), 140),
            Subtitle = $"回答 · {response.Model} · 按 Enter 复制",
            Icon = new IconInfo(""),
            MoreCommands = [new CommandContextItem(new CopyTextCommand(response.Content)) { Title = "复制回答" }],
        };
    }

    private static ListItem CreateHelpItem() => new(new NoOpCommand())
    {
        Title = "在顶部输入框输入问题",
        Subtitle = "按 Enter 发送。左侧可直接选择或添加模型提供商。",
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
        _isBusy = false;
        IsLoading = false;
        if (SearchText.Trim().Equals(prompt, StringComparison.Ordinal))
        {
            SetSearchNoUpdate(string.Empty);
            OnPropertyChanged(nameof(SearchText));
        }

        RaiseItemsChanged();
    }

    private void SelectProvider(string providerId)
    {
        _settingsManager.SelectProvider(providerId);
        RaiseItemsChanged();
    }

    private static string Preview(string value, int maxLength) => value.Length <= maxLength
        ? value
        : value[..maxLength] + "...";

    private static string MaskBaseUrl(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return string.IsNullOrWhiteSpace(value) ? "未配置 Base URL" : value;
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

    private sealed partial class SelectProviderCommand : InvokableCommand
    {
        private readonly AskAiPage _page;
        private readonly string _providerId;

        public SelectProviderCommand(AskAiPage page, string providerId)
        {
            _page = page;
            _providerId = providerId;
            Name = "选择提供商";
            Icon = new IconInfo("");
        }

        public override ICommandResult Invoke()
        {
            _page.SelectProvider(_providerId);
            return CommandResult.KeepOpen();
        }
    }
}
