// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Security.Credentials;

namespace QuickAskAI;

internal sealed class CopilotCredentialStore
{
    private const string ResourceName = "QuickAskAI.GitHubCopilot";

    public static string? TryGetToken(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return null;
        }

        try
        {
            var credential = new PasswordVault().Retrieve(ResourceName, providerId);
            credential.RetrievePassword();
            return string.IsNullOrWhiteSpace(credential.Password) ? null : credential.Password;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool HasToken(string providerId) => !string.IsNullOrWhiteSpace(TryGetToken(providerId));

    public static bool TrySaveToken(string providerId, string token)
    {
        if (string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            RemoveToken(providerId);
            new PasswordVault().Add(new PasswordCredential(ResourceName, providerId, token));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void RemoveToken(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return;
        }

        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(ResourceName, providerId);
            vault.Remove(credential);
        }
        catch (Exception)
        {
        }
    }
}
