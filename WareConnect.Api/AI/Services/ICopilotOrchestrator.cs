using WareConnect.Api.AI.Models;

namespace WareConnect.Api.AI.Services;

/// <summary>Orchestrates a full copilot turn: memory → prompt → model → memory.</summary>
public interface ICopilotOrchestrator
{
    /// <summary>
    /// Processes a user message and yields streaming chunks.
    /// Persists user message and assistant response to conversation memory.
    /// </summary>
    IAsyncEnumerable<StreamingChunk> HandleAsync(
        ChatRequest request,
        CancellationToken ct = default);
}
