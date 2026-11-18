using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using SmartCafeteria.Licensing.Hardware;
using SmartCafeteria.Licensing.Logging;
using SmartCafeteria.Licensing.Models;

namespace SmartCafeteria.Licensing.State;

public sealed class EncryptedLicenseStateStore : ILicenseStateStore
{
    private const string StateRegistryPath = @"Software\SmartCafeteria\Licensing";
    private const string StateRegistryValue = "State";
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("SCLS1");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    private readonly ILicenseLog _log;
    private readonly IReadOnlyList<string> _filePaths;
    private readonly bool _includeRegistry;
    private readonly string _machineGuid;

    public EncryptedLicenseStateStore(
        IHardwareIdProvider hardware,
        ILicenseLog log,
        IEnumerable<string> filePaths,
        bool includeRegistry)
    {
        _log = log;
        _filePaths = filePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _includeRegistry = includeRegistry;

        var identity = hardware.GetIdentity();
        _machineGuid = string.IsNullOrWhiteSpace(identity.MachineGuid)
            ? Environment.MachineName
            : identity.MachineGuid;
    }

    public LicenseStateLoadResult Load()
    {
        var states = new List<LicenseState>();
        var anyData = false;
        var tampered = false;
        string? lastError = null;

        foreach (var path in _filePaths)
        {
            var read = ReadFile(path);
            if (!read.Exists)
                continue;

            anyData = true;
            if (!read.Success || string.IsNullOrWhiteSpace(read.Value))
            {
                lastError = read.Error;
                continue;
            }

            if (TryDecrypt(read.Value, out var state) && state is not null)
                states.Add(state);
            else
            {
                tampered = true;
                lastError = $"License state failed authentication: {path}";
                _log.Write(lastError);
            }
        }

        if (_includeRegistry)
        {
            var read = ReadRegistry();
            if (read.Exists)
            {
                anyData = true;
                if (!read.Success || string.IsNullOrWhiteSpace(read.Value))
                {
                    lastError = read.Error;
                }
                else if (TryDecrypt(read.Value, out var state) && state is not null)
                {
                    states.Add(state);
                }
                else
                {
                    tampered = true;
                    lastError = "License state failed authentication: registry";
                    _log.Write(lastError);
                }
            }
        }

        if (states.Count == 0)
            return new LicenseStateLoadResult(null, tampered, anyData ? lastError : null);

        return new LicenseStateLoadResult(Merge(states), tampered, lastError);
    }

    public bool Save(LicenseState state)
    {
        if (!TryEncrypt(state, out var encoded))
        {
            _log.Write("Unable to encrypt license state.");
            return false;
        }

        var saved = 0;

        foreach (var path in _filePaths)
        {
            if (WriteFile(path, encoded))
            {
                saved++;
            }
            else
            {
                _log.Write($"Unable to write license state: {path}");
            }
        }

        if (_includeRegistry && WriteRegistry(encoded))
            saved++;

        _log.Write($"License state saved to {saved} anchor(s).");
        return saved > 0;
    }

    private bool TryEncrypt(LicenseState state, out string encoded)
    {
        encoded = string.Empty;

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(PersistedState.FromState(state), JsonOptions);
            var key = DeriveKey();
            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var ciphertext = new byte[payload.Length];

            using (var aes = new AesGcm(key, tag.Length))
                aes.Encrypt(nonce, payload, ciphertext, tag);

            var blob = new byte[Magic.Length + nonce.Length + tag.Length + ciphertext.Length];
            Magic.CopyTo(blob, 0);
            nonce.CopyTo(blob, Magic.Length);
            tag.CopyTo(blob, Magic.Length + nonce.Length);
            ciphertext.CopyTo(blob, Magic.Length + nonce.Length + tag.Length);

            encoded = Convert.ToBase64String(blob);
            return true;
        }
        catch (Exception exception)
        {
            _log.Write($"License state encryption failed: {exception.Message}");
            return false;
        }
    }

    private bool TryDecrypt(string encoded, out LicenseState? state)
    {
        state = null;

        try
        {
            var blob = Convert.FromBase64String(encoded);
            if (blob.Length < Magic.Length + 12 + 16)
                return false;

            if (!blob.AsSpan(0, Magic.Length).SequenceEqual(Magic))
                return false;

            var nonce = blob.AsSpan(Magic.Length, 12);
            var tag = blob.AsSpan(Magic.Length + 12, 16);
            var ciphertext = blob.AsSpan(Magic.Length + 12 + 16);
            var payload = new byte[ciphertext.Length];

            using (var aes = new AesGcm(DeriveKey(), tag.Length))
                aes.Decrypt(nonce, ciphertext, tag, payload);

            var persisted = JsonSerializer.Deserialize<PersistedState>(payload, JsonOptions);
            if (persisted is null || persisted.Version != PersistedState.CurrentVersion)
                return false;

            state = persisted.ToState();
            return state.DeviceId.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private byte[] DeriveKey() =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"SmartCafeteria.LicenseState.v1|{_machineGuid}"));

    private static LicenseState Merge(IReadOnlyList<LicenseState> states)
    {
        var latest = states.OrderByDescending(state => state.LastSeenUtc).First();
        var firstRun = states.Min(state => state.FirstRunUtc);
        var lastSeen = states.Max(state => state.LastSeenUtc);
        var kind = (LicenseKind)states.Max(state => (int)state.Kind);

        DateTimeOffset? expires = kind switch
        {
            LicenseKind.Permanent => null,
            LicenseKind.Monthly => states
                .Where(state => state.Kind == LicenseKind.Monthly && state.ExpiresUtc.HasValue)
                .Max(state => state.ExpiresUtc),
            _ => null
        };

        return new LicenseState(firstRun, lastSeen, kind, expires, latest.DeviceId);
    }

    private static AnchorReadResult ReadFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return AnchorReadResult.Missing();

            return new AnchorReadResult(true, true, File.ReadAllText(path), null);
        }
        catch (Exception exception)
        {
            return new AnchorReadResult(true, false, null, exception.Message);
        }
    }

    private static bool WriteFile(string path, string value)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var temporary = path + ".tmp";
            File.WriteAllText(temporary, value);
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static AnchorReadResult ReadRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StateRegistryPath);
            var value = key?.GetValue(StateRegistryValue) as string;

            return value is null
                ? AnchorReadResult.Missing()
                : new AnchorReadResult(true, true, value, null);
        }
        catch (Exception exception)
        {
            return new AnchorReadResult(true, false, null, exception.Message);
        }
    }

    private bool WriteRegistry(string value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(StateRegistryPath);
            key?.SetValue(StateRegistryValue, value, RegistryValueKind.String);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    private readonly record struct AnchorReadResult(bool Exists, bool Success, string? Value, string? Error)
    {
        public static AnchorReadResult Missing() => new(false, false, null, null);
    }

    private sealed class PersistedState
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public DateTimeOffset FirstRunUtc { get; set; }

        public DateTimeOffset LastSeenUtc { get; set; }

        public LicenseKind Kind { get; set; }

        public DateTimeOffset? ExpiresUtc { get; set; }

        public string DeviceId { get; set; } = string.Empty;

        public static PersistedState FromState(LicenseState state) => new()
        {
            FirstRunUtc = state.FirstRunUtc,
            LastSeenUtc = state.LastSeenUtc,
            Kind = state.Kind,
            ExpiresUtc = state.ExpiresUtc,
            DeviceId = state.DeviceId
        };

        public LicenseState ToState() => new(
            FirstRunUtc,
            LastSeenUtc,
            Kind,
            ExpiresUtc,
            DeviceId);
    }
}
