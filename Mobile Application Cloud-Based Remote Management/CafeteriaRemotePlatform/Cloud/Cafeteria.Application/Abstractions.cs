using Cafeteria.Contracts;
using Cafeteria.Domain;

namespace Cafeteria.Application;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    TokenResult CreateUserAccessToken(User user);
    TokenResult CreateInstallationAccessToken(Installation installation);
    string CreateRefreshToken();
    string HashToken(string token);
}

public sealed record TokenResult(string Token, DateTimeOffset ExpiresAt);

public interface IEmailSender
{
    Task SendInvitationAsync(
        string email,
        string inviteCode,
        string tenantName,
        CancellationToken cancellationToken);
}

public interface IPushSender
{
    Task SendAsync(
        IReadOnlyCollection<string> expoPushTokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken);
}

public interface IRealtimePublisher
{
    Task PublishCommandCreatedAsync(
        Guid tenantId,
        Guid requestedByUserId,
        RemoteCommandDto command,
        CancellationToken cancellationToken);

    Task PublishCommandCompletedAsync(
        Guid tenantId,
        Guid requestedByUserId,
        RemoteCommandDto command,
        CancellationToken cancellationToken);

    Task PublishNotificationAsync(
        Guid userId,
        NotificationDto notification,
        CancellationToken cancellationToken);

    Task PublishInstallationStatusAsync(
        Guid tenantId,
        InstallationDto installation,
        CancellationToken cancellationToken);
}

public interface IInstallationCommandPusher
{
    Task PushAsync(
        Guid installationId,
        RemoteCommandDto command,
        CancellationToken cancellationToken);
}
