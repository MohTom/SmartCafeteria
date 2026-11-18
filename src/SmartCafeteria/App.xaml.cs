using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Threading;
using Cafeteria.RemoteAccess;
using Microsoft.Extensions.DependencyInjection;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using SmartCafeteria.Licensing.Crypto;
using SmartCafeteria.Licensing.Guard;
using SmartCafeteria.Licensing.Hardware;
using SmartCafeteria.Licensing.Logging;
using SmartCafeteria.Licensing.Service;
using SmartCafeteria.Licensing.State;
using SmartCafeteria.Services;
using SmartCafeteria.ViewModels;
using SmartCafeteria.Views;

namespace SmartCafeteria;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    /// <summary>Persists a crash report where the app can always write: the
    /// per-user application data folder first, then the install folder, then the
    /// temp folder. A read-only install directory must not lose the evidence.</summary>
    private static void WriteCrashLog(Exception ex, string source)
    {
        var version = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";
        var entry =
            $"[{DateTime.Now:O}] {source} (v{version}, {Environment.OSVersion}){Environment.NewLine}" +
            $"{ex}{Environment.NewLine}{Environment.NewLine}";

        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmartCafeteria", "Logs"),
            AppDomain.CurrentDomain.BaseDirectory,
            Path.GetTempPath()
        };

        foreach (var folder in candidates)
        {
            try
            {
                Directory.CreateDirectory(folder);
                File.AppendAllText(
                    Path.Combine(folder, $"crash-{DateTime.Now:yyyyMMdd}.log"),
                    entry);
                return;
            }
            catch (Exception folderError)
            {
                // Try the next location; the temp folder is the last resort.
                System.Diagnostics.Trace.TraceWarning(
                    "Crash log write to {0} failed: {1}", folder, folderError);
            }
        }
    }

    private static void ShowCrashDialog(string message)
    {
        try
        {
            MessageBox.Show(
                message, "خطأ غير متوقع", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception dialogError)
        {
            // There is no safe UI left; the crash log is the remaining record.
            System.Diagnostics.Trace.TraceWarning(
                "Crash dialog could not be shown: {0}", dialogError);
        }
    }

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            WriteCrashLog(
                args.ExceptionObject as Exception ?? new Exception("unknown error"),
                "AppDomain.UnhandledException");

        DispatcherUnhandledException += (s, args) =>
        {
            WriteCrashLog(args.Exception, "DispatcherUnhandledException");
            args.Handled = true;
            ShowCrashDialog("حدث خطأ غير متوقع. تم حفظ التفاصيل في ملف السجل وسيتم إغلاق التطبيق.");
            Shutdown(1);
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            await StartupCore();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex, "Startup");
            ShowCrashDialog("تعذر بدء التطبيق. تم حفظ التفاصيل في ملف السجل، راجع الدعم الفني.");
            Shutdown(1);
        }
    }

    private async Task StartupCore()
    {
        // QuestPDF reads this global once; configuring it here keeps report
        // services free of configuration and avoids concurrent assignments.
        QuestPdfConfiguration.EnsureCommunityLicense();

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var installGuard = ServiceProvider.GetRequiredService<IInstallGuard>();
        var installResult = installGuard.Check();
        if (!installResult.IsAllowed)
        {
            MessageBox.Show(
                installResult.Message,
                "تعذر تشغيل البرنامج",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var licenseService = ServiceProvider.GetRequiredService<ILicenseService>();
        var activationViewModel = ServiceProvider.GetRequiredService<ActivationViewModel>();
        var licenseStatus = await licenseService.GetStatusAsync();

        if (licenseStatus.IsLocked)
        {
            var lockWindow = new ActivationLockWindow(activationViewModel);
            if (lockWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            licenseStatus = await licenseService.GetStatusAsync();
            if (licenseStatus.IsLocked)
            {
                MessageBox.Show(
                    licenseStatus.Message,
                    "الترخيص غير صالح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }
        }

        // Initialize database
        var dbContext = ServiceProvider.GetRequiredService<DbContext>();
        // A database file that already exists belongs to the user: never
        // re-seed products into it, even if it is empty (the user may have
        // deleted the whole menu on purpose). Seed only a fresh database.
        bool freshDatabase = !File.Exists(dbContext.DatabasePath);
        var initializer = new DatabaseInitializer(dbContext);
        await initializer.InitializeAsync();

        // Seed the default Sudanese menu once on a fresh database.
        var productService = ServiceProvider.GetRequiredService<IProductService>();
        var products = await productService.GetAllProductsAsync();
        if (freshDatabase && !products.Any())
        {
            var seedProducts = SmartCafeteria.Data.Seed.SeedData.GetSeedProducts();
            foreach (var product in seedProducts)
            {
                await productService.AddProductAsync(product);
            }

            var logService = ServiceProvider.GetRequiredService<ILogService>();
            await logService.AddLogAsync("تم تهيئة النظام بالقائمة السودانية الافتراضية", "success");
        }

        // Seed the first administrator with a random password. The credentials
        // are shown exactly once and the account must pick its own password on
        // first login, so no guessable default ever exists.
        var userService = ServiceProvider.GetRequiredService<IUserService>();
        var users = await userService.GetAllUsersAsync();
        string? firstRunPassword = null;
        if (!users.Any())
        {
            firstRunPassword = GenerateInitialPassword();
            await userService.CreateUserAsync(new Core.Models.User
            {
                Username = "admin",
                FullName = "مدير النظام",
                Role = Core.Enums.UserRole.Admin,
                MustChangePassword = true
            }, firstRunPassword);
            await ServiceProvider.GetRequiredService<ILogService>()
                .AddLogAsync("تم إنشاء حساب المدير الأول بكلمة مرور مؤقتة", "success");
        }

        if (firstRunPassword != null)
        {
            new FirstRunCredentialsWindow("admin", firstRunPassword).ShowDialog();
        }

        try
        {
            var remoteAccess = ServiceProvider.GetRequiredService<IRemoteAccessCoordinator>();
            await remoteAccess.StartAsync();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex, "RemoteAccessStart");
        }

        // Show login window first
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        ShowLoginWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            var remoteAccess = ServiceProvider?.GetService<IRemoteAccessCoordinator>();
            if (remoteAccess is not null)
                Task.Run(() => remoteAccess.StopAsync()).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex, "RemoteAccessStop");
        }

        base.OnExit(e);
    }

    private static string GenerateInitialPassword()
    {
        // Ambiguous characters are omitted so the one-time password can be read
        // from the screen and typed without mistakes.
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        return RandomNumberGenerator.GetString(alphabet, 16);
    }

    private void ShowLoginWindow()
    {
        var loginWindow = ServiceProvider!.GetRequiredService<LoginWindow>();

        var authenticated = false;
        loginWindow.LoginSucceeded += (_, _) =>
        {
            authenticated = true;
            var mainWindow = ServiceProvider!.GetRequiredService<MainWindow>();
            if (mainWindow.DataContext is MainViewModel mainVm)
                mainVm.LogoutRequested += OnLogoutRequested;
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
            loginWindow.Close();
        };
        loginWindow.Closed += (_, _) =>
        {
            if (!authenticated)
                Shutdown();
        };

        loginWindow.Show();
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        if (sender is MainViewModel oldVm)
            oldVm.LogoutRequested -= OnLogoutRequested;
        var oldMain = MainWindow;
        // Closing the current main window must not terminate the app while
        // the re-login window is shown.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        oldMain?.Close();
        ShowLoginWindow();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Database
        services.AddSingleton<DbContext>();
        services.AddSingleton<DatabaseInitializer>();

        // Repositories (implement service interfaces)
        services.AddSingleton<IProductService, ProductRepository>();
        services.AddSingleton<IOrderService>(sp =>
            new OrderRepository(
                sp.GetRequiredService<DbContext>(),
                sp.GetRequiredService<IProductService>(),
                sp.GetRequiredService<ITicketConfigStore>()));
        services.AddSingleton<ISettingsService, SettingsRepository>();
        services.AddSingleton<ILogService, LogRepository>();
        services.AddSingleton<IUserService, UserRepository>();
        services.AddSingleton<IAccountService>(sp =>
            new AccountRepository(
                sp.GetRequiredService<DbContext>(),
                sp.GetRequiredService<IOrderService>(),
                sp.GetRequiredService<ICurrentUserContext>()));
        services.AddSingleton<ISupplierService, SupplierRepository>();
        services.AddSingleton<IEmployeeService, EmployeeRepository>();
        services.AddSingleton<ICategoryService, CategoryRepository>();

        // Services
        services.AddSingleton<OrderManagementService>();
        services.AddSingleton<MenuManagementService>();
        services.AddSingleton<ReportService>();
        services.AddSingleton<IPdfService, PdfReportService>();
        services.AddSingleton<IAnalyticsService, AnalyticsService>();
        services.AddSingleton<IAnalyticsPdfService, AnalyticsPdfService>();
        services.AddTransient<IPrintService>(sp =>
            new Printing.ThermalPrinter(sp.GetRequiredService<ITicketConfigStore>()));
        services.AddSingleton<SmartCafeteria.Services.UserSession>();
        services.AddCafeteriaRemoteAccess(
            sp => sp.GetRequiredService<SmartCafeteria.Services.UserSession>());
        services.AddSingleton(TimeProvider.System);

        // Licensing
        services.AddSingleton<ILicenseLog>(_ => new FileLicenseLog());
        services.AddSingleton<IHardwareIdProvider, WindowsHardwareIdProvider>();
        services.AddSingleton<ILicenseStateStore>(sp =>
            LicenseStateStoreFactory.CreateDefault(
                sp.GetRequiredService<IHardwareIdProvider>(),
                sp.GetRequiredService<ILicenseLog>()));
        services.AddSingleton<EcdsaLicenseVerifier>();
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<IInstallGuard>(sp =>
            InstallBindingGuard.CreateDefault(
                sp.GetRequiredService<IHardwareIdProvider>(),
                sp.GetRequiredService<ILicenseLog>()));
        services.AddSingleton<ActivationViewModel>();

        // Ticket preferences live in a JSON config file next to the database,
        // so no database column is added for them.
        services.AddSingleton<ITicketConfigStore, TicketConfigStore>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<MenuViewModel>();
        services.AddTransient<CartViewModel>();
        services.AddTransient<OrdersViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<ReportsAnalyticsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<AccountingViewModel>();
        services.AddTransient<SuppliersViewModel>();
        services.AddTransient<EmployeesViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<LoginWindow>();
    }
}
