using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SmartCafeteria.Core.Enums;

namespace SmartCafeteria.Converters;
public class EnumArabicNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            AccountEntryType.Income => "إيرادات",
            AccountEntryType.Expense => "مصروف",
            ExpenseCategory.ProductsSupplies => "توريدات منتجات",
            ExpenseCategory.Rent => "إيجار",
            ExpenseCategory.Salaries => "رواتب",
            ExpenseCategory.Utilities => "مرافق",
            ExpenseCategory.Maintenance => "صيانة",
            ExpenseCategory.Transport => "نقل",
            ExpenseCategory.Marketing => "تسويق",
            ExpenseCategory.Equipment => "معدات",
            ExpenseCategory.Taxes => "ضرائب",
            ExpenseCategory.Other => "أخرى",
            EmployeePosition.Cashier => "كاشير",
            EmployeePosition.Barista => "باريستا",
            EmployeePosition.Cook => "طباخ",
            EmployeePosition.Waiter => "نادل",
            EmployeePosition.Supervisor => "مشرف",
            EmployeePosition.Manager => "مدير",
            EmployeePosition.Cleaner => "عامل نظافة",
            EmployeePosition.Other => "أخرى",
            PurchaseOrderStatus.Pending => "قيد الانتظار",
            PurchaseOrderStatus.Received => "تم الاستلام",
            PurchaseOrderStatus.Cancelled => "ملغي",
            OrderStatus.Received => "تم الاستلام",
            OrderStatus.Cancelled => "ملغي",
            UserRole.Admin => "مدير",
            UserRole.Cashier => "كاشير",
            PaymentMethod.Cash => "نقداً",
            PaymentMethod.Bankak => "بنكك",
            OrderType.Local => "محلي",
            OrderType.Takeaway => "سفري",
            PrintMethod.FullReceiptAndSectionReceipts => "الإيصال الكامل + إيصالات الأقسام",
            PrintMethod.TotalReceiptTwice => "إيصال مزدوج (عميل + صراف)",
            PrintMethod.SingleReceipt => "إيصال واحد فقط",
            PrintMethod.SectionsOnly => "طباعة تذكرة للاقسام فقط",
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}