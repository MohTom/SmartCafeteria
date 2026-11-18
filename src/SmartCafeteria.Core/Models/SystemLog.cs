namespace SmartCafeteria.Core.Models;
public class SystemLog
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "info";
}
