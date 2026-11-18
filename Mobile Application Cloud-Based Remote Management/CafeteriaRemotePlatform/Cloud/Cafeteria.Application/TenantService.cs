using System.Security.Cryptography;
using Cafeteria.Contracts;
using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cafeteria.Application;

public sealed class TenantService
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IClock _clock;
    private readonly IEmailSender _email;

    public TenantService(
        IAppDbContext db,
        ITokenService tokens,
        IClock clock,
        IEmailSender email)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _email = email;
    }

    public async Task<IReadOnlyList<TenantSummary>> GetTenantsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var memberships = await _db.TenantMemberships
            .AsNoTracking()
            .Include(m => m.Tenant)
            .Where(m => m.UserId == userId && m.IsActive && m.Tenant.IsActive)
            .OrderBy(m => m.Tenant.Name)
            .ToListAsync(cancellationToken);

        return memberships
            .Select(m => new TenantSummary(
                m.TenantId,
                m.Tenant.Name,
                m.Tenant.Slug,
                m.Role.ToString()))
            .ToList();
    }

    public async Task<IReadOnlyList<TenantMemberDto>> GetMembersAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(userId, tenantId, cancellationToken);

        return await _db.TenantMemberships
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .OrderBy(m => m.User.DisplayName)
            .Select(m => new TenantMemberDto(
                m.UserId,
                m.User.Email,
                m.User.DisplayName,
                m.Role.ToString(),
                m.IsActive,
                m.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<InvitationCreated> CreateInvitationAsync(
        Guid userId,
        Guid tenantId,
        InviteMemberRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await EnsureMemberAsync(userId, tenantId, cancellationToken);
        if (actor.Role is not (TenantRole.Owner or TenantRole.Admin))
            throw AppException.Forbidden("Only an owner or administrator can invite members.");

        if (!Enum.TryParse<TenantRole>(request.Role, true, out var role) ||
            role is not (TenantRole.Admin or TenantRole.Manager or TenantRole.Viewer))
            throw AppException.BadRequest("Role must be Admin, Manager, or Viewer.", "invalid_role");

        if (role == TenantRole.Admin && actor.Role != TenantRole.Owner)
            throw AppException.Forbidden("Only the owner can invite another administrator.");

        var email = request.Email.Trim();
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('@'))
            throw AppException.BadRequest("A valid email is required.", "invalid_email");

        var code = CreateInviteCode();
        var expires = request.ExpiresAt ?? _clock.UtcNow.AddDays(7);
        if (expires <= _clock.UtcNow)
            throw AppException.BadRequest("Invitation expiry must be in the future.", "invalid_expiry");

        var invitation = new Invitation
        {
            TenantId = tenantId,
            Email = email,
            NormalizedEmail = normalized,
            Role = role,
            TokenHash = _tokens.HashToken(code),
            InvitedByUserId = userId,
            ExpiresAt = expires
        };

        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync(cancellationToken);

        var tenant = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstAsync(cancellationToken);

        await _email.SendInvitationAsync(email, code, tenant, cancellationToken);

        return new InvitationCreated(
            invitation.Id,
            email,
            role.ToString(),
            code,
            expires);
    }

    public async Task<TenantSummary> AcceptInvitationAsync(
        Guid userId,
        AcceptInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InviteCode))
            throw AppException.BadRequest("Invite code is required.", "invalid_invite_code");

        var hash = _tokens.HashToken(request.InviteCode.Trim().ToUpperInvariant());
        var invitation = await _db.Invitations
            .Include(i => i.Tenant)
            .FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);

        if (invitation is null || invitation.AcceptedAt is not null ||
            invitation.ExpiresAt <= _clock.UtcNow || !invitation.Tenant.IsActive)
            throw AppException.BadRequest("Invite code is invalid or expired.", "invalid_invite_code");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw AppException.Unauthorized();

        if (user.NormalizedEmail != invitation.NormalizedEmail)
            throw AppException.Forbidden("This invitation belongs to a different email address.");

        var existing = await _db.TenantMemberships.FirstOrDefaultAsync(
            m => m.TenantId == invitation.TenantId && m.UserId == userId,
            cancellationToken);

        if (existing is null)
        {
            _db.TenantMemberships.Add(new TenantMembership
            {
                TenantId = invitation.TenantId,
                UserId = userId,
                Role = invitation.Role
            });
        }
        else
        {
            existing.IsActive = true;
            existing.Role = invitation.Role;
        }

        invitation.AcceptedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new TenantSummary(
            invitation.TenantId,
            invitation.Tenant.Name,
            invitation.Tenant.Slug,
            invitation.Role.ToString());
    }

    private async Task<TenantMembership> EnsureMemberAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var membership = await _db.TenantMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.TenantId == tenantId && m.IsActive,
                cancellationToken);

        return membership ?? throw AppException.Forbidden("You are not a member of this company.");
    }

    private static string CreateInviteCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(12);
        return new string(bytes.Select(b => alphabet[b % alphabet.Length]).ToArray());
    }

    private static string NormalizeEmail(string email) =>
        (email ?? string.Empty).Trim().ToUpperInvariant();
}
