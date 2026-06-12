// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

    public override IContent[] GetContent() => [new LanguageSettingsForm(this)];

    private string GetCurrentLanguage() => _settingsManager.Language;

    private void ApplyLanguage(string lang)
    {
        _settingsManager.Language = lang;
        _onChanged?.Invoke();
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
}
