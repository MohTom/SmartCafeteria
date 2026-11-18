using Cafeteria.Application;
using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cafeteria.Infrastructure;

public sealed class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Installation> Installations => Set<Installation>();
    public DbSet<InstallationAccess> InstallationAccess => Set<InstallationAccess>();
    public DbSet<InstallationClaimCode> InstallationClaimCodes => Set<InstallationClaimCode>();
    public DbSet<RemoteCommand> RemoteCommands => Set<RemoteCommand>();
    public DbSet<BusinessEvent> BusinessEvents => Set<BusinessEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(80).IsRequired();
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(320).IsRequired();
            entity.Property(e => e.NormalizedEmail).HasMaxLength(320).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(512).IsRequired();
            entity.HasIndex(e => e.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<TenantMembership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Memberships)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.TenantId, e.UserId }).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(320).IsRequired();
            entity.Property(e => e.NormalizedEmail).HasMaxLength(320).IsRequired();
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.NormalizedEmail });
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Installation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
            entity.Property(e => e.MachineName).HasMaxLength(160).IsRequired();
            entity.Property(e => e.AppVersion).HasMaxLength(64).IsRequired();
            entity.Property(e => e.SecretHash).HasMaxLength(128).IsRequired();
            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Installations)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.TenantId, e.Name });
        });

        modelBuilder.Entity<InstallationAccess>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AccessLevel).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.Installation)
                .WithMany(i => i.Access)
                .HasForeignKey(e => e.InstallationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.InstallationId, e.UserId }).IsUnique();
        });

        modelBuilder.Entity<InstallationClaimCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CodeHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.CodeHash).IsUnique();
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RemoteCommand>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(80).IsRequired();
            entity.Property(e => e.PayloadJson).HasColumnType("text").IsRequired();
            entity.Property(e => e.RequiredPermission).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ResultJson).HasColumnType("text");
            entity.Property(e => e.Error).HasColumnType("text");
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Installation)
                .WithMany(i => i.Commands)
                .HasForeignKey(e => e.InstallationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.RequestedBy)
                .WithMany()
                .HasForeignKey(e => e.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new
            {
                e.TenantId,
                e.InstallationId,
                e.RequestedByUserId,
                e.IdempotencyKey
            }).IsUnique();
            entity.HasIndex(e => new { e.InstallationId, e.Status, e.ExpiresAt });
        });

        modelBuilder.Entity<BusinessEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventKey).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PayloadJson).HasColumnType("text").IsRequired();
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Installation)
                .WithMany(i => i.Events)
                .HasForeignKey(e => e.InstallationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.InstallationId, e.EventKey }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.OccurredAt });
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Body).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(40).IsRequired();
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.TenantId, e.IsRead, e.CreatedAt });
        });

        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExpoPushToken).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Platform).HasMaxLength(40).IsRequired();
            entity.HasIndex(e => e.ExpoPushToken).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).HasMaxLength(120).IsRequired();
            entity.Property(e => e.EntityType).HasMaxLength(80).IsRequired();
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.Property(e => e.DetailsJson).HasColumnType("text");
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.HasIndex(e => new { e.TenantId, e.CreatedAt });
        });
    }
}
