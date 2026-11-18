using SmartCafeteria.Core.Models;
using Xunit;

namespace SmartCafeteria.Core.Tests;

public class AccountingSummaryTests
{
    [Fact]
    public void AccountingSummary_TotalIncome_SumOfOrdersAndOther()
    {
        var summary = new AccountingSummary
        {
            IncomeFromOrders = 100,
            OtherIncome = 25
        };
        Assert.Equal(125, summary.TotalIncome);
    }

    [Fact]
    public void AccountingSummary_NetProfit_IncomeMinusExpenses()
    {
        var summary = new AccountingSummary
        {
            IncomeFromOrders = 200,
            TotalExpenses = 80
        };
        Assert.Equal(120, summary.NetProfit);
    }

    [Fact]
    public void AccountingSummary_ExpenseHeavy_NetProfitNegative()
    {
        var summary = new AccountingSummary
        {
            IncomeFromOrders = 50,
            TotalExpenses = 90
        };
        Assert.Equal(-40, summary.NetProfit);
    }
}