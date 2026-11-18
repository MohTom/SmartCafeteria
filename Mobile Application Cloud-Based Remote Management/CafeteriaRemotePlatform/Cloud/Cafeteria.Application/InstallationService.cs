using System.Security.Cryptography;
using System.Text;
using Cafeteria.Contracts;
using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cafeteria.Application;

public sealed class InstallationService
{
    private readonly IAppDbContext _db;
    private readonly PermissionService _permissions;
    private readonly ITokenService _tokens;
    private readonly IClock _clock;
    private readonly IRealtimePublisher _realtime;

    public InstallationService(
        IAppDbContext db,
        PermissionService permissions,
        ITokenService tokens,
        IClock clock,
        IRealtimePublisher realtime)
    {
        _db = db;
        _permissions = permissions;
        _tokens = tokens;
        _clock = clock;
        _realtime = realtime;
    }

    public async Task<IReadOnlyList<InstallationDto>> GetForUserAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var membership = await _permissions.GetMembershipAsync(userId, tenantId, cancellationToken);
        var accessible = await _permissions.GetAccessibleInstallationIdsAsync(
            userId,
            tenantId,
            cancellationToken);

        var installations = await _db.Installations
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.IsActive && accessible.Contains(i.Id))
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);

        var accessRows = await _db.InstallationAccess
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        var accessByInstallation = accessRows.ToDictionary(a => a.InstallationId);
        var isAdmin = membership.Role is TenantRole.Owner or TenantRole.Admin;

        return installations.Select(i =>
        {
            var access = isAdmin
                ? new InstallationAccessDto(true, true, true)
                : accessByInstallation.TryGetValue(i.Id, out var row)
                    ? new InstallationAccessDto(
                        row.CanViewReports,
                        row.CanManageUsers,
                        row.CanAdjustInventory)
                    : new InstallationAccessDto(false, false, false);

            return ToDto(i, access);
        }).ToList();
    }

    public async Task<InstallationDto> GetForUserAsync(
        Guid userId,
        Guid tenantId,
        Guid installationId,
        CancellationToken cancellationToken)
    {
        await _permissions.EnsureInstallationPermissionAsync(
            userId,
            tenantId,
            installationId,
            Permissions.InstallationRead,
            cancellationToken);

        var installation = await _permissions.GetInstallationAsync(
            tenantId,
            installationId,
            cancellationToken);

        var access = await _permissions.GetAccessAsync(
            userId,
            tenantId,
            installationId,
            cancellationToken);

        return ToDto(installation, access);
    }

    public async Task<ClaimCodeResponse> CreateClaimCodeAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var membership = await _permissions.GetMembershipAsync(userId, tenantId, cancellationToken);
        if (membership.Role is not (TenantRole.Owner or TenantRole.Admin or TenantRole.Manager))
            throw AppException.Forbidden("Only an owner, administrator, or manager can link an installation.");

        var code = CreateReadableCode();
        var expires = _clock.UtcNow.AddMinutes(10);

        _db.InstallationClaimCodes.Add(new InstallationClaimCode
        {
            TenantId = tenantId,
            CodeHash = _tokens.HashToken(NormalizeCode(code)),
            CreatedByUserId = userId,
            ExpiresAt = expires
        });

        await _db.SaveChangesAsync(cancellationToken);
        return new ClaimCodeResponse(code, expires);
    }

    public async Task<ClaimInstallationResponse> ClaimAsync(
        ClaimInstallationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw AppException.BadRequest("Claim code is required.", "invalid_claim_code");

        var hash = _tokens.HashToken(NormalizeCode(request.Code));
        var claim = await _db.InstallationClaimCodes
            .Include(c => c.Tenant)
            .FirstOrDefaultAsync(
                c => c.CodeHash == hash && c.UsedAt == null && c.ExpiresAt > _clock.UtcNow,
                cancellationToken);

        if (claim is null)
            throw AppException.BadRequest("Claim code is invalid or expired.", "invalid_claim_code");

        claim.UsedAt = _clock.UtcNow;
        var secret = CreateSecret();
        var installation = new Installation
        {
            TenantId = claim.TenantId,
            Name = string.IsNullOrWhiteSpace(request.Name)
                ? request.MachineName
                : request.Name.Trim(),
            MachineName = request.MachineName.Trim(),
            AppVersion = request.AppVersion.Trim(),
            SecretHash = _tokens.HashToken(secret),
            LastSeenAt = _clock.UtcNow
        };

        _db.Installations.Add(installation);
        _db.InstallationAccess.Add(new InstallationAccess
        {
            Installation = installation,
            UserId = claim.CreatedByUserId,
            AccessLevel = AccessLevel.Manage,
            CanViewReports = true,
            CanManageUsers = true,
            CanAdjustInventory = true
        });

        claim.CreatedInstallationId = installation.Id;
        await _db.SaveChangesAsync(cancellationToken);

        return new ClaimInstallationResponse(
            installation.Id,
            secret,
            claim.TenantId,
            claim.Tenant.Name,
            installation.Name);
    }

    public async Task<InstallationTokenResponse> CreateTokenAsync(
        InstallationTokenRequest request,
        CancellationToken cancellationToken)
    {
        var installation = await _db.Installations.FirstOrDefaultAsync(
            i => i.Id == request.InstallationId && i.IsActive,
            cancellationToken);

        if (installation is null ||
            !FixedTimeEquals(installation.SecretHash, _tokens.HashToken(request.InstallationSecret)))
            throw AppException.Unauthorized("Installation credentials are invalid.");

        installation.LastSeenAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var token = _tokens.CreateInstallationAccessToken(installation);
        return new InstallationTokenResponse(token.Token, token.ExpiresAt);
    }

    public async Task HeartbeatAsync(
        Guid installationId,
        HeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        var installation = await _db.Installations.FirstOrDefaultAsync(
            i => i.Id == installationId && i.IsActive,
            cancellationToken) ?? throw AppException.NotFound("Installation was not found.");

        installation.LastSeenAt = _clock.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.AppVersion))
            installation.AppVersion = request.AppVersion.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        var dto = ToDto(installation, new InstallationAccessDto(false, false, false));
        await _realtime.PublishInstallationStatusAsync(
            installation.TenantId,
            dto,
            cancellationToken);
    }

    public async Task<InstallationDto> UpdateAccessAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid installationId,
        UpdateInstallationAccessRequest request,
        CancellationToken cancellationToken)
    {
        await _permissions.EnsureRoleAsync(
            actorUserId,
            tenantId,
            cancellationToken,
            TenantRole.Owner,
            TenantRole.Admin);

        await _permissions.GetInstallationAsync(tenantId, installationId, cancellationToken);

        var targetMembership = await _db.TenantMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.TenantId == tenantId && m.UserId == request.UserId && m.IsActive,
                cancellationToken) ?? throw AppException.BadRequest(
                    "The selected user is not an active company member.",
                    "invalid_member");

        var access = await _db.InstallationAccess.FirstOrDefaultAsync(
            a => a.InstallationId == installationId && a.UserId == request.UserId,
            cancellationToken);

        if (access is null)
        {
            access = new InstallationAccess
            {
                InstallationId = installationId,
                UserId = request.UserId
            };
            _db.InstallationAccess.Add(access);
        }

        access.AccessLevel = request.CanManageUsers || request.CanAdjustInventory
            ? AccessLevel.Manage
            : AccessLevel.View;
        access.CanViewReports = request.CanViewReports;
        access.CanManageUsers = request.CanManageUsers;
        access.CanAdjustInventory = request.CanAdjustInventory;

        await _db.SaveChangesAsync(cancellationToken);

        var dto = new InstallationAccessDto(
            access.CanViewReports,
            access.CanManageUsers,
            access.CanAdjustInventory);

        return ToDto(
            await _permissions.GetInstallationAsync(tenantId, installationId, cancellationToken),
            dto);
    }

    private InstallationDto ToDto(Installation installation, InstallationAccessDto access) =>
        new(
            installation.Id,
            installation.TenantId,
            installation.Name,
            installation.MachineName,
            installation.AppVersion,
            _permissions.IsOnline(installation),
            installation.LastSeenAt,
            installation.CreatedAt,
            access);

    private static string CreateReadableCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(8);
        var value = new string(bytes.Select(b => alphabet[b % alphabet.Length]).ToArray());
        return $"{value[..4]}-{value[4..]}";
    }

    private static string NormalizeCode(string code) =>
        (code ?? string.Empty).Replace("-", string.Empty).Trim().ToUpperInvariant();

    private static string CreateSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static bool FixedTimeEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left);
        var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
