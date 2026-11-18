using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using System.Collections.ObjectModel;

namespace SmartCafeteria.ViewModels;
public partial class EmployeesViewModel : ObservableObject
{
    private readonly IEmployeeService _employeeService;
    private readonly IAccountService _accountService;
    private readonly ILogService _logService;
    private readonly ICurrentUserContext _currentUser;

    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private decimal _totalSalaries;

    [ObservableProperty] private bool _showEmployeeDialog;
    [ObservableProperty] private bool _isEditingEmployee;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newPhone = string.Empty;
    [ObservableProperty] private EmployeePosition _newPosition = EmployeePosition.Cashier;
    [ObservableProperty] private decimal _newSalary;
    [ObservableProperty] private DateTime _newHireDate = DateTime.Today;
    [ObservableProperty] private bool _newIsActive = true;
    [ObservableProperty] private string _newNotes = string.Empty;
    [ObservableProperty] private Employee? _editingEmployee;

    [ObservableProperty] private bool _showSalaryDialog;
    [ObservableProperty] private Employee? _salaryEmployee;
    [ObservableProperty] private decimal _salaryAmount;
    [ObservableProperty] private string _salaryNotes = string.Empty;

    public ObservableCollection<Employee> Employees { get; } = new();
    public List<EmployeePosition> Positions { get; } = Enum.GetValues<EmployeePosition>().ToList();

    public EmployeesViewModel(IEmployeeService employeeService, IAccountService accountService, ILogService logService, ICurrentUserContext currentUser)
    {
        _employeeService = employeeService;
        _accountService = accountService;
        _logService = logService;
        _currentUser = currentUser;
    }

    public async Task LoadAsync()
    {
        var employees = (await _employeeService.GetAllEmployeesAsync()).ToList();
        Employees.Clear();
        foreach (var employee in employees)
            Employees.Add(employee);

        ActiveCount = await _employeeService.GetActiveEmployeeCountAsync();
        TotalSalaries = await _employeeService.GetTotalMonthlySalariesAsync();
    }

    [RelayCommand]
    private void OpenEmployeeDialog()
    {
        EditingEmployee = null;
        IsEditingEmployee = false;
        NewName = string.Empty;
        NewPhone = string.Empty;
        NewPosition = EmployeePosition.Cashier;
        NewSalary = 0;
        NewHireDate = DateTime.Today;
        NewIsActive = true;
        NewNotes = string.Empty;
        ShowEmployeeDialog = true;
    }

    [RelayCommand]
    private void OpenEditEmployee(Employee employee)
    {
        if (employee == null) return;
        EditingEmployee = employee;
        IsEditingEmployee = true;
        NewName = employee.Name;
        NewPhone = employee.Phone;
        NewPosition = employee.Position;
        NewSalary = employee.MonthlySalary;
        NewHireDate = employee.HireDate;
        NewIsActive = employee.IsActive;
        NewNotes = employee.Notes;
        ShowEmployeeDialog = true;
    }

    [RelayCommand]
    private void CloseEmployeeDialog()
    {
        ShowEmployeeDialog = false;
        EditingEmployee = null;
    }

    [RelayCommand]
    private async Task SaveEmployeeAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName)) return;

        if (EditingEmployee != null)
        {
            EditingEmployee.Name = NewName.Trim();
            EditingEmployee.Phone = NewPhone.Trim();
            EditingEmployee.Position = NewPosition;
            EditingEmployee.MonthlySalary = NewSalary;
            EditingEmployee.HireDate = NewHireDate;
            EditingEmployee.IsActive = NewIsActive;
            EditingEmployee.Notes = NewNotes.Trim();
            await _employeeService.UpdateEmployeeAsync(EditingEmployee);
            await _logService.AddLogAsync($"تم تحديث بيانات الموظف: {EditingEmployee.Name}", "info");
        }
        else
        {
            var employee = new Employee
            {
                Id = $"emp-{Guid.NewGuid():N}",
                Name = NewName.Trim(),
                Phone = NewPhone.Trim(),
                Position = NewPosition,
                MonthlySalary = NewSalary,
                HireDate = NewHireDate,
                IsActive = NewIsActive,
                Notes = NewNotes.Trim()
            };
            await _employeeService.AddEmployeeAsync(employee);
            await _logService.AddLogAsync($"تم إضافة موظف جديد: {employee.Name}", "success");
        }

        ShowEmployeeDialog = false;
        EditingEmployee = null;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteEmployeeAsync(string employeeId)
    {
        var employee = Employees.FirstOrDefault(e => e.Id == employeeId);
        if (!Confirmations.Delete($"الموظف {employee?.Name ?? employeeId}")) return;
        await _employeeService.DeleteEmployeeAsync(employeeId);
        await _logService.AddLogAsync($"تم حذف الموظف: {employee?.Name ?? employeeId}", "warning");
        await LoadAsync();
    }

    [RelayCommand]
    private void OpenSalaryDialog(Employee employee)
    {
        if (employee == null) return;
        SalaryEmployee = employee;
        SalaryAmount = employee.MonthlySalary;
        SalaryNotes = $"راتب شهر لـ {employee.Name}";
        ShowSalaryDialog = true;
    }

    [RelayCommand]
    private void CloseSalaryDialog()
    {
        ShowSalaryDialog = false;
        SalaryEmployee = null;
    }

    [RelayCommand]
    private async Task PaySalaryAsync()
    {
        if (SalaryEmployee == null || SalaryAmount <= 0) return;

        var entry = new AccountEntry
        {
            Id = $"acc-{Guid.NewGuid():N}",
            Type = AccountEntryType.Expense,
            Category = ExpenseCategory.Salaries,
            Description = string.IsNullOrWhiteSpace(SalaryNotes) ? $"راتب شهر لـ {SalaryEmployee.Name}" : SalaryNotes.Trim(),
            Amount = SalaryAmount,
            Date = DateTime.Now,
            PaymentMethod = PaymentMethod.Cash,
            ReferenceCode = SalaryEmployee.Id,
            UserName = _currentUser.CurrentUser?.DisplayName ?? string.Empty
        };

        await _accountService.AddEntryAsync(entry);
        await _logService.AddLogAsync($"تم صرف راتب {SalaryEmployee.Name}: {SalaryAmount:F2} {Settings.CurrencyAr}", "success");

        ShowSalaryDialog = false;
        SalaryEmployee = null;
        await LoadAsync();
    }
}