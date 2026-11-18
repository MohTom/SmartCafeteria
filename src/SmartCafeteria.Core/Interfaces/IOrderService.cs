using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Enums;

namespace SmartCafeteria.Core.Interfaces;
public interface IOrderService
{
    Task<Order> CreateOrderAsync(List<CartItem> items, PaymentMethod paymentMethod, string stationId, string cashierName, bool isPreOrder, string? pickupTime, OrderType orderType = OrderType.Local, string notes = "");
    Task<IEnumerable<Order>> GetAllOrdersAsync();
    Task<Order?> GetOrderByIdAsync(string id);
    Task<bool> UpdateOrderStatusAsync(string orderId, OrderStatus newStatus);
    /// <summary>Removes the order and its items completely from the database.</summary>
    Task<bool> DeleteOrderAsync(string orderId);
    Task<DashboardStats> GetDashboardStatsAsync(int lowStockThreshold);
    Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime start, DateTime end);
}
