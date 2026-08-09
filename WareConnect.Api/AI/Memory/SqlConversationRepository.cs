using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WareConnect.Api.AI.Models;

namespace WareConnect.Api.AI.Memory;

/// <summary>
/// ADO.NET SQL Server implementation of <see cref="IConversationRepository"/>.
/// Uses the same DefaultConnection string as the rest of the application.
/// </summary>
public sealed class SqlConversationRepository : IConversationRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqlConversationRepository> _logger;

    public SqlConversationRepository(
        IConfiguration configuration,
        ILogger<SqlConversationRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _logger = logger;
    }

    // ── Conversation ─────────────────────────────────────────────────────────

    public async Task<ConversationEntity?> GetConversationAsync(string conversationId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT ConversationId, UserId, Title, CurrentPage, CurrentCompany, CurrentVendor,
       CurrentInvoiceId, CurrentModule, Language, TimeZone, IsActive, CreatedAt, LastActivityAt
FROM   dbo.Copilot_Conversations
WHERE  ConversationId = @ConversationId;";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ConversationId", conversationId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return MapConversation(reader);
    }

    public async Task<ConversationEntity> CreateConversationAsync(ConversationEntity entity, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO dbo.Copilot_Conversations
    (ConversationId, UserId, Title, CurrentPage, CurrentCompany, CurrentVendor,
     CurrentInvoiceId, CurrentModule, Language, TimeZone, IsActive, CreatedAt, LastActivityAt)
VALUES
    (@ConversationId, @UserId, @Title, @CurrentPage, @CurrentCompany, @CurrentVendor,
     @CurrentInvoiceId, @CurrentModule, @Language, @TimeZone, 1, SYSUTCDATETIME(), SYSUTCDATETIME());";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@ConversationId",   entity.ConversationId);
        cmd.Parameters.AddWithValue("@UserId",           entity.UserId);
        cmd.Parameters.AddWithValue("@Title",            (object?)entity.Title           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CurrentPage",      (object?)entity.CurrentPage      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CurrentCompany",   (object?)entity.CurrentCompany   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CurrentVendor",    (object?)entity.CurrentVendor    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CurrentInvoiceId", (object?)entity.CurrentInvoiceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CurrentModule",    (object?)entity.CurrentModule    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Language",         entity.Language);
        cmd.Parameters.AddWithValue("@TimeZone",         entity.TimeZone);

        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("Created conversation {Id}", entity.ConversationId);
        return entity;
    }

    public async Task UpdateLastActivityAsync(string conversationId, ScreenContext? ctx, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE dbo.Copilot_Conversations
SET    LastActivityAt   = SYSUTCDATETIME(),
       CurrentPage      = COALESCE(@CurrentPage,      CurrentPage),
       CurrentCompany   = COALESCE(@CurrentCompany,   CurrentCompany),
       CurrentVendor    = COALESCE(@CurrentVendor,    CurrentVendor),
       CurrentInvoiceId = COALESCE(@CurrentInvoiceId, CurrentInvoiceId),
       CurrentModule    = COALESCE(@CurrentModule,    CurrentModule)
WHERE  ConversationId   = @ConversationId;";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@ConversationId",   conversationId);
        cmd.Parameters.AddWithValue("@CurrentPage",      (object?)ctx?.CurrentPage      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CurrentCompany",   (object?)ctx?.CurrentCompany   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CurrentVendor",    (object?)ctx?.CurrentVendor    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CurrentInvoiceId", (object?)ctx?.CurrentInvoiceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CurrentModule",    (object?)ctx?.CurrentModule    ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Messages ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<MessageEntity>> GetMessagesAsync(string conversationId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT MessageId, ConversationId, Role, Content, ToolCallId, ToolName,
       PromptTokens, CompletionTokens, TotalTokens, Model, LatencyMs, CreatedAt
FROM   dbo.Copilot_Messages
WHERE  ConversationId = @ConversationId
ORDER  BY CreatedAt ASC, MessageId ASC;";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ConversationId", conversationId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var messages = new List<MessageEntity>();
        while (await reader.ReadAsync(ct))
            messages.Add(MapMessage(reader));

        return messages;
    }

    public async Task<MessageEntity> AddMessageAsync(MessageEntity entity, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO dbo.Copilot_Messages
    (ConversationId, Role, Content, ToolCallId, ToolName,
     PromptTokens, CompletionTokens, TotalTokens, Model, LatencyMs, CreatedAt)
OUTPUT INSERTED.MessageId
VALUES
    (@ConversationId, @Role, @Content, @ToolCallId, @ToolName,
     @PromptTokens, @CompletionTokens, @TotalTokens, @Model, @LatencyMs, SYSUTCDATETIME());";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@ConversationId",   entity.ConversationId);
        cmd.Parameters.AddWithValue("@Role",             entity.Role);
        cmd.Parameters.AddWithValue("@Content",          entity.Content);
        cmd.Parameters.AddWithValue("@ToolCallId",       (object?)entity.ToolCallId       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ToolName",         (object?)entity.ToolName         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PromptTokens",     (object?)entity.PromptTokens     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CompletionTokens", (object?)entity.CompletionTokens ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TotalTokens",      (object?)entity.TotalTokens      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Model",            (object?)entity.Model            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LatencyMs",        (object?)entity.LatencyMs        ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(ct);
        entity.MessageId = Convert.ToInt64(id);
        return entity;
    }

    public async Task<int> GetMessageCountAsync(string conversationId, CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(1) FROM dbo.Copilot_Messages WHERE ConversationId = @ConversationId;";
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ConversationId", conversationId);
        return (int)await cmd.ExecuteScalarAsync(ct)!;
    }

    // ── Usage log ────────────────────────────────────────────────────────────

    public async Task LogUsageAsync(UsageLogEntity entity, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO dbo.Copilot_UsageLog
    (ConversationId, UserId, Model, PromptTokens, CompletionTokens,
     TotalTokens, ToolInvoked, LatencyMs, CreatedAt)
VALUES
    (@ConversationId, @UserId, @Model, @PromptTokens, @CompletionTokens,
     @TotalTokens, @ToolInvoked, @LatencyMs, SYSUTCDATETIME());";

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@ConversationId",   entity.ConversationId);
            cmd.Parameters.AddWithValue("@UserId",           entity.UserId);
            cmd.Parameters.AddWithValue("@Model",            entity.Model);
            cmd.Parameters.AddWithValue("@PromptTokens",     entity.PromptTokens);
            cmd.Parameters.AddWithValue("@CompletionTokens", entity.CompletionTokens);
            cmd.Parameters.AddWithValue("@TotalTokens",      entity.TotalTokens);
            cmd.Parameters.AddWithValue("@ToolInvoked",      (object?)entity.ToolInvoked ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LatencyMs",        (object?)entity.LatencyMs   ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            // Usage logging must never break the main flow
            _logger.LogWarning(ex, "Failed to log usage for conversation {Id}", entity.ConversationId);
        }
    }

    // ── mappers ──────────────────────────────────────────────────────────────

    private static ConversationEntity MapConversation(SqlDataReader r) => new()
    {
        ConversationId   = r.GetString(0),
        UserId           = r.GetString(1),
        Title            = r.IsDBNull(2)  ? null : r.GetString(2),
        CurrentPage      = r.IsDBNull(3)  ? null : r.GetString(3),
        CurrentCompany   = r.IsDBNull(4)  ? null : r.GetString(4),
        CurrentVendor    = r.IsDBNull(5)  ? null : r.GetString(5),
        CurrentInvoiceId = r.IsDBNull(6)  ? null : r.GetString(6),
        CurrentModule    = r.IsDBNull(7)  ? null : r.GetString(7),
        Language         = r.GetString(8),
        TimeZone         = r.GetString(9),
        IsActive         = r.GetBoolean(10),
        CreatedAt        = r.GetDateTime(11),
        LastActivityAt   = r.GetDateTime(12),
    };

    private static MessageEntity MapMessage(SqlDataReader r) => new()
    {
        MessageId        = r.GetInt64(0),
        ConversationId   = r.GetString(1),
        Role             = r.GetString(2),
        Content          = r.GetString(3),
        ToolCallId       = r.IsDBNull(4)  ? null : r.GetString(4),
        ToolName         = r.IsDBNull(5)  ? null : r.GetString(5),
        PromptTokens     = r.IsDBNull(6)  ? null : r.GetInt32(6),
        CompletionTokens = r.IsDBNull(7)  ? null : r.GetInt32(7),
        TotalTokens      = r.IsDBNull(8)  ? null : r.GetInt32(8),
        Model            = r.IsDBNull(9)  ? null : r.GetString(9),
        LatencyMs        = r.IsDBNull(10) ? null : r.GetInt32(10),
        CreatedAt        = r.GetDateTime(11),
    };
}
