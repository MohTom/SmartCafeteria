using System.Text.RegularExpressions;
using Cafeteria.Contracts;
using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cafeteria.Application;

public sealed partial class AuthService
{
    private readonly IAppDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ITokenService _tokens;
    private readonly IClock _clock;

    public AuthService(
        IAppDbContext db,
        IPasswordService passwords,
        ITokenService tokens,
        IClock clock)
    {
        _db = db;
        _passwords = passwords;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<AuthResponse> RegisterCompanyAsync(
        RegisterCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.OwnerEmail);
        if (!EmailPattern().IsMatch(email))
            throw AppException.BadRequest("A valid owner email is required.", "invalid_email");

        if (string.IsNullOrWhiteSpace(request.OwnerDisplayName) || request.OwnerDisplayName.Length > 120)
            throw AppException.BadRequest("Owner display name is required.", "invalid_display_name");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw AppException.BadRequest("Password must be at least 8 characters.", "weak_password");

        if (string.IsNullOrWhiteSpace(request.CompanyName) || request.CompanyName.Length > 160)
            throw AppException.BadRequest("Company name is required.", "invalid_company_name");

        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == email, cancellationToken))
            throw AppException.Conflict("An account with this email already exists.", "email_exists");

        var tenant = new Tenant
        {
            Name = request.CompanyName.Trim(),
            Slug = await CreateUniqueSlugAsync(request.CompanyName, cancellationToken)
        };

        var user = new User
        {
            Email = request.OwnerEmail.Trim(),
            NormalizedEmail = email,
            DisplayName = request.OwnerDisplayName.Trim(),
            PasswordHash = _passwords.Hash(request.Password)
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(user);
        _db.TenantMemberships.Add(new TenantMembership
        {
            Tenant = tenant,
            User = user,
            Role = TenantRole.Owner
        });

        await _db.SaveChangesAsync(cancellationToken);
        return await IssueAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.NormalizedEmail == email,
            cancellationToken);

        if (user is null || !user.IsActive || !_passwords.Verify(request.Password, user.PasswordHash))
            throw AppException.Unauthorized("Invalid email or password.");

        user.LastLoginAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return await IssueAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw AppException.Unauthorized("A refresh token is required.");

        var hash = _tokens.HashToken(request.RefreshToken);
        var token = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is null || token.RevokedAt is not null ||
            token.ExpiresAt <= _clock.UtcNow || !token.User.IsActive)
            throw AppException.Unauthorized("Refresh token is invalid or expired.");

        token.RevokedAt = _clock.UtcNow;
        var replacement = await IssueAuthResponseAsync(token.User, cancellationToken, save: false);
        token.ReplacedByTokenHash = _tokens.HashToken(replacement.Tokens.RefreshToken);
        await _db.SaveChangesAsync(cancellationToken);
        return replacement;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return;

        var hash = _tokens.HashToken(request.RefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash,
            cancellationToken);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserProfile> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw AppException.Unauthorized("User was not found.");

        return await BuildProfileAsync(user, cancellationToken);
    }

    private async Task<AuthResponse> IssueAuthResponseAsync(
        User user,
        CancellationToken cancellationToken,
        bool save = true)
    {
        var access = _tokens.CreateUserAccessToken(user);
        var refreshValue = _tokens.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokens.HashToken(refreshValue),
            ExpiresAt = _clock.UtcNow.AddDays(30)
        });

        if (save)
            await _db.SaveChangesAsync(cancellationToken);

        var profile = await BuildProfileAsync(user, cancellationToken);
        return new AuthResponse(
            new AuthTokens(
                access.Token,
                access.ExpiresAt,
                refreshValue,
                _clock.UtcNow.AddDays(30)),
            profile);
    }

    private async Task<UserProfile> BuildProfileAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var memberships = await _db.TenantMemberships
            .AsNoTracking()
            .Include(m => m.Tenant)
            .Where(m => m.UserId == user.Id && m.IsActive && m.Tenant.IsActive)
            .OrderBy(m => m.Tenant.Name)
            .ToListAsync(cancellationToken);

        var tenants = memberships
            .Select(m => new TenantSummary(
                m.TenantId,
                m.Tenant.Name,
                m.Tenant.Slug,
                m.Role.ToString()))
            .ToList();

        return new UserProfile(user.Id, user.Email, user.DisplayName, tenants);
    }

    private async Task<string> CreateUniqueSlugAsync(
        string companyName,
        CancellationToken cancellationToken)
    {
        var baseSlug = SlugPattern().Replace(
            companyName.Trim().ToLowerInvariant(),
            "-").Trim('-');

        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "company";

        if (baseSlug.Length > 48)
            baseSlug = baseSlug[..48].Trim('-');

        var slug = baseSlug;
        var suffix = 1;
        while (await _db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken))
        {
            suffix++;
            slug = $"{baseSlug}-{suffix}";
        }

        return slug;
    }

    private static string NormalizeEmail(string email) =>
        (email ?? string.Empty).Trim().ToUpperInvariant();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugPattern();
}
