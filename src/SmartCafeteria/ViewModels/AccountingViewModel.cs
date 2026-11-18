using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using System.Collections.ObjectModel;

namespace SmartCafeteria.ViewModels;
public partial class AccountingViewModel : ObservableObject
{
    private readonly IAccountService _accountService;
    private readonly IOrderService _orderService;
    private readonly ILogService _logService;
    private readonly ICurrentUserContext _currentUser;

    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private string _rangeKey = "اليوم";
    [ObservableProperty] private decimal _income;
    [ObservableProperty] private decimal _expenses;
    [ObservableProperty] private decimal _net;
    [ObservableProperty] private int _orderCount;

    [ObservableProperty] private bool _showExpenseDialog;
    [ObservableProperty] private string _newDescription = string.Empty;
    [ObservableProperty] private ExpenseCategory _newCategory = ExpenseCategory.ProductsSupplies;
    [ObservableProperty] private decimal _newAmount;
    [ObservableProperty] private DateTime _newDate = DateTime.Today;
    [ObservableProperty] private PaymentMethod _newPaymentMethod = PaymentMethod.Cash;
    [ObservableProperty] private AccountEntry? _editingEntry;

    public ObservableCollection<AccountEntry> Entries { get; } = new();
    public ObservableCollection<LedgerRow> Ledger { get; } = new();
    public List<string> RangeOptions { get; } = new() { "اليوم", "هذا الأسبوع", "هذا الشهر", "كل الفترات" };
    public List<ExpenseCategory> ExpenseCategories { get; } = Enum.GetValues<ExpenseCategory>().ToList();

    public AccountingViewModel(IAccountService accountService, IOrderService orderService, ILogService logService, ICurrentUserContext currentUser)
    {
        _accountService = accountService;
        _orderService = orderService;
        _logService = logService;
        _currentUser = currentUser;
    }

    public async Task LoadAsync()
    {
        var (start, end) = GetRange();

        var summary = await _accountService.GetSummaryAsync(start, end);
        Income = summary.TotalIncome;
        Expenses = summary.TotalExpenses;
        Net = summary.NetProfit;
        OrderCount = summary.OrderCount;

        var entries = (await _accountService.GetEntriesByDateRangeAsync(start, end)).ToList();
        Entries.Clear();
        foreach (var entry in entries)
            Entries.Add(entry);

        Ledger.Clear();
        var orders = (await _orderService.GetOrdersByDateRangeAsync(start, end))
            .Where(o => o.Status != OrderStatus.Cancelled);
        foreach (var order in orders)
            Ledger.Add(new LedgerRow(order.Timestamp, $"{order.Code} - {order.CashierName}", order.Total, true, order.Code));

        foreach (var entry in entries)
            Ledger.Add(new LedgerRow(entry.Date, entry.Description, entry.Amount, entry.Type == AccountEntryType.Income, entry.ReferenceCode));

        var sorted = Ledger.OrderByDescending(r => r.Date).ToList();
        Ledger.Clear();
        foreach (var row in sorted)
            Ledger.Add(row);
    }

    partial void OnRangeKeyChanged(string value) => _ = LoadAsync();

    private (DateTime Start, DateTime End) GetRange()
    {
        var now = DateTime.Now;
        return RangeKey switch
        {
            "اليوم" => (now.Date, now.Date.AddDays(1)),
            "هذا الأسبوع" => (now.Date.AddDays(-(int)now.DayOfWeek), now.Date.AddDays(1)),
            "هذا الشهر" => (new DateTime(now.Year, now.Month, 1), now.Date.AddDays(1)),
            _ => (DateTime.MinValue, DateTime.MaxValue)
        };
    }

    [RelayCommand]
    private void OpenExpenseDialog()
    {
        EditingEntry = null;
        NewDescription = string.Empty;
        NewCategory = ExpenseCategory.ProductsSupplies;
        NewAmount = 0;
        NewDate = DateTime.Today;
        NewPaymentMethod = PaymentMethod.Cash;
        ShowExpenseDialog = true;
    }

    [RelayCommand]
    private void OpenEditExpense(AccountEntry entry)
    {
        if (entry == null) return;
        EditingEntry = entry;
        NewDescription = entry.Description;
        NewCategory = entry.Category;
        NewAmount = entry.Amount;
        NewDate = entry.Date;
        NewPaymentMethod = entry.PaymentMethod;
        ShowExpenseDialog = true;
    }

    [RelayCommand]
    private void CloseExpenseDialog()
    {
        ShowExpenseDialog = false;
        EditingEntry = null;
    }

    [RelayCommand]
    private async Task SaveExpenseAsync()
    {
        if (NewAmount <= 0) return;
        if (string.IsNullOrWhiteSpace(NewDescription)) return;

        if (EditingEntry != null)
        {
            EditingEntry.Category = NewCategory;
            EditingEntry.Description = NewDescription.Trim();
            EditingEntry.Amount = NewAmount;
            EditingEntry.Date = NewDate;
            EditingEntry.PaymentMethod = NewPaymentMethod;
            await _accountService.UpdateEntryAsync(EditingEntry);
            await _logService.AddLogAsync($"تم تحديث المصروف: {NewDescription.Trim()}", "info");
        }
        else
        {
            var entry = new AccountEntry
            {
                Id = $"acc-{Guid.NewGuid():N}",
                Type = AccountEntryType.Expense,
                Category = NewCategory,
                Description = NewDescription.Trim(),
                Amount = NewAmount,
                Date = NewDate,
                PaymentMethod = NewPaymentMethod,
                UserName = _currentUser.CurrentUser?.DisplayName ?? string.Empty
            };
            await _accountService.AddEntryAsync(entry);
            await _logService.AddLogAsync($"تم تسجيل مصروف: {NewDescription.Trim()} - {NewAmount:F2} {Settings.CurrencyAr}", "info");
        }

        ShowExpenseDialog = false;
        EditingEntry = null;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteExpenseAsync(string entryId)
    {
        if (!Confirmations.Delete("هذا القيد المالي")) return;
        await _accountService.DeleteEntryAsync(entryId);
        await _logService.AddLogAsync("تم حذف قيد مالي", "warning");
        await LoadAsync();
    }
}

public class LedgerRow
{
    public DateTime Date { get; }
    public string Description { get; }
    public decimal Amount { get; }
    public bool IsIncome { get; }
    public string Reference { get; }

    public LedgerRow(DateTime date, string description, decimal amount, bool isIncome, string reference)
    {
        Date = date;
        Description = description;
        Amount = amount;
        IsIncome = isIncome;
        Reference = reference;
    }
}