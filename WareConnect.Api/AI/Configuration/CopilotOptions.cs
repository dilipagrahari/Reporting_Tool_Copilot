namespace WareConnect.Api.AI.Configuration;

/// <summary>A single AI model/deployment the user can choose from.</summary>
public sealed class ModelDefinition
{
    /// <summary>Azure deployment name (or OpenAI model name). Sent as ModelOverride from the UI.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-friendly name shown in the model picker.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// True for reasoning models (o1, o3, o4-mini) which reject Temperature and max_tokens.
    /// </summary>
    public bool IsReasoning { get; init; }
}

/// <summary>Strongly typed configuration bound from appsettings Copilot section.</summary>
public sealed class CopilotOptions
{
    public const string SectionName = "Copilot";

    public OpenAIOptions OpenAI { get; init; } = new();
    public MemoryOptions Memory { get; init; } = new();

    /// <summary>Base URL used by tools when calling existing REST APIs.</summary>
    public string BaseApiUrl { get; init; } = "http://localhost:5256";

    /// <summary>Models available to all users for selection in the UI.</summary>
    public IReadOnlyList<ModelDefinition> AvailableModels { get; init; } = [];
}

public sealed class OpenAIOptions
{
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Set this to your Azure OpenAI endpoint (e.g. https://aoai-datadevice-lab.openai.azure.com/)
    /// to use Azure OpenAI instead of the public OpenAI API.
    /// When empty the public OpenAI API is used.
    /// </summary>
    public string AzureEndpoint { get; init; } = string.Empty;

    /// <summary>
    /// Azure OpenAI deployment name (e.g. "gpt-4o").
    /// Ignored when using the public OpenAI API.
    /// </summary>
    public string DeploymentName { get; init; } = string.Empty;

    /// <summary>
    /// Azure OpenAI API version (e.g. "2025-01-01-preview").
    /// Ignored when using the public OpenAI API.
    /// </summary>
    public string ApiVersion { get; init; } = "2025-01-01-preview";

    public string Model { get; init; } = "gpt-4o";
    public int MaxTokens { get; init; } = 4096;
    public float Temperature { get; init; } = 0.2f;
    public int TimeoutSeconds { get; init; } = 120;
    public int MaxRetries { get; init; } = 3;

    /// <summary>True when an Azure endpoint is configured.</summary>
    public bool IsAzure => !string.IsNullOrWhiteSpace(AzureEndpoint);

    /// <summary>
    /// Reasoning models (o1, o3, o4-mini) do not support Temperature.
    /// Setting it causes an API error.
    /// </summary>
    public bool IsReasoningModel => Model.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
                                 || Model.StartsWith("o3", StringComparison.OrdinalIgnoreCase)
                                 || Model.StartsWith("o4", StringComparison.OrdinalIgnoreCase);
}

public sealed class MemoryOptions
{
    /// <summary>Maximum messages included in each OpenAI request.</summary>
    public int MaxMessagesInContext { get; init; } = 15;

    /// <summary>Maximum messages retained in in-memory store per conversation.</summary>
    public int MaxStoredMessages { get; init; } = 100;

    /// <summary>
    /// When true, SQL Server is used for conversation persistence.
    /// Requires the Copilot_ConversationSchema.sql tables to exist.
    /// </summary>
    public bool EnableSqlPersistence { get; init; } = false;
}
