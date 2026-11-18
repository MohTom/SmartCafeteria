namespace SmartCafeteria.Core.Enums;

/// <summary>Which way the money moved in a financial operation. A purchase order
/// is a commitment: it is not cash yet, so it is neither an inflow nor an
/// outflow of the cash flow.</summary>
public enum FinancialDirection
{
    /// <summary>Money came in, for example a sale or other income.</summary>
    Inflow,

    /// <summary>Money went out, for example a supplier payment or a salary.</summary>
    Outflow,

    /// <summary>Money is promised but not paid yet, for example a purchase order.</summary>
    Commitment
}
