namespace SmartCafeteria.Licensing.Logging;

public sealed class NullLicenseLog : ILicenseLog
{
    public static NullLicenseLog Instance { get; } = new();

    public void Write(string message)
    {
    }
}
