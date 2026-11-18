namespace Cafeteria.RemoteAccess;

public sealed class RemoteAccessSettings
{
    public bool Enabled { get; set; } = true;
    public string CloudBaseUrl { get; set; } = string.Empty;
    public Guid InstallationId { get; set; }
    public string InstallationSecret { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string InstallationName { get; set; } = string.Empty;
    public DateTimeOffset LinkedAt { get; set; }

    public bool IsLinked =>
        InstallationId != Guid.Empty &&
        TenantId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(InstallationSecret) &&
        !string.IsNullOrWhiteSpace(CloudBaseUrl);
}

public sealed record RemoteAccessStatus(
    bool IsEnabled,
    bool IsLinked,
    bool IsLocalApiRunning,
    bool IsCloudConnected,
    string InstallationName,
    string CloudBaseUrl,
    string? LocalApiUrl,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset? LastHeartbeatAt,
    string? LastError)
{
    public static RemoteAccessStatus Unlinked { get; } = new(
        false,
        false,
        false,
        false,
        string.Empty,
        string.Empty,
        null,
        null,
        null,
        null);
}

public sealed record RemoteLinkRequest(
    string CloudBaseUrl,
    string ClaimCode,
    string InstallationName);

public sealed record RemoteLinkResult(
    bool Success,
    string? Error,
    RemoteAccessStatus Status);
