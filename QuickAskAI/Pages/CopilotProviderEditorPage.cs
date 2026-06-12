// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickAskAI;

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
        Title = string.IsNullOrWhiteSpace(_profile.Name) ? ResourceHelper.GetString("CopilotEditor_AddTitle") : ResourceHelper.GetString("CopilotEditor_EditTitlePrefix") + _profile.Name;
        Name = ResourceHelper.GetString("CopilotEditor_Name");
    }

    public override IContent[] GetContent() => [new CopilotProviderEditorForm(this, _profile)];

    private CommandResult Save(string inputs)
    {
        try
        {
            var payload = JsonNode.Parse(inputs)?.AsObject();
            if (payload is null)
            {
                return CommandResult.ShowToast(ResourceHelper.GetString("CopilotEditor_CannotReadConfig"));
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
                return CommandResult.ShowToast(ResourceHelper.GetString("CopilotEditor_FillName"));
            }

            if (string.IsNullOrWhiteSpace(_profile.Model))
            {
                return CommandResult.ShowToast(ResourceHelper.GetString("CopilotEditor_FillModel"));
            }

            _settingsManager.SaveProvider(_profile);
            return CommandResult.ShowToast(ResourceHelper.GetString("CopilotEditor_Saved") + _profile.Name);
        }
        catch (JsonException)
        {
            return CommandResult.ShowToast(ResourceHelper.GetString("CopilotEditor_CannotReadConfig"));
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
                  "label": "${AC_ProviderName}",
                  "isRequired": true,
                  "errorMessage": "${AC_ProviderNameError}",
                  "placeholder": "GitHub Copilot",
                  "value": "${Name}"
                },
                {
                  "type": "Input.Text",
                  "id": "GitHubClientId",
                  "label": "${AC_ClientIdLabel}",
                  "placeholder": "${AC_ClientIdPlaceholder}",
                  "value": "${GitHubClientId}"
                },
                {
                  "type": "Input.Text",
                  "id": "Model",
                  "label": "Model",
                  "isRequired": true,
                  "errorMessage": "${AC_ModelError}",
                  "placeholder": "gpt-4.1",
                  "value": "${Model}"
                },
                {
                  "type": "Input.Text",
                  "id": "SystemPrompt",
                  "label": "System Prompt",
                  "isMultiline": true,
                  "placeholder": "${AC_SystemPromptPlaceholder}",
                  "value": "${SystemPrompt}"
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
                ["GitHubClientId"] = profile.GitHubClientId,
                ["Model"] = profile.Model,
                ["SystemPrompt"] = profile.SystemPrompt,
                ["AC_ProviderName"] = ResourceHelper.GetString("CopilotEditor_AC_ProviderName"),
                ["AC_ProviderNameError"] = ResourceHelper.GetString("CopilotEditor_AC_ProviderNameError"),
                ["AC_ClientIdLabel"] = ResourceHelper.GetString("CopilotEditor_AC_ClientIdLabel"),
                ["AC_ClientIdPlaceholder"] = ResourceHelper.GetString("CopilotEditor_AC_ClientIdPlaceholder"),
                ["AC_ModelError"] = ResourceHelper.GetString("CopilotEditor_AC_ModelError"),
                ["AC_SystemPromptPlaceholder"] = ResourceHelper.GetString("CopilotEditor_AC_SystemPromptPlaceholder"),
                ["AC_SaveButton"] = ResourceHelper.GetString("CopilotEditor_AC_SaveButton"),
            }.ToJsonString();
        }

        public override CommandResult SubmitForm(string inputs) => _page.Save(inputs);
    }
}
