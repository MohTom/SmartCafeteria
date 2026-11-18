namespace SmartCafeteria.Licensing.Guard;

public sealed record InstallGuardResult(bool IsAllowed, string Message)
{
    public static InstallGuardResult Allowed() => new(true, string.Empty);

    public static InstallGuardResult Blocked(string message) => new(false, message);
}

public interface IInstallGuard
{
    InstallGuardResult Check();
}
