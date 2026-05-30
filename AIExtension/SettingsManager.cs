// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AIExtension;

internal sealed class SettingsManager
{
    private const string BaseUrlKey = "baseUrl";
    private const string ApiKeyKey = "apiKey";
    private const string ModelKey = "model";
    private const string SystemPromptKey = "systemPrompt";
    private const string TemperatureKey = "temperature";
    private const string ActiveProviderKey = "activeProvider";

    private readonly Settings _settings = new();
    private readonly string _profilesPath;
    private readonly List<ProviderProfile> _profiles;

    public SettingsManager()
    {
        _profilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickAskAI",
            "providers.json");

        _profiles = LoadProfiles();

        var baseUrl = new TextSetting(
            BaseUrlKey,
            "Base URL",
            "当前提供商的 OpenAI-compatible API 地址。保存设置后会同步到已选提供商。",
            ActiveProvider.BaseUrl)
        {
            Placeholder = "https://api.example.com/v1",
        };

        var apiKey = new TextSetting(
            ApiKeyKey,
            "API Key",
            "当前提供商的 Bearer token。保存设置后会同步到已选提供商。",
            ActiveProvider.ApiKey)
        {
            Placeholder = "sk-...",
        };

        var model = new TextSetting(
            ModelKey,
            "Model",
            "当前提供商的模型名。",
            ActiveProvider.Model)
        {
            Placeholder = "gpt-4.1-mini",
        };

        var systemPrompt = new TextSetting(
            SystemPromptKey,
            "System Prompt",
            "当前提供商的系统提示词。",
            ActiveProvider.SystemPrompt)
        {
            Multiline = true,
            Placeholder = "你是一个简洁、可靠的中文 AI 助手。",
        };

        var temperature = new TextSetting(
            TemperatureKey,
            "Temperature",
            "当前提供商的 temperature，留空则使用服务默认值。",
            ActiveProvider.Temperature)
        {
            Placeholder = "0.7",
        };

        var activeProvider = new TextSetting(
            ActiveProviderKey,
            "Active Provider",
            "当前选择的提供商名称。也可以在主界面左侧切换。",
            ActiveProvider.Name)
        {
            Placeholder = "OpenAI",
        };

        _settings.Add(baseUrl);
        _settings.Add(apiKey);
        _settings.Add(model);
        _settings.Add(systemPrompt);
        _settings.Add(temperature);
        _settings.Add(activeProvider);
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public ICommandSettings Settings => _settings;

    public IReadOnlyList<ProviderProfile> Profiles => _profiles;

    public ProviderProfile ActiveProvider => _profiles.FirstOrDefault(p => p.Id == ActiveProviderId) ?? _profiles[0];

    public string ActiveProviderId { get; private set; } = "default";

    public string BaseUrl => ActiveProvider.BaseUrl.Trim();

    public string ApiKey => ActiveProvider.ApiKey.Trim();

    public string Model => ActiveProvider.Model.Trim();

    public string SystemPrompt => ActiveProvider.SystemPrompt.Trim();

