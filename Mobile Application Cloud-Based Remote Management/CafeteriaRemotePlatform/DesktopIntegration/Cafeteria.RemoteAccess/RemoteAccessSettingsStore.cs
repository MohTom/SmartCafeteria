using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace Cafeteria.RemoteAccess;

public interface IRemoteAccessSettingsStore
{
    Task<RemoteAccessSettings?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(RemoteAccessSettings settings, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class RemoteAccessSettingsStore : IRemoteAccessSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly RemoteAccessPaths _paths;
    private readonly ISecretProtector _protector;
    private readonly bool _applyAcl;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RemoteAccessSettingsStore(
        RemoteAccessPaths paths,
        ISecretProtector protector,
        bool applyAcl = true)
    {
        _paths = paths;
        _protector = protector;
        _applyAcl = applyAcl;
    }

    public async Task<RemoteAccessSettings?> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_paths.SettingsFile))
                return null;

            var protectedBytes = await File.ReadAllBytesAsync(
                _paths.SettingsFile,
                cancellationToken);

            try
            {
                var jsonBytes = _protector.Unprotect(protectedBytes);
                return JsonSerializer.Deserialize<RemoteAccessSettings>(jsonBytes, JsonOptions);
            }
            catch (CryptographicException ex)
            {
                throw new RemoteAccessException(
                    "The saved remote-access configuration cannot be decrypted on this computer.",
                    ex);
            }
            catch (JsonException ex)
            {
                throw new RemoteAccessException(
                    "The saved remote-access configuration is invalid.",
                    ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        RemoteAccessSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureSettingsDirectory();

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
            var protectedBytes = _protector.Protect(jsonBytes);
            var tempPath = _paths.SettingsFile + ".tmp";

            try
            {
                await File.WriteAllBytesAsync(tempPath, protectedBytes, cancellationToken);
                File.Move(tempPath, _paths.SettingsFile, true);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new RemoteAccessException(
                    "Only a Windows administrator can link or change remote access for this computer.",
                    ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch (IOException)
                {
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_paths.SettingsFile))
                File.Delete(_paths.SettingsFile);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RemoteAccessException(
                "Only a Windows administrator can unlink remote access for this computer.",
                ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureSettingsDirectory()
    {
        if (!_applyAcl)
        {
            Directory.CreateDirectory(_paths.SettingsDirectory);
            return;
        }

        Directory.CreateDirectory(_paths.SettingsDirectory);

        var directory = new DirectoryInfo(_paths.SettingsDirectory);
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
        Allow(WellKnownSidType.BuiltinUsersSid, FileSystemRights.ReadAndExecute);

        directory.SetAccessControl(security);
    }
}
