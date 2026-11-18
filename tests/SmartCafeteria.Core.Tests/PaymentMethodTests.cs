using Xunit;
using SmartCafeteria.Core.Enums;

namespace SmartCafeteria.Core.Tests;

public class PaymentMethodTests
{
    [Fact]
    public void PaymentMethod_AllValuesValid()
    {
        Assert.True(Enum.IsDefined(PaymentMethod.Cash));
        Assert.True(Enum.IsDefined(PaymentMethod.Bankak));
    }

    [Fact]
    public void PaymentMethod_HasTwoMethods()
    {
        Assert.Equal(2, Enum.GetValues<PaymentMethod>().Length);
    }

    [Fact]
    public void PaymentMethod_Default_IsCash()
    {
        PaymentMethod method = default;
        Assert.Equal(PaymentMethod.Cash, method);
    }
}