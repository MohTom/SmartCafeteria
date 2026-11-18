using System.IO;
using Microsoft.Data.Sqlite;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;

namespace SmartCafeteria.UI.Tests;

/// <summary>
/// Creates an isolated per-test copy of the application data (database and ticket
/// config), seeds known test accounts, and points the app process at the copy via
/// environment variables. The real user profile is never opened for writing.
/// </summary>
public sealed class UiTestDatabase : IDisposable
{
    internal const string AdminUsername = "ui_admin";
    internal const string AdminPassword = "UiTest!Pass123";
    internal const string CashierUsername = "ui_cashier";
    internal const string CashierPassword = "UiTest!Pass123";

    private const string DatabasePathVariable = "SMART_CAFETERIA_DB_PATH";
    private const string PrinterConfigVariable = "SMART_CAFETERIA_PRINTER_CONFIG";
    private const string LicenseDirectoryVariable = "SMART_CAFETERIA_LICENSE_DIR";
    private const string SkipInstallGuardVariable = "SMART_CAFETERIA_SKIP_INSTALL_GUARD";

    private readonly string? _previousDatabasePath;
    private readonly string? _previousPrinterConfig;
    private readonly string? _previousLicenseDirectory;
    private readonly string? _previousSkipInstallGuard;
    private bool _disposed;

    public string DirectoryPath { get; }
    public string DatabasePath { get; }
    public string PrinterConfigPath { get; }
    public string LicenseDirectoryPath { get; }

    private UiTestDatabase(string directoryPath, string databasePath, string printerConfigPath, string licenseDirectoryPath)
    {
        DirectoryPath = directoryPath;
        DatabasePath = databasePath;
        PrinterConfigPath = printerConfigPath;
        LicenseDirectoryPath = licenseDirectoryPath;
        _previousDatabasePath = Environment.GetEnvironmentVariable(DatabasePathVariable);
        _previousPrinterConfig = Environment.GetEnvironmentVariable(PrinterConfigVariable);
        _previousLicenseDirectory = Environment.GetEnvironmentVariable(LicenseDirectoryVariable);
        _previousSkipInstallGuard = Environment.GetEnvironmentVariable(SkipInstallGuardVariable);

        Environment.SetEnvironmentVariable(DatabasePathVariable, databasePath);
        Environment.SetEnvironmentVariable(PrinterConfigVariable, printerConfigPath);
        Environment.SetEnvironmentVariable(LicenseDirectoryVariable, licenseDirectoryPath);
        Environment.SetEnvironmentVariable(SkipInstallGuardVariable, "1");
    }

    public static UiTestDatabase Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"smart_cafeteria_ui_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, "smart_cafeteria.db");
        var printerConfigPath = Path.Combine(directory, "printer.json");
        var licenseDirectoryPath = Path.Combine(directory, "license");
        Directory.CreateDirectory(licenseDirectoryPath);

        var liveDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartCafeteria");

        CopyIfExists(Path.Combine(liveDirectory, "smart_cafeteria.db"), databasePath);
        CopyIfExists(Path.Combine(liveDirectory, "smart_cafeteria.db-wal"), databasePath + "-wal");
        new TicketConfigStore(printerConfigPath).Save(new TicketPrintConfig());

        var fixture = new UiTestDatabase(directory, databasePath, printerConfigPath, licenseDirectoryPath);
        fixture.Initialize();
        return fixture;
    }

    private void Initialize()
    {
        var context = new DbContext(DatabasePath);
        new DatabaseInitializer(context).InitializeAsync().GetAwaiter().GetResult();

        EnsureUser(AdminUsername, AdminPassword, "UI Test Admin", UserRole.Admin);
        EnsureUser(CashierUsername, CashierPassword, "UI Test Cashier", UserRole.Cashier);
    }

    public void EnsureUser(
        string username,
        string password,
        string fullName,
        UserRole role,
        bool isActive = true,
        bool mustChangePassword = false)
    {
        var context = new DbContext(DatabasePath);
        using var connection = context.GetConnectionAsync().GetAwaiter().GetResult();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users
                (Id, Username, PasswordHash, FullName, Role, IsActive, CreatedAt,
                 MustChangePassword, FailedLoginCount, LockoutUntil)
            VALUES
                ($id, $username, $passwordHash, $fullName, $role, $isActive, $createdAt,
                 $mustChangePassword, 0, NULL)
            ON CONFLICT(Username) DO UPDATE SET
                PasswordHash = excluded.PasswordHash,
                FullName = excluded.FullName,
                Role = excluded.Role,
                IsActive = excluded.IsActive,
                MustChangePassword = excluded.MustChangePassword,
                FailedLoginCount = 0,
                LockoutUntil = NULL
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$passwordHash", PasswordHasher.HashPassword(password));
        command.Parameters.AddWithValue("$fullName", fullName);
        command.Parameters.AddWithValue("$role", (int)role);
        command.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", DateTime.Now.ToString("O"));
        command.Parameters.AddWithValue("$mustChangePassword", mustChangePassword ? 1 : 0);
        command.ExecuteNonQuery();
    }

    private static void CopyIfExists(string source, string destination)
    {
        if (File.Exists(source))
            File.Copy(source, destination, overwrite: true);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Environment.SetEnvironmentVariable(DatabasePathVariable, _previousDatabasePath);
        Environment.SetEnvironmentVariable(PrinterConfigVariable, _previousPrinterConfig);
        Environment.SetEnvironmentVariable(LicenseDirectoryVariable, _previousLicenseDirectory);
        Environment.SetEnvironmentVariable(SkipInstallGuardVariable, _previousSkipInstallGuard);

        SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(DirectoryPath, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(200);
            }
        }
    }
}
