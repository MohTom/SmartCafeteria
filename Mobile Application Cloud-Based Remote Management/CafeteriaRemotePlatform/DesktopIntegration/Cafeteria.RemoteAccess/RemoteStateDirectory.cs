using System.Security.AccessControl;
using System.Security.Principal;

namespace Cafeteria.RemoteAccess;

internal static class RemoteStateDirectory
{
    public static void Ensure(string stateDirectory)
    {
        if (Directory.Exists(stateDirectory))
            return;

        Directory.CreateDirectory(stateDirectory);

        var directory = new DirectoryInfo(stateDirectory);
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);

        void Allow(WellKnownSidType sidType, FileSystemRights rights)
        {
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(sidType, null),
                rights,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        Allow(WellKnownSidType.LocalSystemSid, FileSystemRights.FullControl);
        Allow(WellKnownSidType.BuiltinAdministratorsSid, FileSystemRights.FullControl);
        Allow(WellKnownSidType.BuiltinUsersSid, FileSystemRights.Modify);

        try
        {
            directory.SetAccessControl(security);
        }
        catch (UnauthorizedAccessException) when (Directory.Exists(stateDirectory))
        {
        }
    }
}
