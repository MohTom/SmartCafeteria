using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Services.Analytics;

/// <summary>The data every analytical report is built from. It is assembled in
/// one read pass by the analytics service and then never changes, so the
/// calculations stay pure and can be tested without a database.</summary>
public sealed class AnalyticsInput
{
    /// <summary>Orders inside the reported range, whatever their status.</summary>
    public required IReadOnlyList<Order> Orders { get; init; }

    /// <summary>Every order of the history, used to show when a product was
    /// last sold even when that sale falls outside the reported range.</summary>
    public required IReadOnlyList<Order> AllOrders { get; init; }

    /// <summary>Orders inside the reported range that were received, so they count as sales.</summary>
    public required IReadOnlyList<Order> ReceivedOrders { get; init; }

    /// <summary>Every product of the menu.</summary>
    public required IReadOnlyList<Product> Products { get; init; }

    /// <summary>Accounting entries inside the reported range.</summary>
    public required IReadOnlyList<AccountEntry> AccountEntries { get; init; }

    /// <summary>Every supplier.</summary>
    public required IReadOnlyList<Supplier> Suppliers { get; init; }

    /// <summary>Every purchase order.</summary>
    public required IReadOnlyList<PurchaseOrder> PurchaseOrders { get; init; }

    /// <summary>Every payment made to a supplier.</summary>
    public required IReadOnlyList<SupplierPayment> SupplierPayments { get; init; }

    /// <summary>Every employee.</summary>
    public required IReadOnlyList<Employee> Employees { get; init; }

    /// <summary>Every entry of the operations log.</summary>
    public required IReadOnlyList<SystemLog> Logs { get; init; }

    /// <summary>Currency shown next to every amount.</summary>
    public required string Currency { get; init; }

    /// <summary>Number of days the reported range covers, at least one.
    /// Used to turn a sales quantity into a daily average.</summary>
    public int RangeDays { get; init; } = 1;

    /// <summary>Start of the reported range, null when the range is open.</summary>
    public DateTime? Start { get; init; }

    /// <summary>End of the reported range, null when the range is open.</summary>
    public DateTime? End { get; init; }
}
