using System.Globalization;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Models.Analytics;

namespace SmartCafeteria.Services.Analytics;

/// <summary>Calculations for money going out and coming in: expenses by
/// category, a profit and loss statement, supplier balances, payroll and
/// the general financial ledger.</summary>
public static class AnalyticsFinanceCalculators
{
    /// <summary>Every expense category that had money spent on it.</summary>
    public static IReadOnlyList<AnalyticsRow> ExpensesByCategory(AnalyticsInput input)
    {
        var groups = input.AccountEntries
            .Where(e => e.Type == AccountEntryType.Expense)
            .GroupBy(e => e.Category)
            .Select(g => new Bucket(ExpenseLabel(g.Key), g.Count(), g.Sum(e => e.Amount)))
            .OrderByDescending(b => b.Amount)
            .ToList();

        var total = groups.Sum(b => b.Amount);
        var max = AnalyticsSalesCalculators.Max(groups.Select(b => b.Amount));
        var rows = new List<AnalyticsRow>(groups.Count);
        foreach (var b in groups)
        {
            rows.Add(new AnalyticsRow
            {
                Primary = b.Name,
                Secondary = $"عدد القيود: {AnalyticsFormat.Number(b.Count)}",
                Tertiary = $"النسبة: {AnalyticsFormat.Percent(total <= 0m ? 0d : (double)(b.Amount / total))}",
                Value = AnalyticsFormat.Money(b.Amount, input.Currency),
                BarRatio = AnalyticsSalesCalculators.Ratio(b.Amount, max)
            });
        }

        return rows;
    }

    /// <summary>A line-based profit and loss statement.</summary>
    public static IReadOnlyList<AnalyticsRow> ProfitAndLoss(AnalyticsInput input)
    {
        var salesRevenue = input.ReceivedOrders.Sum(o => o.Total);
        var otherIncome = input.AccountEntries.Where(e => e.Type == AccountEntryType.Income).Sum(e => e.Amount);
        var expenses = input.AccountEntries.Where(e => e.Type == AccountEntryType.Expense).Sum(e => e.Amount);
        var gross = salesRevenue + otherIncome;
        var net = gross - expenses;

        return new List<AnalyticsRow>
        {
            new() { Primary = "إيرادات المبيعات", Tertiary = Share(salesRevenue, salesRevenue), Value = AnalyticsFormat.Money(salesRevenue, input.Currency), BarRatio = 1d },
            new() { Primary = "إيرادات أخرى", Tertiary = Share(otherIncome, salesRevenue), Value = AnalyticsFormat.Money(otherIncome, input.Currency), BarRatio = AnalyticsSalesCalculators.Ratio(otherIncome, salesRevenue) },
            new() { Primary = "إجمالي الدخل", Tertiary = Share(gross, salesRevenue), Value = AnalyticsFormat.Money(gross, input.Currency), BarRatio = 1d },
            new() { Primary = "المصروفات", Tertiary = Share(expenses, salesRevenue), Value = AnalyticsFormat.Money(expenses, input.Currency), BarRatio = AnalyticsSalesCalculators.Ratio(expenses, salesRevenue) },
            new() { Primary = "صافي الربح / الخسارة", Tertiary = Share(net, salesRevenue), Value = AnalyticsFormat.Money(net, input.Currency), BarRatio = 1d }
        };
    }

    private static string Share(decimal amount, decimal total)
        => $"النسبة من المبيعات: {AnalyticsFormat.Percent(total <= 0m ? 0d : (double)(amount / total))}";

    /// <summary>Active suppliers with what they were paid for and what is left.</summary>
    public static IReadOnlyList<AnalyticsRow> Suppliers(AnalyticsInput input)
    {
        var purchased = input.PurchaseOrders
            .GroupBy(po => !string.IsNullOrWhiteSpace(po.SupplierId) ? po.SupplierId : po.SupplierName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(po => po.TotalAmount), StringComparer.Ordinal);

        var paid = input.SupplierPayments
            .GroupBy(p => p.SupplierId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount), StringComparer.Ordinal);

        var balances = new List<SupplierBalance>();
        foreach (var supplier in input.Suppliers.Where(s => s.IsActive))
        {
            purchased.TryGetValue(supplier.Id, out var total);
            purchased.TryGetValue(supplier.Name, out var totalByName);
            if (total == 0m)
                total = totalByName;
            paid.TryGetValue(supplier.Id, out var amountPaid);
            balances.Add(new SupplierBalance(supplier.Name, supplier.Phone ?? string.Empty, total, amountPaid, total - amountPaid));
        }

        var max = AnalyticsSalesCalculators.Max(balances.Select(b => b.Balance));
        var rows = new List<AnalyticsRow>();
        var rank = 1;
        foreach (var b in balances.OrderByDescending(x => x.Balance).ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            rows.Add(new AnalyticsRow
            {
                Rank = rank.ToString(CultureInfo.InvariantCulture),
                Primary = b.Name,
                Secondary = b.Phone,
                Tertiary = $"إجمالي: {AnalyticsFormat.Money(b.Total, input.Currency)} | مدفوع: {AnalyticsFormat.Money(b.Paid, input.Currency)}",
                Value = $"الرصيد: {AnalyticsFormat.Money(b.Balance, input.Currency)}",
                BarRatio = b.Balance <= 0m ? 0d : AnalyticsSalesCalculators.Ratio(b.Balance, max)
            });
            rank++;
        }

