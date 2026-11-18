using System.Text.Json;

namespace SmartCafeteria.Core.Configuration;

/// <summary>
/// Stores ticket preferences in a JSON file in the per-user data folder
/// (%LOCALAPPDATA%\SmartCafeteria), the same folder that holds the database.
/// The database schema is therefore never touched by this feature.
/// </summary>
public sealed class TicketConfigStore : ITicketConfigStore
{
    private const string FileName = "printer.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public TicketConfigStore() : this(DefaultConfigPath())
    {
    }

    /// <summary>Uses an explicit file location (tests point this at a temporary folder).</summary>
    public TicketConfigStore(string path)
    {
        _path = string.IsNullOrWhiteSpace(path) ? DefaultConfigPath() : path;
    }

    public string ConfigPath => _path;

    /// <summary>Default location: the writable per-user folder, next to smart_cafeteria.db.
    /// A host process can redirect it with SMART_CAFETERIA_PRINTER_CONFIG (test isolation).</summary>
    public static string DefaultConfigPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("SMART_CAFETERIA_PRINTER_CONFIG");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.IsPathRooted(overridePath)
                ? overridePath
                : Path.GetFullPath(overridePath);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartCafeteria",
            FileName);
    }

    public TicketPrintConfig Load()
    {
        try
        {
            if (!File.Exists(_path)) return new TicketPrintConfig();

            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json)) return new TicketPrintConfig();

            var config = JsonSerializer.Deserialize<TicketPrintConfig>(json, JsonOptions);
            if (config == null) return new TicketPrintConfig();

            config.TicketFontSize = TicketPrintConfig.ClampFontSize(config.TicketFontSize);
            config.TicketLineSpacing = TicketPrintConfig.ClampLineSpacing(config.TicketLineSpacing);
            return config;
        }
        catch (Exception ex)
        {
            // A missing, unreadable or corrupt config must never break startup
            // or printing: fall back to the defaults instead of throwing.
            System.Diagnostics.Trace.TraceWarning(
                "Ticket config {0} could not be read: {1}", _path, ex);
            return new TicketPrintConfig();
        }
    }

    private readonly object _saveLock = new();

    public void Save(TicketPrintConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.TicketFontSize = TicketPrintConfig.ClampFontSize(config.TicketFontSize);
        config.TicketLineSpacing = TicketPrintConfig.ClampLineSpacing(config.TicketLineSpacing);

        lock (_saveLock)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(config, JsonOptions);

            // A unique temp name keeps concurrent or rapid saves from writing
            // into the same file; the swap still makes the write atomic.
            var tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _path, overwrite: true);
            }
            catch
            {
                try { File.Delete(tempPath); }
                catch (Exception cleanupError)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "Temporary ticket config {0} could not be removed: {1}", tempPath, cleanupError);
                }
                throw;
            }
        }
    }
}
