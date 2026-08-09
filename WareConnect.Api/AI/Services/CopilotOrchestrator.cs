using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WareConnect.Api.AI.Configuration;
using WareConnect.Api.AI.Context;
using WareConnect.Api.AI.Memory;
using WareConnect.Api.AI.Models;
using WareConnect.Api.AI.Prompts;
using WareConnect.Api.AI.Tools;

namespace WareConnect.Api.AI.Services;

/// <inheritdoc />
public sealed class CopilotOrchestrator : ICopilotOrchestrator
{
    private readonly IConversationMemory _memory;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IToolDispatcher _toolDispatcher;
    private readonly ICopilotResponseService _responseService;
    private readonly IContextBuilder _contextBuilder;
    private readonly CopilotOptions _options;
    private readonly ILogger<CopilotOrchestrator> _logger;

    public CopilotOrchestrator(
        IConversationMemory memory,
        IPromptBuilder promptBuilder,
        IToolDispatcher toolDispatcher,
        ICopilotResponseService responseService,
        IContextBuilder contextBuilder,
        IOptions<CopilotOptions> options,
        ILogger<CopilotOrchestrator> logger)
    {
        _memory          = memory;
        _promptBuilder   = promptBuilder;
        _toolDispatcher  = toolDispatcher;
        _responseService = responseService;
        _contextBuilder  = contextBuilder;
        _options         = options.Value;
        _logger          = logger;
    }

    public async IAsyncEnumerable<StreamingChunk> HandleAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            yield return ErrorChunk("Message cannot be empty.", string.Empty);
            yield break;
        }

        // Sanitize input – strip potential prompt injection markers
        var userMessage = SanitizeInput(request.Message);

        // Resolve screen context
        var resolvedContext = _contextBuilder.Build(request.ScreenContext);

        // Resolve conversation (scoped to this user)
        var userId = string.IsNullOrWhiteSpace(request.UserId) ? "1" : request.UserId;
        var conversation = await _memory.GetOrCreateAsync(request.ConversationId, userId, ct);
        var conversationId = conversation.ConversationId;

        _logger.LogInformation("Copilot turn | conversation={Id} | user={Msg}", conversationId, userMessage[..Math.Min(80, userMessage.Length)]);

        // Update stored screen context
        await _memory.UpdateContextAsync(conversationId, request.ScreenContext, ct);

        // Persist user message
        await _memory.AppendAsync(conversationId, new ChatMessage
        {
            Role    = MessageRole.User,
            Content = userMessage
        }, ct);

        // Build context window
        var history = await _memory.GetContextWindowAsync(
            conversationId,
            _options.Memory.MaxMessagesInContext,
            ct);

        // Build prompt context
        var promptContext = _promptBuilder.BuildPromptContext(
            conversationId,
            userMessage,
            history,
            _toolDispatcher.GetToolDefinitions(),
            resolvedContext) with { ModelOverride = request.ModelOverride };

        // Stream response, accumulating full text for memory
        var assistantContent = new StringBuilder();
        string? lastError = null;
        AIUsage? lastUsage = null;
        var sw = Stopwatch.StartNew();

        await foreach (var chunk in _responseService.StreamAsync(promptContext, ct))
        {
            if (chunk.Delta is not null)
                assistantContent.Append(chunk.Delta);

            if (chunk.Error is not null)
                lastError = chunk.Error;

            if (chunk.Usage is not null)
                lastUsage = chunk.Usage;

            yield return chunk;
        }

        sw.Stop();

        // Persist assistant response
        var assistantText = assistantContent.ToString();
        if (!string.IsNullOrWhiteSpace(assistantText))
        {
            await _memory.AppendAsync(conversationId, new ChatMessage
            {
                Role    = MessageRole.Assistant,
                Content = assistantText
            }, ct);
        }

        // Log usage
        if (lastUsage is not null)
        {
            await _memory.LogUsageAsync(
                conversationId,
                userId,
                lastUsage,
                toolInvoked: null,
                latencyMs: (int)sw.ElapsedMilliseconds,
                ct);
        }

        if (lastError is not null)
            _logger.LogError("Copilot turn ended with error: {Error}", lastError);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string SanitizeInput(string input)
    {
        // Strip common prompt-injection markers
        return input
            .Replace("IGNORE PREVIOUS INSTRUCTIONS", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("###SYSTEM", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static StreamingChunk ErrorChunk(string message, string conversationId) =>
        new() { ConversationId = conversationId, Delta = message, IsDone = true, Error = message };
}
