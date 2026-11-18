namespace Cafeteria.RemoteAccess;

public sealed record RemoteAccessPaths(
    string SettingsDirectory,
    string SettingsFile,
    string StateDirectory,
    string EventStateFile,
    string OutboxFile,
    string CommandLedgerFile)
{
    public static RemoteAccessPaths CreateDefault()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SmartCafeteria",
            "RemoteAccess");

        return new RemoteAccessPaths(
            Path.Combine(root, "settings"),
            Path.Combine(root, "settings", "settings.dat"),
            Path.Combine(root, "state"),
            Path.Combine(root, "state", "event-state.json"),
            Path.Combine(root, "state", "event-outbox.json"),
            Path.Combine(root, "state", "command-ledger.json"));
    }
}
