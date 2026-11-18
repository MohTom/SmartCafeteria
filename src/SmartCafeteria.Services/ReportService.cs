using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Services;
public class ReportService
{
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    private readonly ISettingsService _settingsService;

    public ReportService(IOrderService orderService, IProductService productService, ISettingsService settingsService)
    {
        _orderService = orderService;
        _productService = productService;
        _settingsService = settingsService;
    }

    public async Task<(decimal revenue, int orderCount, int lowStockCount)> GetSummaryAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        var stats = await _orderService.GetDashboardStatsAsync(settings.LowStockThreshold);
        return (stats.TotalRevenue, stats.CompletedOrders, stats.LowStockCount);
    }

    public async Task<IEnumerable<Order>> GetCompletedOrdersAsync()
    {
        var orders = await _orderService.GetAllOrdersAsync();
        return orders.Where(o => o.Status == Core.Enums.OrderStatus.Received);
    }

    public async Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime start, DateTime end)
    {
        return await _orderService.GetOrdersByDateRangeAsync(start, end);
    }

    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        return await _productService.GetAllProductsAsync();
    }
}
