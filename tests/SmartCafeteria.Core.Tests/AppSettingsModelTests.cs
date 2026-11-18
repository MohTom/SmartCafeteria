using Xunit;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Core.Tests;

public class AppSettingsModelTests
{
    [Fact]
    public void AppSettings_DefaultValues_AreCorrect()
    {
        var settings = new AppSettings();
        Assert.Equal("ج.س", settings.CurrencyAr);
        Assert.Equal("SDG", settings.CurrencyEn);
        Assert.Equal(5, settings.LowStockThreshold);
        Assert.Equal("TERMINAL-01", settings.StationId);
        Assert.Equal(PrintMethod.FullReceiptAndSectionReceipts, settings.PrintMethod);
        Assert.Equal("كافتيريا التميز", settings.CafeteriaNameAr);
    }

    [Fact]
    public void AppSettings_CanSetPrintMethod()
    {
        var settings = new AppSettings { PrintMethod = PrintMethod.TotalReceiptTwice };
        Assert.Equal(PrintMethod.TotalReceiptTwice, settings.PrintMethod);
    }

    [Fact]
    public void AppSettings_LowStockThreshold_VariousValues()
    {
        Assert.InRange(new AppSettings { LowStockThreshold = 1 }.LowStockThreshold, 1, 150);
        Assert.InRange(new AppSettings { LowStockThreshold = 10 }.LowStockThreshold, 1, 150);
        Assert.InRange(new AppSettings { LowStockThreshold = 50 }.LowStockThreshold, 1, 150);
    }
}