namespace WareConnect.Api.AI.Models;

/// <summary>Entity representing a stored conversation row in Copilot_Conversations.</summary>
public sealed class ConversationEntity
{
    public string ConversationId { get; set; } = string.Empty;
    public string UserId { get; set; } = "1";
    public string? Title { get; set; }
    public string? CurrentPage { get; set; }
    public string? CurrentCompany { get; set; }
    public string? CurrentVendor { get; set; }
    public string? CurrentInvoiceId { get; set; }
    public string? CurrentModule { get; set; }
    public string Language { get; set; } = "en";
    public string TimeZone { get; set; } = "UTC";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Entity representing a single stored message row in Copilot_Messages.</summary>
public sealed class MessageEntity
{
    public long MessageId { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string? Model { get; set; }
    public int? LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Entity representing a usage audit row in Copilot_UsageLog.</summary>
public sealed class UsageLogEntity
{
    public long LogId { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string UserId { get; set; } = "1";
    public string Model { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public string? ToolInvoked { get; set; }
    public int? LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
