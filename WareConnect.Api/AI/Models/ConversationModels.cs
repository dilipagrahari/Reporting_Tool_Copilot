namespace WareConnect.Api.AI.Models;

/// <summary>Role of a chat participant.</summary>
public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool
}

/// <summary>A single message in a conversation.</summary>
public sealed record ChatMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public MessageRole Role { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? ToolCallId { get; init; }
    public string? ToolName { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public int? TokenCount { get; init; }
}

/// <summary>Full conversation with history and metadata.</summary>
public sealed class Conversation
{
    public string ConversationId { get; init; } = Guid.NewGuid().ToString("N");
    public string? UserId { get; set; }
    public List<ChatMessage> Messages { get; init; } = [];
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public ScreenContext? LastScreenContext { get; set; }
}

/// <summary>Sliding window snapshot of messages sent to the model.</summary>
public sealed record ConversationHistory
{
    public string ConversationId { get; init; } = string.Empty;
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];
    public int TotalMessageCount { get; init; }
}
