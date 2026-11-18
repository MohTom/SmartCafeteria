using SmartCafeteria.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartCafeteria.Core.Models;
public class Employee
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public EmployeePosition Position { get; set; }
    public decimal MonthlySalary { get; set; }
    public DateTime HireDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string Notes { get; set; } = string.Empty;
}