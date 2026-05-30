// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

internal sealed partial class CopilotProviderEditorPage : ContentPage
{
    private readonly SettingsManager _settingsManager;
    private readonly ProviderProfile _profile;

    public CopilotProviderEditorPage(SettingsManager settingsManager, ProviderProfile profile, Action? onSaved = null)
    {
        _settingsManager = settingsManager;
        _profile = profile.Clone();
        _profile.ProviderType = "copilot";

        Icon = new IconInfo("");
        Title = string.IsNullOrWhiteSpace(_profile.Name) ? "添加 GitHub Copilot" : $"编辑 {_profile.Name}";
        Name = "配置";
    }

    public override IContent[] GetContent() => [new CopilotProviderEditorForm(this, _profile)];

    private CommandResult Save(string inputs)
    {
        try
        {
            var payload = JsonNode.Parse(inputs)?.AsObject();
            if (payload is null)
            {
                return CommandResult.ShowToast("无法读取 Copilot 配置。");
            }

            _profile.Name = ReadString(payload, "Name", _profile.Name);
            _profile.GitHubClientId = ReadString(payload, "GitHubClientId", _profile.GitHubClientId);
            _profile.Model = ReadString(payload, "Model", _profile.Model);
            _profile.SystemPrompt = ReadString(payload, "SystemPrompt", _profile.SystemPrompt);
            _profile.ProviderType = "copilot";
            _profile.BaseUrl = string.Empty;
            _profile.ApiKey = string.Empty;
            _profile.AuthType = string.IsNullOrWhiteSpace(_profile.AuthType) ? "device-code" : _profile.AuthType;

            if (string.IsNullOrWhiteSpace(_profile.Name))
            {
                return CommandResult.ShowToast("请填写提供商名称。");
            }

            if (string.IsNullOrWhiteSpace(_profile.Model))
            {
                return CommandResult.ShowToast("请填写 Copilot 模型名，例如 gpt-4.1。");
            }

            _settingsManager.SaveProvider(_profile);
            return CommandResult.ShowToast($"已保存 {_profile.Name}");
        }
        catch (JsonException)
        {
            return CommandResult.ShowToast("无法读取 Copilot 配置。");
        }
    }

    private static string ReadString(JsonObject payload, string key, string fallback)
    {
        return payload[key]?.ToString()
            ?? payload[char.ToLowerInvariant(key[0]) + key[1..]]?.ToString()
            ?? fallback;
    }

    private sealed partial class CopilotProviderEditorForm : FormContent
    {
        private readonly CopilotProviderEditorPage _page;

        public CopilotProviderEditorForm(CopilotProviderEditorPage page, ProviderProfile profile)
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
                  "id": "Name",
                  "label": "提供商名称",
                  "isRequired": true,
                  "errorMessage": "请填写提供商名称",
                  "placeholder": "GitHub Copilot",
                  "value": "${Name}"
                },
                {
                  "type": "Input.Text",
                  "id": "GitHubClientId",
                  "label": "GitHub OAuth Client ID（高级选项）",
                  "placeholder": "默认使用内置 Client ID；fork 项目后可替换为自己的 Client ID",
                  "value": "${GitHubClientId}"
                },
                {
                  "type": "Input.Text",
                  "id": "Model",
                  "label": "Model",
                  "isRequired": true,
                  "errorMessage": "请填写模型名",
                  "placeholder": "gpt-4.1",
                  "value": "${Model}"
                },
                {
                  "type": "Input.Text",
                  "id": "SystemPrompt",
                  "label": "System Prompt",
                  "isMultiline": true,
                  "placeholder": "你是一个简洁、可靠的中文 AI 助手。",
                  "value": "${SystemPrompt}"
                }
              ],
              "actions": [
                {
                  "type": "Action.Submit",
                  "title": "保存并选择"
                }
              ]
            }
            """;
            DataJson = new JsonObject
            {
                ["Name"] = profile.Name,
                ["GitHubClientId"] = profile.GitHubClientId,
                ["Model"] = profile.Model,
                ["SystemPrompt"] = profile.SystemPrompt,
            }.ToJsonString();
        }

        public override CommandResult SubmitForm(string inputs) => _page.Save(inputs);
    }
}
