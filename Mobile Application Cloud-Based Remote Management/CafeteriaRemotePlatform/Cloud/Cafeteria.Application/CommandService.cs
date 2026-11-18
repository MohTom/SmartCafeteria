using System.Text.Json;
using Cafeteria.Contracts;
using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cafeteria.Application;

public sealed class CommandService
{
    private readonly IAppDbContext _db;
    private readonly PermissionService _permissions;
    private readonly IClock _clock;
    private readonly IRealtimePublisher _realtime;
    private readonly IInstallationCommandPusher _pusher;

    public CommandService(
        IAppDbContext db,
        PermissionService permissions,
        IClock clock,
        IRealtimePublisher realtime,
        IInstallationCommandPusher pusher)
    {
        _db = db;
        _permissions = permissions;
        _clock = clock;
        _realtime = realtime;
        _pusher = pusher;
    }

    public async Task<RemoteCommandDto> CreateAsync(
        Guid actorUserId,
        Guid tenantId,
        CreateCommandRequest request,
        CancellationToken cancellationToken)
    {
        var definition = CommandCatalog.Find(request.Type)
            ?? throw AppException.BadRequest("Unsupported command type.", "unsupported_command");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 100)
            throw AppException.BadRequest("A valid idempotency key is required.", "invalid_idempotency_key");

        var installation = await _permissions.GetInstallationAsync(
            tenantId,
            request.InstallationId,
            cancellationToken);

        await _permissions.EnsureInstallationPermissionAsync(
            actorUserId,
            tenantId,
            installation.Id,
            definition.Permission,
            cancellationToken);

        var online = _permissions.IsOnline(installation);
        if (!online && (!definition.QueueWhenOffline || definition.RequiresOnline))
            throw AppException.Conflict(
                "The installation is offline. This operation requires a live connection.",
                "installation_offline");

