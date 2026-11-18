using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Printing;
using Xunit;

namespace SmartCafeteria.Services.Tests;

/// <summary>
/// Locks the ticket layout metrics to the values that were hard-coded before the
/// font size became configurable. These are the regression guard: if the default
/// layout ever changes, the printed ticket changes with it.
/// </summary>
public class TicketFontSetTests
{
    [Fact]
    public void ReferenceBaseSize_MatchesOriginalHardcodedMetrics()
    {
        var fonts = new TicketFontSet(TicketPrintConfig.DefaultFontSize);

        Assert.Equal(1.0f, fonts.Scale);

        // Font sizes (points)
        Assert.Equal(12f, fonts.Title);
        Assert.Equal(11f, fonts.Section);
        Assert.Equal(9f, fonts.Normal);
        Assert.Equal(8f, fonts.Small);
        Assert.Equal(9f, fonts.Bold);
        Assert.Equal(11f, fonts.Total);

        // Text box heights
        Assert.Equal(20f, fonts.TitleHeight);
        Assert.Equal(18f, fonts.SectionHeight);
        Assert.Equal(14f, fonts.LineHeight);
        Assert.Equal(16f, fonts.TotalHeight);

        // Vertical advances
        Assert.Equal(22f, fonts.TitleAdvance);
        Assert.Equal(20f, fonts.SectionAdvance);
        Assert.Equal(14f, fonts.LineAdvance);
        Assert.Equal(16f, fonts.DetailAdvance);
        Assert.Equal(18f, fonts.TotalAdvance);
        Assert.Equal(4f, fonts.BlockGap);
        Assert.Equal(6f, fonts.RuleGap);

        // Footer reserves
        Assert.Equal(40f, fonts.MinimalFooterReserve);
        Assert.Equal(80f, fonts.FullFooterReserve);
    }

    [Fact]
    public void Default_Prop_ProducesTheReferenceLayout()
    {
        var fonts = TicketFontSet.Default;

        Assert.Equal(TicketPrintConfig.DefaultFontSize, fonts.BaseFontSize);
        Assert.Equal(1.0f, fonts.Scale);
        Assert.Equal(12f, fonts.Title);
        Assert.Equal(14f, fonts.LineHeight);
    }

    [Fact]
    public void DoubleBaseSize_ScalesEveryMetricByTwo()
    {
        var fonts = new TicketFontSet(18);

        Assert.Equal(2.0f, fonts.Scale);
        Assert.Equal(24f, fonts.Title);
        Assert.Equal(18f, fonts.Normal);
        Assert.Equal(16f, fonts.Small);
        Assert.Equal(22f, fonts.Total);
        Assert.Equal(40f, fonts.TitleHeight);
        Assert.Equal(28f, fonts.LineHeight);
        Assert.Equal(32f, fonts.TotalHeight);
        Assert.Equal(44f, fonts.TitleAdvance);
        Assert.Equal(80f, fonts.MinimalFooterReserve);
        Assert.Equal(160f, fonts.FullFooterReserve);
    }

    [Theory]
    [InlineData(7, 0.7777778f)]
    [InlineData(12, 1.3333334f)]
    [InlineData(16, 1.7777778f)]
    public void Scale_IsBaseSizeOverReference(int baseSize, float expectedScale)
    {
        var fonts = new TicketFontSet(baseSize);

        Assert.Equal(expectedScale, fonts.Scale, 5);
    }

    [Fact]
    public void LargerBaseSize_IncreasesEveryMetric()
    {
        var small = new TicketFontSet(7);
        var large = new TicketFontSet(16);

        Assert.True(large.Title > small.Title);
        Assert.True(large.Section > small.Section);
        Assert.True(large.Normal > small.Normal);
        Assert.True(large.Small > small.Small);
        Assert.True(large.Bold > small.Bold);
        Assert.True(large.Total > small.Total);

        // Row heights must grow with the fonts, otherwise the text is clipped.
        Assert.True(large.LineHeight > small.LineHeight);
        Assert.True(large.TitleHeight > small.TitleHeight);
        Assert.True(large.SectionHeight > small.SectionHeight);
        Assert.True(large.TotalHeight > small.TotalHeight);

        // Advances and reserves must grow too.
        Assert.True(large.LineAdvance > small.LineAdvance);
        Assert.True(large.TotalAdvance > small.TotalAdvance);
        Assert.True(large.FullFooterReserve > small.FullFooterReserve);
        Assert.True(large.MinimalFooterReserve > small.MinimalFooterReserve);
    }

    [Fact]
    public void FromConfig_UsesTheStoredFontSize()
    {
        var store = new FakeTicketConfigStore { FontSize = 12 };

        var fonts = TicketFontSet.FromConfig(store);

        Assert.Equal(12, fonts.BaseFontSize);
        Assert.Equal(12f / TicketFontSet.ReferenceBaseSize, fonts.Scale, 5);
    }

