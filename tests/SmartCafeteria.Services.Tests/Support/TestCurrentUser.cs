using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;

namespace SmartCafeteria.TestSupport;

internal sealed class TestCurrentUser : ICurrentUserContext
{
    public TestCurrentUser(User? user) => CurrentUser = user;

    public User? CurrentUser { get; set; }
    public bool IsActiveAdmin => CurrentUser is { IsActive: true, Role: UserRole.Admin };

    public static TestCurrentUser Admin(string id = "test-admin") => new(new User
    {
        Id = id,
        Username = id,
        FullName = "Test Admin",
        Role = UserRole.Admin,
        IsActive = true
    });

    public static TestCurrentUser Cashier(string id = "test-cashier") => new(new User
    {
        Id = id,
        Username = id,
        FullName = "Test Cashier",
        Role = UserRole.Cashier,
        IsActive = true
    });

    public static TestCurrentUser Anonymous() => new(null);
}
