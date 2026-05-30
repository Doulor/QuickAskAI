// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AIExtension;

internal sealed class AiChatResponse
{
    public bool IsSuccess { get; init; }

    public string Content { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public static AiChatResponse Success(string content, string model, string endpoint) => new()
    {
        IsSuccess = true,
        Content = content,
        Model = model,
        Endpoint = endpoint,
    };

    public static AiChatResponse Failure(string errorMessage, string model = "", string endpoint = "") => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage,
        Model = model,
        Endpoint = endpoint,
    };
}