        var existing = await _db.RemoteCommands
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId &&
                     c.InstallationId == installation.Id &&
                     c.RequestedByUserId == actorUserId &&
                     c.IdempotencyKey == request.IdempotencyKey,
                cancellationToken);

        if (existing is not null)
            return ToDto(existing);

        var payload = NormalizeAndValidatePayload(definition.Type, request.PayloadJson);
        var command = new RemoteCommand
        {
            TenantId = tenantId,
            InstallationId = installation.Id,
            RequestedByUserId = actorUserId,
            Type = definition.Type,
            PayloadJson = payload,
            RequiredPermission = definition.Permission,
            IdempotencyKey = request.IdempotencyKey.Trim(),
            ExpiresAt = _clock.UtcNow.Add(definition.Lifetime)
        };

        _db.RemoteCommands.Add(command);
        await _db.SaveChangesAsync(cancellationToken);

        if (online)
        {
            await _pusher.PushAsync(installation.Id, ToDto(command), cancellationToken);
            command.Status = CommandStatus.Dispatched;
            command.DispatchedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var dto = ToDto(command);
        await _realtime.PublishCommandCreatedAsync(
            tenantId,
            actorUserId,
            dto,
            cancellationToken);
        return dto;
    }

    public async Task<RemoteCommandDto> GetAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid commandId,
        CancellationToken cancellationToken)
    {
        var command = await _db.RemoteCommands
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == commandId && c.TenantId == tenantId,
                cancellationToken) ?? throw AppException.NotFound("Command was not found.");

        await _permissions.EnsureInstallationPermissionAsync(
            actorUserId,
            tenantId,
            command.InstallationId,
            Permissions.InstallationRead,
            cancellationToken);

        return ToDto(command);
    }

    public async Task<IReadOnlyList<RemoteCommandDto>> ListAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid? installationId,
        CancellationToken cancellationToken)
    {
        var query = _db.RemoteCommands
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId);

        if (installationId.HasValue)
        {
            await _permissions.EnsureInstallationPermissionAsync(
                actorUserId,
                tenantId,
                installationId.Value,
                Permissions.InstallationRead,
                cancellationToken);
            query = query.Where(c => c.InstallationId == installationId.Value);
        }
        else
        {
            var accessible = await _permissions.GetAccessibleInstallationIdsAsync(
                actorUserId,
                tenantId,
                cancellationToken);
            query = query.Where(c => accessible.Contains(c.InstallationId));
        }

        return (await query
                .OrderByDescending(c => c.CreatedAt)
                .Take(100)
                .ToListAsync(cancellationToken))
            .Select(ToDto)
            .ToList();
    }

    public async Task<RemoteCommandDto> CancelAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid commandId,
        CancellationToken cancellationToken)
    {
        var command = await _db.RemoteCommands.FirstOrDefaultAsync(
            c => c.Id == commandId && c.TenantId == tenantId,
            cancellationToken) ?? throw AppException.NotFound("Command was not found.");

        await _permissions.EnsureInstallationPermissionAsync(
            actorUserId,
            tenantId,
            command.InstallationId,
            Permissions.InstallationRead,
            cancellationToken);

        if (command.Status is not (CommandStatus.Pending or CommandStatus.Dispatched))
            throw AppException.Conflict("Only pending or dispatched commands can be cancelled.");

        command.Status = CommandStatus.Cancelled;
        command.CompletedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var dto = ToDto(command);
        await _realtime.PublishCommandCompletedAsync(
            tenantId,
            command.RequestedByUserId,
            dto,
            cancellationToken);
        return dto;
    }

    public async Task<IReadOnlyList<RemoteCommandDto>> GetPendingForInstallationAsync(
        Guid installationId,
        CancellationToken cancellationToken)
    {
        var commands = await _db.RemoteCommands
            .AsNoTracking()
            .Where(c => c.InstallationId == installationId &&
                        (c.Status == CommandStatus.Pending || c.Status == CommandStatus.Dispatched) &&
                        c.ExpiresAt > _clock.UtcNow)
            .OrderBy(c => c.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return commands.Select(ToDto).ToList();
    }

    public async Task<RemoteCommandDto> AcknowledgeAsync(
        Guid installationId,
        Guid commandId,
        CancellationToken cancellationToken)
    {
        var command = await _db.RemoteCommands.FirstOrDefaultAsync(
            c => c.Id == commandId && c.InstallationId == installationId,
            cancellationToken) ?? throw AppException.NotFound("Command was not found.");

        if (command.Status is CommandStatus.Executing or CommandStatus.Completed)
            return ToDto(command);

        if (command.Status is CommandStatus.Failed or CommandStatus.Expired or CommandStatus.Cancelled)
            throw AppException.Conflict("Command is no longer executable.", "command_terminal");

        command.Status = CommandStatus.Executing;
        command.StartedAt = _clock.UtcNow;
        command.DispatchedAt ??= _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(command);
    }

    public async Task<RemoteCommandDto> CompleteAsync(
        Guid installationId,
        Guid commandId,
        CompleteCommandRequest request,
        CancellationToken cancellationToken)
    {
        var command = await _db.RemoteCommands.FirstOrDefaultAsync(
            c => c.Id == commandId && c.InstallationId == installationId,
            cancellationToken) ?? throw AppException.NotFound("Command was not found.");

        if (command.Status is CommandStatus.Completed or CommandStatus.Failed)
            return ToDto(command);

        command.Status = request.Success ? CommandStatus.Completed : CommandStatus.Failed;
        command.ResultJson = request.ResultJson;
        command.Error = request.Error;
        command.CompletedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var dto = ToDto(command);
        await _realtime.PublishCommandCompletedAsync(
            command.TenantId,
            command.RequestedByUserId,
            dto,
            cancellationToken);
        return dto;
    }

    public async Task MarkDispatchedAsync(Guid commandId, CancellationToken cancellationToken)
    {
        var command = await _db.RemoteCommands.FirstOrDefaultAsync(
            c => c.Id == commandId,
            cancellationToken);

        if (command is null || command.Status != CommandStatus.Pending)
            return;

        command.Status = CommandStatus.Dispatched;
        command.DispatchedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteCommandDto>> GetDispatchableAsync(
        CancellationToken cancellationToken)
    {
        var onlineThreshold = _clock.UtcNow.AddSeconds(-60);
        return (await _db.RemoteCommands
                .AsNoTracking()
                .Include(c => c.Installation)
                .Where(c => c.Status == CommandStatus.Pending &&
                            c.ExpiresAt > _clock.UtcNow &&
                            c.Installation.IsActive &&
                            c.Installation.LastSeenAt != null &&
                            c.Installation.LastSeenAt >= onlineThreshold)
                .OrderBy(c => c.CreatedAt)
                .Take(50)
                .ToListAsync(cancellationToken))
            .Select(ToDto)
            .ToList();
    }

    public async Task<int> ExpireAsync(CancellationToken cancellationToken)
    {
        var expired = await _db.RemoteCommands
            .Where(c => c.ExpiresAt <= _clock.UtcNow &&
                        (c.Status == CommandStatus.Pending || c.Status == CommandStatus.Dispatched))
            .ToListAsync(cancellationToken);

        foreach (var command in expired)
        {
            command.Status = CommandStatus.Expired;
            command.CompletedAt = _clock.UtcNow;
        }

        if (expired.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return expired.Count;
    }

    private static string NormalizeAndValidatePayload(string type, string? payloadJson)
    {
        var payload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            throw AppException.BadRequest("Command payload must be valid JSON.", "invalid_payload");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw AppException.BadRequest("Command payload must be a JSON object.", "invalid_payload");

            switch (type)
            {
                case "users.lock":
                case "users.unlock":
                    RequireString(root, "userId");
                    break;
                case "inventory.adjust":
                    RequireString(root, "productId");
                    if (!root.TryGetProperty("delta", out var delta) ||
                        delta.ValueKind != JsonValueKind.Number ||
                        !delta.TryGetInt32(out var value) ||
                        value == 0)
                        throw AppException.BadRequest(
                            "Inventory adjustment requires a non-zero integer delta.",
                            "invalid_payload");
                    break;
            }
        }

        return payload;
    }

    private static void RequireString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw AppException.BadRequest($"Command payload requires '{name}'.", "invalid_payload");
    }

    public static RemoteCommandDto ToDto(RemoteCommand command) =>
        new(
            command.Id,
            command.TenantId,
            command.InstallationId,
            command.RequestedByUserId,
            command.Type,
            command.PayloadJson,
            command.Status.ToString(),
            command.ResultJson,
            command.Error,
            command.CreatedAt,
            command.ExpiresAt,
            command.DispatchedAt,
            command.StartedAt,
            command.CompletedAt);
}
