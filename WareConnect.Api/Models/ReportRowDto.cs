namespace WareConnect.Api.Models;

public class ReportRowDto
{
    public int RowID { get; set; }
    public int? Year { get; set; }
    public string? MYOBAccount { get; set; }
    public string? AccountName { get; set; }
    public string? AccountType { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? MonthName { get; set; }
    public string? WeekInMonth { get; set; }
    public decimal? MonthAmount { get; set; }
    public string? GroupName { get; set; }
    public string? ItemType { get; set; }
    public decimal? Sales { get; set; }
    public decimal? OtherExp { get; set; }
    public decimal? GP2 { get; set; }
    public decimal? DistinctGP2 { get; set; }
    public decimal? BudgetAmount { get; set; }
    public decimal? LYRBudgetAmount { get; set; }
    public decimal? MonthBudgetAmount { get; set; }
    public decimal? MonthLYRBudgetAmount { get; set; }
}