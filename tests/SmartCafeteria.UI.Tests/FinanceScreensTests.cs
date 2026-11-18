using FlaUI.Core.AutomationElements;
using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>Covers the new Admin screens: Accounting, Suppliers, and Employees.</summary>
public class FinanceScreensTests : AppTestBase
{
    private static int _seq = 0;

    private static string UniqueCode() => $"{++_seq}{System.Environment.TickCount % 100000}";

    [Fact]
    public void Accounting_Screen_ShowsStatsAndAddsExpense()
    {
        NavigateTo("المحاسبة");
        Assert.True(HasText("إجمالي الإيرادات"), "Accounting stats not shown");

        SafeInvoke(FindByName("+ إضافة مصروف"));
        Assert.True(HasText("تسجيل مصروف"), "Expense dialog did not open");

        var desc = $"مصروف اختبار {UniqueCode()}";
        EnterById("ExpenseDescriptionBox", desc);
        EnterById("ExpenseAmountBox", "25");

        SafeInvoke(FindByName("حفظ المصروف"), 1500);
        Assert.True(HasText(desc), "New expense not shown in list");
    }

    [Fact]
    public void Suppliers_AddSupplier_AppearsInList()
    {
        NavigateTo("الموردون");
        Assert.True(HasText("قائمة الموردين"), "Suppliers screen not shown");

        SafeInvoke(FindByName("+ إضافة مورد"));
        Assert.True(HasText("بيانات المورد"), "Supplier dialog did not open");

        var code = UniqueCode();
        var name = $"مورد اختبار {code}";
        EnterById("SupplierNameBox", name);
        EnterById("SupplierPhoneBox", $"091{System.Environment.TickCount % 100000000}");

        SafeInvoke(FindByName("حفظ المورد"), 1500);
        Assert.True(HasText(name), "New supplier not shown in list");
    }

    [Fact]
    public void Suppliers_WithoutSelection_HidesPurchaseOrderActions()
    {
        NavigateTo("الموردون");
        Assert.True(HasText("قائمة الموردين"), "Suppliers screen did not render");

        Assert.True(HasText("اختر مورداً لعرض التفاصيل والطلبات والدفعات"),
            "Empty supplier-details placeholder missing");
        Assert.Null(FindByName("إنشاء أمر شراء"));
        Assert.Null(FindByName("تسجيل دفعة"));
    }

    [Fact]
    public void Suppliers_PurchaseOrderDialog_OpensAndCancels()
    {
        NavigateTo("الموردون");
        Assert.True(HasText("قائمة الموردين"), "Suppliers screen did not render");

        // Ensure at least one supplier exists, then select the first one through
        // the ListBox selection API so the SelectedItem binding is updated.
        var name = $"مورد اختبار {UniqueCode()}";
        SafeInvoke(FindByName("+ إضافة مورد"));
        EnterById("SupplierNameBox", name);
        SafeInvoke(FindByName("حفظ المورد"), 1500);

        var listTitle = FindByName("قائمة الموردين");
        Assert.NotNull(listTitle);
        var listElement = listTitle!.Parent.FindFirstDescendant(
            x => x.ByControlType(FlaUI.Core.Definitions.ControlType.List));
        Assert.NotNull(listElement);
        var listBox = listElement.AsListBox();
        Assert.NotEmpty(listBox.Items);
        listBox.Select(0);
        System.Threading.Thread.Sleep(900);
        Assert.NotNull(listBox.SelectedItem);

        var createPo = FindByName("إنشاء أمر شراء");
        Assert.NotNull(createPo);
        SafeInvoke(createPo);
        Assert.True(HasText("إضافة سطر"), "Purchase order dialog did not open");

        SafeInvoke(FindByName("إلغاء"));
        Assert.False(HasText("إضافة سطر"), "Purchase order dialog did not close");
    }

    [Fact]
    public void Employees_AddEmployeeAndOpenSalaryDialog()
    {
        NavigateTo("الموظفون");
        Assert.True(HasText("عدد الموظفين النشطين"), "Employees screen not shown");

        SafeInvoke(FindByName("+ إضافة موظف"));
        Assert.True(HasText("بيانات الموظف"), "Employee dialog did not open");

        var code = UniqueCode();
        var name = $"موظف اختبار {code}";
        EnterById("EmployeeNameBox", name);
        EnterById("EmployeeSalaryBox", "2000");

        SafeInvoke(FindByName("حفظ الموظف"), 1500);
        Assert.True(HasText(name), "New employee not shown in list");

        // Open the first employee's salary dialog.
        var payBtn = MainWindow.FindAllDescendants(x => x.ByName("صرف راتب")).FirstOrDefault();
        Assert.NotNull(payBtn);
        SafeInvoke(payBtn);
        Assert.True(HasText("تأكيد الصرف"), "Salary dialog did not open");
    }
}