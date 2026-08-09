namespace WareConnect.Api.Models;

/// <summary>Total financial sums for an entire year.</summary>
public class YearSummaryDto
{
    public int Year { get; set; }
    public int TotalRows { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalOtherExp { get; set; }
    public decimal TotalGP2 { get; set; }
    public decimal TotalDistinctGP2 { get; set; }
    public decimal TotalBudgetAmount { get; set; }
    public decimal TotalLYRBudgetAmount { get; set; }
    public decimal TotalMonthAmount { get; set; }
    public decimal TotalMonthBudgetAmount { get; set; }
    public decimal TotalMonthLYRBudgetAmount { get; set; }
}

/// <summary>Aggregated financials grouped by month for a given year.</summary>
public class MonthlyBreakdownDto
{
    public int Year { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int MonthOrder { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalOtherExp { get; set; }
    public decimal TotalGP2 { get; set; }
    public decimal TotalBudgetAmount { get; set; }
    public decimal TotalMonthAmount { get; set; }
    public decimal TotalMonthBudgetAmount { get; set; }
}

/// <summary>Aggregated financials grouped by a dimension (GroupName, AccountType, ItemType).</summary>
public class DimensionBreakdownDto
{
    public int Year { get; set; }
    public string DimensionName { get; set; } = string.Empty;  // GroupName / AccountType / ItemType
    public string DimensionValue { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalOtherExp { get; set; }
    public decimal TotalGP2 { get; set; }
    public decimal TotalBudgetAmount { get; set; }
}

/// <summary>Side-by-side comparison of two years.</summary>
public class YearComparisonDto
{
    public int YearA { get; set; }
    public int YearB { get; set; }
    public decimal AmountA { get; set; }
    public decimal AmountB { get; set; }
    public decimal AmountVariance { get; set; }
    public decimal SalesA { get; set; }
    public decimal SalesB { get; set; }
    public decimal SalesVariance { get; set; }
    public decimal GP2A { get; set; }
    public decimal GP2B { get; set; }
    public decimal GP2Variance { get; set; }
    public decimal BudgetA { get; set; }
    public decimal BudgetB { get; set; }
    public decimal BudgetVariance { get; set; }
    public decimal OtherExpA { get; set; }
    public decimal OtherExpB { get; set; }
    public decimal OtherExpVariance { get; set; }
}

/// <summary>Filtered/searched rows with optional month, group, accountType, itemType filters.</summary>
public class FilteredReportResultDto
{
    public int Year { get; set; }
    public string? MonthFilter { get; set; }
    public string? GroupFilter { get; set; }
    public string? AccountTypeFilter { get; set; }
    public string? ItemTypeFilter { get; set; }
    public int TotalMatchingRows { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalOtherExp { get; set; }
    public decimal TotalGP2 { get; set; }
    public decimal TotalBudgetAmount { get; set; }
    public IReadOnlyList<ReportRowDto> Rows { get; set; } = Array.Empty<ReportRowDto>();
}
