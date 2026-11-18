namespace Cafeteria.Domain;

public class RemoteCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid InstallationId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string RequiredPermission { get; set; } = string.Empty;
    public CommandStatus Status { get; set; } = CommandStatus.Pending;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ResultJson { get; set; }
    public string? Error { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Installation Installation { get; set; } = null!;
    public User RequestedBy { get; set; } = null!;
}
