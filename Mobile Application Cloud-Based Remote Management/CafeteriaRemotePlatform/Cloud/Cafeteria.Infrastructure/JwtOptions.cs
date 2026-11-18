namespace Cafeteria.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "CafeteriaCloud";
    public string Audience { get; set; } = "CafeteriaClients";
    public string UserSigningKey { get; set; } = string.Empty;
    public string InstallationSigningKey { get; set; } = string.Empty;
    public int UserAccessTokenMinutes { get; set; } = 30;
    public int InstallationAccessTokenHours { get; set; } = 24;
}
