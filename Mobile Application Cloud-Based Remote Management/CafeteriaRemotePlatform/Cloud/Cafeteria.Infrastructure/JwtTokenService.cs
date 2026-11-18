using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Cafeteria.Application;
using Cafeteria.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Cafeteria.Infrastructure;

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;

        if (string.IsNullOrWhiteSpace(_options.UserSigningKey) ||
            _options.UserSigningKey.Length < 32)
            throw new InvalidOperationException(
                "Jwt:UserSigningKey must be configured with at least 32 characters.");

        if (string.IsNullOrWhiteSpace(_options.InstallationSigningKey) ||
            _options.InstallationSigningKey.Length < 32)
            throw new InvalidOperationException(
                "Jwt:InstallationSigningKey must be configured with at least 32 characters.");
    }

    public TokenResult CreateUserAccessToken(User user)
    {
        var expires = _clock.UtcNow.AddMinutes(_options.UserAccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("token_type", "user"),
            new Claim("email", user.Email),
            new Claim("name", user.DisplayName)
        };

        return new TokenResult(CreateToken(claims, expires, _options.UserSigningKey), expires);
    }

    public TokenResult CreateInstallationAccessToken(Installation installation)
    {
        var expires = _clock.UtcNow.AddHours(_options.InstallationAccessTokenHours);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, installation.Id.ToString()),
            new Claim("token_type", "installation"),
            new Claim("tenant_id", installation.TenantId.ToString()),
            new Claim("name", installation.Name),
            new Claim("machine_name", installation.MachineName)
        };

        return new TokenResult(
            CreateToken(claims, expires, _options.InstallationSigningKey),
            expires);
    }

    public string CreateRefreshToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private string CreateToken(
        IEnumerable<Claim> claims,
        DateTimeOffset expires,
        string signingKey)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = _clock.UtcNow.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
