using SmartCafeteria.Core.Configuration;
using Xunit;

namespace SmartCafeteria.Services.Tests;

/// <summary>
/// Covers the ticket preference file. Every test uses its own temporary folder,
/// so the real user config is never read or written.
/// </summary>
public class TicketConfigStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;

    public TicketConfigStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "sc-ticket-config-" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "printer.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
        catch { }
    }

    private TicketConfigStore CreateStore() => new(_path);

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var store = CreateStore();

        var config = store.Load();

        Assert.Equal(TicketPrintConfig.DefaultFontSize, config.TicketFontSize);
    }

    [Fact]
    public void Load_MissingFile_DoesNotThrow()
    {
        var store = CreateStore();

        var ex = Record.Exception(() => store.Load());

        Assert.Null(ex);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheFontSize()
    {
        var store = CreateStore();

        store.Save(new TicketPrintConfig { TicketFontSize = 13 });

        Assert.True(File.Exists(_path));
        Assert.Equal(13, store.Load().TicketFontSize);
    }

    [Fact]
    public void Save_CreatesTheMissingDirectory()
    {
        var store = CreateStore();
        Assert.False(Directory.Exists(_directory));

        store.Save(new TicketPrintConfig { TicketFontSize = 11 });

        Assert.True(Directory.Exists(_directory));
        Assert.Equal(11, store.Load().TicketFontSize);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaultsWithoutThrowing()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "{ this is not json ");

        var store = CreateStore();

        var config = store.Load();

        Assert.Equal(TicketPrintConfig.DefaultFontSize, config.TicketFontSize);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, string.Empty);

        var store = CreateStore();

        Assert.Equal(TicketPrintConfig.DefaultFontSize, store.Load().TicketFontSize);
    }

    [Theory]
    [InlineData(1, TicketPrintConfig.MinFontSize)]
    [InlineData(0, TicketPrintConfig.MinFontSize)]
    [InlineData(-50, TicketPrintConfig.MinFontSize)]
    [InlineData(999, TicketPrintConfig.MaxFontSize)]
    public void Load_OutOfRangeValue_IsClamped(int stored, int expected)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, $"{{ \"TicketFontSize\": {stored} }}");

        var store = CreateStore();

        Assert.Equal(expected, store.Load().TicketFontSize);
    }

    [Theory]
    [InlineData(1, TicketPrintConfig.MinFontSize)]
    [InlineData(999, TicketPrintConfig.MaxFontSize)]
    public void Save_OutOfRangeValue_IsClamped(int requested, int expected)
    {
        var store = CreateStore();

        store.Save(new TicketPrintConfig { TicketFontSize = requested });

        Assert.Equal(expected, store.Load().TicketFontSize);
    }

    [Fact]
    public void Save_OverwritesAnExistingFile()
    {
        var store = CreateStore();
        store.Save(new TicketPrintConfig { TicketFontSize = 10 });

        store.Save(new TicketPrintConfig { TicketFontSize = 15 });

        Assert.Equal(15, store.Load().TicketFontSize);
    }

    [Fact]
    public void Save_LeavesNoTemporaryFileBehind()
    {
        var store = CreateStore();

        store.Save(new TicketPrintConfig { TicketFontSize = 12 });

        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public void ConfigPath_IsTheRequestedPath()
    {
        Assert.Equal(_path, CreateStore().ConfigPath);
    }

    [Fact]
    public void DefaultConfigPath_LivesNextToTheDatabaseFolder()
    {
        var path = TicketConfigStore.DefaultConfigPath();

        Assert.EndsWith("printer.json", path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SmartCafeteria", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClampFontSize_KeepsValuesInsideThePrintableRange()
    {
        Assert.Equal(TicketPrintConfig.MinFontSize, TicketPrintConfig.ClampFontSize(0));
        Assert.Equal(TicketPrintConfig.MinFontSize, TicketPrintConfig.ClampFontSize(-1));
        Assert.Equal(TicketPrintConfig.MaxFontSize, TicketPrintConfig.ClampFontSize(100));
        Assert.Equal(12, TicketPrintConfig.ClampFontSize(12));
    }

    [Fact]
    public void Load_MissingFile_ReturnsTheDefaultLineSpacing()
    {
        var store = CreateStore();

        var config = store.Load();

        Assert.Equal(TicketPrintConfig.DefaultLineSpacing, config.TicketLineSpacing);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheLineSpacing()
    {
        var store = CreateStore();

        store.Save(new TicketPrintConfig { TicketLineSpacing = 105 });

        Assert.True(File.Exists(_path));
        Assert.Equal(105, store.Load().TicketLineSpacing);
    }

    /// <summary>
    /// A config file written by an older build has no line spacing. The default
    /// must then be used instead of a broken layout.
    /// </summary>
    [Fact]
    public void Load_ConfigFileWithoutTheLineSpacing_KeepsTheDefault()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "{ \"TicketFontSize\": 12 }");

        var store = CreateStore();

        var config = store.Load();

        Assert.Equal(12, config.TicketFontSize);
        Assert.Equal(TicketPrintConfig.DefaultLineSpacing, config.TicketLineSpacing);
    }

    [Theory]
    [InlineData(1, TicketPrintConfig.MinLineSpacing)]
    [InlineData(0, TicketPrintConfig.MinLineSpacing)]
    [InlineData(-50, TicketPrintConfig.MinLineSpacing)]
    [InlineData(999, TicketPrintConfig.MaxLineSpacing)]
    public void Load_OutOfRangeLineSpacing_IsClamped(int stored, int expected)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, $"{{ \"TicketLineSpacing\": {stored} }}");

        var store = CreateStore();

        Assert.Equal(expected, store.Load().TicketLineSpacing);
    }

    [Theory]
    [InlineData(1, TicketPrintConfig.MinLineSpacing)]
    [InlineData(999, TicketPrintConfig.MaxLineSpacing)]
    public void Save_OutOfRangeLineSpacing_IsClamped(int requested, int expected)
    {
        var store = CreateStore();

        store.Save(new TicketPrintConfig { TicketLineSpacing = requested });

        Assert.Equal(expected, store.Load().TicketLineSpacing);
    }

    [Fact]
    public void Save_KeepsTheFontSizeAndTheLineSpacingTogether()
    {
        var store = CreateStore();

        store.Save(new TicketPrintConfig { TicketFontSize = 12, TicketLineSpacing = 100 });

        var config = store.Load();

        Assert.Equal(12, config.TicketFontSize);
        Assert.Equal(100, config.TicketLineSpacing);
    }

    [Fact]
    public void ClampLineSpacing_KeepsValuesInsideThePrintableRange()
    {
        Assert.Equal(TicketPrintConfig.MinLineSpacing, TicketPrintConfig.ClampLineSpacing(0));
        Assert.Equal(TicketPrintConfig.MinLineSpacing, TicketPrintConfig.ClampLineSpacing(-1));
        Assert.Equal(TicketPrintConfig.MaxLineSpacing, TicketPrintConfig.ClampLineSpacing(1000));
        Assert.Equal(100, TicketPrintConfig.ClampLineSpacing(100));

        // The smallest share the user can choose is inside the range and must
        // not be pulled up to the default share.
        Assert.Equal(10, TicketPrintConfig.ClampLineSpacing(10));
        Assert.Equal(10, TicketPrintConfig.ClampLineSpacing(5));
    }

    /// <summary>
    /// The smallest share the user can choose is a valid value: it must survive
    /// the round trip through the config file instead of being pulled up to the
    /// default share.
    /// </summary>
    [Fact]
    public void SaveThenLoad_RoundTripsTheSmallestSelectableLineSpacing()
    {
        var store = CreateStore();

        store.Save(new TicketPrintConfig { TicketLineSpacing = 10 });

        Assert.Equal(10, store.Load().TicketLineSpacing);
    }

    /// <summary>
    /// The file must hold the share that was saved, not one that only becomes
    /// valid when it is read back.
    /// </summary>
    [Fact]
    public void Save_WritesTheShareThatWasGiven()
    {
        var store = CreateStore();

        store.Save(new TicketPrintConfig { TicketLineSpacing = 10 });

        var json = File.ReadAllText(_path);

        Assert.Contains("\"TicketLineSpacing\": 10", json, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefaultLineSpacing_ReproducesTheCurrentLayout()
    {
        // 90 % of the line box is the distance between two rows the receipt has
        // been printed with since the spacing was tightened.
        Assert.Equal(90, TicketPrintConfig.DefaultLineSpacing);
    }

    [Fact]
    public void Load_MissingFile_EnablesInventoryTrackingByDefault()
    {
        Assert.True(CreateStore().Load().EnforceInventoryTracking);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsDisabledInventoryTracking()
    {
        var store = CreateStore();

        store.Save(new TicketPrintConfig { EnforceInventoryTracking = false });

        Assert.False(store.Load().EnforceInventoryTracking);
    }

    [Fact]
    public void Load_ConfigFileWithoutInventorySetting_KeepsTrackingEnabled()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "{ \"TicketFontSize\": 12 }");

        var config = CreateStore().Load();

        Assert.Equal(12, config.TicketFontSize);
        Assert.True(config.EnforceInventoryTracking);
    }
}
