using WareConnect.Api.AI.Models;

namespace WareConnect.Api.AI.Memory;

/// <summary>
/// SQL Server-backed repository for conversation and message persistence.
/// Designed so it can be replaced with a Redis or CosmosDB implementation without
/// changing the <see cref="IConversationMemory"/> contract.
/// </summary>
public interface IConversationRepository
{
    // ── Conversation CRUD ────────────────────────────────────────────────────

    Task<ConversationEntity?> GetConversationAsync(string conversationId, CancellationToken ct = default);

    Task<ConversationEntity> CreateConversationAsync(ConversationEntity entity, CancellationToken ct = default);

    Task UpdateLastActivityAsync(string conversationId, ScreenContext? ctx, CancellationToken ct = default);

    // ── Message CRUD ─────────────────────────────────────────────────────────

    Task<IReadOnlyList<MessageEntity>> GetMessagesAsync(string conversationId, CancellationToken ct = default);

    Task<MessageEntity> AddMessageAsync(MessageEntity entity, CancellationToken ct = default);

    Task<int> GetMessageCountAsync(string conversationId, CancellationToken ct = default);

    // ── Usage logging ────────────────────────────────────────────────────────

    Task LogUsageAsync(UsageLogEntity entity, CancellationToken ct = default);
}
