// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace AIExtension;

internal sealed class AiChatRequest
{
    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string SystemPrompt { get; init; } = string.Empty;

    public double? Temperature { get; init; }

    public string Prompt { get; init; } = string.Empty;

    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];
}
