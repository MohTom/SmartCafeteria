using Dapper;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Data.Database;

namespace SmartCafeteria.Data.Repositories;
public class OrderRepository : IOrderService
{
    private readonly DbContext _dbContext;
    private readonly IProductService _productService;
    private readonly ITicketConfigStore? _ticketConfigStore;

    public OrderRepository(DbContext dbContext, IProductService productService, ITicketConfigStore? ticketConfigStore = null)
    {
        _dbContext = dbContext;
        _productService = productService;
        _ticketConfigStore = ticketConfigStore;
    }

    private bool InventoryTrackingEnabled =>
        _ticketConfigStore?.Load().EnforceInventoryTracking ?? true;

    public async Task<Order> CreateOrderAsync(List<CartItem> items, PaymentMethod paymentMethod, string stationId, string cashierName, bool isPreOrder, string? pickupTime, OrderType orderType = OrderType.Local, string notes = "")
    {
        using var conn = await _dbContext.GetConnectionAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            var orderId = $"ord-{Guid.NewGuid():N}";
            // Daily receipt number allocated atomically inside the same
            // transaction as the order. If today has no counter row yet it is
            // seeded from the existing orders of today (upgrade path), then the
            // atomic upsert increments it. Deleting an order never reuses a
            // number because the counter never goes down.
            var counterDate = DateTime.Now.ToString("yyyy-MM-dd");
            await conn.ExecuteAsync(
                @"INSERT INTO OrderCounters (Date, LastNumber)
                  SELECT @Date, COUNT(*) FROM Orders WHERE date(Timestamp) = @Date
                  ON CONFLICT(Date) DO NOTHING",
                new { Date = counterDate }, transaction);
            var nextNumber = await conn.ExecuteScalarAsync<int>(
                @"INSERT INTO OrderCounters (Date, LastNumber) VALUES (@Date, 1)
                  ON CONFLICT(Date) DO UPDATE SET LastNumber = LastNumber + 1
                  RETURNING LastNumber",
                new { Date = counterDate }, transaction);
            var code = nextNumber.ToString();

            decimal subtotal = 0;
            decimal customizationsTotal = 0;

            var orderItems = new List<OrderItem>();
            foreach (var item in items)
            {
                var itemCustomizationsTotal = item.SelectedCustomizations.Sum(c => c.Price) * item.Quantity;
                subtotal += item.Product.Price * item.Quantity;
                customizationsTotal += itemCustomizationsTotal;

                var orderItem = new OrderItem
                {
                    Id = $"oi-{Guid.NewGuid():N}",
                    OrderId = orderId,
                    ProductId = item.Product.Id,
                    ProductNameAr = item.Product.NameAr,
                    ProductNameEn = item.Product.NameEn,
                    ProductPrice = item.Product.Price,
                    ProductCategory = item.Product.Category,
                    Quantity = item.Quantity,
                    CustomizationsTotal = itemCustomizationsTotal,
                    Notes = item.Notes,
                    SelectedCustomizationsJson = System.Text.Json.JsonSerializer.Serialize(item.SelectedCustomizations)
                };
                orderItems.Add(orderItem);
            }

            var order = new Order
            {
                Id = orderId,
                Code = code,
                Subtotal = subtotal,
                CustomizationsTotal = customizationsTotal,
                Total = subtotal + customizationsTotal,
                PaymentMethod = paymentMethod,
                PaymentStatus = "paid",
                Status = OrderStatus.Received,
                Timestamp = DateTime.Now,
                StationId = stationId,
                CashierName = cashierName,
                IsPreOrder = isPreOrder,
                PickupTime = pickupTime,
                OrderType = orderType,
                Notes = notes ?? string.Empty,
                Items = orderItems
            };

            await conn.ExecuteAsync(
                "INSERT INTO Orders (Id, Code, Subtotal, CustomizationsTotal, Total, PaymentMethod, PaymentStatus, Status, Timestamp, StationId, CashierName, IsPreOrder, PickupTime, OrderType, Notes) VALUES (@Id, @Code, @Subtotal, @CustomizationsTotal, @Total, @PaymentMethod, @PaymentStatus, @Status, @Timestamp, @StationId, @CashierName, @IsPreOrder, @PickupTime, @OrderType, @Notes)",
                order, transaction);

            foreach (var item in orderItems)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO OrderItems (Id, OrderId, ProductId, ProductNameAr, ProductNameEn, ProductPrice, ProductCategory, Quantity, CustomizationsTotal, Notes, SelectedCustomizationsJson) VALUES (@Id, @OrderId, @ProductId, @ProductNameAr, @ProductNameEn, @ProductPrice, @ProductCategory, @Quantity, @CustomizationsTotal, @Notes, @SelectedCustomizationsJson)",
                    item, transaction);
                if (InventoryTrackingEnabled)
                {
                    var affected = await conn.ExecuteAsync(
                        "UPDATE Products SET Inventory = Inventory - @Quantity WHERE Id = @Id AND Inventory >= @Quantity",
                        new { Id = item.ProductId, Quantity = item.Quantity }, transaction);
                    if (affected == 0)
                        throw new InvalidOperationException($"Insufficient inventory for product {item.ProductId}");
                }
            }

            transaction.Commit();
            return order;
        }
        catch
        {
            try { transaction.Rollback(); }
            catch (Exception rollbackError)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "Transaction rollback failed: {0}", rollbackError);
            }
            throw;
        }
    }

    public async Task<IEnumerable<Order>> GetAllOrdersAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var orders = (await conn.QueryAsync<Order>("SELECT * FROM Orders ORDER BY Timestamp DESC")).ToList();
        if (orders.Count == 0) return orders;

        var items = await conn.QueryAsync<OrderItem>(
            "SELECT * FROM OrderItems WHERE OrderId IN @Ids",
            new { Ids = orders.Select(o => o.Id).ToList() });
        var byOrder = items.GroupBy(i => i.OrderId)
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var order in orders)
        {
            order.Items = byOrder.TryGetValue(order.Id, out var list) ? list : new List<OrderItem>();
        }
        return orders;
    }

    public async Task<Order?> GetOrderByIdAsync(string id)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var order = await conn.QueryFirstOrDefaultAsync<Order>("SELECT * FROM Orders WHERE Id = @Id", new { Id = id });
        if (order != null)
        {
            order.Items = (await conn.QueryAsync<OrderItem>(
                "SELECT * FROM OrderItems WHERE OrderId = @OrderId",
                new { OrderId = id })).ToList();
        }
        return order;
    }

    public async Task<bool> UpdateOrderStatusAsync(string orderId, OrderStatus newStatus)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            var current = await conn.QueryFirstOrDefaultAsync<Order>(
                "SELECT * FROM Orders WHERE Id = @Id", new { Id = orderId }, transaction);
            if (current == null) return false;

            var affected = await conn.ExecuteAsync(
                "UPDATE Orders SET Status = @Status WHERE Id = @Id",
                new { Id = orderId, Status = (int)newStatus }, transaction);

            // Restore stock when an order is cancelled (but wasn't cancelled before)
            if (InventoryTrackingEnabled && newStatus == OrderStatus.Cancelled && current.Status != OrderStatus.Cancelled)
            {
                var items = await conn.QueryAsync<OrderItem>(
                    "SELECT * FROM OrderItems WHERE OrderId = @OrderId",
                    new { OrderId = orderId }, transaction);
                foreach (var item in items)
                {
                    await conn.ExecuteAsync(
                        "UPDATE Products SET Inventory = Inventory + @Quantity WHERE Id = @Id",
                        new { Id = item.ProductId, Quantity = item.Quantity }, transaction);
                }
            }

            transaction.Commit();
            return affected > 0;
        }
        catch
        {
            try { transaction.Rollback(); }
            catch (Exception rollbackError)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "Transaction rollback failed: {0}", rollbackError);
            }
            throw;
        }
    }

    public async Task<bool> DeleteOrderAsync(string orderId)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            var current = await conn.QueryFirstOrDefaultAsync<Order>(
                "SELECT * FROM Orders WHERE Id = @Id", new { Id = orderId }, transaction);
            if (current == null) return false;

            // Restore stock unless it was already restored by a cancellation.
            if (InventoryTrackingEnabled && current.Status != OrderStatus.Cancelled)
            {
                var items = await conn.QueryAsync<OrderItem>(
                    "SELECT * FROM OrderItems WHERE OrderId = @OrderId",
                    new { OrderId = orderId }, transaction);
                foreach (var item in items)
                {
                    await conn.ExecuteAsync(
                        "UPDATE Products SET Inventory = Inventory + @Quantity WHERE Id = @Id",
                        new { Id = item.ProductId, Quantity = item.Quantity }, transaction);
                }
            }

            await conn.ExecuteAsync(
                "DELETE FROM OrderItems WHERE OrderId = @OrderId",
                new { OrderId = orderId }, transaction);
            var affected = await conn.ExecuteAsync(
                "DELETE FROM Orders WHERE Id = @Id",
                new { Id = orderId }, transaction);

            transaction.Commit();
            return affected > 0;
        }
        catch
        {
            try { transaction.Rollback(); }
            catch (Exception rollbackError)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "Transaction rollback failed: {0}", rollbackError);
            }
            throw;
        }
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(int lowStockThreshold)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var receivedOrders = await conn.QueryAsync<Order>(
            "SELECT * FROM Orders WHERE Status = @Status",
            new { Status = (int)OrderStatus.Received });

        var products = await conn.QueryAsync<Product>("SELECT * FROM Products");

        return new DashboardStats
        {
            TotalRevenue = receivedOrders.Sum(o => o.Total),
            CompletedOrders = receivedOrders.Count(),
            LowStockCount = products.Count(p => p.Inventory <= lowStockThreshold),
            TotalProducts = products.Count()
        };
    }

    public async Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime start, DateTime end)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var orders = (await conn.QueryAsync<Order>(
            "SELECT * FROM Orders WHERE Timestamp >= @Start AND Timestamp <= @End ORDER BY Timestamp DESC",
            new { Start = start, End = end })).ToList();
        foreach (var order in orders)
        {
            order.Items = (await conn.QueryAsync<OrderItem>(
                "SELECT * FROM OrderItems WHERE OrderId = @OrderId",
                new { OrderId = order.Id })).ToList();
        }
        return orders;
    }
}
