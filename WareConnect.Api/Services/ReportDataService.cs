using Microsoft.Data.SqlClient;
using WareConnect.Api.Models;

namespace WareConnect.Api.Services;

public class ReportDataService : IReportDataService
{
    private const int MinimumYear = 2000;
    private const int MaximumYear = 2100;
    private const int MaximumPageSize = 200;
    private readonly IConfiguration _configuration;

    public ReportDataService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken)
    {
        var years = new List<int>();
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME LIKE 'Data_[0-9][0-9][0-9][0-9]';";

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var tableName = reader.GetString(0);
            var suffix = tableName.Replace("Data_", string.Empty);
            if (int.TryParse(suffix, out var year) && year >= MinimumYear && year <= MaximumYear)
            {
                years.Add(year);
            }
        }

        years.Sort();
        years.Reverse();
        return years;
    }

    public async Task<PagedResult<ReportRowDto>> GetRowsByYearAsync(int year, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        if (year < MinimumYear || year > MaximumYear)
        {
            throw new ArgumentOutOfRangeException(nameof(year), $"Year must be between {MinimumYear} and {MaximumYear}.");
        }

        if (pageNumber <= 0)
        {
            pageNumber = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 20;
        }

        if (pageSize > MaximumPageSize)
        {
            pageSize = MaximumPageSize;
        }

        var tableName = $"Data_{year}";

        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, tableName, cancellationToken))
        {
            return new PagedResult<ReportRowDto>
            {
                Items = Array.Empty<ReportRowDto>(),
                TotalCount = 0,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        var totalCount = await GetTotalCountAsync(connection, tableName, cancellationToken);

        var rows = new List<ReportRowDto>();
        var safeTableName = $"[dbo].[{tableName}]";
        var offset = (pageNumber - 1) * pageSize;
        var sql = $@"
SELECT
    [RowID], [Year], [MYOBAccount], [AccountName], [AccountType], [Amount],
    [StartDate], [EndDate], [MonthName], [WeekInMonth], [MonthAmount],
    [GroupName], [ItemType], [Sales], [OtherExp], [GP2], [DistinctGP2],
    [BudgetAmount], [LYRBudgetAmount], [MonthBudgetAmount], [MonthLYRBudgetAmount]
FROM {safeTableName}
ORDER BY [RowID] DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Offset", offset));
        command.Parameters.Add(new SqlParameter("@PageSize", pageSize));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ReportRowDto
            {
                RowID = reader.GetInt32(0),
                Year = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                MYOBAccount = reader.IsDBNull(2) ? null : reader.GetString(2),
                AccountName = reader.IsDBNull(3) ? null : reader.GetString(3),
                AccountType = reader.IsDBNull(4) ? null : reader.GetString(4),
                Amount = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                StartDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                EndDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                MonthName = reader.IsDBNull(8) ? null : reader.GetString(8),
                WeekInMonth = reader.IsDBNull(9) ? null : reader.GetString(9),
                MonthAmount = reader.IsDBNull(10) ? null : reader.GetDecimal(10),
                GroupName = reader.IsDBNull(11) ? null : reader.GetString(11),
                ItemType = reader.IsDBNull(12) ? null : reader.GetString(12),
                Sales = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                OtherExp = reader.IsDBNull(14) ? null : reader.GetDecimal(14),
                GP2 = reader.IsDBNull(15) ? null : reader.GetDecimal(15),
                DistinctGP2 = reader.IsDBNull(16) ? null : reader.GetDecimal(16),
                BudgetAmount = reader.IsDBNull(17) ? null : reader.GetDecimal(17),
                LYRBudgetAmount = reader.IsDBNull(18) ? null : reader.GetDecimal(18),
                MonthBudgetAmount = reader.IsDBNull(19) ? null : reader.GetDecimal(19),
                MonthLYRBudgetAmount = reader.IsDBNull(20) ? null : reader.GetDecimal(20)
            });
        }

        return new PagedResult<ReportRowDto>
        {
            Items = rows,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    // ── UpdateAmountAsync ─────────────────────────────────────────────────────

    public async Task<bool> UpdateAmountAsync(int year, int rowId, decimal newAmount, CancellationToken cancellationToken)
    {
        if (year < MinimumYear || year > MaximumYear)
            throw new ArgumentOutOfRangeException(nameof(year));

        var tableName = $"Data_{year}";
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, tableName, cancellationToken))
            return false;

        var sql = $"UPDATE [dbo].[{tableName}] SET [Amount] = @Amount WHERE [RowID] = @RowId;";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add(new SqlParameter("@Amount", newAmount));
        cmd.Parameters.Add(new SqlParameter("@RowId", rowId));
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    // ── GetYearSummaryAsync ──────────────────────────────────────────────────

    public async Task<YearSummaryDto?> GetYearSummaryAsync(int year, CancellationToken cancellationToken)
    {
        var tableName = $"Data_{year}";
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, tableName, cancellationToken))
            return null;

        var sql = $@"
