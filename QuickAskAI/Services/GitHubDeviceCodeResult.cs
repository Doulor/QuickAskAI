// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace QuickAskAI;

internal sealed class GitHubDeviceCodeResult
{
    public string DeviceCode { get; init; } = string.Empty;

    public string UserCode { get; init; } = string.Empty;

    public string VerificationUri { get; init; } = "https://github.com/login/device";

    public string VerificationUriComplete { get; init; } = string.Empty;

    public int ExpiresIn { get; init; }

    public int Interval { get; init; } = 5;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset ExpiresAt => CreatedAt.AddSeconds(ExpiresIn);
}
