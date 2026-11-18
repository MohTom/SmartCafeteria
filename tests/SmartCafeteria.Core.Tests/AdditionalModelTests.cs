using Xunit;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using Xunit.Abstractions;

namespace SmartCafeteria.Core.Tests;

public class CategoryModelTests
{
    [Fact]
    public void Category_DefaultValues_AreCorrect()
    {
        var category = new Category();
        Assert.Equal(string.Empty, category.Id);
        Assert.Equal(string.Empty, category.NameAr);
        Assert.Equal(string.Empty, category.NameEn);
        Assert.Equal(0, category.SortOrder);
    }

    [Fact]
    public void Category_CanSetProperties()
    {
        var category = new Category
        {
            Id = "HotDrinks",
            NameAr = "مشروبات ساخنة",
            NameEn = "Hot Drinks",
            SortOrder = 0
        };
        Assert.Equal("HotDrinks", category.Id);
        Assert.Equal("مشروبات ساخنة", category.NameAr);
        Assert.Equal("Hot Drinks", category.NameEn);
        Assert.Equal(0, category.SortOrder);
    }
}

public class SystemLogTests
{
    [Fact]
    public void SystemLog_DefaultLevel_IsInfo()
    {
        var log = new SystemLog();
        Assert.Equal("info", log.Level);
        Assert.Equal(DateTime.MinValue, log.Timestamp);
    }

    [Fact]
    public void SystemLog_CanSetProperties()
    {
        var log = new SystemLog
        {
            Id = "log-1",
            Message = "تم بدء التشغيل",
            Level = "success",
            Timestamp = DateTime.Now
        };
        Assert.Equal("log-1", log.Id);
        Assert.Equal("تم بدء التشغيل", log.Message);
        Assert.Equal("success", log.Level);
    }
}

public class DashboardStatsTests
{
    [Fact]
    public void DashboardStats_DefaultValues_AreZero()
    {
        var stats = new DashboardStats();
        Assert.Equal(0, stats.TotalRevenue);
        Assert.Equal(0, stats.CompletedOrders);
        Assert.Equal(0, stats.LowStockCount);
        Assert.Equal(0, stats.TotalProducts);
    }

    [Fact]
    public void DashboardStats_CanSetValues()
    {
        var stats = new DashboardStats
        {
            TotalRevenue = 100.5m,
            CompletedOrders = 10,
            LowStockCount = 2,
            TotalProducts = 20
        };
        Assert.Equal(100.5m, stats.TotalRevenue);
        Assert.Equal(10, stats.CompletedOrders);
        Assert.Equal(2, stats.LowStockCount);
        Assert.Equal(20, stats.TotalProducts);
    }
}

public class OrderItemTests
{
    [Fact]
    public void OrderItem_DefaultValues()
    {
        var item = new OrderItem();
        Assert.Equal(0, item.Quantity);
        Assert.Equal(string.Empty, item.ProductNameAr);
        Assert.Equal("[]", item.SelectedCustomizationsJson);
    }
}