using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.ClientModel;
using WareConnect.Api.AI.Configuration;
using WareConnect.Api.AI.Models;
using WareConnect.Api.AI.Services;

namespace WareConnect.Api.Controllers;

/// <summary>
/// WareConnect Copilot endpoint.
/// Streams the AI response as Server-Sent Events (SSE) so the browser can
/// render tokens as they arrive without waiting for the full response.
/// </summary>
[ApiController]
[Route("api/copilot")]
public sealed class CopilotController : ControllerBase
{
    private readonly ICopilotOrchestrator _orchestrator;
    private readonly CopilotOptions _copilotOptions;
    private readonly ILogger<CopilotController> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CopilotController(
        ICopilotOrchestrator orchestrator,
        IOptions<CopilotOptions> options,
        ILogger<CopilotController> logger)
    {
        _orchestrator    = orchestrator;
        _copilotOptions  = options.Value;
        _logger          = logger;
    }

    /// <summary>Returns the list of available AI models for the model picker.</summary>
    [HttpGet("models")]
    public IActionResult GetModels()
    {
        var defaultId = _copilotOptions.OpenAI.DeploymentName;
        var models = _copilotOptions.AvailableModels.Select(m => new ModelOption
        {
            Id          = m.Id,
            DisplayName = m.DisplayName,
            IsDefault   = m.Id == defaultId
        }).ToList();

        // Ensure the default deployment is always present even if not listed
        if (!models.Any(m => m.Id == defaultId))
        {
            models.Insert(0, new ModelOption
            {
                Id          = defaultId,
                DisplayName = _copilotOptions.OpenAI.Model,
                IsDefault   = true
            });
        }

        return Ok(models);
    }

    /// <summary>
    /// Accepts a user message and streams the AI response via SSE.
    /// Each data event contains a JSON-serialised <see cref="StreamingChunk"/>.
    /// The stream ends with a chunk where <c>isDone</c> is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// **Frontend consumption (JavaScript / Angular)**
    /// <code>
    /// const es = new EventSource('/api/copilot/chat', ...);
    /// es.onmessage = (e) => { const chunk = JSON.parse(e.data); ... };
    /// </code>
    ///
    /// Because SSE requires a GET, this endpoint accepts the request body via POST
    /// but returns the response as <c>text/event-stream</c>.
    /// </remarks>
    [HttpPost("chat")]
    public async Task StreamChat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("Message is required.", cancellationToken);
            return;
        }

        // Configure SSE headers
        Response.Headers["Content-Type"]  = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"]    = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no"; // Disable nginx buffering

        _logger.LogInformation("SSE chat started | conversation={Id}", request.ConversationId ?? "new");

        try
        {
            await foreach (var chunk in _orchestrator.HandleAsync(request, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested) break;

                var json    = JsonSerializer.Serialize(chunk, _jsonOptions);
                var payload = $"data: {json}\n\n";

                await Response.WriteAsync(payload, Encoding.UTF8, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            // Terminal SSE event signals the browser to close the connection
            await Response.WriteAsync("event: done\ndata: {}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SSE stream cancelled | conversation={Id}", request.ConversationId);
        }
        catch (ClientResultException crex)
        {
            _logger.LogError(crex, "OpenAI API error | status={Status} | conversation={Id}",
                crex.Status, request.ConversationId);

            var userMessage = crex.Status switch
            {
                429 => "⚠️ The AI service is temporarily unavailable: your OpenAI account has no remaining credits. Please add credits at https://platform.openai.com/settings/organization/billing and try again.",
                401 => "⚠️ The AI service rejected the request: invalid or missing API key. Please check the OpenAI API key in appsettings.json.",
                503 => "⚠️ The AI service is currently overloaded. Please wait a moment and try again.",
                _   => $"⚠️ The AI service returned an error (HTTP {crex.Status}). Please try again."
            };

            await WriteSseErrorAsync(userMessage, request.ConversationId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSE stream error | conversation={Id}", request.ConversationId);
            await WriteSseErrorAsync("An unexpected error occurred. Please try again.", request.ConversationId, cancellationToken);
        }
    }

    private async Task WriteSseErrorAsync(string message, string? conversationId, CancellationToken ct)
    {
        var errorChunk = new StreamingChunk
        {
            ConversationId = conversationId ?? string.Empty,
            Delta          = message,
            IsDone         = true,
            Error          = message
        };

        var json    = JsonSerializer.Serialize(errorChunk, _jsonOptions);
        var payload = $"data: {json}\n\n";

        try
        {
            await Response.WriteAsync(payload, ct);
            await Response.Body.FlushAsync(ct);
        }
        catch
        {
            // Client already disconnected – swallow
        }
    }
}
