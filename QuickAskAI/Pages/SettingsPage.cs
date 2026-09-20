// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickAskAI;

internal sealed partial class SettingsPage : ContentPage
{
    private readonly SettingsManager _settingsManager;
    private readonly Action _onChanged;

    public SettingsPage(SettingsManager settingsManager, Action onChanged)
    {
        _settingsManager = settingsManager;
        _onChanged = onChanged;
        Icon = new IconInfo("\uE713");
        Title = ResourceHelper.GetString("Settings_Title");
        Name = ResourceHelper.GetString("Settings_Name");
    }

    public override IContent[] GetContent() => [new LanguageSettingsForm(this), new ChatSettingsForm(this)];

    private string GetCurrentLanguage() => _settingsManager.Language;

    private void ApplyLanguage(string lang)
    {
        _settingsManager.Language = lang;
        _onChanged?.Invoke();
    }

    private (string Mode, int Timeout) GetCurrentChatMode()
        => (_settingsManager.ChatMode, _settingsManager.ChatTimeoutMinutes);

    private void ApplyChatMode(string mode, int timeout)
    {
        _settingsManager.ChatMode = mode;
        _settingsManager.ChatTimeoutMinutes = timeout;
    }

    private sealed partial class LanguageSettingsForm : FormContent
    {
        private readonly SettingsPage _page;

        public LanguageSettingsForm(SettingsPage page)
        {
            _page = page;
            TemplateJson = """
            {
              "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
              "type": "AdaptiveCard",
              "version": "1.5",
              "body": [
                {
                  "type": "Input.ChoiceSet",
                  "id": "Language",
                  "label": "${AC_LanguageLabel}",
                  "style": "expanded",
                  "value": "${Language}",
                  "choices": [
                    { "title": "${AC_LangAuto}", "value": "auto" },
                    { "title": "${AC_LangZhCN}", "value": "zh-CN" },
                    { "title": "${AC_LangEnUS}", "value": "en-US" }
                  ]
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
            DataJson = BuildDataJson();
        }

        private string BuildDataJson()
        {
            return new JsonObject
            {
                ["Language"] = _page.GetCurrentLanguage(),
                ["AC_LanguageLabel"] = ResourceHelper.GetString("Settings_LanguageTitle"),
                ["AC_LangAuto"] = ResourceHelper.GetString("Settings_LangAuto"),
                ["AC_LangZhCN"] = "中文",
                ["AC_LangEnUS"] = "English",
                ["AC_SaveButton"] = ResourceHelper.GetString("Settings_LangSelectCommand_Name"),
            }.ToJsonString();
        }

        public override CommandResult SubmitForm(string inputs)
        {
            var payload = JsonNode.Parse(inputs)?.AsObject();
            var lang = payload?["Language"]?.ToString();
            if (!string.IsNullOrWhiteSpace(lang))
            {
                _page.ApplyLanguage(lang);
            }

            return CommandResult.KeepOpen();
        }
    }

    private sealed partial class ChatSettingsForm : FormContent
    {
        private readonly SettingsPage _page;

        public ChatSettingsForm(SettingsPage page)
        {
            _page = page;
            TemplateJson = """
            {
              "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
              "type": "AdaptiveCard",
              "version": "1.5",
              "body": [
                {
                  "type": "Input.ChoiceSet",
                  "id": "ChatMode",
                  "label": "${AC_ChatModeLabel}",
                  "style": "expanded",
                  "value": "${ChatMode}",
                  "choices": [
                    { "title": "${AC_ChatModeContinue}", "value": "continue" },
                    { "title": "${AC_ChatModeTimeout}", "value": "timeout" },
                    { "title": "${AC_ChatModeNew}", "value": "new" }
                  ]
                },
                {
                  "type": "Input.Text",
                  "id": "ChatTimeout",
                  "label": "${AC_ChatTimeoutLabel}",
                  "isRequired": true,
                  "errorMessage": "${AC_ChatTimeoutError}",
                  "placeholder": "30",
                  "value": "${ChatTimeout}"
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
            DataJson = BuildDataJson();
        }

        private string BuildDataJson()
        {
            var (mode, timeout) = _page.GetCurrentChatMode();
            return new JsonObject
            {
                ["ChatMode"] = mode,
                ["ChatTimeout"] = timeout.ToString(CultureInfo.InvariantCulture),
                ["AC_ChatModeLabel"] = ResourceHelper.GetString("Settings_ChatModeTitle"),
                ["AC_ChatModeContinue"] = ResourceHelper.GetString("Settings_ChatModeContinue"),
                ["AC_ChatModeTimeout"] = ResourceHelper.GetString("Settings_ChatModeTimeout"),
                ["AC_ChatModeNew"] = ResourceHelper.GetString("Settings_ChatModeNew"),
                ["AC_ChatTimeoutLabel"] = ResourceHelper.GetString("Settings_ChatTimeoutTitle"),
                ["AC_ChatTimeoutError"] = ResourceHelper.GetString("Settings_ChatTimeoutError"),
                ["AC_SaveButton"] = ResourceHelper.GetString("Settings_ChatSaveButton"),
            }.ToJsonString();
        }

        public override CommandResult SubmitForm(string inputs)
        {
            var payload = JsonNode.Parse(inputs)?.AsObject();
            var mode = SettingsManager.NormalizeChatMode(payload?["ChatMode"]?.ToString());
            var timeout = ReadTimeout(payload?["ChatTimeout"]);

            _page.ApplyChatMode(mode, timeout);
            return CommandResult.KeepOpen();
        }

        private static int ReadTimeout(JsonNode? node)
        {
            var text = node?.ToString();
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)
                ? Math.Clamp(minutes, 1, 1440)
                : 30;
        }
    }
}
