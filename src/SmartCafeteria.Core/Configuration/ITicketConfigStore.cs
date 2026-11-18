namespace SmartCafeteria.Core.Configuration;

/// <summary>
/// Reads and writes <see cref="TicketPrintConfig"/> as a JSON file next to the
/// application database. Implementations must never throw on a missing or
/// corrupt file: a broken preference file must not break startup or printing.
/// </summary>
public interface ITicketConfigStore
{
    /// <summary>Absolute path of the backing config file.</summary>
    string ConfigPath { get; }

    /// <summary>Reads the config, falling back to defaults when it is missing or unreadable.</summary>
    TicketPrintConfig Load();

    /// <summary>Persists the config, creating the containing directory when needed.</summary>
    void Save(TicketPrintConfig config);
}
