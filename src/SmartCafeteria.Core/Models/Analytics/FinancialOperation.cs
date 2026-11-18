using SmartCafeteria.Core.Enums;

namespace SmartCafeteria.Core.Models.Analytics;

/// <summary>One money movement of the general financial report: a sale, an
/// income entry, a supplier payment, a salary, another expense or a purchase
/// order commitment.</summary>
public sealed class FinancialOperation
{
    /// <summary>Date the operation happened.</summary>
    public DateTime Date { get; init; }

    /// <summary>Arabic name of the operation type, for example "مبيعات".</summary>
    public string KindLabel { get; init; } = string.Empty;

    /// <summary>What the operation was about.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Who the money came from or went to.</summary>
    public string Party { get; init; } = string.Empty;

    /// <summary>Arabic name of the payment method.</summary>
    public string PaymentLabel { get; init; } = string.Empty;

    /// <summary>Whether the money came in, went out or is still only promised.</summary>
    public FinancialDirection Direction { get; init; }

    /// <summary>Amount of the operation, always positive.</summary>
    public decimal Amount { get; init; }

    /// <summary>Order code, entry id or purchase order code the row comes from.</summary>
    public string Reference { get; init; } = string.Empty;
}
