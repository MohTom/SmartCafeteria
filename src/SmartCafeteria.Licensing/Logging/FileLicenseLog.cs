namespace SmartCafeteria.Licensing.Logging;

public sealed class FileLicenseLog : ILicenseLog
{
    private readonly object _gate = new();
    private readonly string _path;

    public FileLicenseLog(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartCafeteria",
            "Logs",
            "license.log");
    }

    public void Write(string message)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.AppendAllText(_path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }
}
