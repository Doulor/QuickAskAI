// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AIExtension;

internal sealed class AiChatService
{
    private readonly HttpClient _httpClient;

    public AiChatService()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
    {
    }

    private AiChatService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public static string? Validate(AiChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return "请输入要询问 AI 的内容。";
        }

        if (string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            return "请先在扩展设置里填写 Base URL。";
        }

        if (!TryCreateEndpoint(request.BaseUrl, out _))
        {
            return "Base URL 不是有效的 HTTP/HTTPS 地址，请填写类似 https://api.example.com 或 https://api.example.com/v1 的地址。";
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return "请先在扩展设置里填写 API Key。";
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return "请先在扩展设置里填写模型名。";
        }

        return null;
    }

    public async Task<AiChatResponse> AskAsync(AiChatRequest chatRequest, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(chatRequest);
        if (validationError is not null)
        {
            return AiChatResponse.Failure(validationError, chatRequest.Model);
        }

        if (!TryCreateEndpoint(chatRequest.BaseUrl, out var endpoint))
        {
            return AiChatResponse.Failure("Base URL 不是有效的 HTTP/HTTPS 地址。", chatRequest.Model);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chatRequest.ApiKey);
        request.Content = new StringContent(CreateRequestJson(chatRequest), Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return AiChatResponse.Failure(
                    BuildHttpErrorMessage(response.StatusCode, responseBody, chatRequest.ApiKey),
                    chatRequest.Model,
                    endpoint.Host);
            }

            return ParseSuccess(responseBody, chatRequest.Model, endpoint.Host);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AiChatResponse.Failure("请求超时，请检查网络或稍后重试。", chatRequest.Model, endpoint.Host);
        }
        catch (HttpRequestException ex)
        {
            return AiChatResponse.Failure($"网络请求失败：{ex.Message}", chatRequest.Model, endpoint.Host);
        }
        catch (JsonException ex)
        {
            return AiChatResponse.Failure($"AI 服务返回了无法解析的 JSON：{ex.Message}", chatRequest.Model, endpoint.Host);
        }
    }

    private static bool TryCreateEndpoint(string baseUrl, out Uri endpoint)
    {
        endpoint = null!;

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var baseUri))
        {
            return false;
        }

        if (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var path = baseUri.AbsolutePath.TrimEnd('/');
        var endpointPath = path switch
        {
            "" => "/v1/chat/completions",
            "/v1" => "/v1/chat/completions",
            _ when path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) => path,
            _ => $"{path}/chat/completions",
        };

        var builder = new UriBuilder(baseUri)
        {
            Path = endpointPath,
            Query = string.Empty,
            Fragment = string.Empty,
        };

        endpoint = builder.Uri;
        return true;
    }

    private static string CreateRequestJson(AiChatRequest request)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("model", request.Model);
            writer.WriteBoolean("stream", false);
            writer.WritePropertyName("messages");
            writer.WriteStartArray();

            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            {
                writer.WriteStartObject();
                writer.WriteString("role", "system");
                writer.WriteString("content", request.SystemPrompt);
                writer.WriteEndObject();
            }

            writer.WriteStartObject();
            writer.WriteString("role", "user");
            writer.WriteString("content", request.Prompt);
            writer.WriteEndObject();
            writer.WriteEndArray();

            if (request.Temperature is not null)
            {
                writer.WriteNumber("temperature", request.Temperature.Value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static AiChatResponse ParseSuccess(string responseBody, string model, string endpoint)
    {
        var root = JsonNode.Parse(responseBody);
        var choices = root?["choices"] as JsonArray;
        var content = choices is { Count: > 0 }
            ? choices[0]?["message"]?["content"]?.GetValue<string>()
            : null;

        if (string.IsNullOrWhiteSpace(content))
        {
            return AiChatResponse.Failure("AI 服务返回成功，但没有包含可显示的回答内容。", model, endpoint);
        }

        return AiChatResponse.Success(content.Trim(), model, endpoint);
    }

    private static string BuildHttpErrorMessage(HttpStatusCode statusCode, string responseBody, string apiKey)
    {
        var providerMessage = RedactApiKey(ExtractProviderMessage(responseBody), apiKey);
        var statusMessage = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "认证失败，请检查 API Key。",
            HttpStatusCode.NotFound => "接口未找到，请检查 Base URL 是否以 /v1 结尾。",
            (HttpStatusCode)429 => "请求过于频繁或额度不足。",
            var code when (int)code >= 500 => "AI 服务暂时不可用。",
            _ => $"AI 服务返回 HTTP {(int)statusCode}。",
        };

        return string.IsNullOrWhiteSpace(providerMessage)
            ? statusMessage
            : $"{statusMessage}\n\n服务返回：{providerMessage}";
    }

    private static string ExtractProviderMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        try
        {
            var root = JsonNode.Parse(responseBody);
            var message = root?["error"]?["message"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(message))
            {
                return Truncate(message.Trim());
            }
        }
        catch (JsonException)
        {
        }

        return Truncate(responseBody.Trim());
    }

    private static string RedactApiKey(string value, string apiKey)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(apiKey))
        {
            return value;
        }

        return value.Replace(apiKey, "[redacted]", StringComparison.Ordinal);
    }

    private static string Truncate(string value) => value.Length <= 600 ? value : value[..600] + "...";
}
