namespace Cafeteria.Contracts;

public sealed record CreateCommandRequest(
    Guid InstallationId,
    string Type,
    string PayloadJson,
    string IdempotencyKey);

public sealed record RemoteCommandDto(
    Guid Id,
    Guid TenantId,
    Guid InstallationId,
    Guid RequestedByUserId,
    string Type,
    string PayloadJson,
    string Status,
    string? ResultJson,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? DispatchedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record CommandExecutionRequest(
    Guid CommandId,
    string Type,
    string PayloadJson,
    Guid RequestedByUserId,
    string RequestedByName);

public sealed record CommandExecutionResponse(
    bool Success,
    string? ResultJson,
    string? Error);

public sealed record CompleteCommandRequest(
    bool Success,
    string? ResultJson,
    string? Error);

public sealed record CompleteCommandResponse(
    Guid CommandId,
    string Status);
