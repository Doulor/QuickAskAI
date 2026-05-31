// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIExtension;

internal sealed class ConversationStore
{
    private readonly string _sessionsPath;
    private readonly List<ConversationSession> _sessions;

    public ConversationStore()
    {
        _sessionsPath = StableStorage.GetPath("conversations.json");
        StableStorage.MigrateFromLegacyPath("conversations.json", _sessionsPath);
        _sessions = LoadSessions();
        if (_sessions.Count == 0)
        {
            _sessions.Add(CreateSession("新会话"));
            ActiveSessionId = _sessions[0].Id;
            Save();
        }
    }

    public IReadOnlyList<ConversationSession> Sessions => _sessions;

    public string ActiveSessionId { get; private set; } = string.Empty;

    public ConversationSession ActiveSession => GetSession(ActiveSessionId) ?? _sessions[0];

    public ConversationSession? GetSession(string id) => _sessions.FirstOrDefault(session => session.Id == id);

    public ConversationSession StartNewSession()
    {
        var unusedSession = _sessions.FirstOrDefault(session => session.Messages.Count == 0);
        if (unusedSession is not null)
        {
            ActiveSessionId = unusedSession.Id;
            Save();
            return unusedSession;
        }

        var session = CreateSession("新会话");
        _sessions.Insert(0, session);
        ActiveSessionId = session.Id;
        Save();
        return session;
    }

    public ConversationSession EnsureUnusedSessionActive()
    {
        return StartNewSession();
    }

    public void SelectSession(string id)
    {
        if (_sessions.Any(session => session.Id == id))
        {
            ActiveSessionId = id;
            Save();
        }
    }

    public bool DeleteSession(string id)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == id);
        if (session is null)
        {
            return false;
        }

        _sessions.Remove(session);
        if (ActiveSessionId == id)
        {
            if (_sessions.Count == 0)
            {
                _sessions.Add(CreateSession("新会话"));
            }

            ActiveSessionId = _sessions[0].Id;
        }

        Save();
        return true;
    }

    public void AddUserMessage(string content)
    {
        AddMessage("user", content);
        var session = ActiveSession;
        if (session.Messages.Count == 1 || session.Title == "新会话")
        {
            session.Title = CreateTitle(content);
        }

        Save();
    }

    public void AddAssistantMessage(string content)
    {
        AddMessage("assistant", content);
        Save();
    }

    public void AddErrorMessage(string content)
    {
        AddMessage("assistant", $"请求失败：{content}");
        Save();
    }

    private void AddMessage(string role, string content)
    {
        var session = ActiveSession;
        session.Messages.Add(new ChatMessage { Role = role, Content = content });
        session.UpdatedAt = DateTimeOffset.Now;
    }

    private List<ConversationSession> LoadSessions()
    {
        try
        {
            if (!File.Exists(_sessionsPath))
            {
                return [];
            }

            var root = JsonNode.Parse(File.ReadAllText(_sessionsPath))?.AsObject();
            ActiveSessionId = root?["activeSessionId"]?.ToString() ?? string.Empty;
            return root?["sessions"]?.AsArray()
                .Select(ReadSession)
                .Where(session => !string.IsNullOrWhiteSpace(session.Id))
                .OrderByDescending(session => session.UpdatedAt)
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_sessionsPath)!);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("activeSessionId", ActiveSessionId);
            writer.WritePropertyName("sessions");
            writer.WriteStartArray();

            foreach (var session in _sessions.OrderByDescending(session => session.UpdatedAt))
            {
                writer.WriteStartObject();
                writer.WriteString("id", session.Id);
                writer.WriteString("title", session.Title);
                writer.WriteString("createdAt", session.CreatedAt.ToString("O"));
                writer.WriteString("updatedAt", session.UpdatedAt.ToString("O"));
                writer.WritePropertyName("messages");
                writer.WriteStartArray();

                foreach (var message in session.Messages)
                {
                    writer.WriteStartObject();
                    writer.WriteString("role", message.Role);
                    writer.WriteString("content", message.Content);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        File.WriteAllText(_sessionsPath, Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static ConversationSession ReadSession(JsonNode? node)
    {
        var session = new ConversationSession
        {
            Id = node?["id"]?.ToString() ?? string.Empty,
            Title = node?["title"]?.ToString() ?? "新会话",
            CreatedAt = ReadDate(node?["createdAt"]?.ToString()),
            UpdatedAt = ReadDate(node?["updatedAt"]?.ToString()),
        };

        var messages = node?["messages"]?.AsArray();
        if (messages is not null)
        {
            foreach (var message in messages)
            {
                session.Messages.Add(new ChatMessage
                {
                    Role = message?["role"]?.ToString() ?? string.Empty,
                    Content = message?["content"]?.ToString() ?? string.Empty,
                });
            }
        }

        return session;
    }

    private static DateTimeOffset ReadDate(string? value) => DateTimeOffset.TryParse(value, out var parsed)
        ? parsed
        : DateTimeOffset.Now;

    private static ConversationSession CreateSession(string title) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Title = title,
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
    };

    private static string CreateTitle(string prompt)
    {
        var title = prompt.ReplaceLineEndings(" ").Trim();
        return title.Length <= 30 ? title : title[..30] + "...";
    }
}
