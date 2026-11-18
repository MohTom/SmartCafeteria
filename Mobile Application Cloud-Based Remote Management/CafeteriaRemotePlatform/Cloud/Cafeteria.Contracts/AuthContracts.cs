namespace Cafeteria.Contracts;

public sealed record RegisterCompanyRequest(
    string CompanyName,
    string OwnerEmail,
    string OwnerDisplayName,
    string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record TenantSummary(
    Guid Id,
    string Name,
    string Slug,
    string Role);

public sealed record UserProfile(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<TenantSummary> Tenants);

public sealed record AuthResponse(
    AuthTokens Tokens,
    UserProfile User);
