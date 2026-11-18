using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Cafeteria.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Cafeteria.Api.Authentication;

public static class JwtAuthenticationExtensions
{
    public const string UserScheme = "User";
    public const string InstallationScheme = "Installation";

    public static IServiceCollection AddCafeteriaAuthentication(
        this IServiceCollection services,
        JwtOptions jwt)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = UserScheme;
                options.DefaultChallengeScheme = UserScheme;
            })
            .AddJwtBearer(UserScheme, options =>
                Configure(options, jwt, jwt.UserSigningKey, "user"))
            .AddJwtBearer(InstallationScheme, options =>
                Configure(options, jwt, jwt.InstallationSigningKey, "installation"));

        services.AddAuthorization(options =>
        {
            options.AddPolicy(InstallationScheme, policy =>
                policy.RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(InstallationScheme));
        });

        return services;
    }

    private static void Configure(
        JwtBearerOptions options,
        JwtOptions jwt,
        string signingKey,
        string expectedTokenType)
    {
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
            throw new InvalidOperationException(
                $"JWT signing key for '{expectedTokenType}' tokens must have at least 32 characters.");

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var tokenType = context.Principal?.FindFirst("token_type")?.Value;
                if (!string.Equals(tokenType, expectedTokenType, StringComparison.Ordinal))
                    context.Fail($"Expected a {expectedTokenType} token.");

                return Task.CompletedTask;
            }
        };
    }
}
