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

namespace QuickAskAI;

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
            return ResourceHelper.GetString("AiChat_EmptyPrompt");
        }

        if (IsCopilot(request))
        {
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                return ResourceHelper.GetString("AiChat_CopilotModelRequired");
            }

            return string.IsNullOrWhiteSpace(request.ApiKey)
                ? ResourceHelper.GetString("AiChat_CopilotGitHubRequired")
                : null;
        }

        if (string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            return ResourceHelper.GetString("AiChat_BaseUrlRequired");
        }

        if (!TryCreateEndpoint(request.BaseUrl, out _))
        {
            return ResourceHelper.GetString("AiChat_BaseUrlInvalidDetail");
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return ResourceHelper.GetString("AiChat_ApiKeyRequired");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return ResourceHelper.GetString("AiChat_ModelRequired");
        }

        return null;
    }

    public async Task<AiChatResponse> AskAsync(AiChatRequest chatRequest, CancellationToken cancellationToken = default)
    {
        if (IsCopilot(chatRequest))
        {
            return await CopilotChatService.AskAsync(chatRequest, cancellationToken).ConfigureAwait(false);
        }

        var validationError = Validate(chatRequest);
        if (validationError is not null)
        {
            return AiChatResponse.Failure(validationError, chatRequest.Model);
        }

        if (!TryCreateEndpoint(chatRequest.BaseUrl, out var endpoint))
        {
            return AiChatResponse.Failure(ResourceHelper.GetString("AiChat_BaseUrlInvalid"), chatRequest.Model);
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
            return AiChatResponse.Failure(ResourceHelper.GetString("AiChat_RequestTimeout"), chatRequest.Model, endpoint.Host);
        }
        catch (HttpRequestException ex)
        {
            return AiChatResponse.Failure(string.Format(ResourceHelper.GetString("AiChat_NetworkError"), ex.Message), chatRequest.Model, endpoint.Host);
        }
        catch (JsonException ex)
        {
            return AiChatResponse.Failure(string.Format(ResourceHelper.GetString("AiChat_JsonParseError"), ex.Message), chatRequest.Model, endpoint.Host);
        }
    }

    private static bool IsCopilot(AiChatRequest request) => request.ProviderType.Equals("copilot", StringComparison.OrdinalIgnoreCase);

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

            foreach (var message in request.Messages)
            {
                if (string.IsNullOrWhiteSpace(message.Role) || string.IsNullOrWhiteSpace(message.Content))
                {
                    continue;
                }

                writer.WriteStartObject();
                writer.WriteString("role", message.Role);
                writer.WriteString("content", message.Content);
                writer.WriteEndObject();
            }

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
            return AiChatResponse.Failure(ResourceHelper.GetString("AiChat_EmptyResponse"), model, endpoint);
        }

        return AiChatResponse.Success(content.Trim(), model, endpoint);
    }

    private static string BuildHttpErrorMessage(HttpStatusCode statusCode, string responseBody, string apiKey)
    {
        var providerMessage = RedactApiKey(ExtractProviderMessage(responseBody), apiKey);
        var statusMessage = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ResourceHelper.GetString("AiChat_AuthFailed"),
            HttpStatusCode.NotFound => ResourceHelper.GetString("AiChat_NotFound"),
            (HttpStatusCode)429 => ResourceHelper.GetString("AiChat_RateLimited"),
            var code when (int)code >= 500 => ResourceHelper.GetString("AiChat_ServiceUnavailable"),
            _ => string.Format(ResourceHelper.GetString("AiChat_HttpError"), (int)statusCode),
        };

        return string.IsNullOrWhiteSpace(providerMessage)
            ? statusMessage
            : string.Format(ResourceHelper.GetString("AiChat_ProviderMessage"), statusMessage, providerMessage);
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
