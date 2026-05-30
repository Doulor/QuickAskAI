// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GitHub.Copilot;

namespace AIExtension;

internal sealed class CopilotChatService
{
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

        var options = new CopilotClientOptions
        {
            GitHubToken = chatRequest.ApiKey,
            UseLoggedInUser = false,
        };

        await using var client = new CopilotClient(options);
        var started = await TryStartClientAsync(client, cancellationToken).ConfigureAwait(false);
        if (started is not null)
        {
            return AiChatResponse.Failure(BuildSafeError("GitHub Copilot SDK 启动失败", started, chatRequest.ApiKey), chatRequest.Model, "GitHub Copilot");
        }

        try
        {
            try
            {
                await using var session = await client.CreateSessionAsync(new SessionConfig
                {
                    Model = chatRequest.Model,
                    OnPermissionRequest = PermissionHandler.ApproveAll,
                }, cancellationToken).ConfigureAwait(false);

                try
                {
                    var response = await session.SendAndWaitAsync(new MessageOptions
                    {
                        Prompt = BuildPrompt(chatRequest),
                    }, timeout: null, cancellationToken).ConfigureAwait(false);

                    var content = response?.Data?.Content?.Trim();
                    return string.IsNullOrWhiteSpace(content)
                        ? AiChatResponse.Failure("GitHub Copilot 返回了空回答。", chatRequest.Model, "GitHub Copilot")
                        : AiChatResponse.Success(content, chatRequest.Model, "GitHub Copilot");
                }
                catch (Exception ex)
                {
                    return AiChatResponse.Failure(BuildSafeError("GitHub Copilot 发送消息失败", ex, chatRequest.ApiKey), chatRequest.Model, "GitHub Copilot");
                }
            }
            catch (Exception ex)
            {
                return AiChatResponse.Failure(BuildSafeError("GitHub Copilot 创建会话失败", ex, chatRequest.ApiKey), chatRequest.Model, "GitHub Copilot");
            }
        }
        finally
        {
            await client.StopAsync().ConfigureAwait(false);
        }
    }

    private static async Task<Exception?> TryStartClientAsync(CopilotClient client, CancellationToken cancellationToken)
    {
        try
        {
            await client.StartAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (IsCliExtractionError(ex))
        {
            var warmupError = await WarmUpBundledCliAsync(cancellationToken).ConfigureAwait(false);
            if (warmupError is not null)
            {
                return new InvalidOperationException($"Copilot CLI 首次解压失败，且自动预热也失败：{warmupError.Message}", ex);
            }

            try
            {
                await client.StartAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }
            catch (Exception retryEx)
            {
                return retryEx;
            }
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static bool IsCliExtractionError(Exception exception)
    {
        return exception.Message.Contains("EXDEV", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("cross-device link", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("Failed to extract bundled package", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Exception?> WarmUpBundledCliAsync(CancellationToken cancellationToken)
    {
        var cliPath = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "copilot.exe");
        if (!File.Exists(cliPath))
        {
            return new FileNotFoundException("找不到随扩展打包的 copilot.exe。", cliPath);
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = "--help",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                },
            };

            process.Start();
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            _ = await stdoutTask.ConfigureAwait(false);

            return process.ExitCode == 0
                ? null
                : new InvalidOperationException($"copilot.exe --help 退出码 {process.ExitCode}：{stderr.Trim()}");
        }
        catch (Exception ex)
        {
            return ex;
        }
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

    private static string BuildPrompt(AiChatRequest request)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            builder.AppendLine(request.SystemPrompt);
            builder.AppendLine();
        }

        builder.AppendLine("以下是当前会话上下文，请基于上下文回答最后一个用户问题。");
        builder.AppendLine();

        foreach (var message in request.Messages)
        {
            var label = message.Role switch
            {
                "user" => "用户",
                "assistant" => "助手",
                _ => message.Role,
            };
            builder.Append(label).Append('：').AppendLine(message.Content);
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