    [Fact]
    public void FromConfig_NullStore_FallsBackToDefault()
    {
        var fonts = TicketFontSet.FromConfig(null!);

        Assert.Equal(TicketPrintConfig.DefaultFontSize, fonts.BaseFontSize);
        Assert.Equal(1.0f, fonts.Scale);
    }

    [Fact]
    public void FromConfig_ThrowingStore_FallsBackToDefault()
    {
        var fonts = TicketFontSet.FromConfig(new ThrowingTicketConfigStore());

        Assert.Equal(TicketPrintConfig.DefaultFontSize, fonts.BaseFontSize);
        Assert.Equal(1.0f, fonts.Scale);
    }

    [Fact]
    public void TheDefaultLineSpacing_ReproducesTheCurrentLayout()
    {
        var fonts = new TicketFontSet(TicketPrintConfig.DefaultFontSize);

        Assert.Equal(TicketPrintConfig.DefaultLineSpacing, fonts.LineSpacingPercent);
        Assert.Equal(0.9f, fonts.LineSpacingFactor, 5);
    }

    [Fact]
    public void TheLineSpacingFactor_IsThePercentOverHundred()
    {
        var fonts = new TicketFontSet(TicketPrintConfig.DefaultFontSize, 105);

        Assert.Equal(105, fonts.LineSpacingPercent);
        Assert.Equal(1.05f, fonts.LineSpacingFactor, 5);
    }

    [Fact]
    public void AnOutOfRangeLineSpacing_IsClamped()
    {
        var tooSmall = new TicketFontSet(TicketPrintConfig.DefaultFontSize, 1);
        var tooBig = new TicketFontSet(TicketPrintConfig.DefaultFontSize, 999);

        Assert.Equal(TicketPrintConfig.MinLineSpacing, tooSmall.LineSpacingPercent);
        Assert.Equal(TicketPrintConfig.MaxLineSpacing, tooBig.LineSpacingPercent);
    }

    /// <summary>
    /// The smallest share the user can choose must be accepted as it is and must
    /// be below the default share, otherwise the slider would offer a value that
    /// the layout silently replaces.
    /// </summary>
    [Fact]
    public void TheSmallestSelectableShare_IsAcceptedAndIsBelowTheDefault()
    {
        var fonts = new TicketFontSet(TicketPrintConfig.DefaultFontSize, TicketPrintConfig.MinLineSpacing);

        Assert.Equal(TicketPrintConfig.MinLineSpacing, fonts.LineSpacingPercent);
        Assert.Equal(TicketPrintConfig.MinLineSpacing / 100f, fonts.LineSpacingFactor, 5);
        Assert.True(TicketPrintConfig.MinLineSpacing < TicketPrintConfig.DefaultLineSpacing,
            $"The smallest selectable share ({TicketPrintConfig.MinLineSpacing} %) is not below the default share ({TicketPrintConfig.DefaultLineSpacing} %).");
    }

    [Fact]
    public void FromConfig_UsesTheStoredLineSpacing()
    {
        var store = new FakeTicketConfigStore { FontSize = 12, LineSpacing = 105 };

        var fonts = TicketFontSet.FromConfig(store);

        Assert.Equal(105, fonts.LineSpacingPercent);
        Assert.Equal(1.05f, fonts.LineSpacingFactor, 5);
    }

    [Fact]
    public void FromConfig_NullStore_FallsBackToTheDefaultLineSpacing()
    {
        var fonts = TicketFontSet.FromConfig(null!);

        Assert.Equal(TicketPrintConfig.DefaultLineSpacing, fonts.LineSpacingPercent);
    }

    [Fact]
    public void FromConfig_ThrowingStore_FallsBackToTheDefaultLineSpacing()
    {
        var fonts = TicketFontSet.FromConfig(new ThrowingTicketConfigStore());

        Assert.Equal(TicketPrintConfig.DefaultLineSpacing, fonts.LineSpacingPercent);
    }

    private sealed class FakeTicketConfigStore : ITicketConfigStore
    {
        public int FontSize { get; set; } = TicketPrintConfig.DefaultFontSize;
        public int LineSpacing { get; set; } = TicketPrintConfig.DefaultLineSpacing;
        public string ConfigPath => "fake";

        public TicketPrintConfig Load() => new() { TicketFontSize = FontSize, TicketLineSpacing = LineSpacing };
        public void Save(TicketPrintConfig config)
        {
            FontSize = config.TicketFontSize;
            LineSpacing = config.TicketLineSpacing;
        }
    }

    private sealed class ThrowingTicketConfigStore : ITicketConfigStore
    {
        public string ConfigPath => "throwing";

        public TicketPrintConfig Load() => throw new IOException("boom");
        public void Save(TicketPrintConfig config) => throw new IOException("boom");
    }
}