SELECT
    COUNT(1)                        AS TotalRows,
    ISNULL(SUM([Amount]),0)         AS TotalAmount,
    ISNULL(SUM([Sales]),0)          AS TotalSales,
    ISNULL(SUM([OtherExp]),0)       AS TotalOtherExp,
    ISNULL(SUM([GP2]),0)            AS TotalGP2,
    ISNULL(SUM([DistinctGP2]),0)    AS TotalDistinctGP2,
    ISNULL(SUM([BudgetAmount]),0)   AS TotalBudgetAmount,
    ISNULL(SUM([LYRBudgetAmount]),0)       AS TotalLYRBudgetAmount,
    ISNULL(SUM([MonthAmount]),0)           AS TotalMonthAmount,
    ISNULL(SUM([MonthBudgetAmount]),0)     AS TotalMonthBudgetAmount,
    ISNULL(SUM([MonthLYRBudgetAmount]),0)  AS TotalMonthLYRBudgetAmount
FROM [dbo].[{tableName}];";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new YearSummaryDto
            {
                Year                    = year,
                TotalRows               = reader.GetInt32(0),
                TotalAmount             = reader.GetDecimal(1),
                TotalSales              = reader.GetDecimal(2),
                TotalOtherExp           = reader.GetDecimal(3),
                TotalGP2                = reader.GetDecimal(4),
                TotalDistinctGP2        = reader.GetDecimal(5),
                TotalBudgetAmount       = reader.GetDecimal(6),
                TotalLYRBudgetAmount    = reader.GetDecimal(7),
                TotalMonthAmount        = reader.GetDecimal(8),
                TotalMonthBudgetAmount  = reader.GetDecimal(9),
                TotalMonthLYRBudgetAmount = reader.GetDecimal(10)
            };
        }
        return null;
    }

    // ── GetMonthlyBreakdownAsync ─────────────────────────────────────────────

    public async Task<IReadOnlyList<MonthlyBreakdownDto>> GetMonthlyBreakdownAsync(int year, CancellationToken cancellationToken)
    {
        var tableName = $"Data_{year}";
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, tableName, cancellationToken))
            return Array.Empty<MonthlyBreakdownDto>();

        var sql = $@"
SELECT
    [MonthName],
    CASE [MonthName]
        WHEN 'January'   THEN 1  WHEN 'February'  THEN 2  WHEN 'March'    THEN 3
        WHEN 'April'     THEN 4  WHEN 'May'        THEN 5  WHEN 'June'     THEN 6
        WHEN 'July'      THEN 7  WHEN 'August'     THEN 8  WHEN 'September'THEN 9
        WHEN 'October'   THEN 10 WHEN 'November'   THEN 11 WHEN 'December' THEN 12
        ELSE 99
    END AS MonthOrder,
    ISNULL(SUM([Amount]),0)             AS TotalAmount,
    ISNULL(SUM([Sales]),0)              AS TotalSales,
    ISNULL(SUM([OtherExp]),0)           AS TotalOtherExp,
    ISNULL(SUM([GP2]),0)                AS TotalGP2,
    ISNULL(SUM([BudgetAmount]),0)       AS TotalBudgetAmount,
    ISNULL(SUM([MonthAmount]),0)        AS TotalMonthAmount,
    ISNULL(SUM([MonthBudgetAmount]),0)  AS TotalMonthBudgetAmount
