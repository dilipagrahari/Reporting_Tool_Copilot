using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using WareConnect.Api.AI.Configuration;
using WareConnect.Api.AI.Models;
using WareConnect.Api.AI.Tools;
using AppChatMessage = WareConnect.Api.AI.Models.ChatMessage;
using OaiChatMessage = OpenAI.Chat.ChatMessage;

namespace WareConnect.Api.AI.Services;

/// <inheritdoc />
public sealed class CopilotResponseService : ICopilotResponseService
{
    private readonly AzureOpenAIClient? _azureClient;
    private readonly OpenAIClient? _openAiClient;
    private readonly ChatClient _defaultChatClient;
    private readonly IToolDispatcher _toolDispatcher;
    private readonly CopilotOptions _options;
    private readonly ILogger<CopilotResponseService> _logger;

    private const int MaxToolRounds = 5;

    public CopilotResponseService(
        IOptions<CopilotOptions> options,
        IToolDispatcher toolDispatcher,
        ILogger<CopilotResponseService> logger)
    {
        _options        = options.Value;
        _toolDispatcher = toolDispatcher;
        _logger         = logger;

        var ai = _options.OpenAI;
        if (ai.IsAzure)
        {
            _azureClient        = new AzureOpenAIClient(new Uri(ai.AzureEndpoint), new AzureKeyCredential(ai.ApiKey));
            _defaultChatClient  = _azureClient.GetChatClient(ai.DeploymentName);
        }
        else
        {
            _openAiClient       = new OpenAIClient(ai.ApiKey);
            _defaultChatClient  = _openAiClient.GetChatClient(ai.Model);
        }
    }

    public async IAsyncEnumerable<StreamingChunk> StreamAsync(
        PromptContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages    = BuildOpenAiMessages(context);

        // Pick the chat client and reasoning flag for this request
        var deploymentId   = context.ModelOverride ?? _options.OpenAI.DeploymentName;
        var chatClient     = deploymentId == _options.OpenAI.DeploymentName
            ? _defaultChatClient
            : GetChatClientForDeployment(deploymentId);
        var isReasoning    = GetIsReasoning(deploymentId);
        var chatOptions    = BuildChatOptions(context.Tools, isReasoning);

        _logger.LogInformation("Using deployment={Dep} reasoning={R}", deploymentId, isReasoning);

        int promptTokens     = 0;
        int completionTokens = 0;

        for (int round = 0; round < MaxToolRounds; round++)
        {
            _logger.LogDebug("OpenAI round {Round}, messages={Count}", round + 1, messages.Count);

            var fullContent = new StringBuilder();

            // Accumulate tool call state keyed by index
            var toolCallIdsByIndex   = new Dictionary<int, string>();
            var toolCallNamesByIndex = new Dictionary<int, string>();
            var toolCallArgsByIndex  = new Dictionary<int, StringBuilder>();

            await foreach (var update in chatClient.CompleteChatStreamingAsync(messages, chatOptions, ct))
            {
                // Token usage (present on last update)
                if (update.Usage is { } usage)
                {
                    promptTokens     = usage.InputTokenCount;
                    completionTokens = usage.OutputTokenCount;
                }

                // Accumulate tool-call deltas
                foreach (var tc in update.ToolCallUpdates)
                {
                    var idx = tc.Index;

                    if (!string.IsNullOrEmpty(tc.ToolCallId))
                        toolCallIdsByIndex[idx] = tc.ToolCallId;

                    if (!string.IsNullOrEmpty(tc.FunctionName))
                        toolCallNamesByIndex[idx] = tc.FunctionName;

                    if (!toolCallArgsByIndex.ContainsKey(idx))
                        toolCallArgsByIndex[idx] = new StringBuilder();

                    toolCallArgsByIndex[idx].Append(tc.FunctionArgumentsUpdate?.ToString() ?? string.Empty);
                }

                // Stream text deltas to client immediately
                foreach (var part in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(part.Text))
                    {
                        fullContent.Append(part.Text);
                        yield return new StreamingChunk
                        {
                            ConversationId = context.ConversationId,
                            Delta          = part.Text,
                            IsDone         = false
                        };
                    }
                }
            }

            // No tool calls → final text response is complete
            if (toolCallIdsByIndex.Count == 0)
            {
                yield return new StreamingChunk
                {
                    ConversationId = context.ConversationId,
                    IsDone         = true,
                    Usage = new AIUsage
                    {
                        PromptTokens     = promptTokens,
                        CompletionTokens = completionTokens,
                        TotalTokens      = promptTokens + completionTokens,
                        Model            = _options.OpenAI.Model
                    }
                };
                yield break;
            }

            // Build the assistant tool-call message and feed tool results back
            var toolCalls = toolCallIdsByIndex.Keys.OrderBy(k => k).Select(idx =>
                ChatToolCall.CreateFunctionToolCall(
                    toolCallIdsByIndex[idx],
                    toolCallNamesByIndex.GetValueOrDefault(idx, string.Empty),
                    BinaryData.FromString(toolCallArgsByIndex[idx].ToString())
                )).ToList();

            messages.Add(new AssistantChatMessage(toolCalls));

            foreach (var tc in toolCalls)
            {
                var toolRequest = new ToolRequest
                {
                    ToolCallId = tc.Id,
                    ToolName   = tc.FunctionName,
                    Arguments  = ParseArguments(tc.FunctionArguments.ToString())
                };

                var toolResult = await _toolDispatcher.ExecuteAsync(toolRequest, ct);
                _logger.LogInformation("Tool {Name} executed, success={Ok}", toolRequest.ToolName, toolResult.Success);

                messages.Add(new ToolChatMessage(toolResult.ToolCallId, toolResult.Content));
            }
        }