        return rows;
    }

    /// <summary>Active employees with their position and monthly salary.</summary>
    public static IReadOnlyList<AnalyticsRow> Employees(AnalyticsInput input)
    {
        var max = AnalyticsSalesCalculators.Max(input.Employees.Where(e => e.IsActive).Select(e => e.MonthlySalary));
        var rows = new List<AnalyticsRow>();
        foreach (var employee in input.Employees.Where(e => e.IsActive).OrderBy(e => e.Position).ThenBy(e => e.Name))
        {
            rows.Add(new AnalyticsRow
            {
                Primary = employee.Name,
                Secondary = PositionLabel(employee.Position),
                Tertiary = string.IsNullOrWhiteSpace(employee.Phone)
                    ? $"تاريخ التعيين: {employee.HireDate:yyyy/MM/dd}"
                    : $"الهاتف: {employee.Phone} | تاريخ التعيين: {employee.HireDate:yyyy/MM/dd}",
                Value = AnalyticsFormat.Money(employee.MonthlySalary, input.Currency),
                BarRatio = AnalyticsSalesCalculators.Ratio(employee.MonthlySalary, max)
            });
        }

        return rows;
    }

    /// <summary>The most recent entries of the operations log.</summary>
    public static IReadOnlyList<AnalyticsRow> ErrorLog(AnalyticsInput input)
    {
        var rows = new List<AnalyticsRow>();
        foreach (var log in input.Logs.OrderByDescending(l => l.Timestamp).Take(100))
        {
            rows.Add(new AnalyticsRow
            {
                Primary = log.Message,
                Secondary = log.Level.ToUpperInvariant(),
                Tertiary = log.Timestamp.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture),
                Value = string.Empty,
                BarRatio = 0d
            });
        }

        return rows;
    }

    /// <summary>Builds the general financial report: every money movement of the
    /// period in one chronological ledger, closed by a summary block.</summary>
    public static FinancialReport GeneralFinancial(AnalyticsInput input)
    {
        var start = input.Start;
        var end = input.End;
        var suppliers = input.Suppliers.ToDictionary(s => s.Id, s => s.Name, StringComparer.Ordinal);
        var employees = input.Employees.ToDictionary(e => e.Id, e => e.Name, StringComparer.Ordinal);

        var operations = new List<FinancialOperation>();
        var sales = 0m;
        var otherIncome = 0m;
        var supplierPayments = 0m;
        var salaryPayments = 0m;
        var otherExpenses = 0m;
        var supplierPurchases = 0m;

        foreach (var order in input.ReceivedOrders
                     .Where(o => InRange(o.Timestamp, start, end))
                     .OrderBy(o => o.Timestamp))
        {
            operations.Add(new FinancialOperation
            {
                Date = order.Timestamp,
                KindLabel = "مبيعات",
                Description = $"طلب {order.Code}",
                Party = string.IsNullOrWhiteSpace(order.CashierName) ? "كاشير" : order.CashierName,
                PaymentLabel = PaymentLabel(order.PaymentMethod),
                Direction = FinancialDirection.Inflow,
                Amount = order.Total,
                Reference = order.Id
            });
            sales += order.Total;
        }

        foreach (var entry in input.AccountEntries
                     .Where(e => InRange(e.Date, start, end))
                     .OrderBy(e => e.Date))
        {
            if (entry.Type == AccountEntryType.Income)
            {
                operations.Add(new FinancialOperation
                {
                    Date = entry.Date,
                    KindLabel = "إيراد آخر",
                    Description = string.IsNullOrWhiteSpace(entry.Description) ? "إيراد" : entry.Description,
                    Party = string.IsNullOrWhiteSpace(entry.UserName) ? "المدير" : entry.UserName,
                    PaymentLabel = PaymentLabel(entry.PaymentMethod),
                    Direction = FinancialDirection.Inflow,
                    Amount = entry.Amount,
                    Reference = entry.Id
                });
                otherIncome += entry.Amount;
            }
            else if (entry.Category == ExpenseCategory.Salaries)
            {
                var party = ResolveEmployee(entry.ReferenceCode, employees);
                operations.Add(new FinancialOperation
                {
                    Date = entry.Date,
                    KindLabel = "رواتب",
                    Description = string.IsNullOrWhiteSpace(entry.Description) ? "راتب" : entry.Description,
                    Party = party,
                    PaymentLabel = PaymentLabel(entry.PaymentMethod),
                    Direction = FinancialDirection.Outflow,
                    Amount = entry.Amount,
                    Reference = entry.Id
                });
                salaryPayments += entry.Amount;
            }
            else
            {
                operations.Add(new FinancialOperation
                {
                    Date = entry.Date,
                    KindLabel = "مصروف",
                    Description = string.IsNullOrWhiteSpace(entry.Description) ? ExpenseLabel(entry.Category) : entry.Description,
                    Party = string.IsNullOrWhiteSpace(entry.UserName) ? "المدير" : entry.UserName,
                    PaymentLabel = PaymentLabel(entry.PaymentMethod),
                    Direction = FinancialDirection.Outflow,
                    Amount = entry.Amount,
                    Reference = entry.Id
                });
                otherExpenses += entry.Amount;
            }
        }

        foreach (var payment in input.SupplierPayments
                     .Where(p => InRange(p.Date, start, end))
                     .OrderBy(p => p.Date))
        {
            var party = suppliers.TryGetValue(payment.SupplierId, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : "مورد";
            operations.Add(new FinancialOperation
            {
                Date = payment.Date,
                KindLabel = "دفعة مورد",
                Description = string.IsNullOrWhiteSpace(payment.Notes) ? "دفعة لمورد" : payment.Notes,
                Party = party,
                PaymentLabel = PaymentLabel(payment.PaymentMethod),
                Direction = FinancialDirection.Outflow,
                Amount = payment.Amount,
                Reference = payment.Id
            });
            supplierPayments += payment.Amount;
        }

        foreach (var po in input.PurchaseOrders
                     .Where(p => InRange(p.Date, start, end))
                     .Where(p => p.Status != PurchaseOrderStatus.Cancelled)
                     .OrderBy(p => p.Date))
        {
            var party = !string.IsNullOrWhiteSpace(po.SupplierName)
                ? po.SupplierName
                : suppliers.TryGetValue(po.SupplierId, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : "مورد";
            operations.Add(new FinancialOperation
            {
                Date = po.Date,
                KindLabel = "أمر شراء",
                Description = $"أمر شراء {po.Code}",
                Party = party,
                PaymentLabel = "آجل",
                Direction = FinancialDirection.Commitment,
                Amount = po.TotalAmount,
                Reference = po.Id
            });
            supplierPurchases += po.TotalAmount;
        }

        var ordered = operations.OrderBy(o => o.Date).ThenBy(o => o.KindLabel).ToList();

        return new FinancialReport
        {
            PeriodLabel = FormatFinancialPeriod(start, end),
            Currency = string.IsNullOrWhiteSpace(input.Currency) ? "SDG" : input.Currency,
            Operations = ordered,
            Summary = new FinancialSummary
            {
                Sales = sales,
                OtherIncome = otherIncome,
                SupplierPayments = supplierPayments,
                SalaryPayments = salaryPayments,
                OtherExpenses = otherExpenses,
                SupplierPurchases = supplierPurchases,
                Operations = ordered.Count
            }
        };
    }

    private static bool InRange(DateTime timestamp, DateTime? start, DateTime? end)
        => (!start.HasValue || timestamp >= start.Value) && (!end.HasValue || timestamp <= end.Value);

    private static string FormatFinancialPeriod(DateTime? start, DateTime? end)
    {
        if (start.HasValue && end.HasValue)
            return $"من {start.Value.Date:yyyy/MM/dd} إلى {end.Value.Date:yyyy/MM/dd}";
        if (start.HasValue)
            return $"من {start.Value.Date:yyyy/MM/dd}";
        if (end.HasValue)
            return $"حتى {end.Value.Date:yyyy/MM/dd}";
        return "كل الفترات";
    }

    private static string ResolveEmployee(string reference, IReadOnlyDictionary<string, string> employees)
    {
        if (!string.IsNullOrWhiteSpace(reference) && employees.TryGetValue(reference, out var name) && !string.IsNullOrWhiteSpace(name))
            return name;
        return "موظف";
    }

    private static string PaymentLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "نقداً",
        PaymentMethod.Bankak => "بنكك",
        _ => method.ToString()
    };

    private static string ExpenseLabel(ExpenseCategory category) => category switch
    {
        ExpenseCategory.ProductsSupplies => "المشتريات واللوازم",
        ExpenseCategory.Rent => "الإيجار",
        ExpenseCategory.Salaries => "الرواتب",
        ExpenseCategory.Utilities => "المرافق",
        ExpenseCategory.Maintenance => "الصيانة",
        ExpenseCategory.Transport => "النقل",
        ExpenseCategory.Marketing => "التسويق",
        ExpenseCategory.Equipment => "المعدات",
        ExpenseCategory.Taxes => "الضرائب",
        ExpenseCategory.Other => "أخرى",
        _ => category.ToString()
    };

    private static string PositionLabel(EmployeePosition position) => position switch
    {
        EmployeePosition.Cashier => "كاشير",
        EmployeePosition.Barista => "باريستا",
        EmployeePosition.Cook => "طباخ",
        EmployeePosition.Waiter => "نادل",
        EmployeePosition.Supervisor => "مشرف",
        EmployeePosition.Manager => "مدير",
        EmployeePosition.Cleaner => "عامل نظافة",
        EmployeePosition.Other => "أخرى",
        _ => position.ToString()
    };

    private sealed record Bucket(string Name, int Count, decimal Amount);

    private sealed record SupplierBalance(string Name, string Phone, decimal Total, decimal Paid, decimal Balance);
}
