using Cafeteria.RemoteAccess;
using Moq;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using Xunit;

namespace Cafeteria.RemoteAccess.Tests;

public sealed class DesktopEventCollectorTests
{
    [Fact]
    public async Task FirstPollSeedsState_ThenEmitsCreatedAndCancelledEvents()
    {
        using var temp = new TempDirectory();
        var orders = new List<Order>();
        var orderService = new Mock<IOrderService>();
        orderService
            .Setup(s => s.GetAllOrdersAsync())
            .ReturnsAsync(() => orders);

        var outbox = new RecordingOutbox();
        var collector = new DesktopEventCollector(
            orderService.Object,
            Mock.Of<IUserService>(s => s.GetAllUsersAsync() == Task.FromResult<IEnumerable<User>>(Array.Empty<User>())),
            Mock.Of<IProductService>(s => s.GetAllProductsAsync() == Task.FromResult<IEnumerable<Product>>(Array.Empty<Product>())),
            Mock.Of<ISettingsService>(s => s.GetSettingsAsync() == Task.FromResult(new AppSettings { LowStockThreshold = 5 })),
            outbox,
            temp.CreatePaths().EventStateFile);

        await collector.PollAsync();
        Assert.Empty(outbox.Events);

        orders.Add(new Order
        {
            Id = "order-1",
            Code = "A-1",
            Total = 100,
            Status = OrderStatus.Received,
            Timestamp = DateTime.Now,
            CashierName = "Cashier",
            Items = new List<OrderItem>
            {
                new() { Quantity = 2 }
            }
        });

        await collector.PollAsync();
        Assert.Single(outbox.Events);
        Assert.Equal("order.created", outbox.Events[0].Type);

        orders[0].Status = OrderStatus.Cancelled;
        await collector.PollAsync();

        Assert.Equal(2, outbox.Events.Count);
        Assert.Equal("order.cancelled", outbox.Events[1].Type);
    }
}
