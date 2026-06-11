// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AIExtension;

internal sealed class CopilotAuthService
{
    private const string DeviceCodeEndpoint = "https://github.com/login/device/code";
    private const string AccessTokenEndpoint = "https://github.com/login/oauth/access_token";
    private const string UserEndpoint = "https://api.github.com/user";
    private const string DeviceGrantType = "urn:ietf:params:oauth:grant-type:device_code";
    private static readonly TimeSpan DefaultHttpTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _httpClient;

    public CopilotAuthService()
        : this(new HttpClient { Timeout = DefaultHttpTimeout })
    {
    }

    private CopilotAuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<GitHubDeviceCodeResult> StartDeviceLoginAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException(ResourceHelper.GetString("CopilotAuth_ClientIdRequired"));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, DeviceCodeEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId.Trim(),
                ["scope"] = "read:user user:email",
            }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(string.Format(ResourceHelper.GetString("CopilotAuth_InitHttpError"), (int)response.StatusCode));
        }

        var root = JsonNode.Parse(responseBody)?.AsObject()
            ?? throw new InvalidOperationException(ResourceHelper.GetString("CopilotAuth_InitParseError"));
        var error = ReadString(root, "error");
        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(MapDeviceCodeError(error));
        }

        var deviceCode = ReadString(root, "device_code");
        var userCode = ReadString(root, "user_code");
        if (string.IsNullOrWhiteSpace(deviceCode) || string.IsNullOrWhiteSpace(userCode))
        {
            throw new InvalidOperationException(ResourceHelper.GetString("CopilotAuth_NoDeviceCode"));
        }

        return new GitHubDeviceCodeResult
        {
            DeviceCode = deviceCode,
            UserCode = userCode,
            VerificationUri = ReadString(root, "verification_uri", "https://github.com/login/device"),
            VerificationUriComplete = ReadString(root, "verification_uri_complete"),
            ExpiresIn = ReadInt(root, "expires_in", 900),
            Interval = Math.Max(1, ReadInt(root, "interval", 5)),
            CreatedAt = DateTimeOffset.Now,
        };
    }

    public async Task<GitHubTokenResult> PollForTokenAsync(string clientId, GitHubDeviceCodeResult deviceCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException(ResourceHelper.GetString("CopilotAuth_ClientIdRequired"));
        }

        if (string.IsNullOrWhiteSpace(deviceCode.DeviceCode))
        {
            throw new InvalidOperationException(ResourceHelper.GetString("CopilotAuth_InvalidDeviceCode"));
        }

        var interval = Math.Max(1, deviceCode.Interval);
        while (DateTimeOffset.Now < deviceCode.ExpiresAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(HttpMethod.Post, AccessTokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId.Trim(),
                    ["device_code"] = deviceCode.DeviceCode,
                    ["grant_type"] = DeviceGrantType,
                }),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(string.Format(ResourceHelper.GetString("CopilotAuth_PollHttpError"), (int)response.StatusCode));
            }

            var root = JsonNode.Parse(responseBody)?.AsObject()
                ?? throw new InvalidOperationException(ResourceHelper.GetString("CopilotAuth_PollParseError"));
            var accessToken = ReadString(root, "access_token");
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                return new GitHubTokenResult
                {
                    AccessToken = accessToken,
                    TokenType = ReadString(root, "token_type"),
                    Scope = ReadString(root, "scope"),
                };
            }

            var error = ReadString(root, "error");
            if (error.Equals("authorization_pending", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (error.Equals("slow_down", StringComparison.OrdinalIgnoreCase))
            {
                interval = Math.Max(interval + 5, ReadInt(root, "interval", interval + 5));
                await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken).ConfigureAwait(false);
                continue;
            }

            throw new InvalidOperationException(MapTokenError(error));
        }

        throw new InvalidOperationException(ResourceHelper.GetString("CopilotAuth_CodeExpired"));
    }

    public async Task<string> GetUserLoginAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, UserEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("QuickAskAI");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var root = JsonNode.Parse(responseBody)?.AsObject();
            return ReadString(root, "login");
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string ReadString(JsonObject? root, string key, string fallback = "")
    {
        return root?[key]?.ToString() ?? fallback;
    }

    private static int ReadInt(JsonObject root, string key, int fallback)
    {
        return root[key]?.GetValue<int>() ?? fallback;
    }

    private static string MapDeviceCodeError(string error) => error switch
    {
        "device_flow_disabled" => ResourceHelper.GetString("CopilotAuth_DeviceFlowDisabled"),
        "incorrect_client_credentials" => ResourceHelper.GetString("CopilotAuth_InvalidClientId"),
        _ => string.Format(ResourceHelper.GetString("CopilotAuth_InitFailedWithError"), error),
    };

    private static string MapTokenError(string error) => error switch
    {
        "access_denied" => ResourceHelper.GetString("CopilotAuth_AccessDenied"),
        "expired_token" => ResourceHelper.GetString("CopilotAuth_ExpiredToken"),
        "incorrect_client_credentials" => ResourceHelper.GetString("CopilotAuth_InvalidClientId"),
        "incorrect_device_code" => ResourceHelper.GetString("CopilotAuth_InvalidDeviceCode"),
        "device_flow_disabled" => ResourceHelper.GetString("CopilotAuth_DeviceFlowDisabledNoHint"),
        "unsupported_grant_type" => ResourceHelper.GetString("CopilotAuth_UnsupportedGrantType"),
        var value when string.IsNullOrWhiteSpace(value) => ResourceHelper.GetString("CopilotAuth_EmptyError"),
        var value => string.Format(ResourceHelper.GetString("CopilotAuth_LoginFailedWithError"), value),
    };
}
