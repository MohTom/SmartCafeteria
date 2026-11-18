using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Core.Interfaces;
public interface ISupplierService
{
    Task<IEnumerable<Supplier>> GetAllSuppliersAsync();
    Task<Supplier?> GetSupplierByIdAsync(string id);
    Task<bool> AddSupplierAsync(Supplier supplier);
    Task<bool> UpdateSupplierAsync(Supplier supplier);
    Task<bool> DeleteSupplierAsync(string id);

    Task<IEnumerable<PurchaseOrder>> GetAllPurchaseOrdersAsync();
    Task<IEnumerable<PurchaseOrder>> GetPurchaseOrdersForSupplierAsync(string supplierId);
    Task<bool> CreatePurchaseOrderAsync(PurchaseOrder order);
    Task<bool> ReceivePurchaseOrderAsync(string purchaseOrderId, string userName);
    Task<bool> CancelPurchaseOrderAsync(string purchaseOrderId);

    Task<IEnumerable<SupplierPayment>> GetPaymentsForSupplierAsync(string supplierId);
    Task<bool> AddSupplierPaymentAsync(SupplierPayment payment);
    Task<decimal> GetSupplierBalanceAsync(string supplierId);
    string GeneratePurchaseOrderCode();
}