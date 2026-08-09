using Microsoft.AspNetCore.Mvc;
using WareConnect.Api.Models;
using WareConnect.Api.Services;

namespace WareConnect.Api.Controllers;

[ApiController]
[Route("api/report-data")]
public class ReportDataController : ControllerBase
{
    private readonly IReportDataService _reportDataService;

    public ReportDataController(IReportDataService reportDataService)
    {
        _reportDataService = reportDataService;
    }

    // ── Basic endpoints ──────────────────────────────────────────────────────

    /// <summary>Lists all available year tables (e.g. 2018–2026).</summary>
    [HttpGet("years")]
    public async Task<IActionResult> GetYears(CancellationToken cancellationToken)
    {
        var years = await _reportDataService.GetAvailableYearsAsync(cancellationToken);
        return Ok(years);
    }

    /// <summary>Returns paginated raw rows for a given year.</summary>
    [HttpGet("{year:int}")]
    public async Task<ActionResult<PagedResult<ReportRowDto>>> GetByYear(
        int year,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _reportDataService.GetRowsByYearAsync(year, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    // ── Mutation endpoints ──────────────────────────────────────────────────

    /// <summary>
    /// Updates the Amount for a single row in the given year table.
    /// Body: { "amount": 1234.56 }
    /// </summary>
    [HttpPut("{year:int}/rows/{rowId:int}/amount")]
    public async Task<IActionResult> UpdateAmount(
        int year,
        int rowId,
        [FromBody] UpdateAmountRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _reportDataService.UpdateAmountAsync(year, rowId, request.Amount, cancellationToken);
        if (!updated) return NotFound($"Row {rowId} not found in year {year}.");
        return Ok(new { rowId, year, amount = request.Amount });
    }

    // ── Aggregate / summary endpoints ────────────────────────────────────────

    /// <summary>
    /// Returns grand-total sums (Amount, Sales, OtherExp, GP2, Budget, etc.)
    /// for the entire year — the fastest way to answer "what is the total X for 2025?".
    /// </summary>
    [HttpGet("{year:int}/summary")]
    public async Task<IActionResult> GetYearSummary(int year, CancellationToken cancellationToken)
    {
        var summary = await _reportDataService.GetYearSummaryAsync(year, cancellationToken);
        if (summary is null) return NotFound($"No data table found for year {year}.");
        return Ok(summary);
    }

    /// <summary>
    /// Returns Amount, Sales, OtherExp, GP2, Budget broken down by month,
    /// ordered January → December.
    /// </summary>
    [HttpGet("{year:int}/by-month")]
    public async Task<IActionResult> GetMonthlyBreakdown(int year, CancellationToken cancellationToken)
    {
        var result = await _reportDataService.GetMonthlyBreakdownAsync(year, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns totals grouped by GroupName, ordered by TotalAmount descending.</summary>
    [HttpGet("{year:int}/by-group")]
    public async Task<IActionResult> GetGroupBreakdown(int year, CancellationToken cancellationToken)
    {
        var result = await _reportDataService.GetGroupBreakdownAsync(year, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns totals grouped by AccountType, ordered by TotalAmount descending.</summary>
    [HttpGet("{year:int}/by-account-type")]
    public async Task<IActionResult> GetAccountTypeBreakdown(int year, CancellationToken cancellationToken)
    {
        var result = await _reportDataService.GetAccountTypeBreakdownAsync(year, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns totals grouped by ItemType, ordered by TotalAmount descending.</summary>
    [HttpGet("{year:int}/by-item-type")]
    public async Task<IActionResult> GetItemTypeBreakdown(int year, CancellationToken cancellationToken)
    {
        var result = await _reportDataService.GetItemTypeBreakdownAsync(year, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Side-by-side financial comparison of two years (variance included).
    /// E.g. GET /api/report-data/compare?yearA=2024&amp;yearB=2025
    /// </summary>
    [HttpGet("compare")]
    public async Task<IActionResult> CompareYears(
        [FromQuery] int yearA,
        [FromQuery] int yearB,
        CancellationToken cancellationToken)
    {
        if (yearA <= 0 || yearB <= 0)
            return BadRequest("Both yearA and yearB are required.");

        var result = await _reportDataService.CompareYearsAsync(yearA, yearB, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns filtered rows AND aggregate totals for those rows.
    /// All filter parameters are optional — combine freely to slice the data.
    /// E.g. GET /api/report-data/2025/filter?month=June&amp;groupName=Food
    /// </summary>
    [HttpGet("{year:int}/filter")]
    public async Task<IActionResult> GetFilteredData(
        int year,
        [FromQuery] string? month        = null,
        [FromQuery] string? groupName    = null,
        [FromQuery] string? accountType  = null,
        [FromQuery] string? itemType     = null,
        [FromQuery] int pageNumber       = 1,
        [FromQuery] int pageSize         = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _reportDataService.GetFilteredDataAsync(
            year, month, groupName, accountType, itemType,
            pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }
}
