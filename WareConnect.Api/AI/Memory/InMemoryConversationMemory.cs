using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WareConnect.Api.AI.Configuration;
using WareConnect.Api.AI.Models;

namespace WareConnect.Api.AI.Memory;

/// <summary>
/// In-process conversation memory backed by a concurrent dictionary.
/// Replace with a Redis-backed implementation when horizontal scaling is needed.
/// </summary>
public sealed class InMemoryConversationMemory : IConversationMemory
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Conversation> _store = new();
    private readonly MemoryOptions _options;
    private readonly ILogger<InMemoryConversationMemory> _logger;

    public InMemoryConversationMemory(
        IOptions<CopilotOptions> options,
        ILogger<InMemoryConversationMemory> logger)
    {
        _options = options.Value.Memory;
        _logger = logger;
    }

    public Task<Conversation> GetOrCreateAsync(string? conversationId, string userId = "1", CancellationToken ct = default)
    {
        var key = string.IsNullOrWhiteSpace(conversationId) ? Guid.NewGuid().ToString("N") : conversationId;
        var conversation = _store.GetOrAdd(key, id => new Conversation { ConversationId = id, UserId = userId });
        return Task.FromResult(conversation);
    }

    public Task AppendAsync(string conversationId, ChatMessage message, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(conversationId, out var conversation))
        {
            _logger.LogWarning("AppendAsync: conversation {Id} not found, creating.", conversationId);
            conversation = _store.GetOrAdd(conversationId, id => new Conversation { ConversationId = id });
        }

        lock (conversation.Messages)
        {
            conversation.Messages.Add(message);
            conversation.LastActivityAt = DateTime.UtcNow;

            // Trim oldest non-system messages when storage cap exceeded
            while (conversation.Messages.Count > _options.MaxStoredMessages)
            {
                var oldest = conversation.Messages.FirstOrDefault(m => m.Role != MessageRole.System);
                if (oldest is not null)
                    conversation.Messages.Remove(oldest);
                else
                    break;
            }
        }

        return Task.CompletedTask;
    }

    public Task<ConversationHistory> GetContextWindowAsync(string conversationId, int maxMessages, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(conversationId, out var conversation))
        {
            return Task.FromResult(new ConversationHistory
            {
                ConversationId = conversationId,
                Messages = [],
                TotalMessageCount = 0
            });
        }

        List<ChatMessage> window;
        lock (conversation.Messages)
        {
            // Always keep system messages; take latest N of the rest
            var system = conversation.Messages.Where(m => m.Role == MessageRole.System).ToList();
            var nonSystem = conversation.Messages
                .Where(m => m.Role != MessageRole.System)
                .TakeLast(maxMessages)
                .ToList();

            window = [.. system, .. nonSystem];
        }

        return Task.FromResult(new ConversationHistory
        {
            ConversationId = conversationId,
            Messages = window,
            TotalMessageCount = conversation.Messages.Count
        });
    }

    public Task ClearAsync(string conversationId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(conversationId, out var conversation))
        {
            lock (conversation.Messages)
            {
                conversation.Messages.Clear();
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>No-op for in-memory: context is already held in the Conversation object.</summary>
    public Task UpdateContextAsync(string conversationId, ScreenContext? ctx, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>No-op for in-memory: usage logging requires SQL persistence.</summary>
    public Task LogUsageAsync(
        string conversationId,
        string userId,
        AIUsage usage,
        string? toolInvoked,
        int? latencyMs,
        CancellationToken ct = default)
        => Task.CompletedTask;
}
