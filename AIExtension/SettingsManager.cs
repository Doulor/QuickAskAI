// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
    private const string DefaultGitHubClientId = "Ov23liDa9NDfYl29YozQ";

    private readonly Settings _settings = new();
    private readonly string _profilesPath;
    private readonly List<ProviderProfile> _profiles;

    public SettingsManager()
    {
        _profilesPath = StableStorage.GetPath("providers.json");
        StableStorage.MigrateFromLegacyPath("providers.json", _profilesPath);

        _profiles = LoadProfiles();
        MigrateCopilotApiKeysToCredentialStore();

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
    }

    public ICommandSettings Settings => _settings;

    public event EventHandler? ProvidersChanged;

    public IReadOnlyList<ProviderProfile> Profiles => _profiles;

    public ProviderProfile ActiveProvider => _profiles.FirstOrDefault(p => p.Id == ActiveProviderId) ?? _profiles[0];

    public string ActiveProviderId { get; private set; } = "default";

    public string BaseUrl => ActiveProvider.BaseUrl.Trim();

    public string ApiKey => ActiveProvider.ProviderType.Equals("copilot", StringComparison.OrdinalIgnoreCase)
        ? GetCopilotToken(ActiveProvider) ?? ActiveProvider.ApiKey.Trim()
        : ActiveProvider.ApiKey.Trim();

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
        ProviderType = ActiveProvider.ProviderType,
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        Model = Model,
        SystemPrompt = SystemPrompt,
        Temperature = Temperature,
        Prompt = prompt,
        Messages = messages,
    };

    public ProviderProfile? GetProvider(string id) => _profiles.FirstOrDefault(p => p.Id == id);

    public static bool IsCopilotProvider(ProviderProfile profile) => profile.ProviderType.Equals("copilot", StringComparison.OrdinalIgnoreCase);

    public static bool HasCopilotToken(ProviderProfile profile)
    {
        return IsCopilotProvider(profile)
            && (!string.IsNullOrWhiteSpace(profile.ApiKey) || CopilotCredentialStore.HasToken(profile.Id));
    }

    public static string? GetCopilotToken(ProviderProfile profile)
    {
        if (!IsCopilotProvider(profile))
        {
            return null;
        }

        var storedToken = CopilotCredentialStore.TryGetToken(profile.Id);
        return !string.IsNullOrWhiteSpace(storedToken)
            ? storedToken
            : profile.ApiKey.Trim();
    }

    public void SaveCopilotToken(string providerId, string token, string login, string tokenTypeHint = "")
    {
        var provider = GetProvider(providerId);
        if (provider is null || !IsCopilotProvider(provider))
        {
            return;
        }

        if (!CopilotCredentialStore.TrySaveToken(providerId, token))
        {
            throw new InvalidOperationException("无法保存 GitHub 授权，请检查 Windows 凭据存储是否可用。");
        }

        provider.AuthType = "device-code";
        provider.GitHubLogin = login.Trim();
        provider.TokenTypeHint = tokenTypeHint.Trim();
        provider.ApiKey = string.Empty;
        SaveProfiles();
        ProvidersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveCopilotToken(string providerId)
    {
        var provider = GetProvider(providerId);
        if (provider is null || !IsCopilotProvider(provider))
        {
            return;
        }

        CopilotCredentialStore.RemoveToken(providerId);
        provider.AuthType = string.Empty;
        provider.GitHubLogin = string.Empty;
        provider.TokenTypeHint = string.Empty;
        provider.ApiKey = string.Empty;
        SaveProfiles();
        ProvidersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MigrateCopilotApiKeysToCredentialStore()
    {
        var changed = false;
        foreach (var profile in _profiles.Where(profile => IsCopilotProvider(profile) && !string.IsNullOrWhiteSpace(profile.ApiKey)))
        {
            if (!CopilotCredentialStore.HasToken(profile.Id) && !CopilotCredentialStore.TrySaveToken(profile.Id, profile.ApiKey))
            {
                continue;
            }

            profile.ApiKey = string.Empty;
            profile.AuthType = string.IsNullOrWhiteSpace(profile.AuthType) ? "device-code" : profile.AuthType;
            changed = true;
        }

        if (changed)
        {
            SaveProfiles();
        }
    }

    public void SelectProvider(string id)
    {
        if (_profiles.Any(p => p.Id == id))
        {
            ActiveProviderId = id;
            SaveProfiles();
            ProvidersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ProviderProfile SaveProvider(ProviderProfile input, bool selectAfterSave = true)
    {
        var profile = input.Clone();
        profile.Id = string.IsNullOrWhiteSpace(profile.Id) ? CreateId() : profile.Id;
        profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "未命名提供商" : profile.Name.Trim();
        profile.ProviderType = NormalizeProviderType(profile.ProviderType);
        profile.BaseUrl = profile.BaseUrl.Trim();
        profile.ApiKey = profile.ApiKey.Trim();
        profile.AuthType = profile.AuthType.Trim();
        profile.GitHubLogin = profile.GitHubLogin.Trim();
        profile.GitHubClientId = profile.GitHubClientId.Trim();
        if (IsCopilotProvider(profile) && string.IsNullOrWhiteSpace(profile.GitHubClientId))
        {
            profile.GitHubClientId = DefaultGitHubClientId;
        }

        profile.TokenTypeHint = profile.TokenTypeHint.Trim();
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
        ProvidersChanged?.Invoke(this, EventArgs.Empty);
        return profile;
    }

    public ProviderProfile CreateEmptyProvider() => new()
    {
        Id = CreateId(),
        Name = $"提供商 {_profiles.Count + 1}",
        ProviderType = "openai",
        BaseUrl = "https://api.openai.com/v1",
        Model = "gpt-4.1-mini",
        SystemPrompt = "你是一个简洁、可靠的中文 AI 助手。",
        Temperature = "0.7",
    };

    public static ProviderProfile CreateCopilotProvider() => new()
    {
        Id = CreateId(),
        Name = "GitHub Copilot",
        ProviderType = "copilot",
        AuthType = "device-code",
        GitHubClientId = DefaultGitHubClientId,
        Model = "gpt-4.1",
        SystemPrompt = "你是一个简洁、可靠的中文 AI 助手。",
        Temperature = string.Empty,
    };

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
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("activeProviderId", ActiveProviderId);
            writer.WritePropertyName("profiles");
            writer.WriteStartArray();

            foreach (var profile in _profiles)
            {
                writer.WriteStartObject();
                writer.WriteString("id", profile.Id);
                writer.WriteString("name", profile.Name);
                writer.WriteString("providerType", profile.ProviderType);
                writer.WriteString("baseUrl", profile.BaseUrl);
                writer.WriteString("apiKey", profile.ApiKey);
                writer.WriteString("authType", profile.AuthType);
                writer.WriteString("gitHubLogin", profile.GitHubLogin);
                writer.WriteString("gitHubClientId", profile.GitHubClientId);
                writer.WriteString("tokenTypeHint", profile.TokenTypeHint);
                writer.WriteString("model", profile.Model);
                writer.WriteString("systemPrompt", profile.SystemPrompt);
                writer.WriteString("temperature", profile.Temperature);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        File.WriteAllText(_profilesPath, Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static ProviderProfile ReadProfile(JsonNode? node)
    {
        return new ProviderProfile
        {
            Id = node?["id"]?.ToString() ?? string.Empty,
            Name = node?["name"]?.ToString() ?? string.Empty,
            ProviderType = NormalizeProviderType(node?["providerType"]?.ToString()),
            BaseUrl = node?["baseUrl"]?.ToString() ?? string.Empty,
            ApiKey = node?["apiKey"]?.ToString() ?? string.Empty,
            AuthType = node?["authType"]?.ToString() ?? string.Empty,
            GitHubLogin = node?["gitHubLogin"]?.ToString() ?? string.Empty,
            GitHubClientId = string.IsNullOrWhiteSpace(node?["gitHubClientId"]?.ToString())
                ? DefaultGitHubClientId
                : node?["gitHubClientId"]?.ToString() ?? string.Empty,
            TokenTypeHint = node?["tokenTypeHint"]?.ToString() ?? string.Empty,
            Model = node?["model"]?.ToString() ?? string.Empty,
            SystemPrompt = node?["systemPrompt"]?.ToString() ?? string.Empty,
            Temperature = node?["temperature"]?.ToString() ?? string.Empty,
        };
    }

    private static ProviderProfile CreateDefaultProvider() => new()
    {
        Id = "default",
        Name = "OpenAI",
        ProviderType = "openai",
        BaseUrl = "https://api.openai.com/v1",
        Model = "gpt-4.1-mini",
        SystemPrompt = "你是一个简洁、可靠的中文 AI 助手。",
        Temperature = "0.7",
    };

    private static string NormalizeProviderType(string? providerType)
    {
        return providerType?.Trim().Equals("copilot", StringComparison.OrdinalIgnoreCase) == true
            ? "copilot"
            : "openai";
    }

    private static string CreateId() => Guid.NewGuid().ToString("N");
}
