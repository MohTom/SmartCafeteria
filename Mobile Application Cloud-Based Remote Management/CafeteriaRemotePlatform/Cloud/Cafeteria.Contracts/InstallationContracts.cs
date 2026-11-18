namespace Cafeteria.Contracts;

public sealed record CreateClaimCodeRequest(DateTimeOffset? ExpiresAt = null);

public sealed record ClaimCodeResponse(
    string Code,
    DateTimeOffset ExpiresAt);

public sealed record ClaimInstallationRequest(
    string Code,
    string Name,
    string MachineName,
    string AppVersion);

public sealed record ClaimInstallationResponse(
    Guid InstallationId,
    string InstallationSecret,
    Guid TenantId,
    string TenantName,
    string InstallationName);

public sealed record InstallationTokenRequest(
    Guid InstallationId,
    string InstallationSecret);

public sealed record InstallationTokenResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt);

public sealed record InstallationDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string MachineName,
    string AppVersion,
    bool IsOnline,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset CreatedAt,
    InstallationAccessDto Access);

public sealed record InstallationAccessDto(
    bool CanViewReports,
    bool CanManageUsers,
    bool CanAdjustInventory);

public sealed record UpdateInstallationAccessRequest(
    Guid UserId,
    bool CanViewReports,
    bool CanManageUsers,
    bool CanAdjustInventory);

public sealed record HeartbeatRequest(string AppVersion);