FROM [dbo].[{tableName}]
GROUP BY [MonthName]
ORDER BY MonthOrder;";

        var results = new List<MonthlyBreakdownDto>();
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new MonthlyBreakdownDto
            {
                Year                 = year,
                MonthName            = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0),
                MonthOrder           = reader.GetInt32(1),
                TotalAmount          = reader.GetDecimal(2),
                TotalSales           = reader.GetDecimal(3),
                TotalOtherExp        = reader.GetDecimal(4),
                TotalGP2             = reader.GetDecimal(5),
                TotalBudgetAmount    = reader.GetDecimal(6),
                TotalMonthAmount     = reader.GetDecimal(7),
                TotalMonthBudgetAmount = reader.GetDecimal(8)
            });
        }
        return results;
    }

    // ── Dimension breakdowns (GroupName / AccountType / ItemType) ────────────

    public Task<IReadOnlyList<DimensionBreakdownDto>> GetGroupBreakdownAsync(int year, CancellationToken cancellationToken)
        => GetDimensionBreakdownAsync(year, "GroupName", cancellationToken);

    public Task<IReadOnlyList<DimensionBreakdownDto>> GetAccountTypeBreakdownAsync(int year, CancellationToken cancellationToken)
        => GetDimensionBreakdownAsync(year, "AccountType", cancellationToken);

    public Task<IReadOnlyList<DimensionBreakdownDto>> GetItemTypeBreakdownAsync(int year, CancellationToken cancellationToken)
        => GetDimensionBreakdownAsync(year, "ItemType", cancellationToken);

    private async Task<IReadOnlyList<DimensionBreakdownDto>> GetDimensionBreakdownAsync(
        int year, string dimension, CancellationToken cancellationToken)
    {
        var tableName = $"Data_{year}";
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, tableName, cancellationToken))
            return Array.Empty<DimensionBreakdownDto>();

        var sql = $@"
SELECT
    ISNULL([{dimension}], '(Unspecified)') AS DimValue,
    COUNT(1)                      AS RowCount,
    ISNULL(SUM([Amount]),0)       AS TotalAmount,
    ISNULL(SUM([Sales]),0)        AS TotalSales,
    ISNULL(SUM([OtherExp]),0)     AS TotalOtherExp,
    ISNULL(SUM([GP2]),0)          AS TotalGP2,
    ISNULL(SUM([BudgetAmount]),0) AS TotalBudgetAmount
