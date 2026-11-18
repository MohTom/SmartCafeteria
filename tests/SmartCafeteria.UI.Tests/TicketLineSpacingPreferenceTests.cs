using System.IO;
using Cafeteria.RemoteAccess;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Licensing.Service;
using SmartCafeteria.Printing;
using SmartCafeteria.ViewModels;
using Moq;
using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>
/// Covers the ticket line-spacing preference end to end: the settings view
/// model writes it to the JSON config file (never to the database) and the
/// thermal printer reads it back.
/// </summary>
public class TicketLineSpacingPreferenceTests : IDisposable
{
    private readonly string _configPath;
    private readonly TicketConfigStore _store;

    public TicketLineSpacingPreferenceTests()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"ticket_spacing_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        _configPath = Path.Combine(folder, "printer.json");
        _store = new TicketConfigStore(_configPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_configPath)!, true); } catch { }
    }

    [Fact]
    public void SettingsViewModel_StartsAtTheDefaultLineSpacing()
    {
        var vm = CreateViewModel();

        Assert.Equal(TicketPrintConfig.DefaultLineSpacing, vm.TicketLineSpacing);
    }

    [Fact]
    public void SettingsViewModel_PersistsTheLineSpacingToTheConfigFile()
    {
        var vm = CreateViewModel();

        vm.TicketLineSpacing = 120;

        Assert.True(File.Exists(_configPath), "The config file was not created.");
        Assert.Equal(120, new TicketConfigStore(_configPath).Load().TicketLineSpacing);
    }

    [Fact]
    public void SettingsViewModel_LoadsThePersistedLineSpacing()
    {
        var first = CreateViewModel();
        first.TicketLineSpacing = 115;

        var second = CreateViewModel();

        Assert.Equal(115, second.TicketLineSpacing);
    }

    [Fact]
    public void SettingsViewModel_ClampsAnOutOfRangeLineSpacing()
    {
        var vm = CreateViewModel();

        vm.TicketLineSpacing = 999;

        Assert.Equal(TicketPrintConfig.MaxLineSpacing, vm.TicketLineSpacing);
        Assert.Equal(TicketPrintConfig.MaxLineSpacing, new TicketConfigStore(_configPath).Load().TicketLineSpacing);
    }

    [Fact]
    public void SettingsViewModel_ClampsALineSpacingBelowTheSmallestPrintableShare()
    {
        var vm = CreateViewModel();

        vm.TicketLineSpacing = 5;

        Assert.Equal(TicketPrintConfig.MinLineSpacing, vm.TicketLineSpacing);
        Assert.Equal(TicketPrintConfig.MinLineSpacing, new TicketConfigStore(_configPath).Load().TicketLineSpacing);
    }

    /// <summary>
    /// The smallest share the slider offers is a valid value: the view model must
    /// keep it and store it instead of pulling it up to the default share.
    /// </summary>
    [Fact]
    public void SettingsViewModel_AcceptsTheSmallestSelectableShare()
    {
        var vm = CreateViewModel();

        vm.TicketLineSpacing = 10;

        Assert.Equal(10, vm.TicketLineSpacing);
        Assert.Equal(10, new TicketConfigStore(_configPath).Load().TicketLineSpacing);
    }

    [Fact]
    public void SettingsViewModel_NeverTouchesTheDatabase()
    {
        var vm = CreateViewModel();

        vm.TicketLineSpacing = 110;

        // The preference lives in a file, so the settings service is never asked
        // to persist a spacing and the AppSettings model has no such property.
        Assert.DoesNotContain(
            typeof(AppSettings).GetProperties(),
            p => p.Name.Contains("Spacing", StringComparison.OrdinalIgnoreCase)
              || p.Name.Contains("Ticket", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ThermalPrinter_UsesTheLineSpacingStoredInTheConfigFile()
    {
        var vm = CreateViewModel();
        vm.TicketLineSpacing = 120;

        var fonts = TicketFontSet.FromConfig(_store);

        Assert.Equal(120, fonts.LineSpacingPercent);
        Assert.Equal(1.2f, fonts.LineSpacingFactor, 3);
    }

    private SettingsViewModel CreateViewModel() =>
        new(new StubSettingsService(), new StubLogService(), new StubCategoryService(), new StubPrintService(), _store, new StubCurrentUser(), new ActivationViewModel(Mock.Of<ILicenseService>()), CreateRemoteAccess());

    private static IRemoteAccessCoordinator CreateRemoteAccess() =>
        Mock.Of<IRemoteAccessCoordinator>(m => m.Status == RemoteAccessStatus.Unlinked);

    private sealed class StubCurrentUser : ICurrentUserContext
    {
        public User? CurrentUser => new() { Username = "admin", FullName = "مدير النظام", Role = UserRole.Admin };
        public bool IsActiveAdmin => true;
    }

    private sealed class StubSettingsService : ISettingsService
    {
        public Task<AppSettings> GetSettingsAsync() => Task.FromResult(new AppSettings());

        public Task<bool> SaveSettingsAsync(AppSettings settings) => Task.FromResult(true);
    }

    private sealed class StubLogService : ILogService
    {
        public Task<IEnumerable<SystemLog>> GetAllLogsAsync() => Task.FromResult<IEnumerable<SystemLog>>(Array.Empty<SystemLog>());

        public Task<bool> AddLogAsync(string message, string level = "info") => Task.FromResult(true);

        public Task<bool> ClearLogsAsync() => Task.FromResult(true);
    }

    private sealed class StubCategoryService : ICategoryService
    {
        public Task<IEnumerable<Category>> GetAllAsync() => Task.FromResult<IEnumerable<Category>>(Array.Empty<Category>());

        public Task<Category?> GetByIdAsync(string id) => Task.FromResult<Category?>(null);

        public Task<Category> AddAsync(Category category) => Task.FromResult(category);

        public Task<bool> UpdateAsync(Category category) => Task.FromResult(true);

        public Task<bool> DeleteAsync(string id) => Task.FromResult(true);

        public Task<bool> IsInUseAsync(string id) => Task.FromResult(false);

        public Task<int> GetProductCountAsync(string id) => Task.FromResult(0);

        public Task<Dictionary<string, int>> GetProductCountsAsync() =>
            Task.FromResult(new Dictionary<string, int>());
    }

    private sealed class StubPrintService : IPrintService
    {
        public Task PrintReceiptAsync(Order order, AppSettings settings) => Task.CompletedTask;

        public Task PrintCashierSummaryAsync(Order order, AppSettings settings) => Task.CompletedTask;

        public Task PrintKitchenReceiptAsync(Order order, string sectionName, List<OrderItem> sectionItems, AppSettings settings, bool minimal = false) =>
            Task.CompletedTask;

        public Task<List<string>> GetInstalledPrintersAsync() => Task.FromResult(new List<string>());

        public Task<string> GetDefaultPrinterAsync() => Task.FromResult(string.Empty);
    }
}

