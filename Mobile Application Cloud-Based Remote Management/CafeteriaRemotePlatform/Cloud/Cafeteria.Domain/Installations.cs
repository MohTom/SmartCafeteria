namespace Cafeteria.Domain;

public class Installation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string SecretHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<InstallationAccess> Access { get; set; } = new List<InstallationAccess>();
    public ICollection<RemoteCommand> Commands { get; set; } = new List<RemoteCommand>();
    public ICollection<BusinessEvent> Events { get; set; } = new List<BusinessEvent>();
}

public class InstallationAccess
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstallationId { get; set; }
    public Guid UserId { get; set; }
    public AccessLevel AccessLevel { get; set; } = AccessLevel.View;
    public bool CanViewReports { get; set; } = true;
    public bool CanManageUsers { get; set; }
    public bool CanAdjustInventory { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Installation Installation { get; set; } = null!;
    public User User { get; set; } = null!;
}

public class InstallationClaimCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public Guid? CreatedInstallationId { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
