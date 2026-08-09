using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WareConnect.Api.AI.Configuration;
using WareConnect.Api.AI.Models;

namespace WareConnect.Api.AI.Memory;

/// <summary>
/// SQL Server-backed implementation of <see cref="IConversationMemory"/>.
/// Stores every message in <c>Copilot_Messages</c> and returns a sliding
/// context window of the most recent messages for each OpenAI request.
/// <para>
/// Set <c>Copilot:Memory:EnableSqlPersistence = false</c> to fall back to
/// <see cref="InMemoryConversationMemory"/> during local development.
/// </para>
/// </summary>
public sealed class SqlConversationMemory : IConversationMemory
{
    private readonly IConversationRepository _repo;
    private readonly MemoryOptions _opts;
    private readonly ILogger<SqlConversationMemory> _logger;

    public SqlConversationMemory(
        IConversationRepository repo,
        IOptions<CopilotOptions> options,
        ILogger<SqlConversationMemory> logger)
    {
        _repo   = repo;
        _opts   = options.Value.Memory;
        _logger = logger;
    }

    public async Task<Conversation> GetOrCreateAsync(string? conversationId, string userId = "1", CancellationToken ct = default)
    {
        var id = string.IsNullOrWhiteSpace(conversationId)
            ? Guid.NewGuid().ToString("N")
            : conversationId;

        var entity = await _repo.GetConversationAsync(id, ct);

        if (entity is null)
        {
            entity = await _repo.CreateConversationAsync(
                new ConversationEntity { ConversationId = id, UserId = userId }, ct);
            _logger.LogInformation("New conversation created: {Id} for user {UserId}", id, userId);
        }

        // Load messages into a transient in-memory Conversation object
        var messages = await _repo.GetMessagesAsync(id, ct);
        var conversation = new Conversation
        {
            ConversationId  = id,
            UserId          = entity.UserId,
            CreatedAt       = entity.CreatedAt,
            LastActivityAt  = entity.LastActivityAt,
        };

        foreach (var m in messages)
        {
            conversation.Messages.Add(new ChatMessage
            {
                Role       = ParseRole(m.Role),
                Content    = m.Content,
                ToolCallId = m.ToolCallId,
                ToolName   = m.ToolName,
                CreatedAt  = m.CreatedAt,
            });
        }

        return conversation;
    }

    public async Task AppendAsync(string conversationId, ChatMessage message, CancellationToken ct = default)
    {
        var entity = new MessageEntity
        {
            ConversationId = conversationId,
            Role           = message.Role.ToString().ToLowerInvariant(),
            Content        = message.Content,
            ToolCallId     = message.ToolCallId,
            ToolName       = message.ToolName,
            PromptTokens   = message.TokenCount,
        };

        await _repo.AddMessageAsync(entity, ct);
    }

    public async Task<ConversationHistory> GetContextWindowAsync(
        string conversationId,
        int maxMessages,
        CancellationToken ct = default)
    {
        var all = await _repo.GetMessagesAsync(conversationId, ct);

        // Always retain system messages; take the last N non-system messages
        var system    = all.Where(m => m.Role == "system").ToList();
        var nonSystem = all.Where(m => m.Role != "system")
                           .TakeLast(maxMessages)
                           .ToList();

        var window = system.Concat(nonSystem)
            .Select(m => new ChatMessage
            {
                Role       = ParseRole(m.Role),
                Content    = m.Content,
                ToolCallId = m.ToolCallId,
                ToolName   = m.ToolName,
                CreatedAt  = m.CreatedAt,
            })
            .ToList();

        return new ConversationHistory
        {
            ConversationId    = conversationId,
            Messages          = window,
            TotalMessageCount = all.Count
        };
    }

    public async Task UpdateContextAsync(string conversationId, ScreenContext? ctx, CancellationToken ct = default)
    {
        await _repo.UpdateLastActivityAsync(conversationId, ctx, ct);
    }

    public async Task LogUsageAsync(string conversationId, string userId, AIUsage usage, string? toolInvoked, int? latencyMs, CancellationToken ct = default)
    {
        await _repo.LogUsageAsync(new UsageLogEntity
        {
            ConversationId   = conversationId,
            UserId           = userId,
            Model            = usage.Model,
            PromptTokens     = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            TotalTokens      = usage.TotalTokens,
            ToolInvoked      = toolInvoked,
            LatencyMs        = latencyMs,
        }, ct);
    }

    public Task ClearAsync(string conversationId, CancellationToken ct = default)
    {
        // SQL cascade delete is handled via FK; for a clear we re-create the conversation
        _logger.LogInformation("Clear not fully implemented for SQL memory — messages remain in DB.");
        return Task.CompletedTask;
    }

    private static MessageRole ParseRole(string role) => role.ToLowerInvariant() switch
    {
        "user"      => MessageRole.User,
        "assistant" => MessageRole.Assistant,
        "tool"      => MessageRole.Tool,
        _           => MessageRole.System
    };
}
