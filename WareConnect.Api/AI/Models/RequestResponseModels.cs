namespace WareConnect.Api.AI.Models;

/// <summary>
/// Raw context payload sent by the Angular frontend with every chat request.
/// All fields are optional; the backend must never ask GPT to infer missing values.
/// </summary>
public sealed record ScreenContext
{
    public string? CurrentPage { get; init; }
    public string? CurrentModule { get; init; }
    public string? CurrentCompanyId { get; init; }
    public string? CurrentCompany { get; init; }
    public string? CurrentVendorId { get; init; }
    public string? CurrentVendor { get; init; }
    public string? CurrentInvoiceId { get; init; }
    public string? SelectedRowId { get; init; }
    public Dictionary<string, string> ActiveFilters { get; init; } = [];
    public string? Language { get; init; } = "en";
    public string? TimeZone { get; init; } = "UTC";
}

/// <summary>
/// Sanitised, resolved context produced by <c>IContextBuilder</c> for use in prompts and tool calls.
/// </summary>
public sealed record ResolvedContext
{
    public string? CurrentPage { get; init; }
    public string? CurrentModule { get; init; }
    public string? CurrentCompanyId { get; init; }
    public string? CurrentCompany { get; init; }
    public string? CurrentVendorId { get; init; }
    public string? CurrentVendor { get; init; }
    public string? CurrentInvoiceId { get; init; }
    public string? SelectedRowId { get; init; }
    public Dictionary<string, string> ActiveFilters { get; init; } = [];
    public string Language { get; init; } = "en";
    public string TimeZone { get; init; } = "UTC";
}

/// <summary>Inbound chat request from the Angular frontend.</summary>
public sealed record ChatRequest
{
    public string? ConversationId { get; init; }

    /// <summary>
    /// The authenticated user identifier. Defaults to "1" for single-user testing.
    /// Replace with a real identity claim once authentication is added.
    /// </summary>
    public string UserId { get; init; } = "1";

    public string Message { get; init; } = string.Empty;
    public ScreenContext? ScreenContext { get; init; }

    /// <summary>Optional deployment/model the user selected in the UI. Null = use default.</summary>
    public string? ModelOverride { get; init; }
}

/// <summary>A model option returned by GET /api/copilot/models.</summary>
public sealed record ModelOption
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
}

/// <summary>Single chunk emitted via SSE during streaming.</summary>
public sealed record StreamingChunk
{
    public string ConversationId { get; init; } = string.Empty;
    public string? Delta { get; init; }
    public bool IsDone { get; init; }
    public string? Error { get; init; }
    public AIUsage? Usage { get; init; }
}

/// <summary>Token-usage summary attached to final streaming chunk.</summary>
public sealed record AIUsage
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }
    public string Model { get; init; } = string.Empty;
}
