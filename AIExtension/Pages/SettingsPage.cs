// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

internal sealed partial class SettingsPage : ListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly Action _onChanged;

    public SettingsPage(SettingsManager settingsManager, Action onChanged)
    {
        _settingsManager = settingsManager;
        _onChanged = onChanged;
        Icon = new IconInfo("");
        Title = "设置";
        Name = "设置";
        ShowDetails = false;
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>
        {
            CreateLanguageItem(),
        };
        return [.. items];
    }

    private ListItem CreateLanguageItem()
    {
        var currentLang = _settingsManager.Language;
        var currentLabel = currentLang switch
        {
            "en-US" => "English",
            "zh-CN" => "中文",
            _ => "自动（跟随系统）"
        };

        return new ListItem(new LanguageSelectorPage(_settingsManager, _onChanged))
        {
            Title = "界面语言",
            Subtitle = $"当前：{currentLabel} · 点击切换",
            Icon = new IconInfo(""),
        };
    }
}

internal sealed partial class LanguageSelectorPage : ListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly Action _onChanged;

    public LanguageSelectorPage(SettingsManager settingsManager, Action onChanged)
    {
        _settingsManager = settingsManager;
        _onChanged = onChanged;
        Icon = new IconInfo("");
        Title = "选择语言";
        Name = "语言";
        ShowDetails = false;
    }

    public override IListItem[] GetItems()
    {
        var currentLang = _settingsManager.Language;
        var items = new List<IListItem>
        {
            CreateLanguageSelectItem("auto", "自动（跟随系统）", currentLang),
            CreateLanguageSelectItem("zh-CN", "中文", currentLang),
            CreateLanguageSelectItem("en-US", "English", currentLang),
        };
        return [.. items];
    }

    private ListItem CreateLanguageSelectItem(string lang, string label, string currentLang)
    {
        var isSelected = lang == currentLang;
        return new ListItem(new SetLanguageCommand(_settingsManager, lang, _onChanged))
        {
            Title = label,
            Subtitle = isSelected ? "✓ 当前选中" : "点击选择",
            Icon = new IconInfo(isSelected ? "" : "○"),
        };
    }

    private sealed partial class SetLanguageCommand : InvokableCommand
    {
        private readonly SettingsManager _settingsManager;
        private readonly string _lang;
        private readonly Action _onChanged;

        public SetLanguageCommand(SettingsManager settingsManager, string lang, Action onChanged)
        {
            _settingsManager = settingsManager;
            _lang = lang;
            _onChanged = onChanged;
            Name = "选择";
        }

        public override ICommandResult Invoke()
        {
            _settingsManager.Language = _lang;
            _onChanged?.Invoke();
            return CommandResult.KeepOpen();
        }
    }
}