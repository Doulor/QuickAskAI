// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

internal sealed partial class ProviderEditorPage : ContentPage
{
    private readonly SettingsManager _settingsManager;
    private readonly ProviderProfile _profile;

    public ProviderEditorPage(SettingsManager settingsManager, ProviderProfile profile, Action? onSaved = null)
    {
        _settingsManager = settingsManager;
        _profile = profile.Clone();

        Icon = new IconInfo("");
        Title = string.IsNullOrWhiteSpace(_profile.Name) ? "添加模型提供商" : $"编辑 {_profile.Name}";
        Name = "配置";
    }

    public override IContent[] GetContent() => [new ProviderEditorForm(this, _profile)];

    private CommandResult Save(string inputs)
    {
        try
        {
            var payload = JsonNode.Parse(inputs)?.AsObject();
            if (payload is null)
            {
                return CommandResult.ShowToast("无法读取提供商配置。");
            }

            _profile.Name = ReadString(payload, "Name", _profile.Name);
            _profile.BaseUrl = ReadString(payload, "BaseUrl", _profile.BaseUrl);
            _profile.ApiKey = ReadString(payload, "ApiKey", _profile.ApiKey);
            _profile.Model = ReadString(payload, "Model", _profile.Model);
            _profile.SystemPrompt = ReadString(payload, "SystemPrompt", _profile.SystemPrompt);
            _profile.Temperature = ReadString(payload, "Temperature", _profile.Temperature);

            if (string.IsNullOrWhiteSpace(_profile.Name))
            {
                return CommandResult.ShowToast("请填写提供商名称。");
            }

            if (string.IsNullOrWhiteSpace(_profile.BaseUrl))
            {
                return CommandResult.ShowToast("请填写 Base URL。");
            }

            if (string.IsNullOrWhiteSpace(_profile.Model))
            {
                return CommandResult.ShowToast("请填写模型名。");
            }

            _settingsManager.SaveProvider(_profile);
            return CommandResult.ShowToast($"已保存 {_profile.Name}");
        }
        catch (JsonException)
        {
            return CommandResult.ShowToast("无法读取提供商配置。");
        }
    }

    private static string ReadString(JsonObject payload, string key, string fallback)
    {
        return payload[key]?.ToString()
            ?? payload[char.ToLowerInvariant(key[0]) + key[1..]]?.ToString()
            ?? fallback;
    }

    private sealed partial class ProviderEditorForm : FormContent
    {
        private readonly ProviderEditorPage _page;

        public ProviderEditorForm(ProviderEditorPage page, ProviderProfile profile)
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
                  "placeholder": "OpenAI",
                  "value": "${Name}"
                },
                {
                  "type": "Input.Text",
                  "id": "BaseUrl",
                  "label": "Base URL",
                  "isRequired": true,
                  "errorMessage": "请填写 Base URL",
                  "placeholder": "https://api.example.com/v1",
                  "value": "${BaseUrl}"
                },
                {
                  "type": "Input.Text",
                  "id": "ApiKey",
                  "label": "API Key",
                  "placeholder": "sk-...",
                  "value": "${ApiKey}"
                },
                {
                  "type": "Input.Text",
                  "id": "Model",
                  "label": "Model",
                  "isRequired": true,
                  "errorMessage": "请填写模型名",
                  "placeholder": "gpt-4.1-mini",
                  "value": "${Model}"
                },
                {
                  "type": "Input.Text",
                  "id": "SystemPrompt",
                  "label": "System Prompt",
                  "isMultiline": true,
                  "placeholder": "你是一个简洁、可靠的中文 AI 助手。",
                  "value": "${SystemPrompt}"
                },
                {
                  "type": "Input.Text",
                  "id": "Temperature",
                  "label": "Temperature",
                  "placeholder": "0.7",
                  "value": "${Temperature}"
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
                ["BaseUrl"] = profile.BaseUrl,
                ["ApiKey"] = profile.ApiKey,
                ["Model"] = profile.Model,
                ["SystemPrompt"] = profile.SystemPrompt,
                ["Temperature"] = profile.Temperature,
            }.ToJsonString();
        }

        public override CommandResult SubmitForm(string inputs) => _page.Save(inputs);
    }
}
