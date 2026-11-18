namespace Cafeteria.Contracts;

public sealed record EventDto(
    string EventId,
    string Type,
    string PayloadJson,
    DateTimeOffset OccurredAt);

public sealed record EventBatchRequest(IReadOnlyList<EventDto> Events);

public sealed record EventBatchResponse(int Accepted, int Duplicates);

public sealed record NotificationDto(
    Guid Id,
    Guid TenantId,
    Guid? InstallationId,
    string Title,
    string Body,
    string Type,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record UnreadCountResponse(int Count);

public sealed record RegisterDeviceRequest(
    string ExpoPushToken,
    string Platform);
