using WareConnect.Api.Models;

namespace WareConnect.Api.Services;

public interface IReportDataService
{
    Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken);
    Task<PagedResult<ReportRowDto>> GetRowsByYearAsync(int year, int pageNumber, int pageSize, CancellationToken cancellationToken);

    /// <summary>Updates the Amount for a single row. Returns false if row not found.</summary>
    Task<bool> UpdateAmountAsync(int year, int rowId, decimal newAmount, CancellationToken cancellationToken);

    /// <summary>Total financial sums for an entire year.</summary>
    Task<YearSummaryDto?> GetYearSummaryAsync(int year, CancellationToken cancellationToken);

    /// <summary>Aggregated financials grouped by month for a given year, ordered chronologically.</summary>
    Task<IReadOnlyList<MonthlyBreakdownDto>> GetMonthlyBreakdownAsync(int year, CancellationToken cancellationToken);

    /// <summary>Aggregated financials grouped by GroupName for a given year.</summary>
    Task<IReadOnlyList<DimensionBreakdownDto>> GetGroupBreakdownAsync(int year, CancellationToken cancellationToken);

    /// <summary>Aggregated financials grouped by AccountType for a given year.</summary>
    Task<IReadOnlyList<DimensionBreakdownDto>> GetAccountTypeBreakdownAsync(int year, CancellationToken cancellationToken);

    /// <summary>Aggregated financials grouped by ItemType for a given year.</summary>
    Task<IReadOnlyList<DimensionBreakdownDto>> GetItemTypeBreakdownAsync(int year, CancellationToken cancellationToken);

    /// <summary>Side-by-side financial comparison of two years.</summary>
    Task<YearComparisonDto> CompareYearsAsync(int yearA, int yearB, CancellationToken cancellationToken);

    /// <summary>Filtered rows + aggregate totals by optional month, group, accountType, itemType.</summary>
    Task<FilteredReportResultDto> GetFilteredDataAsync(
        int year,
        string? month,
        string? groupName,
        string? accountType,
        string? itemType,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}