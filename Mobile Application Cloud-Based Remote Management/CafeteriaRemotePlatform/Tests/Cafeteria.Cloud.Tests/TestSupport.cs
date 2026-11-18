using Cafeteria.Application;
using Cafeteria.Contracts;
using Cafeteria.Domain;
using Cafeteria.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cafeteria.Cloud.Tests;

internal sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}

internal sealed class FakePasswordService : IPasswordService
{
    public string Hash(string password) => "hash:" + password;

    public bool Verify(string password, string hash) => hash == "hash:" + password;
}

internal sealed class FakeTokenService : ITokenService
{
    private int _counter;

    public TokenResult CreateUserAccessToken(User user) =>
        new($"access:{user.Id}:{Interlocked.Increment(ref _counter)}",
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public TokenResult CreateInstallationAccessToken(Installation installation) =>
        new($"installation-access:{installation.Id}:{Interlocked.Increment(ref _counter)}",
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public string CreateRefreshToken() => $"refresh:{Interlocked.Increment(ref _counter)}";

    public string HashToken(string token) => "hash:" + token;
}

internal sealed class RecordingEmailSender : IEmailSender
{
    public List<(string Email, string Code, string TenantName)> Invitations { get; } = [];

    public Task SendInvitationAsync(
        string email,
        string inviteCode,
        string tenantName,
        CancellationToken cancellationToken)
    {
        Invitations.Add((email, inviteCode, tenantName));
        return Task.CompletedTask;
    }
}

internal sealed class RecordingPushSender : IPushSender
{
    public List<(IReadOnlyCollection<string> Tokens, string Title, string Body)> Sends { get; } = [];

    public Task SendAsync(
        IReadOnlyCollection<string> expoPushTokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        Sends.Add((expoPushTokens, title, body));
        return Task.CompletedTask;
    }
}

internal sealed class RecordingRealtimePublisher : IRealtimePublisher
{
    public List<RemoteCommandDto> CommandCreated { get; } = [];
    public List<RemoteCommandDto> CommandCompleted { get; } = [];
    public List<NotificationDto> Notifications { get; } = [];
    public List<InstallationDto> InstallationStatuses { get; } = [];

    public Task PublishCommandCreatedAsync(
        Guid tenantId,
        Guid requestedByUserId,
        RemoteCommandDto command,
        CancellationToken cancellationToken)
    {
        CommandCreated.Add(command);
        return Task.CompletedTask;
    }

    public Task PublishCommandCompletedAsync(
        Guid tenantId,
        Guid requestedByUserId,
        RemoteCommandDto command,
        CancellationToken cancellationToken)
    {
        CommandCompleted.Add(command);
        return Task.CompletedTask;
    }

    public Task PublishNotificationAsync(
        Guid userId,
        NotificationDto notification,
        CancellationToken cancellationToken)
    {
        Notifications.Add(notification);
        return Task.CompletedTask;
    }

    public Task PublishInstallationStatusAsync(
        Guid tenantId,
        InstallationDto installation,
        CancellationToken cancellationToken)
    {
        InstallationStatuses.Add(installation);
        return Task.CompletedTask;
    }
}

internal sealed class RecordingCommandPusher : IInstallationCommandPusher
{
    public List<(Guid InstallationId, RemoteCommandDto Command)> Pushes { get; } = [];

    public Task PushAsync(
        Guid installationId,
        RemoteCommandDto command,
        CancellationToken cancellationToken)
    {
        Pushes.Add((installationId, command));
        return Task.CompletedTask;
    }
}

internal sealed class TestDatabase : IDisposable
{
    private readonly string _databaseName = Guid.NewGuid().ToString("N");

    public TestDatabase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;
        Db = new AppDbContext(options);
        Clock = new TestClock();
        Passwords = new FakePasswordService();
        Tokens = new FakeTokenService();
        Email = new RecordingEmailSender();
        Push = new RecordingPushSender();
        Realtime = new RecordingRealtimePublisher();
        Pusher = new RecordingCommandPusher();
    }

    public AppDbContext Db { get; }
    public TestClock Clock { get; }
    public FakePasswordService Passwords { get; }
    public FakeTokenService Tokens { get; }
    public RecordingEmailSender Email { get; }
    public RecordingPushSender Push { get; }
    public RecordingRealtimePublisher Realtime { get; }
    public RecordingCommandPusher Pusher { get; }

    public PermissionService Permissions() => new(Db, Clock);

    public async Task<(Tenant Tenant, User User, TenantMembership Membership)> SeedTenantAsync(
        string tenantName = "Cafe",
        TenantRole role = TenantRole.Owner,
        string email = "owner@example.com")
    {
        var tenant = new Tenant { Name = tenantName, Slug = tenantName.ToLowerInvariant() };
        var user = new User
        {
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = "Owner",
            PasswordHash = Passwords.Hash("password123")
        };
        var membership = new TenantMembership
        {
            Tenant = tenant,
            User = user,
            Role = role
        };

        Db.Tenants.Add(tenant);
        Db.Users.Add(user);
        Db.TenantMemberships.Add(membership);
        await Db.SaveChangesAsync();
        return (tenant, user, membership);
    }

    public async Task<Installation> SeedInstallationAsync(
        Guid tenantId,
        bool online = true,
        string name = "Main")
    {
        var installation = new Installation
        {
            TenantId = tenantId,
            Name = name,
            MachineName = "CAFE-PC",
            AppVersion = "1.0.0",
            SecretHash = Tokens.HashToken("secret"),
            LastSeenAt = online ? Clock.UtcNow : Clock.UtcNow.AddMinutes(-10)
        };
        Db.Installations.Add(installation);
        await Db.SaveChangesAsync();
        return installation;
    }

    public void Dispose() => Db.Dispose();
}
