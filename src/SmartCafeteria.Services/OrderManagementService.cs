using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Enums;

namespace SmartCafeteria.Services;
public class OrderManagementService
{
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    private readonly ILogService _logService;

    public OrderManagementService(IOrderService orderService, IProductService productService, ILogService logService)
    {
        _orderService = orderService;
        _productService = productService;
        _logService = logService;
    }

    public async Task<Order> PlaceOrderAsync(List<CartItem> cartItems, PaymentMethod paymentMethod, string stationId, string cashierName, bool isPreOrder, string? pickupTime, OrderType orderType = OrderType.Local, string notes = "")
    {
        var order = await _orderService.CreateOrderAsync(cartItems, paymentMethod, stationId, cashierName, isPreOrder, pickupTime, orderType, notes);
        await _logService.AddLogAsync($"تم إنشاء الطلب #{order.Code} - المبلغ: {order.Total}", "success");
        return order;
    }

    public async Task<bool> ChangeOrderStatusAsync(string orderId, OrderStatus newStatus)
    {
        var result = await _orderService.UpdateOrderStatusAsync(orderId, newStatus);
        if (result)
        {
            var statusText = newStatus switch
            {
                OrderStatus.Received => "تم الاستلام",
                OrderStatus.Cancelled => "ملغي",
                _ => newStatus.ToString()
            };
            await _logService.AddLogAsync($"تم تغيير حالة الطلب إلى: {statusText}", "info");
        }
        return result;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(int lowStockThreshold)
    {
        return await _orderService.GetDashboardStatsAsync(lowStockThreshold);
    }

    public async Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime start, DateTime end)
    {
        return await _orderService.GetOrdersByDateRangeAsync(start, end);
    }
}
