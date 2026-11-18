namespace SmartCafeteria.Core.Models;
public class DashboardStats
{
    public decimal TotalRevenue { get; set; }
    public int CompletedOrders { get; set; }
    public int LowStockCount { get; set; }
    public int TotalProducts { get; set; }
}
