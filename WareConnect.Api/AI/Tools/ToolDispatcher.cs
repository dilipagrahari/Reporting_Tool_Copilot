using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WareConnect.Api.AI.Configuration;
using WareConnect.Api.AI.Models;

namespace WareConnect.Api.AI.Tools;

/// <summary>
/// Routes model-requested tool calls to the corresponding internal GET APIs.
/// Every tool is a safe, read-only HTTP GET against the existing REST endpoints.
/// </summary>
public sealed class ToolDispatcher : IToolDispatcher
{
    private readonly HttpClient _http;
    private readonly ILogger<ToolDispatcher> _logger;
    private readonly string _baseUrl;

    private static readonly IReadOnlyList<ToolDefinition> _definitions =
    [
        new()
        {
            Name        = "GetReportYears",
            Description = $"Returns the list of available report year tables (e.g. 2018–{DateTime.UtcNow.Year}). Call this only when you genuinely need to list all years; do NOT call it just to find the current year — the current year is always {DateTime.UtcNow.Year}.",
            Parameters  = []
        },
        new()
        {
            Name        = "GetReportData",
            Description = $"Returns paginated raw rows for a specific year. Use this only when the user needs to see individual rows. For totals or sums use GetYearSummary instead. If no year is specified by the user, use {DateTime.UtcNow.Year}.",
            Parameters  =
            [
                new() { Name = "year",       Type = "integer", Description = "The report year (e.g. 2024).",          Required = true  },
                new() { Name = "pageNumber", Type = "integer", Description = "1-based page number.",                   Required = false },
                new() { Name = "pageSize",   Type = "integer", Description = "Rows per page (max 100).",               Required = false }
            ]
        },
        new()
        {
            Name        = "GetYearSummary",
            Description = $"Returns grand-total sums for an entire year: TotalAmount, TotalSales, TotalOtherExp, TotalGP2, TotalDistinctGP2, TotalBudgetAmount, TotalLYRBudgetAmount, TotalMonthAmount, TotalMonthBudgetAmount, TotalMonthLYRBudgetAmount, and TotalRows. Use this whenever the user asks for a total, sum, or overall figure for a year. If the user says 'this year' or 'current year', use {DateTime.UtcNow.Year}. If the user says 'last year' or 'previous year', use {DateTime.UtcNow.Year - 1}.",
            Parameters  =
            [
                new() { Name = "year", Type = "integer", Description = "The report year.", Required = true }
            ]
        },
        new()
        {
            Name        = "GetMonthlyBreakdown",
            Description = $"Returns Amount, Sales, OtherExp, GP2, BudgetAmount, and MonthAmount grouped by month (January → December) for a given year. Use this for monthly trends, comparisons between months, or when the user asks about a specific month's totals. Default year = {DateTime.UtcNow.Year} if unspecified.",
            Parameters  =
            [
                new() { Name = "year", Type = "integer", Description = "The report year.", Required = true }
            ]
        },
        new()
        {
            Name        = "GetGroupBreakdown",
            Description = "Returns Amount, Sales, OtherExp, GP2, and BudgetAmount grouped by GroupName for a given year, sorted by highest amount. Use this when the user asks which group has the most sales, or wants a breakdown by group.",
            Parameters  =
            [
                new() { Name = "year", Type = "integer", Description = "The report year.", Required = true }
            ]
        },
        new()
        {
            Name        = "GetAccountTypeBreakdown",
            Description = "Returns totals grouped by AccountType for a given year. Use this when the user asks about account types.",
            Parameters  =
            [
                new() { Name = "year", Type = "integer", Description = "The report year.", Required = true }
            ]
        },
        new()
        {
            Name        = "GetItemTypeBreakdown",
            Description = "Returns totals grouped by ItemType for a given year. Use this when the user asks about item types or categories.",
            Parameters  =
            [
                new() { Name = "year", Type = "integer", Description = "The report year.", Required = true }
            ]
        },
        new()
        {
            Name        = "CompareYears",
            Description = "Compares two years side by side showing Amount, Sales, OtherExp, GP2, and Budget for each year plus the variance (yearB − yearA). Use this when the user asks to compare two years or asks about growth/decline.",
            Parameters  =
            [
                new() { Name = "yearA", Type = "integer", Description = "The base/earlier year.",      Required = true },
                new() { Name = "yearB", Type = "integer", Description = "The comparison/later year.",  Required = true }
            ]
        },
        new()
        {
            Name        = "GetFilteredData",
            Description = $"Returns filtered rows AND aggregate totals (Amount, Sales, OtherExp, GP2, Budget) for a year. All filter parameters are optional — combine them to answer questions like 'total sales for June' or 'GP2 for the Food group'. Default year = {DateTime.UtcNow.Year} when the user says 'this year', 'current year', or gives no year. Use {DateTime.UtcNow.Year - 1} for 'last year'.",
            Parameters  =
            [
                new() { Name = "year",        Type = "integer", Description = "The report year.",                                    Required = true  },
                new() { Name = "month",       Type = "string",  Description = "Filter by month name (e.g. 'June').",                 Required = false },
                new() { Name = "groupName",   Type = "string",  Description = "Filter by GroupName (exact match).",                  Required = false },
                new() { Name = "accountType", Type = "string",  Description = "Filter by AccountType (exact match).",                Required = false },
                new() { Name = "itemType",    Type = "string",  Description = "Filter by ItemType (exact match).",                   Required = false },
                new() { Name = "pageNumber",  Type = "integer", Description = "1-based page number for the row list.",               Required = false },
                new() { Name = "pageSize",    Type = "integer", Description = "Rows per page (max 100).",                            Required = false }
            ]
        }
    ];

