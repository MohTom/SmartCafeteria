using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SmartCafeteria.Converters;
using SmartCafeteria.Core.Enums;
using Xunit;

namespace SmartCafeteria.Core.Tests;

public class ConvertersTests
{
    [Fact]
    public void BoolToVisibility_True_IsVisible()
    {
        var converter = new BoolToVisibilityConverter();
        var result = converter.Convert(true, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void BoolToVisibility_False_IsCollapsed()
    {
        var converter = new BoolToVisibilityConverter();
        var result = converter.Convert(false, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void BoolToVisibility_NonBool_IsCollapsed()
    {
        var converter = new BoolToVisibilityConverter();
        var result = converter.Convert("x", typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void BoolToVisibility_ConvertBack_Visible_IsTrue()
    {
        var converter = new BoolToVisibilityConverter();
        var result = converter.ConvertBack(Visibility.Visible, typeof(bool), null!, CultureInfo.InvariantCulture);
        Assert.Equal(true, result);
    }

    [Fact]
    public void InverseBool_True_IsFalse()
    {
        var converter = new InverseBoolConverter();
        Assert.Equal(false, converter.Convert(true, typeof(bool), null!, CultureInfo.InvariantCulture));
        Assert.Equal(true, converter.Convert(false, typeof(bool), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CurrencyFormat_FormatsWithCurrency()
    {
        var converter = new CurrencyFormatConverter();
        var result = converter.Convert(new object[] { 25.5m, "ج.س" }, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal("25.50 ج.س", result);
    }

    [Fact]
    public void CurrencyFormat_Invalid_ReturnsEmpty()
    {
        var converter = new CurrencyFormatConverter();
        var result = converter.Convert(new object[] { "notdecimal" }, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void StatusToColor_Received_IsBlue()
    {
        var converter = new StatusToColorConverter();
        var result = Assert.IsType<SolidColorBrush>(converter.Convert("Received", typeof(Brush), null!, CultureInfo.InvariantCulture));
        Assert.Equal(Color.FromRgb(0x19, 0x76, 0xD2), result.Color);
    }

    [Fact]
    public void StatusToColor_RetiredStatus_IsGray()
    {
        var converter = new StatusToColorConverter();
        var result = Assert.IsType<SolidColorBrush>(converter.Convert("Completed", typeof(Brush), null!, CultureInfo.InvariantCulture));
        Assert.Equal(Colors.Gray, result.Color);
    }

    [Fact]
    public void StatusToColor_Cancelled_IsRed()
    {
        var converter = new StatusToColorConverter();
        var result = Assert.IsType<SolidColorBrush>(converter.Convert("Cancelled", typeof(Brush), null!, CultureInfo.InvariantCulture));
        Assert.Equal(Color.FromRgb(0xD3, 0x2F, 0x2F), result.Color);
    }

    [Fact]
    public void StatusToColor_Unknown_IsGray()
    {
        var converter = new StatusToColorConverter();
        var result = Assert.IsType<SolidColorBrush>(converter.Convert("All", typeof(Brush), null!, CultureInfo.InvariantCulture));
        Assert.Equal(Colors.Gray, result.Color);
    }

    [Fact]
    public void NullToVisibility_ValueNull_IsCollapsed()
    {
        var converter = new NullToVisibilityConverter();
        var result = converter.Convert(null!, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void NullToVisibility_ValueNotNull_IsVisible()
    {
        var converter = new NullToVisibilityConverter();
        var result = converter.Convert("x", typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void StockLevel_Zero_IsRed()
    {
        var converter = new StockLevelToColorConverter();
        var result = Assert.IsType<SolidColorBrush>(converter.Convert(new[] { 0, 100 }, typeof(Brush), null!, CultureInfo.InvariantCulture));
        Assert.Equal(Color.FromRgb(0xD3, 0x2F, 0x2F), result.Color);
    }

    [Fact]
    public void StockLevel_LowRatio_IsOrange()
    {
        var converter = new StockLevelToColorConverter();
        var result = Assert.IsType<SolidColorBrush>(converter.Convert(new[] { 10, 100 }, typeof(Brush), null!, CultureInfo.InvariantCulture));
        Assert.Equal(Color.FromRgb(0xF5, 0x7C, 0x00), result.Color);
    }

    [Theory]
    [InlineData(OrderType.Local, "محلي")]
    [InlineData(OrderType.Takeaway, "سفري")]
    public void EnumArabicName_OrderType_IsArabic(OrderType type, string expected)
    {
        var converter = new EnumArabicNameConverter();
        var result = converter.Convert(type, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(OrderType.Local, "محلي")]
    [InlineData(OrderType.Takeaway, "سفري")]
    public void OrderTypeToArabic_MatchesTheConverter(OrderType type, string expected)
    {
        Assert.Equal(expected, type.ToArabic());
    }

    [Theory]
    [InlineData("ملاحظة", Visibility.Visible)]
    [InlineData("", Visibility.Collapsed)]
    [InlineData("   ", Visibility.Collapsed)]
    [InlineData(null, Visibility.Collapsed)]
    public void NonEmptyStringToVisibility_OnlyTextIsVisible(object? value, Visibility expected)
    {
        var converter = new NonEmptyStringToVisibilityConverter();
        var result = converter.Convert(value!, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void StockLevel_Healthy_IsGreen()
    {
        var converter = new StockLevelToColorConverter();
        var result = Assert.IsType<SolidColorBrush>(converter.Convert(new[] { 80, 100 }, typeof(Brush), null!, CultureInfo.InvariantCulture));
        Assert.Equal(Color.FromRgb(0x2E, 0x7D, 0x32), result.Color);
    }
}