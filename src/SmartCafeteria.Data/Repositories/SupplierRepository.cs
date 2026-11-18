using Dapper;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;

namespace SmartCafeteria.Data.Repositories;
public class SupplierRepository : ISupplierService
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;

    public SupplierRepository(DbContext dbContext, ICurrentUserContext currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public string GeneratePurchaseOrderCode()
    {
        return $"PO-{DateTime.Now:yyMMdd}-{Guid.NewGuid():N}"[..13].ToUpperInvariant();
    }

    public async Task<IEnumerable<Supplier>> GetAllSuppliersAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryAsync<Supplier>("SELECT * FROM Suppliers ORDER BY IsActive DESC, Name COLLATE NOCASE ASC");
    }

    public async Task<Supplier?> GetSupplierByIdAsync(string id)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<Supplier>("SELECT * FROM Suppliers WHERE Id = @Id", new { Id = id });
    }

    public async Task<bool> AddSupplierAsync(Supplier supplier)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Adding a supplier");
        SmartCafeteria.Data.Validation.BusinessRules.ValidateSupplier(supplier);
        using var conn = await _dbContext.GetConnectionAsync();
        if (string.IsNullOrEmpty(supplier.Id))
            supplier.Id = $"sup-{Guid.NewGuid():N}";
        if (supplier.CreatedAt == default)
            supplier.CreatedAt = DateTime.Now;
        var affected = await conn.ExecuteAsync(
            "INSERT INTO Suppliers (Id, Name, Phone, Email, Address, Notes, IsActive, CreatedAt) VALUES (@Id, @Name, @Phone, @Email, @Address, @Notes, @IsActive, @CreatedAt)",
            supplier);
        return affected > 0;
    }

    public async Task<bool> UpdateSupplierAsync(Supplier supplier)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Updating a supplier");
        SmartCafeteria.Data.Validation.BusinessRules.ValidateSupplier(supplier);
        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync(
            "UPDATE Suppliers SET Name=@Name, Phone=@Phone, Email=@Email, Address=@Address, Notes=@Notes, IsActive=@IsActive WHERE Id=@Id",
            supplier);
        return affected > 0;
    }

    public async Task<bool> DeleteSupplierAsync(string id)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Deleting a supplier");
        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync("DELETE FROM Suppliers WHERE Id = @Id", new { Id = id });
        return affected > 0;
    }

    public async Task<IEnumerable<PurchaseOrder>> GetAllPurchaseOrdersAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var orders = (await conn.QueryAsync<PurchaseOrder>("SELECT * FROM PurchaseOrders ORDER BY Date DESC")).ToList();
        foreach (var order in orders)
        {
            order.Items = (await conn.QueryAsync<PurchaseOrderItem>(
                "SELECT * FROM PurchaseOrderItems WHERE PurchaseOrderId = @Id",
                new { Id = order.Id })).ToList();
        }
        return orders;
    }

    public async Task<IEnumerable<PurchaseOrder>> GetPurchaseOrdersForSupplierAsync(string supplierId)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var orders = (await conn.QueryAsync<PurchaseOrder>(
            "SELECT * FROM PurchaseOrders WHERE SupplierId = @SupplierId ORDER BY Date DESC",
            new { SupplierId = supplierId })).ToList();
        foreach (var order in orders)
        {
            order.Items = (await conn.QueryAsync<PurchaseOrderItem>(
                "SELECT * FROM PurchaseOrderItems WHERE PurchaseOrderId = @Id",
                new { Id = order.Id })).ToList();
        }
        return orders;
    }

    public async Task<bool> CreatePurchaseOrderAsync(PurchaseOrder order)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Creating a purchase order");
        SmartCafeteria.Data.Validation.BusinessRules.ValidatePurchaseOrder(order);
        using var conn = await _dbContext.GetConnectionAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            if (string.IsNullOrEmpty(order.Id))
                order.Id = $"po-{Guid.NewGuid():N}";
            if (string.IsNullOrEmpty(order.Code))
                order.Code = GeneratePurchaseOrderCode();
            if (order.Date == default)
                order.Date = DateTime.Now;
            order.TotalAmount = order.Items.Sum(i => i.Total);

            await conn.ExecuteAsync(
                "INSERT INTO PurchaseOrders (Id, Code, SupplierId, SupplierName, Date, TotalAmount, Status, Notes) VALUES (@Id, @Code, @SupplierId, @SupplierName, @Date, @TotalAmount, @Status, @Notes)",
                order, transaction);

            foreach (var item in order.Items)
            {
                item.Id = $"poi-{Guid.NewGuid():N}";
                item.PurchaseOrderId = order.Id;
                await conn.ExecuteAsync(
                    "INSERT INTO PurchaseOrderItems (Id, PurchaseOrderId, ProductId, ProductName, Quantity, UnitCost) VALUES (@Id, @PurchaseOrderId, @ProductId, @ProductName, @Quantity, @UnitCost)",
                    item, transaction);
            }

            transaction.Commit();
            return true;
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

    public async Task<bool> ReceivePurchaseOrderAsync(string purchaseOrderId, string userName)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Receiving a purchase order");
        using var conn = await _dbContext.GetConnectionAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            var order = await conn.QueryFirstOrDefaultAsync<PurchaseOrder>(
                "SELECT * FROM PurchaseOrders WHERE Id = @Id", new { Id = purchaseOrderId }, transaction);
            if (order == null || order.Status != PurchaseOrderStatus.Pending)
                return false;

            var items = (await conn.QueryAsync<PurchaseOrderItem>(
                "SELECT * FROM PurchaseOrderItems WHERE PurchaseOrderId = @Id",
                new { Id = order.Id }, transaction)).ToList();

            foreach (var item in items)
            {
                if (item.Quantity <= 0)
                    continue;
                var affected = await conn.ExecuteAsync(
                    "UPDATE Products SET Inventory = Inventory + @Quantity, Barcode = COALESCE(Barcode, @Barcode) WHERE Id = @Id",
                    new { Id = item.ProductId, Quantity = item.Quantity, Barcode = item.ProductId }, transaction);
                if (affected == 0)
                    throw new InvalidOperationException($"المنتج {item.ProductName} غير موجود في المخزون");
            }

            await conn.ExecuteAsync(
                "UPDATE PurchaseOrders SET Status = @Status WHERE Id = @Id",
                new { Id = order.Id, Status = (int)PurchaseOrderStatus.Received }, transaction);

            transaction.Commit();
            return true;
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

    public async Task<bool> CancelPurchaseOrderAsync(string purchaseOrderId)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Cancelling a purchase order");
        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync(
            "UPDATE PurchaseOrders SET Status = @Status WHERE Id = @Id AND Status = @Pending",
            new { Id = purchaseOrderId, Status = (int)PurchaseOrderStatus.Cancelled, Pending = (int)PurchaseOrderStatus.Pending });
        return affected > 0;
    }

    public async Task<IEnumerable<SupplierPayment>> GetPaymentsForSupplierAsync(string supplierId)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryAsync<SupplierPayment>(
            "SELECT * FROM SupplierPayments WHERE SupplierId = @SupplierId ORDER BY Date DESC",
            new { SupplierId = supplierId });
    }

    public async Task<bool> AddSupplierPaymentAsync(SupplierPayment payment)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Recording a supplier payment");
        SmartCafeteria.Data.Validation.BusinessRules.ValidateSupplierPayment(payment);
        using var conn = await _dbContext.GetConnectionAsync();
        if (string.IsNullOrEmpty(payment.Id))
            payment.Id = $"sp-{Guid.NewGuid():N}";
        if (payment.Date == default)
            payment.Date = DateTime.Now;
        var affected = await conn.ExecuteAsync(
            "INSERT INTO SupplierPayments (Id, SupplierId, Date, Amount, PaymentMethod, Notes) VALUES (@Id, @SupplierId, @Date, @Amount, @PaymentMethod, @Notes)",
            payment);
        return affected > 0;
    }

    public async Task<decimal> GetSupplierBalanceAsync(string supplierId)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var ordersTotal = await conn.QueryFirstOrDefaultAsync<decimal?>(
            "SELECT SUM(TotalAmount) FROM PurchaseOrders WHERE SupplierId = @SupplierId AND Status IN (0, 1)",
            new { SupplierId = supplierId }) ?? 0m;
        var paymentsTotal = await conn.QueryFirstOrDefaultAsync<decimal?>(
            "SELECT SUM(Amount) FROM SupplierPayments WHERE SupplierId = @SupplierId",
            new { SupplierId = supplierId }) ?? 0m;
        return ordersTotal - paymentsTotal;
    }
}