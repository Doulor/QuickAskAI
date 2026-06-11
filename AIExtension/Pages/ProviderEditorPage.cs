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
        Title = string.IsNullOrWhiteSpace(_profile.Name) ? ResourceHelper.GetString("ProvEditor_AddTitle") : ResourceHelper.GetString("ProvEditor_EditTitlePrefix") + _profile.Name;
        Name = ResourceHelper.GetString("ProvEditor_Name");
    }

    public override IContent[] GetContent() => [new ProviderEditorForm(this, _profile)];

    private CommandResult Save(string inputs)
    {
        try
        {
            var payload = JsonNode.Parse(inputs)?.AsObject();
            if (payload is null)
            {
                return CommandResult.ShowToast(ResourceHelper.GetString("ProvEditor_CannotReadConfig"));
            }

            _profile.Name = ReadString(payload, "Name", _profile.Name);
            _profile.ProviderType = ReadString(payload, "ProviderType", _profile.ProviderType);
            _profile.BaseUrl = ReadString(payload, "BaseUrl", _profile.BaseUrl);
            _profile.ApiKey = ReadString(payload, "ApiKey", _profile.ApiKey);
            _profile.Model = ReadString(payload, "Model", _profile.Model);
            _profile.SystemPrompt = ReadString(payload, "SystemPrompt", _profile.SystemPrompt);
            _profile.Temperature = ReadString(payload, "Temperature", _profile.Temperature);

            if (string.IsNullOrWhiteSpace(_profile.Name))
            {
                return CommandResult.ShowToast(ResourceHelper.GetString("ProvEditor_FillName"));
            }

            if (string.IsNullOrWhiteSpace(_profile.BaseUrl))
            {
                return CommandResult.ShowToast(ResourceHelper.GetString("ProvEditor_FillBaseUrl"));
            }

            if (string.IsNullOrWhiteSpace(_profile.Model))
            {
                return CommandResult.ShowToast(ResourceHelper.GetString("ProvEditor_FillModel"));
            }

            _settingsManager.SaveProvider(_profile);
            return CommandResult.ShowToast(ResourceHelper.GetString("ProvEditor_Saved") + _profile.Name);
        }
        catch (JsonException)
        {
            return CommandResult.ShowToast(ResourceHelper.GetString("ProvEditor_CannotReadConfig"));
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
                  "label": "${AC_ProviderName}",
                  "isRequired": true,
                  "errorMessage": "${AC_ProviderNameError}",
                  "placeholder": "OpenAI",
                  "value": "${Name}"
                },
                {
                  "type": "Input.Text",
                  "id": "ProviderType",
                  "label": "Provider Type",
                  "placeholder": "${AC_ProviderTypePlaceholder}",
                  "value": "${ProviderType}"
                },
                {
                  "type": "Input.Text",
                  "id": "BaseUrl",
                  "label": "Base URL",
                  "isRequired": true,
                  "errorMessage": "${AC_BaseUrlError}",
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
                  "errorMessage": "${AC_ModelError}",
                  "placeholder": "gpt-4.1-mini",
                  "value": "${Model}"
                },
                {
                  "type": "Input.Text",
                  "id": "SystemPrompt",
                  "label": "System Prompt",
                  "isMultiline": true,
                  "placeholder": "${AC_SystemPromptPlaceholder}",
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
                  "title": "${AC_SaveButton}"
                }
              ]
            }
            """;
            DataJson = new JsonObject
            {
                ["Name"] = profile.Name,
                ["ProviderType"] = profile.ProviderType,
                ["BaseUrl"] = profile.BaseUrl,
                ["ApiKey"] = profile.ApiKey,
                ["Model"] = profile.Model,
                ["SystemPrompt"] = profile.SystemPrompt,
                ["Temperature"] = profile.Temperature,
                ["AC_ProviderName"] = ResourceHelper.GetString("ProvEditor_AC_ProviderName"),
                ["AC_ProviderNameError"] = ResourceHelper.GetString("ProvEditor_AC_ProviderNameError"),
                ["AC_BaseUrlError"] = ResourceHelper.GetString("ProvEditor_AC_BaseUrlError"),
                ["AC_ModelError"] = ResourceHelper.GetString("ProvEditor_AC_ModelError"),
                ["AC_SystemPromptPlaceholder"] = ResourceHelper.GetString("ProvEditor_AC_SystemPromptPlaceholder"),
                ["AC_SaveButton"] = ResourceHelper.GetString("ProvEditor_AC_SaveButton"),
                ["AC_ProviderTypePlaceholder"] = ResourceHelper.GetString("ProvEditor_ProviderTypePlaceholder"),
            }.ToJsonString();
        }

        public override CommandResult SubmitForm(string inputs) => _page.Save(inputs);
    }
}