    public double? Temperature
    {
        get
        {
            var value = ActiveProvider.Temperature.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var temperature)
                ? Math.Clamp(temperature, 0, 2)
                : null;
        }
    }

    public AiChatRequest CreateRequest(string prompt, IReadOnlyList<ChatMessage> messages) => new()
    {
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        Model = Model,
        SystemPrompt = SystemPrompt,
        Temperature = Temperature,
        Prompt = prompt,
        Messages = messages,
    };

    public ProviderProfile? GetProvider(string id) => _profiles.FirstOrDefault(p => p.Id == id);

    public void SelectProvider(string id)
    {
        if (_profiles.Any(p => p.Id == id))
        {
            ActiveProviderId = id;
            SaveProfiles();
        }
    }

    public ProviderProfile SaveProvider(ProviderProfile input, bool selectAfterSave = true)
    {
        var profile = input.Clone();
        profile.Id = string.IsNullOrWhiteSpace(profile.Id) ? CreateId() : profile.Id;
        profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "未命名提供商" : profile.Name.Trim();
        profile.BaseUrl = profile.BaseUrl.Trim();
        profile.ApiKey = profile.ApiKey.Trim();
        profile.Model = profile.Model.Trim();
        profile.SystemPrompt = profile.SystemPrompt.Trim();
        profile.Temperature = profile.Temperature.Trim();

        var index = _profiles.FindIndex(p => p.Id == profile.Id);
        if (index >= 0)
        {
            _profiles[index] = profile;
        }
        else
        {
            _profiles.Add(profile);
        }

        if (selectAfterSave)
        {
            ActiveProviderId = profile.Id;
        }

        SaveProfiles();
        return profile;
    }

    public ProviderProfile CreateEmptyProvider() => new()
    {
        Id = CreateId(),
        Name = $"提供商 {_profiles.Count + 1}",
        BaseUrl = "https://api.openai.com/v1",
        Model = "gpt-4.1-mini",
        SystemPrompt = "你是一个简洁、可靠的中文 AI 助手。",
        Temperature = "0.7",
    };

    private void OnSettingsChanged(object? sender, Settings args)
    {
        var current = ActiveProvider;
        current.BaseUrl = (_settings.GetSetting<string>(BaseUrlKey) ?? current.BaseUrl).Trim();
        current.ApiKey = (_settings.GetSetting<string>(ApiKeyKey) ?? current.ApiKey).Trim();
        current.Model = (_settings.GetSetting<string>(ModelKey) ?? current.Model).Trim();
        current.SystemPrompt = (_settings.GetSetting<string>(SystemPromptKey) ?? current.SystemPrompt).Trim();
        current.Temperature = (_settings.GetSetting<string>(TemperatureKey) ?? current.Temperature).Trim();
        SaveProfiles();
    }

    private List<ProviderProfile> LoadProfiles()
    {
        try
        {
            if (File.Exists(_profilesPath))
            {
                var saved = JsonNode.Parse(File.ReadAllText(_profilesPath))?.AsObject();
                var profiles = saved?["profiles"]?.AsArray()
                    .Select(ReadProfile)
                    .Where(profile => !string.IsNullOrWhiteSpace(profile.Id))
                    .ToList();

                if (profiles is { Count: > 0 })
                {
                    ActiveProviderId = saved?["activeProviderId"]?.ToString() ?? profiles[0].Id;
                    return profiles;
                }
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        ActiveProviderId = "default";
        return [CreateDefaultProvider()];
    }

    private void SaveProfiles()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_profilesPath)!);
        var profiles = new JsonArray();
        foreach (var profile in _profiles)
        {
            profiles.Add(JsonValue.Create(new JsonObject
            {
                ["id"] = profile.Id,
                ["name"] = profile.Name,
                ["baseUrl"] = profile.BaseUrl,
                ["apiKey"] = profile.ApiKey,
                ["model"] = profile.Model,
                ["systemPrompt"] = profile.SystemPrompt,
                ["temperature"] = profile.Temperature,
            }));
        }

        var state = new JsonObject
        {
            ["activeProviderId"] = ActiveProviderId,
            ["profiles"] = profiles,
        };
        File.WriteAllText(_profilesPath, state.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static ProviderProfile ReadProfile(JsonNode? node)
    {
        return new ProviderProfile
        {
            Id = node?["id"]?.ToString() ?? string.Empty,
            Name = node?["name"]?.ToString() ?? string.Empty,
            BaseUrl = node?["baseUrl"]?.ToString() ?? string.Empty,
            ApiKey = node?["apiKey"]?.ToString() ?? string.Empty,
            Model = node?["model"]?.ToString() ?? string.Empty,
            SystemPrompt = node?["systemPrompt"]?.ToString() ?? string.Empty,
            Temperature = node?["temperature"]?.ToString() ?? string.Empty,
        };
    }

    private static ProviderProfile CreateDefaultProvider() => new()
    {
        Id = "default",
        Name = "OpenAI",
        BaseUrl = "https://api.openai.com/v1",
        Model = "gpt-4.1-mini",
        SystemPrompt = "你是一个简洁、可靠的中文 AI 助手。",
        Temperature = "0.7",
    };

    private static string CreateId() => Guid.NewGuid().ToString("N");
}
