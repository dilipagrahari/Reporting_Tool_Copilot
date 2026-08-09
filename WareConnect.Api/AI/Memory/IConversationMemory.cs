using WareConnect.Api.AI.Models;

namespace WareConnect.Api.AI.Memory;

/// <summary>Manages conversation storage and the sliding context window.</summary>
public interface IConversationMemory
{
    /// <summary>
    /// Returns existing conversation or creates a new one.
    /// <paramref name="userId"/> is stored on creation; use "1" for single-user testing.
    /// </summary>
    Task<Conversation> GetOrCreateAsync(string? conversationId, string userId = "1", CancellationToken ct = default);

    /// <summary>Appends a message to the conversation.</summary>
    Task AppendAsync(string conversationId, ChatMessage message, CancellationToken ct = default);

    /// <summary>Returns up to <c>maxMessages</c> most recent messages to include in the AI context.</summary>
    Task<ConversationHistory> GetContextWindowAsync(string conversationId, int maxMessages, CancellationToken ct = default);

    /// <summary>Updates the stored screen context associated with the conversation.</summary>
    Task UpdateContextAsync(string conversationId, ScreenContext? ctx, CancellationToken ct = default);

    /// <summary>Writes a token-usage audit record for the completed turn.</summary>
    Task LogUsageAsync(string conversationId, string userId, AIUsage usage, string? toolInvoked, int? latencyMs, CancellationToken ct = default);

    /// <summary>Clears all messages for a conversation.</summary>
    Task ClearAsync(string conversationId, CancellationToken ct = default);
}
