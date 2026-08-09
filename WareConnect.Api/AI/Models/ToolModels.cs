namespace WareConnect.Api.AI.Models;

/// <summary>Describes a tool (function) the model may call.</summary>
public sealed record ToolDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<ToolParameter> Parameters { get; init; } = [];
}

/// <summary>A single parameter within a tool definition.</summary>
public sealed record ToolParameter
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "string";
    public string Description { get; init; } = string.Empty;
    public bool Required { get; init; }
    public IReadOnlyList<string>? AllowedValues { get; init; }
}

/// <summary>A tool invocation requested by the model.</summary>
public sealed record ToolRequest
{
    public string ToolCallId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public Dictionary<string, object?> Arguments { get; init; } = [];
}

/// <summary>Result returned after executing a tool.</summary>
public sealed record ToolResponse
{
    public string ToolCallId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}

/// <summary>Context assembled for a single model invocation.</summary>
public sealed record PromptContext
{
    public string ConversationId { get; init; } = string.Empty;
    public string SystemPrompt { get; init; } = string.Empty;
    public IReadOnlyList<ChatMessage> History { get; init; } = [];
    public IReadOnlyList<ToolDefinition> Tools { get; init; } = [];
    public ResolvedContext? ResolvedContext { get; init; }
    public string UserMessage { get; init; } = string.Empty;
    /// <summary>Optional deployment/model override chosen by the user in the UI.</summary>
    public string? ModelOverride { get; init; }
}
