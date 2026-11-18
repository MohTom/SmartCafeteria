using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using SmartCafeteria.TestSupport;
using Xunit;

namespace SmartCafeteria.Data.Tests;

public class FinancesIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbContext _dbContext;
    private readonly ProductRepository _productRepo;
    private readonly OrderRepository _orderRepo;

    public FinancesIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"smart_cafe_fin_{Guid.NewGuid():N}.db");
        _dbContext = new DbContext(_dbPath);
        var initializer = new DatabaseInitializer(_dbContext);
        initializer.InitializeAsync().GetAwaiter().GetResult();
        _productRepo = new ProductRepository(_dbContext);
        _orderRepo = new OrderRepository(_dbContext, _productRepo);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    private (DateTime Start, DateTime End) TodayRange()
        => (DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

    [Fact]
    public async Task AccountRepository_AddExpense_AndGetSummary()
    {
        await _productRepo.AddProductAsync(new Product { Id = "p-sum", NameAr = "مشروب", NameEn = "Drink", Price = 10, Category = "HotDrinks", Inventory = 50, MaxInventory = 100 });
        var order = await _orderRepo.CreateOrderAsync(
            new List<CartItem> { new CartItem { Product = new Product { Id = "p-sum", NameAr = "مشروب", Price = 10, Category = "HotDrinks" }, Quantity = 2 } },
            PaymentMethod.Cash, "T1", "أحمد", false, null);
        Assert.NotNull(order);

        var accountRepo = new AccountRepository(_dbContext, _orderRepo, TestCurrentUser.Admin());
        await accountRepo.AddEntryAsync(new AccountEntry
        {
            Id = "acc-1",
            Type = AccountEntryType.Expense,
            Category = ExpenseCategory.ProductsSupplies,
            Description = "توريد قهوة",
            Amount = 5,
            Date = DateTime.Now,
            PaymentMethod = PaymentMethod.Cash
        });

        var (start, end) = TodayRange();
        var summary = await accountRepo.GetSummaryAsync(start, end);

        Assert.Equal(20, summary.IncomeFromOrders);
        Assert.Equal(5, summary.TotalExpenses);
        Assert.Equal(15, summary.NetProfit);
        Assert.Equal(1, summary.OrderCount);
    }

    [Fact]
    public async Task AccountRepository_OtherIncome_AddsToIncome()
    {
        var accountRepo = new AccountRepository(_dbContext, _orderRepo, TestCurrentUser.Admin());
        await accountRepo.AddEntryAsync(new AccountEntry
        {
            Id = "acc-income",
            Type = AccountEntryType.Income,
            Category = ExpenseCategory.Other,
            Description = "إيراد خارجي",
            Amount = 100,
            Date = DateTime.Now,
            PaymentMethod = PaymentMethod.Bankak
        });

        var (start, end) = TodayRange();
        var summary = await accountRepo.GetSummaryAsync(start, end);
        Assert.Equal(100, summary.OtherIncome);
        Assert.Equal(100, summary.TotalIncome);
    }

    [Fact]
    public async Task AccountRepository_DeleteEntry_RemovesIt()
    {
        var accountRepo = new AccountRepository(_dbContext, _orderRepo, TestCurrentUser.Admin());
        await accountRepo.AddEntryAsync(new AccountEntry
        {
            Id = "acc-del",
            Type = AccountEntryType.Expense,
            Category = ExpenseCategory.Rent,
            Description = "إيجار",
            Amount = 50,
            Date = DateTime.Now
        });

        Assert.True(await accountRepo.DeleteEntryAsync("acc-del"));
        var entries = await accountRepo.GetAllEntriesAsync();
        Assert.DoesNotContain(entries, e => e.Id == "acc-del");
    }

    [Fact]
    public async Task SupplierRepository_CreateAndReceive_IncreaseInventoryAndBalance()
    {
        await _productRepo.AddProductAsync(new Product { Id = "p-sup", NameAr = "قهوة خام", NameEn = "Raw Coffee", Price = 5, Category = "HotDrinks", Inventory = 10, MaxInventory = 100 });

        var supplierRepo = new SupplierRepository(_dbContext, TestCurrentUser.Admin());
        var supplier = new Supplier { Id = "sup-1", Name = "مورد القهوة", Phone = "0911" };
        await supplierRepo.AddSupplierAsync(supplier);

        var po = new PurchaseOrder
        {
            Id = "po-1",
            SupplierId = "sup-1",
            SupplierName = "مورد القهوة",
            Status = PurchaseOrderStatus.Pending,
            Items = new List<PurchaseOrderItem>
            {
                new PurchaseOrderItem { ProductId = "p-sup", ProductName = "قهوة خام", Quantity = 5, UnitCost = 20 }
            }
        };
        Assert.True(await supplierRepo.CreatePurchaseOrderAsync(po));
        Assert.Equal(100, po.TotalAmount);

        var balance = await supplierRepo.GetSupplierBalanceAsync("sup-1");
        Assert.Equal(100, balance);

        Assert.True(await supplierRepo.ReceivePurchaseOrderAsync("po-1", "أحمد"));

        var product = await _productRepo.GetProductByIdAsync("p-sup");
        Assert.NotNull(product);
        Assert.Equal(15, product.Inventory);

        var orders = await supplierRepo.GetPurchaseOrdersForSupplierAsync("sup-1");
        var received = Assert.Single(orders, o => o.Id == "po-1");
        Assert.Equal(PurchaseOrderStatus.Received, received.Status);
    }

    [Fact]
    public async Task SupplierRepository_Cancel_NotReceivable()
    {
        var supplierRepo = new SupplierRepository(_dbContext, TestCurrentUser.Admin());
        var supplier = new Supplier { Id = "sup-2", Name = "مورد ثانٍ" };
        await supplierRepo.AddSupplierAsync(supplier);

        var po = new PurchaseOrder
        {
            Id = "po-2",
            SupplierId = "sup-2",
            SupplierName = "مورد ثانٍ",
            Items = new List<PurchaseOrderItem>
            {
                new PurchaseOrderItem { ProductId = "nope", ProductName = "غير موجود", Quantity = 1, UnitCost = 5 }
            }
        };
        await supplierRepo.CreatePurchaseOrderAsync(po);

        Assert.True(await supplierRepo.CancelPurchaseOrderAsync("po-2"));
        Assert.False(await supplierRepo.ReceivePurchaseOrderAsync("po-2", "أحمد"));

        var balance = await supplierRepo.GetSupplierBalanceAsync("sup-2");
        Assert.Equal(0, balance);
    }

    [Fact]
    public async Task SupplierRepository_Payments_ReduceBalance()
    {
        await _productRepo.AddProductAsync(new Product { Id = "p-pay", NameAr = "حليب", NameEn = "Milk", Price = 3, Category = "HotDrinks", Inventory = 5, MaxInventory = 100 });

        var supplierRepo = new SupplierRepository(_dbContext, TestCurrentUser.Admin());
        await supplierRepo.AddSupplierAsync(new Supplier { Id = "sup-3", Name = "مورد الحليب" });

        await supplierRepo.CreatePurchaseOrderAsync(new PurchaseOrder
        {
            Id = "po-3",
            SupplierId = "sup-3",
            SupplierName = "مورد الحليب",
            Items = new List<PurchaseOrderItem>
            {
                new PurchaseOrderItem { ProductId = "p-pay", ProductName = "حليب", Quantity = 10, UnitCost = 3 }
            }
        });

        await supplierRepo.AddSupplierPaymentAsync(new SupplierPayment
        {
            Id = "pay-1",
            SupplierId = "sup-3",
            Amount = 20,
            Date = DateTime.Now,
            PaymentMethod = PaymentMethod.Cash
        });

        var balance = await supplierRepo.GetSupplierBalanceAsync("sup-3");
        Assert.Equal(10, balance);

        var payments = await supplierRepo.GetPaymentsForSupplierAsync("sup-3");
        Assert.Single(payments);
    }

    [Fact]
    public async Task EmployeeRepository_AddAndTotals()
    {
        var repo = new EmployeeRepository(_dbContext, TestCurrentUser.Admin());
        await repo.AddEmployeeAsync(new Employee { Id = "emp-1", Name = "أحمد", Position = EmployeePosition.Cashier, MonthlySalary = 1000, IsActive = true });
        await repo.AddEmployeeAsync(new Employee { Id = "emp-2", Name = "سارة", Position = EmployeePosition.Barista, MonthlySalary = 2000, IsActive = true });
        await repo.AddEmployeeAsync(new Employee { Id = "emp-3", Name = "خالد", Position = EmployeePosition.Cook, MonthlySalary = 500, IsActive = false });

        var all = await repo.GetAllEmployeesAsync();
        Assert.Equal(3, all.Count());
        Assert.Equal(2, await repo.GetActiveEmployeeCountAsync());
        Assert.Equal(3000, await repo.GetTotalMonthlySalariesAsync());

        var result = await repo.GetEmployeeByIdAsync("emp-2");
        Assert.NotNull(result);
        Assert.Equal(EmployeePosition.Barista, result.Position);
    }

    [Fact]
    public async Task EmployeeRepository_UpdateAndDelete()
    {
        var repo = new EmployeeRepository(_dbContext, TestCurrentUser.Admin());
        await repo.AddEmployeeAsync(new Employee { Id = "emp-x", Name = "قديم", Position = EmployeePosition.Waiter, MonthlySalary = 700 });

        var employee = await repo.GetEmployeeByIdAsync("emp-x");
        Assert.NotNull(employee);
        employee.Name = "جديد";
        employee.MonthlySalary = 900;
        Assert.True(await repo.UpdateEmployeeAsync(employee));

        var updated = await repo.GetEmployeeByIdAsync("emp-x");
        Assert.NotNull(updated);
        Assert.Equal("جديد", updated.Name);
        Assert.Equal(900, updated.MonthlySalary);

        Assert.True(await repo.DeleteEmployeeAsync("emp-x"));
        Assert.Null(await repo.GetEmployeeByIdAsync("emp-x"));
    }
}
