using SmartCafeteria.Core.Models;
using Xunit;

namespace SmartCafeteria.Core.Tests;

public class UserDisplayNameTests
{
    [Fact]
    public void DisplayName_UsesFullName_WhenPresent()
    {
        var user = new User { Username = "cashier", FullName = "أحمد علي" };

        Assert.Equal("أحمد علي", user.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DisplayName_FallsBackToUsername_WhenFullNameIsBlank(string fullName)
    {
        var user = new User { Username = "cashier", FullName = fullName };

        Assert.Equal("cashier", user.DisplayName);
    }
}
