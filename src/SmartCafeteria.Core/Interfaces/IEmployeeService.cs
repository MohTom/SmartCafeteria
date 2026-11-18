using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Core.Interfaces;
public interface IEmployeeService
{
    Task<IEnumerable<Employee>> GetAllEmployeesAsync();
    Task<Employee?> GetEmployeeByIdAsync(string id);
    Task<bool> AddEmployeeAsync(Employee employee);
    Task<bool> UpdateEmployeeAsync(Employee employee);
    Task<bool> DeleteEmployeeAsync(string id);
    Task<decimal> GetTotalMonthlySalariesAsync();
    Task<int> GetActiveEmployeeCountAsync();
}