        // Exceeded max tool rounds — safe fallback
        _logger.LogWarning("Max tool rounds ({Max}) exceeded for conversation {Id}", MaxToolRounds, context.ConversationId);
        yield return new StreamingChunk
        {
            ConversationId = context.ConversationId,
            Delta          = "I was unable to complete your request within the allowed steps. Please try rephrasing your question.",
            IsDone         = false
        };
        yield return new StreamingChunk { ConversationId = context.ConversationId, IsDone = true };
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private ChatClient GetChatClientForDeployment(string deploymentId)
    {
        if (_azureClient is not null) return _azureClient.GetChatClient(deploymentId);
        if (_openAiClient is not null) return _openAiClient.GetChatClient(deploymentId);
        throw new InvalidOperationException("No AI client configured.");
    }

    private bool GetIsReasoning(string deploymentId)
    {
        // Check explicit config first
        var def = _options.AvailableModels.FirstOrDefault(m => m.Id == deploymentId);
        if (def is not null) return def.IsReasoning;
        // Fallback: name-based heuristic
        return deploymentId.Contains("o1", StringComparison.OrdinalIgnoreCase)
            || deploymentId.Contains("o3", StringComparison.OrdinalIgnoreCase)
            || deploymentId.Contains("o4", StringComparison.OrdinalIgnoreCase);
    }

    private static List<OaiChatMessage> BuildOpenAiMessages(PromptContext context)
    {
        var messages = new List<OaiChatMessage>
        {
            new SystemChatMessage(context.SystemPrompt)
        };

        foreach (var msg in context.History)
        {
            OaiChatMessage? chatMsg = msg.Role switch
            {
                MessageRole.User      => new UserChatMessage(msg.Content),
                MessageRole.Assistant => new AssistantChatMessage(msg.Content),
                MessageRole.Tool      => new ToolChatMessage(msg.ToolCallId ?? string.Empty, msg.Content),
                _                     => null
            };

            if (chatMsg is not null)
                messages.Add(chatMsg);
        }

        messages.Add(new UserChatMessage(context.UserMessage));
        return messages;
    }

    private ChatCompletionOptions BuildChatOptions(IReadOnlyList<ToolDefinition> tools, bool isReasoning)
    {
        var options = new ChatCompletionOptions();

        // Reasoning models (o1, o3, o4-mini) do not support max_tokens or Temperature.
        if (!isReasoning)
        {
            options.MaxOutputTokenCount = _options.OpenAI.MaxTokens;
            options.Temperature         = _options.OpenAI.Temperature;
        }

        foreach (var tool in tools)
        {
            var parametersJson = BuildParametersJson(tool.Parameters);
            options.Tools.Add(ChatTool.CreateFunctionTool(
                functionName:        tool.Name,
                functionDescription: tool.Description,
                functionParameters:  BinaryData.FromString(parametersJson)));
        }

        return options;
    }

    private static string BuildParametersJson(IReadOnlyList<ToolParameter> parameters)
    {
        if (parameters.Count == 0)
            return """{"type":"object","properties":{}}""";

        var props    = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var p in parameters)
        {
            var prop = new Dictionary<string, object> { ["type"] = p.Type, ["description"] = p.Description };
            if (p.AllowedValues?.Count > 0)
                prop["enum"] = p.AllowedValues;

            props[p.Name] = prop;
            if (p.Required)
                required.Add(p.Name);
        }

        return JsonSerializer.Serialize(new
        {
            type       = "object",
            properties = props,
            required
        });
    }

    private static Dictionary<string, object?> ParseArguments(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
