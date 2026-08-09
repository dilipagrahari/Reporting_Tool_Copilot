using WareConnect.Api.AI.Models;

namespace WareConnect.Api.AI.Prompts;

/// <summary>Builds the system prompt and assembles the full prompt context for each model call.</summary>
public interface IPromptBuilder
{
    /// <summary>Semantic version of the current system prompt (e.g. "1.0").</summary>
    string PromptVersion { get; }

    /// <summary>Returns the system prompt incorporating the resolved screen context.</summary>
    string BuildSystemPrompt(ResolvedContext? resolvedContext);

    /// <summary>Assembles the full <see cref="PromptContext"/> used to call the model.</summary>
    PromptContext BuildPromptContext(
        string conversationId,
        string userMessage,
        ConversationHistory history,
        IReadOnlyList<ToolDefinition> tools,
        ResolvedContext? resolvedContext);
}
