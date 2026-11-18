namespace SmartCafeteria.Core.Enums;

/// <summary>Identifies every analytical report the analytics section can show.
/// The kind is the key the UI and the PDF exporter use to address a report,
/// so its title and column headers live in one place only.</summary>
public enum AnalyticsReportKind
{
    /// <summary>الأصناف الأكثر مبيعاً.</summary>
    TopItems,

    /// <summary>المبيعات حسب الفئة.</summary>
    SalesByCategory,

    /// <summary>توزيع طرق الدفع.</summary>
    PaymentMethods,

    /// <summary>أداء الكاشيرين.</summary>
    CashierPerformance,

    /// <summary>المبيعات حسب ساعة اليوم.</summary>
    SalesByHour,

    /// <summary>المبيعات حسب اليوم.</summary>
    SalesByDay,

    /// <summary>المبيعات حسب يوم الأسبوع.</summary>
    SalesByWeekday,

    /// <summary>هندسة القائمة: نجمة، حصان، لغز، راكد.</summary>
    MenuEngineering,

    /// <summary>الأصناف التي لم تُبَع في النطاق.</summary>
    DeadStock,

    /// <summary>أولوية إعادة الطلب.</summary>
    ReorderPriority,

    /// <summary>الإضافات والخيارات الأكثر استخداماً.</summary>
    FavouriteCustomizations,

    /// <summary>المصروفات حسب البند.</summary>
    ExpensesByCategory,

    /// <summary>قائمة الأرباح والخسائر.</summary>
    ProfitAndLoss,

    /// <summary>الموردون وأرصدتهم.</summary>
    Suppliers,

    /// <summary>الموظفون ورواتبهم.</summary>
    Employees,

    /// <summary>سجل الأخطاء والتنبيهات.</summary>
    ErrorLog,

    /// <summary>توزيع الطلبات حسب النوع (محلي / سفري).</summary>
    OrderTypes,

    /// <summary>الطلبات التي تحمل ملاحظات مقابل التي لا تحمل.</summary>
    OrdersWithNotes
}
