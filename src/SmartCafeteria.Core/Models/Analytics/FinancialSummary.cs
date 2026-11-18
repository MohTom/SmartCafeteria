namespace SmartCafeteria.Core.Models.Analytics;

/// <summary>The totals that close the general financial report. Purchase orders
/// are shown as commitments, so they are kept out of the cash totals and are
/// only compared with the supplier payments in <see cref="SupplierMovement"/>.</summary>
public sealed class FinancialSummary
{
    /// <summary>Revenue of the received orders in the period.</summary>
    public decimal Sales { get; init; }

    /// <summary>Income recorded outside of sales in the period.</summary>
    public decimal OtherIncome { get; init; }

    /// <summary>Money received: sales plus other income.</summary>
    public decimal TotalInflow => Sales + OtherIncome;

    /// <summary>Cash paid to suppliers in the period.</summary>
    public decimal SupplierPayments { get; init; }

    /// <summary>Salary expenses of the period.</summary>
    public decimal SalaryPayments { get; init; }

    /// <summary>Expenses of the period that are neither salaries nor supplier payments.</summary>
    public decimal OtherExpenses { get; init; }

    /// <summary>Cash paid out: supplier payments, salaries and other expenses.</summary>
    public decimal TotalOutflow => SupplierPayments + SalaryPayments + OtherExpenses;

    /// <summary>Money received minus money paid out.</summary>
    public decimal NetCashFlow => TotalInflow - TotalOutflow;

    /// <summary>Value of the purchase orders placed in the period, a commitment
    /// rather than cash.</summary>
    public decimal SupplierPurchases { get; init; }

    /// <summary>Purchases minus the cash already paid to the suppliers.</summary>
    public decimal SupplierMovement => SupplierPurchases - SupplierPayments;

    /// <summary>Number of operations in the ledger.</summary>
    public int Operations { get; init; }
}
