using WareConnect.Api.AI.Models;

namespace WareConnect.Api.AI.Services;

/// <summary>
/// Sends a prompt to the OpenAI model and streams the response token by token.
/// Handles function-call loops internally until the model produces a final text response.
/// </summary>
public interface ICopilotResponseService
{
    /// <summary>
    /// Streams <see cref="StreamingChunk"/> items via an <see cref="IAsyncEnumerable{T}"/>.
    /// The final chunk has <see cref="StreamingChunk.IsDone"/> set to <c>true</c>.
    /// </summary>
    IAsyncEnumerable<StreamingChunk> StreamAsync(
        PromptContext context,
        CancellationToken ct = default);
}
