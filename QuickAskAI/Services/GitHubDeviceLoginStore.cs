// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace QuickAskAI;

internal static class GitHubDeviceLoginStore
{
    private static readonly Dictionary<string, GitHubDeviceCodeResult> DeviceCodes = [];
    private static readonly object Gate = new();

    public static GitHubDeviceCodeResult? TryGet(string providerId, string clientId)
    {
        var key = CreateKey(providerId, clientId);
        lock (Gate)
        {
            if (!DeviceCodes.TryGetValue(key, out var deviceCode))
            {
                return null;
            }

            if (deviceCode.ExpiresAt <= DateTimeOffset.Now)
            {
                DeviceCodes.Remove(key);
                return null;
            }

            return deviceCode;
        }
    }

    public static void Save(string providerId, string clientId, GitHubDeviceCodeResult deviceCode)
    {
        var key = CreateKey(providerId, clientId);
        lock (Gate)
        {
            DeviceCodes[key] = deviceCode;
        }
    }

    public static void Clear(string providerId, string clientId)
    {
        var key = CreateKey(providerId, clientId);
        lock (Gate)
        {
            DeviceCodes.Remove(key);
        }
    }

    private static string CreateKey(string providerId, string clientId) => $"{providerId}:{clientId.Trim()}";
}
