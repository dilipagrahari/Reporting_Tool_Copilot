using WareConnect.Api.AI.Models;

namespace WareConnect.Api.AI.Tools;

/// <summary>Resolves and executes a named AI tool against the existing REST APIs.</summary>
public interface IToolDispatcher
{
    /// <summary>Returns definitions of all registered tools for inclusion in the OpenAI request.</summary>
    IReadOnlyList<ToolDefinition> GetToolDefinitions();

    /// <summary>Executes the tool identified by <paramref name="request"/> and returns its result.</summary>
    Task<ToolResponse> ExecuteAsync(ToolRequest request, CancellationToken ct = default);
}
