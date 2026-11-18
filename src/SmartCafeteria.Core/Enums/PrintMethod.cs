namespace SmartCafeteria.Core.Enums;
public enum PrintMethod
{
    FullReceiptAndSectionReceipts,
    TotalReceiptTwice,
    SingleReceipt,
    /// <summary>Only one ticket per section/category; no customer receipt.</summary>
    SectionsOnly
}