    public ToolDispatcher(
        IHttpClientFactory httpClientFactory,
        IOptions<CopilotOptions> options,
        ILogger<ToolDispatcher> logger)
    {
        _http    = httpClientFactory.CreateClient("CopilotInternal");
        _logger  = logger;
        _baseUrl = options.Value.BaseApiUrl.TrimEnd('/');
    }

    public IReadOnlyList<ToolDefinition> GetToolDefinitions() => _definitions;

    public async Task<ToolResponse> ExecuteAsync(ToolRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Executing tool {Tool} (callId={Id})", request.ToolName, request.ToolCallId);

        try
        {
            var content = request.ToolName switch
            {
                "GetReportYears"        => await GetAsync($"{_baseUrl}/api/report-data/years", ct),
                "GetReportData"         => await GetReportDataAsync(request.Arguments, ct),
                "GetYearSummary"        => await GetAsync($"{_baseUrl}/api/report-data/{GetArg<int>(request.Arguments, "year")}/summary", ct),
                "GetMonthlyBreakdown"   => await GetAsync($"{_baseUrl}/api/report-data/{GetArg<int>(request.Arguments, "year")}/by-month", ct),
                "GetGroupBreakdown"     => await GetAsync($"{_baseUrl}/api/report-data/{GetArg<int>(request.Arguments, "year")}/by-group", ct),
                "GetAccountTypeBreakdown" => await GetAsync($"{_baseUrl}/api/report-data/{GetArg<int>(request.Arguments, "year")}/by-account-type", ct),
                "GetItemTypeBreakdown"  => await GetAsync($"{_baseUrl}/api/report-data/{GetArg<int>(request.Arguments, "year")}/by-item-type", ct),
                "CompareYears"          => await CompareYearsAsync(request.Arguments, ct),
                "GetFilteredData"       => await GetFilteredDataAsync(request.Arguments, ct),
                _                       => throw new NotSupportedException($"Unknown tool: {request.ToolName}")
            };

            return new ToolResponse { ToolCallId = request.ToolCallId, ToolName = request.ToolName, Success = true, Content = content };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {Tool} failed", request.ToolName);
            return new ToolResponse { ToolCallId = request.ToolCallId, ToolName = request.ToolName, Success = false, Content = "{}", ErrorMessage = ex.Message };
        }
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private async Task<string> GetAsync(string url, CancellationToken ct)
    {
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private Task<string> GetReportDataAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var year       = GetArg<int>(args, "year");
        var pageNumber = GetArg<int>(args, "pageNumber", 1);
        var pageSize   = Math.Min(GetArg<int>(args, "pageSize", 20), 100);
        return GetAsync($"{_baseUrl}/api/report-data/{year}?pageNumber={pageNumber}&pageSize={pageSize}", ct);
    }

    private Task<string> CompareYearsAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var yearA = GetArg<int>(args, "yearA");
        var yearB = GetArg<int>(args, "yearB");
        return GetAsync($"{_baseUrl}/api/report-data/compare?yearA={yearA}&yearB={yearB}", ct);
    }

    private Task<string> GetFilteredDataAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var year        = GetArg<int>(args, "year");
        var month       = GetArg<string>(args, "month");
        var groupName   = GetArg<string>(args, "groupName");
        var accountType = GetArg<string>(args, "accountType");
        var itemType    = GetArg<string>(args, "itemType");
        var pageNumber  = GetArg<int>(args, "pageNumber", 1);
        var pageSize    = Math.Min(GetArg<int>(args, "pageSize", 20), 100);

        var qs = $"pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(month))       qs += $"&month={Uri.EscapeDataString(month)}";
        if (!string.IsNullOrEmpty(groupName))   qs += $"&groupName={Uri.EscapeDataString(groupName)}";
        if (!string.IsNullOrEmpty(accountType)) qs += $"&accountType={Uri.EscapeDataString(accountType)}";
        if (!string.IsNullOrEmpty(itemType))    qs += $"&itemType={Uri.EscapeDataString(itemType)}";

        return GetAsync($"{_baseUrl}/api/report-data/{year}/filter?{qs}", ct);
    }

    private static T GetArg<T>(Dictionary<string, object?> args, string key, T defaultValue = default!)
    {
        if (!args.TryGetValue(key, out var raw) || raw is null)
            return defaultValue;

        if (raw is JsonElement je)
        {
            if (typeof(T) == typeof(int))    return (T)(object)je.GetInt32();
            if (typeof(T) == typeof(string)) return je.ValueKind == JsonValueKind.Null ? defaultValue : (T)(object)je.GetString()!;
        }

        return (T)Convert.ChangeType(raw, typeof(T));
    }
}
