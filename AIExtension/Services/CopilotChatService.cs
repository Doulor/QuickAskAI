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

internal sealed class CopilotChatService
{
    private const string CopilotTokenEndpoint = "https://api.github.com/copilot_internal/v2/token";
    private const string CopilotChatEndpoint = "https://api.githubcopilot.com/chat/completions";
    private const string EditorVersion = "vscode/1.104.1";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public static async Task<AiChatResponse> AskAsync(AiChatRequest chatRequest, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chatRequest.Prompt))
        {
            return AiChatResponse.Failure("请输入要询问 AI 的内容。", chatRequest.Model, "GitHub Copilot");
        }

        if (string.IsNullOrWhiteSpace(chatRequest.Model))
        {
            return AiChatResponse.Failure("请填写 Copilot 模型名，例如 gpt-4.1。", chatRequest.Model, "GitHub Copilot");
        }

        try
        {
            var copilotToken = await GetCopilotTokenAsync(chatRequest.ApiKey, cancellationToken).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Post, CopilotChatEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", copilotToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            AddCopilotHeaders(request);
            request.Content = new StringContent(CreateRequestJson(chatRequest), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return AiChatResponse.Failure(BuildHttpErrorMessage(response.StatusCode, responseBody, chatRequest.ApiKey, copilotToken), chatRequest.Model, "GitHub Copilot");
            }

            return ParseSuccess(responseBody, chatRequest.Model);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AiChatResponse.Failure("GitHub Copilot 请求超时，请检查网络或稍后重试。", chatRequest.Model, "GitHub Copilot");
        }
        catch (HttpRequestException ex)
        {
            return AiChatResponse.Failure($"GitHub Copilot 网络请求失败：{ex.Message}", chatRequest.Model, "GitHub Copilot");
        }
        catch (JsonException ex)
        {
            return AiChatResponse.Failure($"GitHub Copilot 返回了无法解析的 JSON：{ex.Message}", chatRequest.Model, "GitHub Copilot");
        }
        catch (Exception ex)
        {
            return AiChatResponse.Failure(BuildSafeError("GitHub Copilot 请求失败", ex, chatRequest.ApiKey), chatRequest.Model, "GitHub Copilot");
        }
    }

    private static async Task<string> GetCopilotTokenAsync(string githubToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(githubToken))
        {
            throw new InvalidOperationException("请先在 GitHub Copilot 提供商中连接 GitHub 账号。");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, CopilotTokenEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        AddCopilotHeaders(request);

        using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildHttpErrorMessage(response.StatusCode, responseBody, githubToken, string.Empty));
        }

        var root = JsonNode.Parse(responseBody)?.AsObject()
            ?? throw new InvalidOperationException("GitHub Copilot token 接口返回了无法解析的数据。");
        var token = root["token"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("GitHub Copilot token 接口没有返回可用 token，请确认账号拥有 Copilot 权益。");
        }

        return token;
    }

    private static void AddCopilotHeaders(HttpRequestMessage request)
    {
        request.Headers.UserAgent.ParseAdd("GitHubCopilotChat/0.1.0");
        request.Headers.TryAddWithoutValidation("Editor-Version", EditorVersion);
        request.Headers.TryAddWithoutValidation("Copilot-Integration-Id", "vscode-chat");
        request.Headers.TryAddWithoutValidation("OpenAI-Intent", "conversation-panel");
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
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static AiChatResponse ParseSuccess(string responseBody, string model)
    {
        var root = JsonNode.Parse(responseBody);
        var choices = root?["choices"] as JsonArray;
        var content = choices is { Count: > 0 }
            ? choices[0]?["message"]?["content"]?.GetValue<string>()
            : null;

        if (string.IsNullOrWhiteSpace(content))
        {
            return AiChatResponse.Failure("GitHub Copilot 返回成功，但没有包含可显示的回答内容。", model, "GitHub Copilot");
        }

        return AiChatResponse.Success(content.Trim(), model, "GitHub Copilot");
    }

    private static string BuildHttpErrorMessage(HttpStatusCode statusCode, string responseBody, string githubToken, string copilotToken)
    {
        var detail = ExtractErrorDetail(responseBody);
        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            detail = detail.Replace(githubToken, "[redacted]", StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(copilotToken))
        {
            detail = detail.Replace(copilotToken, "[redacted]", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(detail)
            ? $"GitHub Copilot 请求失败：HTTP {(int)statusCode}。"
            : $"GitHub Copilot 请求失败：HTTP {(int)statusCode}：{detail}";
    }

    private static string ExtractErrorDetail(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        try
        {
            var root = JsonNode.Parse(responseBody);
            var detail = root?["error"]?["message"]?.GetValue<string>()
                ?? root?["message"]?.GetValue<string>()
                ?? root?["error"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(detail))
            {
                return detail.ReplaceLineEndings(" ").Trim();
            }
        }
        catch (JsonException)
        {
        }

        var compact = responseBody.ReplaceLineEndings(" ").Trim();
        return compact.Length > 500 ? compact[..500] + "..." : compact;
    }

    private static string BuildSafeError(string stage, Exception exception, string token)
    {
        var message = exception.Message;
        if (!string.IsNullOrWhiteSpace(token))
        {
            message = message.Replace(token, "[redacted]", StringComparison.Ordinal);
        }

        message = message.ReplaceLineEndings(" ").Trim();
        if (message.Length > 500)
        {
            message = message[..500] + "...";
        }

        return string.IsNullOrWhiteSpace(message)
            ? $"{stage}：{exception.GetType().Name}。"
            : $"{stage}：{exception.GetType().Name}：{message}";
    }
}
