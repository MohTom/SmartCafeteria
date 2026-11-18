namespace Cafeteria.Infrastructure;

public sealed class ExpoPushOptions
{
    public const string SectionName = "ExpoPush";

    public bool Enabled { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://exp.host/--/api/v2/push/send";
}
