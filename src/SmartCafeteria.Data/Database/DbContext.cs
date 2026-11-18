using Microsoft.Data.Sqlite;
using Dapper;

namespace SmartCafeteria.Data.Database;
public class DbContext
{
    private readonly string _connectionString;

    /// <summary>Resolved database file path (empty when constructed from a raw connection string).</summary>
    public string DatabasePath { get; } = string.Empty;

    public DbContext(string dbPath = "smart_cafeteria.db")
    {
        string fullPath;
        var overridePath = Environment.GetEnvironmentVariable("SMART_CAFETERIA_DB_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            // Isolated test/diagnostic database selected by the host process.
            fullPath = Path.IsPathRooted(overridePath)
                ? overridePath
                : Path.GetFullPath(overridePath);
            var overrideDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(overrideDir))
                Directory.CreateDirectory(overrideDir);
        }
        else if (Path.IsPathRooted(dbPath))
        {
            // Explicit (test) database location: use as-is.
            fullPath = dbPath;
        }
        else
        {
            // Per-user data folder: stays writable when the app is installed under
            // Program Files. One-time carry-over from the legacy exe-side database.
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmartCafeteria");
            Directory.CreateDirectory(dataDir);
            fullPath = Path.Combine(dataDir, dbPath);
            if (!File.Exists(fullPath))
            {
                var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath);
                if (File.Exists(legacyPath))
                {
                    try { File.Copy(legacyPath, fullPath); }
                    catch (Exception ex)
                    {
                        // Best-effort carry-over: a locked or unreadable legacy file
                        // must not stop startup, but the failure stays observable.
                        System.Diagnostics.Trace.TraceWarning(
                            "Legacy database carry-over from {0} failed: {1}", legacyPath, ex);
                    }
                }
            }
        }
        _connectionString = $"Data Source={fullPath}";
        DatabasePath = fullPath;
    }

    public DbContext(string connectionString, bool isConnectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<SqliteConnection> GetConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        await cmd.ExecuteNonQueryAsync();
        return connection;
    }

    public string ConnectionString => _connectionString;
}
