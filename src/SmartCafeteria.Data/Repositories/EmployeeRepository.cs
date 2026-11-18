using Dapper;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;

namespace SmartCafeteria.Data.Repositories;
public class EmployeeRepository : IEmployeeService
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;

    public EmployeeRepository(DbContext dbContext, ICurrentUserContext currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<Employee>> GetAllEmployeesAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryAsync<Employee>("SELECT * FROM Employees ORDER BY IsActive DESC, Name COLLATE NOCASE ASC");
    }

    public async Task<Employee?> GetEmployeeByIdAsync(string id)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<Employee>("SELECT * FROM Employees WHERE Id = @Id", new { Id = id });
    }

    public async Task<bool> AddEmployeeAsync(Employee employee)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Adding an employee");
        using var conn = await _dbContext.GetConnectionAsync();
        if (string.IsNullOrEmpty(employee.Id))
            employee.Id = $"emp-{Guid.NewGuid():N}";
        if (employee.HireDate == default)
            employee.HireDate = DateTime.Today;
        var affected = await conn.ExecuteAsync(
            "INSERT INTO Employees (Id, Name, Phone, Position, MonthlySalary, HireDate, IsActive, Notes) VALUES (@Id, @Name, @Phone, @Position, @MonthlySalary, @HireDate, @IsActive, @Notes)",
            employee);
        return affected > 0;
    }

    public async Task<bool> UpdateEmployeeAsync(Employee employee)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Updating an employee");
        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync(
            "UPDATE Employees SET Name=@Name, Phone=@Phone, Position=@Position, MonthlySalary=@MonthlySalary, HireDate=@HireDate, IsActive=@IsActive, Notes=@Notes WHERE Id=@Id",
            employee);
        return affected > 0;
    }

    public async Task<bool> DeleteEmployeeAsync(string id)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Deleting an employee");
        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync("DELETE FROM Employees WHERE Id = @Id", new { Id = id });
        return affected > 0;
    }

    public async Task<decimal> GetTotalMonthlySalariesAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<decimal?>(
            "SELECT SUM(MonthlySalary) FROM Employees WHERE IsActive = 1") ?? 0m;
    }

    public async Task<int> GetActiveEmployeeCountAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM Employees WHERE IsActive = 1");
    }
}