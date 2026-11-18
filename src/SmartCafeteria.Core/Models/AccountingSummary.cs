namespace SmartCafeteria.Core.Models;
public class AccountingSummary
{
    public decimal IncomeFromOrders { get; set; }
    public decimal OtherIncome { get; set; }
    public decimal TotalIncome => IncomeFromOrders + OtherIncome;
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit => TotalIncome - TotalExpenses;
    public int OrderCount { get; set; }
}