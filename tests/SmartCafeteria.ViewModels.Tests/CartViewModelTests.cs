using Moq;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.ViewModels;
using Xunit;

namespace SmartCafeteria.ViewModels.Tests;

public class CartViewModelTests
{
    [Fact]
    public async Task PlaceOrder_UsesLoggedInUserAndMachineName()
    {
        var orderService = new Mock<IOrderService>();
        orderService
            .Setup(s => s.CreateOrderAsync(
                It.IsAny<List<CartItem>>(),
                PaymentMethod.Cash,
                Environment.MachineName,
                "أحمد علي",
                false,
                null,
                OrderType.Local,
                ""))
            .ReturnsAsync(new Order { Code = "B-1" });

        var logService = new Mock<ILogService>();
        logService.Setup(s => s.AddLogAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var printService = new Mock<IPrintService>();
        var categoryService = new Mock<ICategoryService>();
        var currentUser = new FakeCurrentUser(new User
        {
            Username = "cashier",
            FullName = "أحمد علي",
            Role = UserRole.Cashier
        });

        var viewModel = new CartViewModel(
            orderService.Object,
            logService.Object,
            printService.Object,
            categoryService.Object,
            currentUser);

        viewModel.CartItems.Add(new CartItem
        {
            Product = new Product { Id = "p1", NameAr = "قهوة", Price = 10 },
            Quantity = 1
        });

        await viewModel.PlaceOrderCommand.ExecuteAsync(null);

        orderService.Verify(s => s.CreateOrderAsync(
            It.IsAny<List<CartItem>>(),
            PaymentMethod.Cash,
            Environment.MachineName,
            "أحمد علي",
            false,
            null,
            OrderType.Local,
            ""), Times.Once);
        Assert.Empty(viewModel.CartItems);
    }

    private sealed class FakeCurrentUser : ICurrentUserContext
    {
        public FakeCurrentUser(User user) => CurrentUser = user;

        public User? CurrentUser { get; }
        public bool IsActiveAdmin => CurrentUser is { IsActive: true, Role: UserRole.Admin };
    }
}