FROM [dbo].[{tableName}]
GROUP BY [{dimension}]
ORDER BY TotalAmount DESC;";

        var results = new List<DimensionBreakdownDto>();
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new DimensionBreakdownDto
            {
                Year            = year,
                DimensionName   = dimension,
                DimensionValue  = reader.GetString(0),
                RowCount        = reader.GetInt32(1),
                TotalAmount     = reader.GetDecimal(2),
                TotalSales      = reader.GetDecimal(3),
                TotalOtherExp   = reader.GetDecimal(4),
                TotalGP2        = reader.GetDecimal(5),
                TotalBudgetAmount = reader.GetDecimal(6)
            });
        }
        return results;
    }

    // ── CompareYearsAsync ────────────────────────────────────────────────────

    public async Task<YearComparisonDto> CompareYearsAsync(int yearA, int yearB, CancellationToken cancellationToken)
    {
        var summaryA = await GetYearSummaryAsync(yearA, cancellationToken);
        var summaryB = await GetYearSummaryAsync(yearB, cancellationToken);

        var aAmt     = summaryA?.TotalAmount     ?? 0m;
        var bAmt     = summaryB?.TotalAmount     ?? 0m;
        var aSales   = summaryA?.TotalSales      ?? 0m;
        var bSales   = summaryB?.TotalSales      ?? 0m;
        var aGP2     = summaryA?.TotalGP2        ?? 0m;
        var bGP2     = summaryB?.TotalGP2        ?? 0m;
        var aBudget  = summaryA?.TotalBudgetAmount ?? 0m;
        var bBudget  = summaryB?.TotalBudgetAmount ?? 0m;
        var aOtherExp = summaryA?.TotalOtherExp  ?? 0m;
        var bOtherExp = summaryB?.TotalOtherExp  ?? 0m;

        return new YearComparisonDto
        {
            YearA           = yearA,
            YearB           = yearB,
            AmountA         = aAmt,
            AmountB         = bAmt,
            AmountVariance  = bAmt - aAmt,
            SalesA          = aSales,
            SalesB          = bSales,
            SalesVariance   = bSales - aSales,
            GP2A            = aGP2,
            GP2B            = bGP2,
            GP2Variance     = bGP2 - aGP2,
            BudgetA         = aBudget,
            BudgetB         = bBudget,
            BudgetVariance  = bBudget - aBudget,
            OtherExpA       = aOtherExp,
            OtherExpB       = bOtherExp,
            OtherExpVariance = bOtherExp - aOtherExp
        };
    }

    // ── GetFilteredDataAsync ─────────────────────────────────────────────────

    public async Task<FilteredReportResultDto> GetFilteredDataAsync(
        int year,
        string? month,
        string? groupName,
        string? accountType,
        string? itemType,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0)   pageSize   = 20;
        if (pageSize > MaximumPageSize) pageSize = MaximumPageSize;

        var tableName = $"Data_{year}";
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, tableName, cancellationToken))
        {
            return new FilteredReportResultDto
            {
                Year = year, MonthFilter = month, GroupFilter = groupName,
                AccountTypeFilter = accountType, ItemTypeFilter = itemType
            };
        }

        // Build WHERE clause
        var conditions = new List<string>();
        var parameters = new List<SqlParameter>();

        if (!string.IsNullOrWhiteSpace(month))
        {
            conditions.Add("[MonthName] = @Month");
            parameters.Add(new SqlParameter("@Month", month));
        }
        if (!string.IsNullOrWhiteSpace(groupName))
        {
            conditions.Add("[GroupName] = @GroupName");
            parameters.Add(new SqlParameter("@GroupName", groupName));
        }
        if (!string.IsNullOrWhiteSpace(accountType))
        {
            conditions.Add("[AccountType] = @AccountType");
            parameters.Add(new SqlParameter("@AccountType", accountType));
        }
        if (!string.IsNullOrWhiteSpace(itemType))
        {
            conditions.Add("[ItemType] = @ItemType");
            parameters.Add(new SqlParameter("@ItemType", itemType));
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;

        // Aggregate query
        var aggSql = $@"
SELECT
    COUNT(1)                       AS TotalRows,
    ISNULL(SUM([Amount]),0)        AS TotalAmount,
    ISNULL(SUM([Sales]),0)         AS TotalSales,
    ISNULL(SUM([OtherExp]),0)      AS TotalOtherExp,
    ISNULL(SUM([GP2]),0)           AS TotalGP2,
    ISNULL(SUM([BudgetAmount]),0)  AS TotalBudgetAmount
FROM [dbo].[{tableName}]
{where};";

        int totalRows;
        decimal totalAmount, totalSales, totalOtherExp, totalGP2, totalBudget;

        await using (var aggCmd = new SqlCommand(aggSql, connection))
        {
            aggCmd.Parameters.AddRange(parameters.Select(p => new SqlParameter(p.ParameterName, p.Value)).ToArray());
            await using var aggReader = await aggCmd.ExecuteReaderAsync(cancellationToken);
            await aggReader.ReadAsync(cancellationToken);
            totalRows    = aggReader.GetInt32(0);
            totalAmount  = aggReader.GetDecimal(1);
            totalSales   = aggReader.GetDecimal(2);
            totalOtherExp = aggReader.GetDecimal(3);
            totalGP2     = aggReader.GetDecimal(4);
            totalBudget  = aggReader.GetDecimal(5);
        }

        // Paginated rows query
        var offset  = (pageNumber - 1) * pageSize;
        var rowsSql = $@"
SELECT
    [RowID],[Year],[MYOBAccount],[AccountName],[AccountType],[Amount],
    [StartDate],[EndDate],[MonthName],[WeekInMonth],[MonthAmount],
    [GroupName],[ItemType],[Sales],[OtherExp],[GP2],[DistinctGP2],
    [BudgetAmount],[LYRBudgetAmount],[MonthBudgetAmount],[MonthLYRBudgetAmount]
FROM [dbo].[{tableName}]
{where}
ORDER BY [RowID]
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        var rows = new List<ReportRowDto>();
        await using (var rowsCmd = new SqlCommand(rowsSql, connection))
        {
            rowsCmd.Parameters.AddRange(parameters.Select(p => new SqlParameter(p.ParameterName, p.Value)).ToArray());
            rowsCmd.Parameters.Add(new SqlParameter("@Offset",   offset));
            rowsCmd.Parameters.Add(new SqlParameter("@PageSize", pageSize));

            await using var rowsReader = await rowsCmd.ExecuteReaderAsync(cancellationToken);
            while (await rowsReader.ReadAsync(cancellationToken))
            {
                rows.Add(new ReportRowDto
                {
                    RowID               = rowsReader.GetInt32(0),
                    Year                = rowsReader.IsDBNull(1)  ? null : rowsReader.GetInt32(1),
                    MYOBAccount         = rowsReader.IsDBNull(2)  ? null : rowsReader.GetString(2),
                    AccountName         = rowsReader.IsDBNull(3)  ? null : rowsReader.GetString(3),
                    AccountType         = rowsReader.IsDBNull(4)  ? null : rowsReader.GetString(4),
                    Amount              = rowsReader.IsDBNull(5)  ? null : rowsReader.GetDecimal(5),
                    StartDate           = rowsReader.IsDBNull(6)  ? null : rowsReader.GetDateTime(6),
                    EndDate             = rowsReader.IsDBNull(7)  ? null : rowsReader.GetDateTime(7),
                    MonthName           = rowsReader.IsDBNull(8)  ? null : rowsReader.GetString(8),
                    WeekInMonth         = rowsReader.IsDBNull(9)  ? null : rowsReader.GetString(9),
                    MonthAmount         = rowsReader.IsDBNull(10) ? null : rowsReader.GetDecimal(10),
                    GroupName           = rowsReader.IsDBNull(11) ? null : rowsReader.GetString(11),
                    ItemType            = rowsReader.IsDBNull(12) ? null : rowsReader.GetString(12),
                    Sales               = rowsReader.IsDBNull(13) ? null : rowsReader.GetDecimal(13),
                    OtherExp            = rowsReader.IsDBNull(14) ? null : rowsReader.GetDecimal(14),
                    GP2                 = rowsReader.IsDBNull(15) ? null : rowsReader.GetDecimal(15),
                    DistinctGP2         = rowsReader.IsDBNull(16) ? null : rowsReader.GetDecimal(16),
                    BudgetAmount        = rowsReader.IsDBNull(17) ? null : rowsReader.GetDecimal(17),
                    LYRBudgetAmount     = rowsReader.IsDBNull(18) ? null : rowsReader.GetDecimal(18),
                    MonthBudgetAmount   = rowsReader.IsDBNull(19) ? null : rowsReader.GetDecimal(19),
                    MonthLYRBudgetAmount = rowsReader.IsDBNull(20) ? null : rowsReader.GetDecimal(20)
                });
            }
        }

        return new FilteredReportResultDto
        {
            Year              = year,
            MonthFilter       = month,
            GroupFilter       = groupName,
            AccountTypeFilter = accountType,
            ItemTypeFilter    = itemType,
            TotalMatchingRows = totalRows,
            TotalAmount       = totalAmount,
            TotalSales        = totalSales,
            TotalOtherExp     = totalOtherExp,
            TotalGP2          = totalGP2,
            TotalBudgetAmount = totalBudget,
            Rows              = rows
        };
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private string GetConnectionString()
    {
        return _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT COUNT(1)
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = @TableName;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@TableName", tableName));
        var count = (int)await command.ExecuteScalarAsync(cancellationToken);
        return count > 0;
    }

    private static async Task<int> GetTotalCountAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var safeTableName = $"[dbo].[{tableName}]";
        var sql = $"SELECT COUNT(1) FROM {safeTableName};";
        await using var command = new SqlCommand(sql, connection);
        var count = (int)await command.ExecuteScalarAsync(cancellationToken);
        return count;
    }
}