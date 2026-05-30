// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
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

    private readonly Settings _settings = new();

    public SettingsManager()
    {
        var baseUrl = new TextSetting(
            BaseUrlKey,
            "Base URL",
            "OpenAI-compatible API base URL, for example https://api.example.com or https://api.example.com/v1",
            "https://api.openai.com/v1")
        {
            Placeholder = "https://api.example.com/v1",
        };

        var apiKey = new TextSetting(
            ApiKeyKey,
            "API Key",
            "Bearer token used only for requests to the configured Base URL",
            string.Empty)
        {
            Placeholder = "sk-...",
        };

        var model = new TextSetting(
            ModelKey,
            "Model",
            "Model name sent to the chat completions endpoint",
            "gpt-4.1-mini")
        {
            Placeholder = "gpt-4.1-mini",
        };

        var systemPrompt = new TextSetting(
            SystemPromptKey,
            "System Prompt",
            "Optional system message sent before the user question",
            "你是一个简洁、可靠的中文 AI 助手。")
        {
            Multiline = true,
            Placeholder = "你是一个简洁、可靠的中文 AI 助手。",
        };

        var temperature = new TextSetting(
            TemperatureKey,
            "Temperature",
            "Optional number from 0 to 2. Leave empty to use the provider default.",
            "0.7")
        {
            Placeholder = "0.7",
        };

        _settings.Add(baseUrl);
        _settings.Add(apiKey);
        _settings.Add(model);
        _settings.Add(systemPrompt);
        _settings.Add(temperature);
    }

    public ICommandSettings Settings => _settings;

    public string BaseUrl => (_settings.GetSetting<string>(BaseUrlKey) ?? string.Empty).Trim();

    public string ApiKey => (_settings.GetSetting<string>(ApiKeyKey) ?? string.Empty).Trim();

    public string Model => (_settings.GetSetting<string>(ModelKey) ?? string.Empty).Trim();

    public string SystemPrompt => (_settings.GetSetting<string>(SystemPromptKey) ?? string.Empty).Trim();

    public double? Temperature
    {
        get
        {
            var value = (_settings.GetSetting<string>(TemperatureKey) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var temperature)
                ? Math.Clamp(temperature, 0, 2)
                : null;
        }
    }

    public AiChatRequest CreateRequest(string prompt) => new()
    {
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        Model = Model,
        SystemPrompt = SystemPrompt,
        Temperature = Temperature,
        Prompt = prompt,
    };
}
