// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AIExtension;

internal sealed class ProviderProfile
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ProviderType { get; set; } = "openai";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string AuthType { get; set; } = string.Empty;

    public string GitHubLogin { get; set; } = string.Empty;

    public string GitHubClientId { get; set; } = string.Empty;

    public string TokenTypeHint { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } = string.Empty;

    public string Temperature { get; set; } = string.Empty;

    public ProviderProfile Clone() => new()
    {
        Id = Id,
        Name = Name,
        ProviderType = ProviderType,
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        AuthType = AuthType,
        GitHubLogin = GitHubLogin,
        GitHubClientId = GitHubClientId,
        TokenTypeHint = TokenTypeHint,
        Model = Model,
        SystemPrompt = SystemPrompt,
        Temperature = Temperature,
    };
}
