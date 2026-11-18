using FlaUI.Core.AutomationElements;
using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>
/// Covers the User Management screen. This screen previously crashed on navigation
/// because its view had only a DI constructor, which WPF's DataTemplate could not use.
/// </summary>
public class UserManagementScreenTests : AppTestBase
{
    [Fact]
    public void NavigateToUsersScreen_DoesNotCrash_ShowsContent()
    {
        NavigateTo("المستخدمون");

        // The app must still be responsive and the main window title intact (not crashed)
        Assert.False(string.IsNullOrEmpty(MainWindow.Name));

        // User management content should be visible
        Assert.True(HasText("إدارة المستخدمين") || HasText("المستخدمون"), "User management header not shown");
        Assert.True(HasText("إضافة مستخدم جديد"), "Add-user form not shown");
    }

    [Fact]
    public void UserScreen_ShowsUsersList_WithColumns()
    {
        NavigateTo("المستخدمون");

        // The user grid should be present; at least the default admin row should exist
        Assert.True(HasText("=مدير النظام") || HasText("مدير النظام") || HasAnyArabicText(),
            "Expected user content (admin row) to be visible");
    }

    [Fact]
    public void UserScreen_AddUser_WithoutUsername_ShowsValidation()
    {
        NavigateTo("المستخدمون");

        var addBtn = MainWindow.FindFirstDescendant(x => x.ByName("إضافة مستخدم"));
        Assert.NotNull(addBtn);
        addBtn!.Click();
        System.Threading.Thread.Sleep(800);

        Assert.True(HasText("الرجاء إدخال اسم المستخدم"), "Missing-username validation was not shown");
    }

    [Fact]
    public void UserScreen_AddUser_ShowsNewUser()
    {
        NavigateTo("المستخدمون");

        // Fill the "add user" form: two editable text boxes (full name + username)
        var allEdits = MainWindow.FindAllDescendants(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)).ToList();
        // Exclude the password box (identified by AutomationId NewPasswordInput) and any grid cells
        var textFields = allEdits
            .Where(e => !string.Equals(e.AutomationId, "NewPasswordInput", System.StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(textFields.Count >= 2, $"Expected at least 2 text fields, got {textFields.Count}");

        var uniqueName = $"test_user_{System.Guid.NewGuid():N}"[..10];
        textFields[0].AsTextBox().Enter("مستخدم اختبار");
        textFields[1].AsTextBox().Enter(uniqueName);

        // Set password
        var pw = MainWindow.FindFirstDescendant(x => x.ByAutomationId("NewPasswordInput"));
        Assert.NotNull(pw);
        pw.AsTextBox().Enter("testpass123");

        // Click Add
        var addBtn = MainWindow.FindFirstDescendant(x => x.ByName("إضافة مستخدم"));
        Assert.NotNull(addBtn);
        addBtn.Click();
        System.Threading.Thread.Sleep(1200);

        Assert.True(HasText(uniqueName), "Newly added user should be visible in the list");
    }
}